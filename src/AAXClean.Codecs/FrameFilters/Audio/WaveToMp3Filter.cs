using AAXClean.FrameFilters;
using NAudio.Lame;
using System.IO;
using System.Threading.Tasks;

namespace AAXClean.Codecs.FrameFilters.Audio
{
	internal class WaveToMp3Filter : FrameFinalBase<WaveEntry>
	{
		private readonly LameMP3FileWriter lameMp3Encoder;
		private readonly Stream OutputStream;
		protected override int InputBufferSize => 100;

		public bool Closed { get; private set; }

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
				Year = appleTags.ReleaseDate
			};

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
