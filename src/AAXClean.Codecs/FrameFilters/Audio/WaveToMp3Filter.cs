using AAXClean.FrameFilters;
using NAudio.Lame;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AAXClean.Codecs.FrameFilters.Audio
{
	internal class WaveToMp3Filter : FrameFinalBase<WaveEntry>
	{
		public bool Closed { get; private set; }
		protected override int InputBufferSize => 100;

		private readonly LameMP3FileWriter lameMp3Encoder;
		private readonly Stream OutputStream;

		public WaveToMp3Filter(Stream mp3Output, WaveFormat waveFormat, LameConfig lameConfig)
		{
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
			};
			
			if (DateTime.TryParse(appleTags.ReleaseDate, out var releaseDate))
				tags.Year = releaseDate.Year.ToString();

			tags.AdditionalTags.Add("ARTIST", appleTags.Artist);
			tags.AdditionalTags.Add("TDAT", appleTags.ReleaseDate);
			tags.AdditionalTags.Add("TCOP", appleTags.Copyright);
			tags.AdditionalTags.Add("TCOM", appleTags.Narrator);
			tags.AdditionalTags.Add("TPUB", appleTags.Publisher);
			tags.UserDefinedText.Add("LONG_COMMENT", appleTags.LongDescription);
			tags.UserDefinedText.Add("AUDIBLE_ACR", appleTags.Acr);
			tags.UserDefinedText.Add("AUDIBLE_VERSION", appleTags.Version);
			tags.UserDefinedText.Add("AUDIBLE_ASIN", appleTags.Asin);

			return tags;
		}

		protected override async Task FlushAsync()
		{
			await lameMp3Encoder.FlushAsync();
			lameMp3Encoder.Close();
			OutputStream.Close();
			Closed = true;
		}

		protected override Task PerformFilteringAsync(WaveEntry input)
		{
			lameMp3Encoder.Write(input.FrameData.Span);
			return Task.CompletedTask;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && !Disposed)
			{
				lameMp3Encoder?.Close();
				lameMp3Encoder?.Dispose();
			}
			base.Dispose(disposing);
		}
	}
}
