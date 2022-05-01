using AAXClean.AudioFilters;
using NAudio.Lame;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace AAXClean.Codecs.AudioFilters
{
	internal class AacToMp3Filter : AudioFilterBase
	{
		private const int MAX_BUFFER_SZ = 4 * 1024 * 1024;
		private readonly FfmpegAacDecoder decoder;
		private readonly BlockingCollection<Memory<byte>> waveFrameQueue;
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

		public AacToMp3Filter(Stream mp3Output, byte[] audioSpecificConfig, ushort sampleSize, LameConfig lameConfig)
		{
			if (sampleSize != FfmpegAacDecoder.BITS_PER_SAMPLE)
				throw new ArgumentException($"{nameof(AacToMp3Filter)} only supports 16-bit aac streams.");

			OutputStream = mp3Output;
			decoder = new FfmpegAacDecoder(audioSpecificConfig);

			waveFormat = new WaveFormat(decoder.SampleRate, sampleSize, decoder.Channels);
			lameMp3Encoder = new LameMP3FileWriter(OutputStream, waveFormat, lameConfig);

			int waveFrameSize = 1024 /* Decoded AAC frame size*/ * waveFormat.BlockAlign;
			int maxCachedFrames = MAX_BUFFER_SZ / waveFrameSize;
			waveFrameQueue = new BlockingCollection<Memory<byte>>(maxCachedFrames);

			encoderLoopTask = new Task(EncoderLoop);
			encoderLoopTask.Start();
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

		private void EncoderLoop()
		{
			while (waveFrameQueue.TryTake(out Memory<byte> waveFrame, -1))
			{
				lameMp3Encoder.Write(waveFrame.Span);
			}

			lameMp3Encoder.Flush();
			lameMp3Encoder.Close();
		}

		public override bool FilterFrame(uint chunkIndex, uint frameIndex, Span<byte> aacFrame)
		{
			waveFrameQueue.Add(decoder.Decode(aacFrame));
			return true;
		}

		public override void Close()
		{
			if (Closed) return;
			waveFrameQueue.CompleteAdding();
			encoderLoopTask.Wait();
			lameMp3Encoder.Close();
			OutputStream.Close();
			Closed = true;
		}

		protected override void Dispose(bool disposing)
		{
			if (!Disposed)
			{
				if (disposing)
				{
					Close();
					decoder?.Dispose();
					encoderLoopTask?.Dispose();
					waveFrameQueue?.Dispose();
					lameMp3Encoder?.Dispose();
				}
				base.Dispose(disposing);
			}
		}
	}
}
