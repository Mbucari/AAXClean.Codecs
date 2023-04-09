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
		public string DataText => (Blob[0] == 0 ? Encoding.ASCII : Encoding.Unicode).GetString(Blob, 1, Blob.Length - 1);
		public override string ToString() => Header.FrameID;
	}
}
