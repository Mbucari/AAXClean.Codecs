﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NAudio.Lame.ID3
{
	public class Id3Tag
	{
		public string Identifier { get; }
		public ushort Version { get; }
		public int Flags { get; }
		public int Size { get; }

		public List<Frame> Frames { get; } = new();
		public Id3Tag(Stream file)
		{
			var id = new byte[3];
			file.Read(id);

			Identifier = Encoding.ASCII.GetString(id);
			Version = ReadUInt16BE(file);
			Flags = file.ReadByte();
			var size = ReadUInt32BE(file);

			Size = (int)(((size & 0x7f000000) >> 3) | ((size & 0x7f0000) >> 2) | ((size & 0x7f00) >> 1) | (size & 0x7f));

			var endPos = file.Position + Size;

			while (endPos > file.Position)
			{
				var header = new FrameHeader(file);

				if (header.FrameID == "\0\0\0\0")
				{
					var bts = new byte[endPos - file.Position];
					file.Read(bts);
					break;
				}
				else if (header.FrameID == "TXXX")
					Frames.Add(new TXXXFrame(file, header, null));
				else if (header.FrameID == "APIC")
					Frames.Add(new APICFrame(file, header, null));
				else
					Frames.Add(new UnknownFrame(file, header, null));
			}
		}

		public void Add(Frame frame) => Frames.Add(frame);

		public void AddToc(CTOC ctoc)
		{
			Add(ctoc);
			foreach (var ch in ctoc.Chapters)
				Add(ch);
		}

		public void Save(Stream file)
		{
			file.Write(new byte[] { 0x49, 0x44, 0x33 });
			WriteUInt16BE(file, 0x300);
			file.WriteByte((byte)Flags);

			var renderSize = Frames.Sum(f => f.Size + 10);

			int size = ((renderSize << 3) & 0x7f000000) | ((renderSize << 2) & 0x7f0000) | ((renderSize << 1) & 0x7f00) | (renderSize & 0x7F);

			WriteUInt32BE(file, (uint)size);

			foreach (var frame in Frames)
				frame.Save(file);
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
	}
}