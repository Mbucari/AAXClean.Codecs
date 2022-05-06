using AAXClean.Codecs.FrameFilters.Audio;
using AAXClean.FrameFilters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AAXClean.Codecs
{
	public static class Mp4FileExtensions
	{
		public static IReadOnlyList<SilenceEntry> DetectSilence(this Mp4File mp4File, double decibels, TimeSpan minDuration, Action<SilenceDetectCallback> detectionCallback = null) 
			=> DetectSilenceAsync(mp4File, decibels, minDuration, detectionCallback).GetAwaiter().GetResult();
		public static ConversionResult ConvertToMp3(this Mp4File mp4File, Stream outputStream, NAudio.Lame.LameConfig lameConfig = null, ChapterInfo userChapters = null) 
			=> ConvertToMp3Async(mp4File, outputStream, lameConfig, userChapters).GetAwaiter().GetResult();
		public static ConversionResult ConvertToMultiMp3(this Mp4File mp4File, ChapterInfo userChapters, Action<NewSplitCallback> newFileCallback, NAudio.Lame.LameConfig lameConfig = null) 
			=> ConvertToMultiMp3Async(mp4File, userChapters, newFileCallback, lameConfig).GetAwaiter().GetResult();
		
		public static async Task<IReadOnlyList<SilenceEntry>> DetectSilenceAsync(this Mp4File mp4File, double decibels, TimeSpan minDuration, Action<SilenceDetectCallback> detectionCallback = null)
		{
			if (decibels >= 0 || decibels < -90)
				throw new ArgumentException($"{nameof(decibels)} must fall in [-90,0)");
			if (minDuration.TotalSeconds * mp4File.TimeScale < 2)
				throw new ArgumentException($"{nameof(minDuration)} must be no shorter than 2 audio samples.");

			using FrameTransformBase<FrameEntry, FrameEntry> f1 = mp4File.GetAudioFrameFilter();
			using FfmpegAacDecoder f2 = new(mp4File.AscBlob);
			using SilenceDetectFilter f3 = new(
				decibels,
				minDuration,
				mp4File.AscBlob,
				mp4File.AudioSampleSize,
				detectionCallback);

			f1.LinkTo(f2);
			f2.LinkTo(f3);


			await mp4File.ProcessAudio((mp4File.Moov.AudioTrack, f1));

			return f3.Silences;
		}

		public static async Task<ConversionResult> ConvertToMp3Async(this Mp4File mp4File, Stream outputStream, NAudio.Lame.LameConfig lameConfig = null, ChapterInfo userChapters = null)
		{
			lameConfig ??= GetDefaultLameConfig(mp4File);
			lameConfig.ID3 ??= AacToMp3Filter.GetDefaultMp3Tags(mp4File.AppleTags);

			using FrameTransformBase<FrameEntry, FrameEntry> f1 = mp4File.GetAudioFrameFilter();
			using FfmpegAacDecoder f2 = new(mp4File.AscBlob);
			using AacToMp3Filter f3 = new(
				outputStream,
				(int)mp4File.TimeScale,
				mp4File.AudioSampleSize,
				mp4File.AudioChannels,
				lameConfig
				);

			f1.LinkTo(f2);
			f2.LinkTo(f3);
			ConversionResult result = await mp4File.ProcessAudio((mp4File.Moov.AudioTrack, f1));
			outputStream.Close();
			return await Task.FromResult(result);
		}

		public static async Task<ConversionResult> ConvertToMultiMp3Async(this Mp4File mp4File, ChapterInfo userChapters, Action<NewSplitCallback> newFileCallback, NAudio.Lame.LameConfig lameConfig = null)
		{
			lameConfig ??= GetDefaultLameConfig(mp4File);
			lameConfig.ID3 ??= AacToMp3Filter.GetDefaultMp3Tags(mp4File.AppleTags);

			using FrameTransformBase<FrameEntry, FrameEntry> f1 = mp4File.GetAudioFrameFilter();
			using FfmpegAacDecoder f2 = new(mp4File.AscBlob);
			using AacToMp3MultipartFilter f3 = new(
				userChapters,
				newFileCallback,
				mp4File.AscBlob,
				mp4File.AudioSampleSize,
				lameConfig);

			f1.LinkTo(f2);
			f2.LinkTo(f3);
			ConversionResult result = await mp4File.ProcessAudio((mp4File.Moov.AudioTrack, f1));
			return await Task.FromResult(result);
		}

		private static NAudio.Lame.LameConfig GetDefaultLameConfig(Mp4File mp4File)
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
