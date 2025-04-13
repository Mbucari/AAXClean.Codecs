using AAXClean.Codecs.FrameFilters.Audio;
using AAXClean.FrameFilters;
using AAXClean.FrameFilters.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AAXClean.Codecs
{
	public static class Mp4FileExtensions
	{

		public static void Load() { }

		public static AacWaveStream GetWaveStream(this Mp4File mp4File, TimeSpan bufferTime)
			=> new(new AacDecodeBuffer(mp4File, bufferTime));

		public static Mp4Operation<List<SilenceEntry>> DetectSilenceAsync(this Mp4File mp4File, double decibels, TimeSpan minDuration, Action<SilenceDetectCallback> detectionCallback = null)
		{
			if (mp4File is null) throw new ArgumentNullException(nameof(mp4File));
			if (decibels >= 0 || decibels < -90) throw new ArgumentOutOfRangeException(nameof(decibels), "must fall in [-90,0)");
			if (minDuration.TotalSeconds * (int)mp4File.SampleRate < 2) throw new ArgumentOutOfRangeException(nameof(minDuration), "must be no shorter than 2 audio samples.");

			FrameTransformBase<FrameEntry, FrameEntry> filter1 = mp4File.GetAudioFrameFilter();
			AacToWave filter2 = new(mp4File.AscBlob, WaveFormatEncoding.Pcm);
			SilenceDetectFilter f3 = new(
				decibels,
				minDuration,
				filter2.WaveFormat,
				detectionCallback);

			filter1.LinkTo(filter2);
			filter2.LinkTo(f3);

			List<SilenceEntry> completion(Task t)
			{
				filter1.Dispose();
				return t.IsFaulted ? null : f3.Silences;
			}

			return mp4File.ProcessAudio(TimeSpan.Zero, TimeSpan.MaxValue, completion, (mp4File.Moov.AudioTrack, filter1));
		}

		public static Mp4Operation ConvertToMp3Async(this Mp4File mp4File, Stream outputStream, NAudio.Lame.LameConfig lameConfig = null, ChapterInfo userChapters = null)
		{
			if (mp4File is null) throw new ArgumentNullException(nameof(mp4File));
			if (outputStream is null) throw new ArgumentNullException(nameof(outputStream));
			if (outputStream.CanWrite is false) throw new ArgumentException("output stream is not writable", nameof(outputStream));

			lameConfig ??= mp4File.GetDefaultLameConfig();
			lameConfig.ID3 ??= mp4File.AppleTags?.ToIDTags() ?? new();

			if (!lameConfig.ID3.Chapters.Any() && userChapters is not null)
			{
				foreach (var ch in userChapters)
					lameConfig.ID3.Chapters.Add((ch.StartOffset - userChapters.StartOffset, ch.EndOffset - userChapters.StartOffset, ch.Title));
			}

			var stereo = lameConfig.Mode is not NAudio.Lame.MPEGMode.Mono;
			var sampleRate = mp4File.GetMaxSampleRate((SampleRate?)lameConfig.OutputSampleRate);

			FrameTransformBase<FrameEntry, FrameEntry> filter1 = mp4File.GetAudioFrameFilter();

			AacToWave filter2 = new(
				mp4File.AscBlob,
				WaveFormatEncoding.Pcm,
				sampleRate,
				stereo);

			WaveToMp3Filter filter3 = new(
				outputStream,
				filter2.WaveFormat,
				lameConfig);

			filter1.LinkTo(filter2);
			filter2.LinkTo(filter3);

			var start = userChapters?.StartOffset ?? TimeSpan.Zero;
			var end = userChapters?.EndOffset ?? TimeSpan.MaxValue;

			void completion(Task t)
			{
				filter1.Dispose();
				outputStream.Close();
			}

			return mp4File.ProcessAudio(start, end, completion, (mp4File.Moov.AudioTrack, filter1));
		}

		public static Mp4Operation ConvertToMp4aAsync(this Mp4File mp4File, Stream outputStream, AacEncodingOptions options, ChapterInfo userChapters = null)
		{
			if (mp4File is null) throw new ArgumentNullException(nameof(mp4File));
			if (outputStream is null) throw new ArgumentNullException(nameof(outputStream));
			if (outputStream.CanWrite is false) throw new ArgumentException("output stream is not writable", nameof(outputStream));
			if (options is null) throw new ArgumentNullException(nameof(options));

			var start = userChapters?.StartOffset ?? TimeSpan.Zero;
			var end = userChapters?.EndOffset ?? TimeSpan.MaxValue;

			var stereo = mp4File.AudioChannels > 1 && options.Stereo is true;
			var sampleRate = mp4File.GetMaxSampleRate(options?.SampleRate);

			ChapterQueue chapterQueue = new(mp4File.SampleRate, sampleRate);

			FrameTransformBase<FrameEntry, FrameEntry> filter1 = mp4File.GetAudioFrameFilter();

			AacToWave filter2 = new(
				mp4File.AscBlob,
				WaveFormatEncoding.Pcm,
				sampleRate,
				stereo);

			WaveToAacFilter filter3 = new(
				outputStream,
				mp4File,
				chapterQueue,
				filter2.WaveFormat,
				options.BitRate,
				options.EncoderQuality);

			filter1.LinkTo(filter2);
			filter2.LinkTo(filter3);

			if (mp4File.Moov.TextTrack is not null && userChapters is not null)
				chapterQueue.AddRange(userChapters);

			if (mp4File.Moov.TextTrack is null || userChapters is not null)
			{
				void completion(Task t)
				{
					filter1.Dispose();
					outputStream.Close();
				}

				return mp4File.ProcessAudio(start, end, completion, (mp4File.Moov.AudioTrack, filter1));
			}
			else
			{
				ChapterFilter chapterFilter = new();

				chapterFilter.ChapterRead += (_, e) => chapterQueue.Add(e);

				void completion(Task t)
				{
					filter1.Dispose();
					chapterFilter.Dispose();
					outputStream.Close();
				}

				return mp4File.ProcessAudio(start, end, completion, (mp4File.Moov.AudioTrack, filter1), (mp4File.Moov.TextTrack, chapterFilter));
			}
		}

		public static Mp4Operation ConvertToMultiMp4aAsync(this Mp4File mp4File, ChapterInfo userChapters, Action<NewAacSplitCallback> newFileCallback, AacEncodingOptions options = null)
		{
			if (mp4File is null) throw new ArgumentNullException(nameof(mp4File));
			if (userChapters is null) throw new ArgumentNullException(nameof(mp4File));
			if (newFileCallback is null) throw new ArgumentNullException(nameof(newFileCallback));

			var stereo = mp4File.AudioChannels > 1 && options?.Stereo is true;
			var sampleRate = mp4File.GetMaxSampleRate(options?.SampleRate);

			FrameTransformBase<FrameEntry, FrameEntry> filter1 = mp4File.GetAudioFrameFilter();

			AacToWave filter2 = new(
				mp4File.AscBlob,
				WaveFormatEncoding.Dts,
				sampleRate,
				stereo);

			WaveToAacMultipartFilter filter3 = new(
				userChapters, mp4File.Ftyp, mp4File.Moov,
				filter2.WaveFormat,
				options,
				newFileCallback);

			filter1.LinkTo(filter2);
			filter2.LinkTo(filter3);

			void completion(Task t) => filter1.Dispose();

			return mp4File.ProcessAudio(userChapters.StartOffset, userChapters.EndOffset, completion, (mp4File.Moov.AudioTrack, filter1));
		}

		public static Mp4Operation ConvertToMultiMp3Async(this Mp4File mp4File, ChapterInfo userChapters, Action<NewMP3SplitCallback> newFileCallback, NAudio.Lame.LameConfig lameConfig = null)
		{
			if (mp4File is null) throw new ArgumentNullException(nameof(mp4File));
			if (userChapters is null) throw new ArgumentNullException(nameof(mp4File));
			if (newFileCallback is null) throw new ArgumentNullException(nameof(newFileCallback));

			lameConfig ??= mp4File.GetDefaultLameConfig();
			lameConfig.ID3 ??= mp4File.AppleTags?.ToIDTags() ?? new();

			var stereo = lameConfig.Mode is not NAudio.Lame.MPEGMode.Mono;
			var sampleRate = mp4File.GetMaxSampleRate((SampleRate?)lameConfig.OutputSampleRate);

			FrameTransformBase<FrameEntry, FrameEntry> filter1 = mp4File.GetAudioFrameFilter();

			AacToWave filter2 = new(
				mp4File.AscBlob,
				WaveFormatEncoding.IeeeFloat,
				sampleRate,
				stereo);

			WaveToMp3MultipartFilter filter3 = new(
				userChapters,
				filter2.WaveFormat,
				lameConfig,
				newFileCallback);

			filter1.LinkTo(filter2);
			filter2.LinkTo(filter3);

			void completion(Task t) => filter1.Dispose();

			return mp4File.ProcessAudio(userChapters.StartOffset, userChapters.EndOffset, completion, (mp4File.Moov.AudioTrack, filter1));
		}

		public static NAudio.Lame.LameConfig GetDefaultLameConfig(this Mp4File mp4File)
		{
			if (mp4File is null) throw new ArgumentNullException(nameof(mp4File));

			//USAC is much more efficient than LC, so allow double the bitrate when transcoding
			var USAC_Scaler = mp4File.AudioObjectType == 42 ? 2 : 1;

            NAudio.Lame.LameConfig lameConfig = new NAudio.Lame.LameConfig
			{
				ABRRateKbps = (int)Math.Round(mp4File.AverageBitrate / 1024d / mp4File.AudioChannels * USAC_Scaler),
				Mode = NAudio.Lame.MPEGMode.Mono,
				VBR = NAudio.Lame.VBRMode.ABR,
				ID3 = mp4File.AppleTags?.ToIDTags() ?? new()
			};
			return lameConfig;
		}

		public static SampleRate GetMaxSampleRate(this Mp4File mp4File, SampleRate? sampleRate = null)
		{
			if (mp4File is null) throw new ArgumentNullException(nameof(mp4File));

			return (SampleRate)Math.Min((int)mp4File.SampleRate, (int)(sampleRate ?? SampleRate.Hz_96000));
		}

		public static NAudio.Lame.ID3TagData ToIDTags(this AppleTags appleTags)
		{
			if (appleTags is null) throw new ArgumentNullException(nameof(appleTags));

			NAudio.Lame.ID3TagData tags = new()
			{
				Album = appleTags.Album,
				AlbumArt = appleTags.Cover,
				Artist = appleTags.Artist,
				AlbumArtist = appleTags.AlbumArtists,
				Comment = appleTags.Comment,
				Genre = appleTags.Generes,
				Composer = appleTags.Narrator,
				Title = appleTags.Title,
				AdditionalTags =
				{
					{ "TEXT", appleTags.Artist },
					{ "TPUB", appleTags.Publisher },
					{ "TCOP", appleTags.Copyright?.Replace("(P)", "℗")?.Replace("&#169;", "©") }
				},
				UserDefinedText =
				{
					{ "AUDIBLE_ACR", appleTags.Acr },
					{ "AUDIBLE_ASIN", appleTags.Asin },
					{ "AUDIBLE_VERSION", appleTags.Version },
					{ "LONG_COMMENT", appleTags.LongDescription }
				}
			};

			if (DateTime.TryParse(appleTags.ReleaseDate, out var releaseDate))
			{
				tags.Year = releaseDate.Year.ToString();
				tags.AdditionalTags.Add("TDAT", releaseDate.ToString("ddMM"));
				tags.AdditionalTags.Add("TYER", releaseDate.ToString("yyyy"));
			}
			else
				tags.AdditionalTags.Add("TRDA", appleTags.ReleaseDate);

			return tags;
		}
	}
}

