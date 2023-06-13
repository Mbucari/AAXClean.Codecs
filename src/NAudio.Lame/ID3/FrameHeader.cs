using System.IO;
using System.Text;

namespace NAudio.Lame.ID3
{
	public class FrameHeader : Header
	{
		public override string Identifier { get; }
		public ushort Flags { get; set; }
		public override int HeaderSize => 10;

		public FrameHeader(string frameID, ushort flags)
		{
			Identifier = frameID;
			Flags = flags;
		}

		public override void Render(Stream stream, int renderSize)
		{
			stream.Write(Encoding.ASCII.GetBytes(Identifier));
			WriteUInt32BE(stream, (uint)renderSize);
			WriteUInt16BE(stream, Flags);
		}
	}
}
