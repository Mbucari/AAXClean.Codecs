using AAXClean.FrameFilters;
using System;
using System.Buffers;

namespace AAXClean.Codecs.FrameFilters.Audio
{
	internal class WaveEntry : FrameEntry, IDisposable
	{
		public MemoryHandle hFrameData;
		public void Dispose() => hFrameData.Dispose();
	}
}
