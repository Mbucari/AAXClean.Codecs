using System;
using System.IO;
using System.Text;

namespace NAudio.Lame.ID3
{
	public class APICFrame : Frame
	{
		public override int Size => ImageFormat.Length + 1 + Image.Length;
		public string ImageFormat { get; set; }
		public short Type { get; set; }
		public byte[] Image { get; set; }
		public APICFrame(Stream file, FrameHeader header, Frame parent) : base(header, parent)
		{
			var startPos = file.Position;
			file.ReadByte();
			ImageFormat = ReadNullTerminatedString(file, false);
			var word = new byte[2];
			file.Read(word);
			Type = (short)(word[1] | word[0]);
			Image = new byte[(int)(startPos + header.Size - file.Position)];
			file.Read(Image);
		}


		public override void Render(Stream file)
		{
			file.WriteByte(0);
			file.Write(Encoding.ASCII.GetBytes(ImageFormat));
			file.WriteByte(0);
			file.WriteByte((byte)(Type & 0xff));
			file.WriteByte((byte)(Type >> 8));
			file.Write(Image);
		}
	}
}
