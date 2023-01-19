using AAXClean.FrameFilters;
using System;

namespace AAXClean.Codecs.FrameFilters.Audio
{
	internal class WaveEntry : FrameEntry
	{
		public WaveFormatEncoding Encoding { get; init; }
		/// <summary> Frame data for second channel of 2-channel Planar Audio. </summary>
		public Memory<byte> FrameData2 { get; init; }
	}
}
