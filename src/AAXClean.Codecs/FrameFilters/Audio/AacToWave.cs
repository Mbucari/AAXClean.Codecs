using AAXClean.FrameFilters;
using System;
using System.Buffers;


namespace AAXClean.Codecs.FrameFilters.Audio
{
	internal sealed class AacToWave : FrameTransformBase<FrameEntry, WaveEntry>
	{
		internal static int BitsPerSample => FfmpegAacDecoder.BITS_PER_SAMPLE;
		public int Channels => AacDecoder.Channels;
		public int SampleRate => AacDecoder.SampleRate;

		readonly FfmpegAacDecoder AacDecoder;
		public AacToWave(byte[] asc)
		{
			AacDecoder = new FfmpegAacDecoder(asc);
		}

		protected override WaveEntry PerformFiltering(FrameEntry input)
		{
			(MemoryHandle, Memory<byte>) decoded = AacDecoder.DecodeRaw(input.FrameData.Span, input.FrameDelta);
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
				AacDecoder.Dispose();
			}
			base.Dispose(disposing);
		}
	}
}
