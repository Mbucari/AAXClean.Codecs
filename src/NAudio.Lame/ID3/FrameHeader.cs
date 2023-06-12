using System;
using System.IO;
using System.Text;

namespace NAudio.Lame.ID3
{
	public class FrameHeader
	{
		public string FrameID { get; }
		public ushort Flags { get; set; }
		public int Size { get; }
		public long OriginalPosition { get; }

		public FrameHeader(Stream file)
		{
			OriginalPosition = file.Position;
			Span<byte> title = new byte[4];
			file.Read(title);

			FrameID = Encoding.ASCII.GetString(title);
			Size = (int)Id3Tag.ReadUInt32BE(file);
			Flags = Id3Tag.ReadUInt16BE(file);
		}

		public FrameHeader(string frameID, ushort flags)
		{
			FrameID = frameID;
			Flags = flags;
		}

		public override string ToString() => FrameID;
	}
}
