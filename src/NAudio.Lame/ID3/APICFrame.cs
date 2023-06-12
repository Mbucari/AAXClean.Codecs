using System;
using System.IO;
using System.Text;

namespace NAudio.Lame.ID3
{
	public class APICFrame : Frame
	{
		public override int Size
		{
			get
			{
				var descSize = Encoding.UTF8.GetByteCount(Description);

				var fixedSize = 1 + ImageFormat.Length + 1 + 1 + Image.Length;

				if (descSize == Description.Length)
					return descSize + 1 + fixedSize;
				else
					return UnicodeLength(Description) + 2 + fixedSize;
			}
		}
		public string ImageFormat { get; set; }
		public string Description { get; set; }
		public byte Type { get; set; }
		public byte[] Image { get; set; }
		public APICFrame(Stream file, FrameHeader header, Frame parent) : base(header, parent)
		{
			var startPos = file.Position;
			var textEncoding = file.ReadByte();
			ImageFormat = ReadNullTerminatedString(file, false);
			Description = ReadNullTerminatedString(file, textEncoding == 1);
			Type = (byte)file.ReadByte();
			Image = new byte[(int)(startPos + header.Size - file.Position)];
			file.Read(Image);
		}


		public override void Render(Stream file)
		{
			var descSize = Encoding.UTF8.GetByteCount(Description);
			if (descSize == Description.Length)
				file.WriteByte(0);
			else
				file.WriteByte(1);

			file.Write(Encoding.ASCII.GetBytes(ImageFormat));
			file.WriteByte(0);
			file.WriteByte(Type);

			if (descSize == Description.Length)
			{
				file.Write(Encoding.ASCII.GetBytes(Description));
				file.WriteByte(0);
			}
			else
			{
				file.Write(UnicodeBytes(Description));
				file.Write(new byte[2]);
			}
			file.Write(Image);
		}
	}
}
