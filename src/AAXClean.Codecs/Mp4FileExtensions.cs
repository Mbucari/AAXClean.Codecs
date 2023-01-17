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
	public class AacEncoderOptions
	{
		public SampleRate SampleRate { get; set; }
		public bool Stereo { get; set; }
		public double EncoderQuality { get; set; }
		public long BitRate { get; set; }
	}
	public static class Mp4FileExtensions
	{
		private static IntPtr ffmpegaac;
		private static IntPtr DllImportResolver(string libraryName, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath)
		{
			if (libraryName == FfmpegAacDecoder.libname)
			{
				if (ffmpegaac != IntPtr.Zero)
					return ffmpegaac;

				if (Environment.OSVersion.Platform == PlatformID.Win32NT)
				if (Environment.OSVersion.Platform == PlatformID.Win32NT)
					libraryName = $"{libraryName}.{(Environment.Is64BitProcess ? "x64" : "x86")}.dll";
				
				if (NativeLibrary.TryLoad(@"D:\OneDrive\Projects\AaxClean\AaxTest\ffmpeg5\ffmpeg5\msvc\bin\x64\" + libraryName, assembly, searchPath, out ffmpegaac))
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

		public static IReadOnlyList<SilenceEntry> DetectSilence(this Mp4File mp4File, double decibels, TimeSpan minDuration, Action<SilenceDetectCallback> detectionCallback = null) 
			=> DetectSilenceAsync(mp4File, decibels, minDuration, detectionCallback).GetAwaiter().GetResult();
		public static void ConvertToMp3(this Mp4File mp4File, Stream outputStream, NAudio.Lame.LameConfig lameConfig = null, ChapterInfo userChapters = null, bool trimOutputToChapters = false) 
			=> ConvertToMp3Async(mp4File, outputStream, lameConfig, userChapters, trimOutputToChapters).GetAwaiter().GetResult();
		public static void ConvertToMultiMp3(this Mp4File mp4File, ChapterInfo userChapters, Action<NewMP3SplitCallback> newFileCallback, NAudio.Lame.LameConfig lameConfig = null, bool trimOutputToChapters = false) 
			=> ConvertToMultiMp3Async(mp4File, userChapters, newFileCallback, lameConfig, trimOutputToChapters).GetAwaiter().GetResult();
		
		public static async Task<IReadOnlyList<SilenceEntry>> DetectSilenceAsync(this Mp4File mp4File, double decibels, TimeSpan minDuration, Action<SilenceDetectCallback> detectionCallback = null)
		{
			if (decibels >= 0 || decibels < -90)
				throw new ArgumentException($"{nameof(decibels)} must fall in [-90,0)");
			if (minDuration.TotalSeconds * mp4File.TimeScale < 2)
				throw new ArgumentException($"{nameof(minDuration)} must be no shorter than 2 audio samples.");

			using FrameTransformBase<FrameEntry, FrameEntry> f1 = mp4File.GetAudioFrameFilter();
			using AacToWave f2 = new(mp4File.AscBlob, WaveFormatEncoding.Pcm, SampleRate._22050, true);
			using SilenceDetectFilter f3 = new(
				decibels,
				minDuration,
				f2.WaveFormat,
				detectionCallback);

			f1.LinkTo(f2);
			f2.LinkTo(f3);

			await mp4File.ProcessAudio((mp4File.Moov.AudioTrack, f1));

			return f3.Silences;
		}

		public static async Task ConvertToMp3Async(this Mp4File mp4File, Stream outputStream, NAudio.Lame.LameConfig lameConfig = null, ChapterInfo userChapters = null, bool trimOutputToChapters = false)
		{
			lameConfig ??= GetDefaultLameConfig(mp4File);
			lameConfig.ID3 ??= WaveToMp3Filter.GetDefaultMp3Tags(mp4File.AppleTags);

			var stereo = lameConfig.Mode is not NAudio.Lame.MPEGMode.Mono;
			var sampleRate = (SampleRate)mp4File.TimeScale;

			using FrameTransformBase<FrameEntry, FrameEntry> f1 = mp4File.GetAudioFrameFilter();
			using AacToWave f2 = new(mp4File.AscBlob, WaveFormatEncoding.IeeeFloat, sampleRate, stereo);
			using WaveToMp3Filter f3 = new(
				outputStream,
				f2.WaveFormat,
				lameConfig);

			f1.LinkTo(f2);
			f2.LinkTo(f3);

			var start = userChapters?.StartOffset ?? TimeSpan.Zero;
			var end = userChapters?.EndOffset ?? TimeSpan.Zero;

			if (mp4File.Moov.TextTrack is null)
			{
				await mp4File.ProcessAudio(trimOutputToChapters && userChapters is not null, start, end, (mp4File.Moov.AudioTrack, f1));
			}
			else
			{
				ChapterFilter c1 = new(mp4File.TimeScale);

				await mp4File.ProcessAudio(trimOutputToChapters && userChapters is not null, start, end, (mp4File.Moov.AudioTrack, f1), (mp4File.Moov.TextTrack, c1));

				mp4File.Chapters = userChapters ?? c1.Chapters;
			}

			outputStream.Close();
		}

		public static async Task ConvertToAacAsync(this Mp4File mp4File, Stream outputStream, AacEncoderOptions options, ChapterInfo userChapters = null, bool trimOutputToChapters = false)
		{
			if (options is null) return;

			var stereo = mp4File.AudioChannels > 1 && options.Stereo;
			var sampleRate = (SampleRate)Math.Min(mp4File.TimeScale, (uint)options.SampleRate);

			using FrameTransformBase<FrameEntry, FrameEntry> f1 = mp4File.GetAudioFrameFilter();
			using AacToWave f2 = new(mp4File.AscBlob, WaveFormatEncoding.Dts, sampleRate, stereo);
			using WaveToAacFilter f3 = new(
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
				await mp4File.ProcessAudio(trimOutputToChapters && userChapters is not null, start, end, (mp4File.Moov.AudioTrack, f1));
			}
			else
			{
				using ChapterFilter c1 = new(mp4File.TimeScale);
				f3.SetChapterDelegate(() => c1.Chapters);
				await mp4File.ProcessAudio(trimOutputToChapters && userChapters is not null, start, end, (mp4File.Moov.AudioTrack, f1), (mp4File.Moov.TextTrack, c1));
			}

			if (!mp4File.IsCancelled)
				mp4File.Chapters = f3.Chapters;

			outputStream.Close();
		}

		public static async Task ConvertToMultiMp3Async(this Mp4File mp4File, ChapterInfo userChapters, Action<NewMP3SplitCallback> newFileCallback, NAudio.Lame.LameConfig lameConfig = null, bool trimOutputToChapters = false)
		{
			lameConfig ??= GetDefaultLameConfig(mp4File);
			lameConfig.ID3 ??= WaveToMp3Filter.GetDefaultMp3Tags(mp4File.AppleTags);

			var stereo = lameConfig.Mode is not NAudio.Lame.MPEGMode.Mono;
			var sampleRate = (SampleRate)mp4File.TimeScale;

			using FrameTransformBase<FrameEntry, FrameEntry> f1 = mp4File.GetAudioFrameFilter();
			using AacToWave f2 = new(mp4File.AscBlob, WaveFormatEncoding.IeeeFloat, sampleRate, stereo);
			using WaveToMp3MultipartFilter f3 = new(
				userChapters,
				f2.WaveFormat,
				lameConfig,
				newFileCallback);

			f1.LinkTo(f2);
			f2.LinkTo(f3);

			await mp4File.ProcessAudio(trimOutputToChapters, userChapters.StartOffset, userChapters.EndOffset, (mp4File.Moov.AudioTrack, f1));
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
