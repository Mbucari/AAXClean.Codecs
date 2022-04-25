using NAudio.Lame;
using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace AAXClean.AudioFilters
{
	internal class AacToMp3Filter : AudioFilter
	{
		private const int MAX_BUFFER_SZ = 4 * 1024 * 1024;
		private readonly AacDecoder decoder;
		private readonly BlockingCollection<byte[]> waveFrameQueue;
		private readonly LameMP3FileWriter lameMp3Encoder;
		private readonly Task encoderLoopTask;
		private readonly WaveFormat waveFormat;
		private readonly Stream OutputStream;

		static AacToMp3Filter()
        {
			int bitness = IntPtr.Size * 8;
			string libName = $"libmp3lame.{bitness}.dll";

			if (!File.Exists(libName))
			{
				try
				{
					if (bitness == 64)
						File.WriteAllBytes(libName, AAXClean.Codecs.Properties.Resources.libmp3lame_64);
					else
						File.WriteAllBytes(libName, AAXClean.Codecs.Properties.Resources.libmp3lame_32);
				}
				catch (Exception ex)
				{
					throw new DllNotFoundException($"Dould not load {libName}", ex);
				}
			}
		}

		public AacToMp3Filter(Stream mp3Output, byte[] audioSpecificConfig, ushort sampleSize, LameConfig lameConfig)
		{
			if (sampleSize != AacDecoder.BITS_PER_SAMPLE)
				throw new ArgumentException($"{nameof(AacToMp3Filter)} only supports 16-bit aac streams.");

			OutputStream = mp3Output;
			decoder = new FfmpegAacDecoder(audioSpecificConfig);

			waveFormat = new WaveFormat(decoder.SampleRate, sampleSize, decoder.Channels);

			lameMp3Encoder = new LameMP3FileWriter(OutputStream, waveFormat, lameConfig);

			int waveFrameSize = 1024 /* Decoded AAC frame size*/ * waveFormat.BlockAlign;
			int maxCachedFrames = MAX_BUFFER_SZ / waveFrameSize;
			waveFrameQueue = new BlockingCollection<byte[]>(maxCachedFrames);

			encoderLoopTask = new Task(EncoderLoop);
			encoderLoopTask.Start();
		}

		public static ID3TagData GetDefaultMp3Tags(AppleTags appleTags)
		{
			if (appleTags is null) return new();

            var tags = new ID3TagData
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

		private void EncoderLoop()
		{
			while (waveFrameQueue.TryTake(out byte[] waveFrame, -1))
			{
				lameMp3Encoder.Write(waveFrame);
			}
			lameMp3Encoder.Flush();
			lameMp3Encoder.Close();
		}

		public override bool FilterFrame(uint chunkIndex, uint frameIndex, Span<byte> aacFrame)
		{
			waveFrameQueue.Add(decoder.DecodeBytes(aacFrame).ToArray());
			return true;
		}

		public override void Close()
		{
			waveFrameQueue.CompleteAdding();
			encoderLoopTask.Wait();
			lameMp3Encoder.Close();
			OutputStream.Close();
		}

		protected override void Dispose(bool disposing)
		{
			if (!_disposed && disposing)
			{
				decoder?.Dispose();
			}

			base.Dispose(disposing);
		}

		private class WaveFormat : NAudio.Wave.WaveFormat
		{
			public WaveFormat(int sampleRate, int bitsPerSample, int channels)
			{
				this.sampleRate = sampleRate;
				this.channels = (short)channels;
				this.bitsPerSample = (short)(bitsPerSample);
				blockAlign = (short)(channels * this.bitsPerSample / 8);
				averageBytesPerSecond = blockAlign * sampleRate;
				waveFormatTag = WaveFormatEncoding.Pcm;
			}
		}
	}
}
