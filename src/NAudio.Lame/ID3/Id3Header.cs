using System.IO;

namespace NAudio.Lame.ID3
{
	public class Id3Header : Header
	{
		public override string Identifier => "ID3";
		public ushort Version { get; set; }
		public byte Flags { get; set; }
		public uint ExtendedHeaderSize { get; set; }
		public ushort ExtendedFlags { get; set; }
		public uint SizeOfPadding { get; set; }
		public uint CRC32 { get; set; }
		public override int HeaderSize => (Flags & 0x40) == 0 ? 10 : 20 + ((ExtendedFlags & 0x8000) != 0 ? 4 : 0);

		internal Id3Header(ushort version, Stream file)
		{
			Version = version;
			Flags = (byte)file.ReadByte();

			var size = ReadUInt32BE(file);
			Size = (int)(((size & 0x7f000000) >> 3) | ((size & 0x7f0000) >> 2) | ((size & 0x7f00) >> 1) | (size & 0x7f));

			if ((Flags & 0x40) == 1)
			{
				ExtendedHeaderSize = ReadUInt32BE(file);
				ExtendedFlags = ReadUInt16BE(file);
				SizeOfPadding = ReadUInt32BE(file);
				if (ExtendedHeaderSize == 10)
					CRC32 = ReadUInt32BE(file);
			}
		}

		public override void Render(Stream stream, int renderSize)
		{
			stream.Write(new byte[] { 0x49, 0x44, 0x33 }); //"ID3"
			stream.WriteByte((byte)(Version >> 8));
			stream.WriteByte((byte)(Version & 0xff));
			stream.WriteByte(Flags);
			int size = ((renderSize << 3) & 0x7f000000) | ((renderSize << 2) & 0x7f0000) | ((renderSize << 1) & 0x7f00) | (renderSize & 0x7F);

			WriteUInt32BE(stream, (uint)size);

			if ((Flags & 0x40) == 1)
			{
				if ((ExtendedFlags & 0x8000) != 0)
				{
					WriteUInt32BE(stream, 10);
					WriteUInt16BE(stream, ExtendedFlags);
					WriteUInt32BE(stream, SizeOfPadding);
					WriteUInt32BE(stream, CRC32);
				}
				else
				{
					WriteUInt32BE(stream, 6);
					WriteUInt16BE(stream, ExtendedFlags);
					WriteUInt32BE(stream, SizeOfPadding);
				}
			}
		}
	}
}
