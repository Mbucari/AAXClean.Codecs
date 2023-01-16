using AAXClean.FrameFilters;
using NAudio.Lame;
using System.IO;

namespace AAXClean.Codecs.FrameFilters.Audio
{
	internal class WaveToMp3Filter : FrameFinalBase<WaveEntry>
	{
		private readonly LameMP3FileWriter lameMp3Encoder;
		private readonly Stream OutputStream;

		public bool Closed { get; private set; }

		public WaveToMp3Filter(Stream mp3Output, WaveFormat waveFormat, LameConfig lameConfig)
		{
			//lameConfig.Quality = EncoderQuality.Standard;
			OutputStream = mp3Output;
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

		protected override void Flush()
		{
			lameMp3Encoder.Flush();
			lameMp3Encoder.Close();
			OutputStream.Close();
			Closed = true;
		}
		int t = 0;
		protected override void PerformFiltering(WaveEntry input)
		{
			//if (t++ > 2000) throw new System.Exception("TEST EXCEPTION!");
			lameMp3Encoder.Write(input.FrameData.Span);
			input.hFrameData.Dispose();
		}
	}
}
