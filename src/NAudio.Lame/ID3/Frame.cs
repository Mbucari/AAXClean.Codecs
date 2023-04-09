﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NAudio.Lame.ID3
{
	public abstract class Frame
	{
		public FrameHeader Header { get; }
		public Frame Parent { get; }
		public virtual int Size => Children.Sum(b => b.Size);
		public List<Frame> Children { get; } = new();

		public Frame(FrameHeader header, Frame parent)
		{
			Header = header;
			Parent = parent;
		}

		public void Save(Stream file)
		{
			file.Write(Encoding.ASCII.GetBytes(Header.FrameID));
			Id3Tag.WriteUInt32BE(file, (uint)Size);
			Id3Tag.WriteUInt16BE(file, Header.Flags);

			Render(file);

			foreach (var child in Children)
				child.Save(file);
		}

		public static string ReadSizeString(Stream file, bool unicode, int bytes)
		{
			var buff= new byte[bytes];
			file.Read(buff);

			return (unicode ? Encoding.Unicode : Encoding.ASCII).GetString(buff);
		}

		public static string ReadNullTerminatedString(Stream file, bool unicode)
		{
			List<byte> lst = new List<byte>();
			if (unicode)
			{
				var blob = new byte[2];
				file.Read(blob);
				while (blob[0] != 0 || blob[1] != 0)
				{
					lst.AddRange(blob);
					file.Read(blob);
				}
				return Encoding.Unicode.GetString(lst.ToArray());
			}
			else
			{
				byte b = (byte)file.ReadByte();
				while (b != 0)
				{
					lst.Add(b);
					b = (byte)file.ReadByte();
				}
				return Encoding.ASCII.GetString(lst.ToArray());
			}
		}

		public abstract void Render(Stream file);
	}
}
