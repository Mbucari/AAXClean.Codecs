using AAXClean.Codecs.FrameFilters.Audio;
using AAXClean.FrameFilters;
using AAXClean.FrameFilters.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AAXClean.Codecs
{
	public static class Mp4FileExtensions
	{
		private static IntPtr ffmpegaac;
		private static IntPtr DllImportResolver(string libraryName, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath)
		{
			if (libraryName == FfmpegAacDecoder.libname)
			{
				if (ffmpegaac != IntPtr.Zero)
					return ffmpegaac;

				var architecture = RuntimeInformation.OSArchitecture.ToString().ToLower();

				var extension
					= RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dll"
					: RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "so"
					: "dylib";

				libraryName = $"{libraryName}.{architecture}.{extension}";

				if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out ffmpegaac) ||
					NativeLibrary.TryLoad($"{libraryName}.{architecture}.{extension}", assembly, searchPath, out ffmpegaac))
					return ffmpegaac;
				else
					throw new PlatformNotSupportedException();
			}

			// Otherwise, fallback to default import resolver.
			return IntPtr.Zero;
		}

		static Mp4FileExtensions()
		{
			NativeLibrary.SetDllImportResolver(System.Reflection.Assembly.GetExecutingAssembly(), DllImportResolver);
		}

		public static AacWaveStream GetWaveStream(this Mp4File mp4File, TimeSpan bufferTime)
			=> new(new AacDecodeBuffer(mp4File, bufferTime));


		public static Mp4Operation<List<SilenceEntry>> DetectSilenceAsync(this Mp4File mp4File, double decibels, TimeSpan minDuration, Action<SilenceDetectCallback> detectionCallback = null)
		{
			if (decibels >= 0 || decibels < -90)
				throw new ArgumentException($"{nameof(decibels)} must fall in [-90,0)");
			if (minDuration.TotalSeconds * mp4File.TimeScale < 2)
				throw new ArgumentException($"{nameof(minDuration)} must be no shorter than 2 audio samples.");

			FrameTransformBase<FrameEntry, FrameEntry> f1 = mp4File.GetAudioFrameFilter();
			AacToWave f2 = new(mp4File.AscBlob, WaveFormatEncoding.Pcm);
			SilenceDetectFilter f3 = new(
				decibels,
				minDuration,
				f2.WaveFormat,
				detectionCallback);

			f1.LinkTo(f2);
			f2.LinkTo(f3);

			List<SilenceEntry> completion(Task t)
			{
				f1.Dispose();
				return t.IsFaulted ? null : f3.Silences;
			}

			return mp4File.ProcessAudio(completion, (mp4File.Moov.AudioTrack, f1));
		}

		public static Mp4Operation ConvertToMp3Async(this Mp4File mp4File, Stream outputStream, NAudio.Lame.LameConfig lameConfig = null, ChapterInfo userChapters = null, bool trimOutputToChapters = false)
		{
			lameConfig ??= GetDefaultLameConfig(mp4File);
			lameConfig.ID3 ??= WaveToMp3Filter.GetDefaultMp3Tags(mp4File.AppleTags);

			var stereo = lameConfig.Mode is not NAudio.Lame.MPEGMode.Mono;
			var sampleRate = (SampleRate)mp4File.TimeScale;

			FrameTransformBase<FrameEntry, FrameEntry> f1 = mp4File.GetAudioFrameFilter();
			AacToWave f2 = new(mp4File.AscBlob, WaveFormatEncoding.IeeeFloat, sampleRate, stereo);
			WaveToMp3Filter f3 = new(
				outputStream,
				f2.WaveFormat,
				lameConfig);

			f1.LinkTo(f2);
			f2.LinkTo(f3);

			var start = userChapters?.StartOffset ?? TimeSpan.Zero;
			var end = userChapters?.EndOffset ?? TimeSpan.Zero;

			if (mp4File.Moov.TextTrack is null)
			{
				void completion(Task t)
				{
					f1.Dispose();
					outputStream.Close();
				}

				return mp4File.ProcessAudio(trimOutputToChapters && userChapters is not null, start, end, completion, (mp4File.Moov.AudioTrack, f1));
			}
			else
			{
				ChapterFilter c1 = new(mp4File.TimeScale);

				void completion(Task t)
				{
					f1.Dispose();
					c1.Dispose();
					if (t.IsCompletedSuccessfully)
						mp4File.Chapters = userChapters ?? c1.Chapters;
					outputStream.Close();
				}

				return mp4File.ProcessAudio(trimOutputToChapters && userChapters is not null, start, end, completion, (mp4File.Moov.AudioTrack, f1), (mp4File.Moov.TextTrack, c1));
			}
		}

		public static Mp4Operation ConvertToMp4aAsync(this Mp4File mp4File, Stream outputStream, AacEncodingOptions options, ChapterInfo userChapters = null, bool trimOutputToChapters = false)
		{
			if (options is null) return Mp4Operation.CompletedOperation;

			var stereo = mp4File.AudioChannels > 1 && options.Stereo is true;
			var sampleRate = options.SampleRate.HasValue ? (SampleRate)Math.Min(mp4File.TimeScale, (uint)options.SampleRate) : (SampleRate)mp4File.TimeScale;

			FrameTransformBase<FrameEntry, FrameEntry> f1 = mp4File.GetAudioFrameFilter();
			AacToWave f2 = new(mp4File.AscBlob, WaveFormatEncoding.Dts, sampleRate, stereo);
			WaveToAacFilter f3 = new(
				outputStream,
				mp4File.Ftyp,
				mp4File.Moov,
				f2.WaveFormat,
				options.BitRate,
				options.EncoderQuality);

			f1.LinkTo(f2);
			f2.LinkTo(f3);

			var start = userChapters?.StartOffset ?? TimeSpan.Zero;
			var end = userChapters?.EndOffset ?? TimeSpan.Zero;

			if (mp4File.Moov.TextTrack is null || userChapters is not null)
			{
				f3.SetChapterDelegate(() => userChapters);

				void completion(Task t)
				{
					f1.Dispose();
					outputStream.Close();
				}

				return mp4File.ProcessAudio(trimOutputToChapters && userChapters is not null, start, end, completion, (mp4File.Moov.AudioTrack, f1));
			}
			else
			{
				ChapterFilter c1 = new(mp4File.TimeScale);
				f3.SetChapterDelegate(() => c1.Chapters);

				void completion(Task t)
				{
					f1.Dispose();
					c1.Dispose();
					outputStream.Close();
				}

				return mp4File.ProcessAudio(trimOutputToChapters && userChapters is not null, start, end, completion, (mp4File.Moov.AudioTrack, f1), (mp4File.Moov.TextTrack, c1));
			}
		}

		public static Mp4Operation ConvertToMultiMp4aAsync(this Mp4File mp4File, ChapterInfo userChapters, Action<NewAacSplitCallback> newFileCallback, AacEncodingOptions options = null, bool trimOutputToChapters = false)
		{
			var stereo = mp4File.AudioChannels > 1 && options?.Stereo is true;
			var sampleRate = (options?.SampleRate).HasValue ? (SampleRate)Math.Min(mp4File.TimeScale, (uint)options.SampleRate) : (SampleRate)mp4File.TimeScale;

			FrameTransformBase<FrameEntry, FrameEntry> f1 = mp4File.GetAudioFrameFilter();
			AacToWave f2 = new(mp4File.AscBlob, WaveFormatEncoding.Dts, sampleRate, stereo);
			WaveToAacMultipartFilter f3 = new(
				userChapters, mp4File.Ftyp, mp4File.Moov,
				f2.WaveFormat,
				options,
				newFileCallback);

			f1.LinkTo(f2);
			f2.LinkTo(f3);

			void completion(Task t) => f1.Dispose();

			return mp4File.ProcessAudio(trimOutputToChapters, userChapters.StartOffset, userChapters.EndOffset, completion, (mp4File.Moov.AudioTrack, f1));
		}

		public static Mp4Operation ConvertToMultiMp3Async(this Mp4File mp4File, ChapterInfo userChapters, Action<NewMP3SplitCallback> newFileCallback, NAudio.Lame.LameConfig lameConfig = null, bool trimOutputToChapters = false)
		{
			lameConfig ??= GetDefaultLameConfig(mp4File);
			lameConfig.ID3 ??= WaveToMp3Filter.GetDefaultMp3Tags(mp4File.AppleTags);

			var stereo = lameConfig.Mode is not NAudio.Lame.MPEGMode.Mono;
			var sampleRate = (SampleRate)mp4File.TimeScale;

			FrameTransformBase<FrameEntry, FrameEntry> f1 = mp4File.GetAudioFrameFilter();
			AacToWave f2 = new(mp4File.AscBlob, WaveFormatEncoding.IeeeFloat, sampleRate, stereo);
			WaveToMp3MultipartFilter f3 = new(
				userChapters,
				f2.WaveFormat,
				lameConfig,
				newFileCallback);

			f1.LinkTo(f2);
			f2.LinkTo(f3);

			void completion(Task t) => f1.Dispose();

			return mp4File.ProcessAudio(trimOutputToChapters, userChapters.StartOffset, userChapters.EndOffset, completion, (mp4File.Moov.AudioTrack, f1));
		}

		public static NAudio.Lame.LameConfig GetDefaultLameConfig(Mp4File mp4File)
		{
			NAudio.Lame.LameConfig lameConfig = new NAudio.Lame.LameConfig
			{
				ABRRateKbps = (int)Math.Round(mp4File.AverageBitrate / 1024d / mp4File.AudioChannels),
				Mode = NAudio.Lame.MPEGMode.Mono,
				VBR = NAudio.Lame.VBRMode.ABR,
			};
			return lameConfig;
		}
	}
}
