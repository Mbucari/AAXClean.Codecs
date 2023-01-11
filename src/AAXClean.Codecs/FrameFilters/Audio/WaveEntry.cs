using AAXClean.FrameFilters;
using Mpeg4Lib.Chunks;
using System;
using System.Buffers;

namespace AAXClean.Codecs.FrameFilters.Audio
{
	internal class WaveEntry : FrameEntry
	{
		public MemoryHandle hFrameData;
	}
}
