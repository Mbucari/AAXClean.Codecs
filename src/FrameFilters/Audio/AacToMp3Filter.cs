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

		static AacToMp3Filter()
		{
			int bitness = IntPtr.Size * 8;
			string libName = $"libmp3lame.{bitness}.dll";

			if (!File.Exists(libName))
			{
				try
				{
					if (bitness == 64)
						File.WriteAllBytes(libName, Properties.Resources.libmp3lame_64);
					else
						File.WriteAllBytes(libName, Properties.Resources.libmp3lame_32);
				}
				catch (Exception ex)
				{
					throw new DllNotFoundException($"Could not load {libName}", ex);
				}
			}
		}

		public AacToMp3Filter(Stream mp3Output, int sampleRate, ushort sampleSize, int channels, LameConfig lameConfig)
		{
			if (sampleSize != FfmpegAacDecoder.BITS_PER_SAMPLE)
				throw new ArgumentException($"{nameof(AacToMp3Filter)} only supports 16-bit aac streams.");

			OutputStream = mp3Output;

			waveFormat = new WaveFormat(sampleRate, sampleSize, channels);
			lameMp3Encoder = new LameMP3FileWriter(OutputStream, waveFormat, lameConfig);
		}

		public static ID3TagData GetDefaultMp3Tags(AppleTags appleTags)
		{
			if (appleTags is null) return new();

			ID3TagData tags = new ID3TagData
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
