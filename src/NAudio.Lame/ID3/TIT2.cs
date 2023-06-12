using System.IO;

namespace NAudio.Lame.ID3
{
	public class TIT2 : Frame
	{
		public override int Size => 1 + UnicodeLength(Title);
		public byte EncodingFlag { get; set; }
		public string Title { get; set; }
		public TIT2(FrameHeader header, Frame parent) : base(header, parent) { }

		public static TIT2 Create(Frame parent, string title)
		{
			var tit2 = new TIT2(new FrameHeader("TIT2", 0), parent)
			{
				EncodingFlag = 1,
				Title = title
			};

			parent?.Children.Add(tit2);
			return tit2;
		}

		public override void Render(Stream file)
		{
			file.WriteByte(EncodingFlag);
			file.Write(UnicodeBytes(Title));
		}
	}
}
