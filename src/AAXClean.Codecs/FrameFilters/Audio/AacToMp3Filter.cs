using AAXClean.FrameFilters;
using NAudio.Lame;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AAXClean.Codecs.FrameFilters.Audio
{
	internal class AacToMp3Filter : FrameFinalBase<WaveEntry>
	{
		private readonly LameMP3FileWriter lameMp3Encoder;
		private readonly WaveFormat waveFormat;
		private readonly Stream OutputStream;

		public bool Closed { get; private set; }

		public AacToMp3Filter(Stream mp3Output, int sampleRate, ushort sampleSize, int channels, LameConfig lameConfig)
		{
			if (sampleSize != AacToWave.BitsPerSample)
				throw new ArgumentException($"{nameof(AacToMp3Filter)} only supports 16-bit aac streams.");

			//lameConfig.Quality = EncoderQuality.Standard;
			OutputStream = mp3Output;

			waveFormat = new WaveFormat(sampleRate, sampleSize, channels);
			lameMp3Encoder = new LameMP3FileWriter(OutputStream, waveFormat, lameConfig);
		}

		public static ID3TagData GetDefaultMp3Tags(AppleTags appleTags)
		{
			if (appleTags is null) return new();

			ID3TagData tags = new()
			{
				Album = appleTags.Album,
				AlbumArt = appleTags.Cover,
				AlbumArtist = appleTags.AlbumArtists,
				Comment = appleTags.Comment,
				Genre = appleTags.Generes,
				Title = appleTags.Title,
				Year = appleTags.ReleaseDate
			};

			return tags;
		}

		public override async Task CompleteAsync()
		{
			await base.CompleteAsync();
			if (!Closed)
			{
				lameMp3Encoder.Flush();
				lameMp3Encoder.Close();
				OutputStream.Close();
				Closed = true;
			}
		}

		protected override void PerformFiltering(WaveEntry input)
		{
			lameMp3Encoder.Write(input.FrameData.Span);
			input.hFrameData.Dispose();
		}
	}
}
