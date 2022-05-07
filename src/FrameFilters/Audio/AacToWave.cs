using AAXClean.FrameFilters;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;


namespace AAXClean.Codecs.FrameFilters.Audio
{
	internal sealed unsafe class AacToWave : FrameTransformBase<FrameEntry, WaveEntry>
	{
		internal const int BITS_PER_SAMPLE = 16;
		internal int DecodeSize => AacDecoder.DecodeSize;
		public int Channels => AacDecoder.Channels;
		public int SampleRate => AacDecoder.SampleRate;

		readonly FfmpegAacDecoder AacDecoder;
		public AacToWave(byte[] asc)
		{
			AacDecoder = new FfmpegAacDecoder(asc);
		}

		protected override WaveEntry PerformFiltering(FrameEntry input)
		{
			(MemoryHandle, Memory<byte>) decoded = AacDecoder.DecodeRaw2(input.FrameData.Span);
			return new WaveEntry
			{
				Chunk = input.Chunk,
				FrameDelta = input.FrameDelta,
				FrameData = decoded.Item2,
				hFrameData = decoded.Item1,
				FrameSize = 2 * AacDecoder.Channels * (int)input.FrameDelta,
				FrameIndex = input.FrameIndex
			};
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				AacDecoder.Close();
			}
			base.Dispose(disposing);
		}
	}
}
