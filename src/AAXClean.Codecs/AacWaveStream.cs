using NAudio.Wave;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace AAXClean.Codecs
{
	public class AacWaveStream : WaveStream
	{
		public override long Position { get => _position; set => Seek(value, SeekOrigin.Begin); }
		public override	WaveFormat WaveFormat => DecodeBuffer.WaveFormat;
		public override long Length => DecodeBuffer.Length;
		public TimeSpan Duration => DecodeBuffer.Duration;
		public TimeSpan TimePosition
		{
			get => TimeSpan.FromSeconds(Position / BlockAlign / (double)DecodeBuffer.TimeScale);
			set => Position = (long)(value.TotalSeconds * BlockAlign * DecodeBuffer.TimeScale);
		}

		private long _position = 0;
		private int lastRead = 0;
		private Memory<byte> lastWaveEntry;
		private readonly IDecodeBuffer DecodeBuffer;

		/// <summary>Halving the scale factor reduces the volume by 10 decibels.</summary>
		public float Volume
		{
			get => _volume;
			set => _volume = Math.Max(0f, Math.Min(1f, value));
		}

		private float _volume;

		public AacWaveStream(IDecodeBuffer decodeBuffer)
		{
			DecodeBuffer = decodeBuffer;
			Volume = 0.9f;
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			var newPosition = origin switch
			{
				SeekOrigin.Current => Position + offset,
				SeekOrigin.End => Length + offset,
				_ => offset,
			};

			newPosition -= newPosition % BlockAlign;
			var nextFrame = newPosition / DecodeBuffer.BytesPerFrame;

			DecodeBuffer.SkipToFrame(nextFrame);

			lastRead = (int)(newPosition - nextFrame * DecodeBuffer.BytesPerFrame);
			return _position = newPosition;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			int bytesRead = 0;
			Memory<byte> bufferMem = new Memory<byte>(buffer).Slice(offset, count);

			if (lastRead > 0 && lastRead < DecodeBuffer.BytesPerFrame)
				lastRead += copyAudio(lastRead);

			while (bufferMem.Length > 0)
			{
				if (!DecodeBuffer.TryDequeue(out lastWaveEntry))
					return bytesRead;

				lastRead = copyAudio(0);
			}

			return bytesRead;

			int copyAudio(int start)
			{
				int toCopy = Math.Min(bufferMem.Length, DecodeBuffer.BytesPerFrame - start);
				var entry = lastWaveEntry.Slice(start, toCopy);

				var amplitudeScale = Volume == 0f ? 0 : Math.Pow(10, Math.Log2(Volume) / 2);

				var shorts = MemoryMarshal.Cast<byte, short>(entry.Span);
				for (int i = 0; i < shorts.Length; i++)
					shorts[i] = (short)(shorts[i] * amplitudeScale);

				entry.CopyTo(bufferMem);

				_position += toCopy;
				bytesRead += toCopy;
				bufferMem = bufferMem[toCopy..];
				return toCopy;
			}
		}
	}
}
