using System.IO;
using System.Text;

namespace NAudio.Lame.ID3
{
	public class UnknownFrame : Frame
	{
		public override int Size => Blob.Length;
		public byte[] Blob { get; }
		public UnknownFrame(Stream file, FrameHeader header, Frame parent) : base(header, parent)
		{
			Blob = new byte[header.Size];
			file.Read(Blob);
		}
		public override void Render(Stream file) => file.Write(Blob);
		public string DataText => Encoding.ASCII.GetString(Blob);
		public override string ToString() => Header.FrameID;
	}
}
