using AAXClean.FrameFilters;
using Mpeg4Lib.Chunks;
using System;
using System.Buffers;

namespace AAXClean.Codecs.FrameFilters.Audio
{
	internal class WaveEntry : IFrameEntry
	{
		public ChunkEntry Chunk { get; set; }
		public uint FrameDelta { get; set; }
		public Memory<byte> FrameData { get; set; }
		public MemoryHandle hFrameData;
		public int FrameSize { get; set; }
		public uint FrameIndex { get; set; }
	}
}
