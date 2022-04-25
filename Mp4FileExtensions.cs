using AAXClean.AudioFilters;
using AAXClean.Codecs.AudioFilters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AAXClean.Codecs
{
    public static class Mp4FileExtensions
    {
        public static IEnumerable<SilenceEntry> DetectSilence(this Mp4File mp4File, double decibels, TimeSpan minDuration)
        {

			if (decibels >= 0 || decibels < -90)
				throw new ArgumentException($"{nameof(decibels)} must fall in [-90,0)");
			if (minDuration.TotalSeconds * mp4File.TimeScale < 2)
				throw new ArgumentException($"{nameof(minDuration)} must be no shorter than 2 audio samples.");


			var sil = new SilenceDetectFilter(
				decibels,
				minDuration,
				mp4File.AscBlob,
				mp4File.AudioSampleSize);

			mp4File.FilterAudio(sil);

			return sil.Silences;
		}

		public static ConversionResult ConvertToMp3(this Mp4File mp4File, Stream outputStream, NAudio.Lame.LameConfig lameConfig = null, ChapterInfo userChapters = null)
		{
			lameConfig ??= GetDefaultLameConfig(mp4File);
			lameConfig.ID3 ??= AacToMp3Filter.GetDefaultMp3Tags(mp4File.AppleTags);

			ConversionResult result;
			using (var audioFilter = new AacToMp3Filter(
				outputStream,
				mp4File.AscBlob,
				mp4File.AudioSampleSize,
				lameConfig))
			{
				result = mp4File.FilterAudio(audioFilter, userChapters);
			}

			outputStream.Close();
			return result;
		}

		public static void ConvertToMultiMp3(this Mp4File mp4File, ChapterInfo userChapters, Action<NewSplitCallback> newFileCallback, NAudio.Lame.LameConfig lameConfig = null)
		{
			lameConfig ??= GetDefaultLameConfig(mp4File);
			lameConfig.ID3 ??= AacToMp3Filter.GetDefaultMp3Tags(mp4File.AppleTags);

			using var audioFilter = new AacToMp3MultipartFilter(
				userChapters,
				newFileCallback,
				mp4File.AscBlob,
				mp4File.AudioSampleSize,
				lameConfig);

			mp4File.FilterAudio(audioFilter, userChapters);
		}

		private static NAudio.Lame.LameConfig GetDefaultLameConfig(Mp4File mp4File)
		{
			double channelDown = mp4File.AudioChannels == 1 ? 1 : 0.5;

			var lameConfig = new NAudio.Lame.LameConfig
			{
				ABRRateKbps = (int)(mp4File.AverageBitrate * channelDown / 1024),
				Mode = NAudio.Lame.MPEGMode.Mono,
				VBR = NAudio.Lame.VBRMode.ABR,
			};
			return lameConfig;
		}
	}
}
