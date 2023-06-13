using System;
using System.IO;
using System.Text;

namespace NAudio.Lame.ID3
{
	public abstract class Header
	{
		public abstract string Identifier { get; }
		public long OriginalPosition { get; protected init; }
		public int Size { get; protected init; }
		public abstract int HeaderSize { get; }
		public abstract void Render(Stream stream, int renderSize);

		public static Header Create(Stream file)
		{
			var originalPosition = file.Position;
			Span<byte> bts = stackalloc byte[4];
			file.Read(bts);

			if (bts[0] == 0x49 && bts[1] == 0x44 && bts[2] == 0x33) //"ID3"
			{
				ushort version = (ushort)(bts[3] << 8 | file.ReadByte());

				return new Id3Header(version, file) { OriginalPosition = originalPosition };
			}
			else
			{
				var frameID = Encoding.ASCII.GetString(bts);
				var size = (int)ReadUInt32BE(file);
				var flags = ReadUInt16BE(file);
				return new FrameHeader(frameID, flags) { Size = size, OriginalPosition = originalPosition };
			}
		}

		public static ushort ReadUInt16BE(Stream stream)
		{
			Span<byte> word = stackalloc byte[2];
			stream.Read(word);

			return (ushort)(word[0] << 8 | word[1]);
		}

		public static uint ReadUInt32BE(Stream stream)
		{
			Span<byte> dword = stackalloc byte[4];
			stream.Read(dword);

			return (uint)(dword[0] << 24 | dword[1] << 16 | dword[2] << 8 | dword[3]);
		}

		public static void WriteUInt32BE(Stream stream, uint value)
			=> stream.Write(stackalloc byte[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value });

		public static void WriteUInt16BE(Stream stream, uint value)
			=> stream.Write(stackalloc byte[] { (byte)(value >> 8), (byte)value });

		public override string ToString() => Identifier;
	}
}
