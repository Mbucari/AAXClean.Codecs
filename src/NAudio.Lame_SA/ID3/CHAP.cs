using LameDLLWrap;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace NAudio.Lame.ID3
{	
	[Flags]
	public enum ChapterFlags
	{
		TopLevel = 1,
		Ordered = 2
	}

	public class CHAP : Frame
	{
		public override int Size => Encoding.ASCII.GetByteCount(ChapterID) + 1 + 4 * 4 + Children.Sum(c => c.Size + 10);
		public string ChapterID { get; set; }
		public TimeSpan StartTime { get; set; }
		public TimeSpan EndTime { get; set; }
		public uint ByteStart { get; set; }
		public uint ByteEnd { get; set; }

		public CHAP(TimeSpan startTime, TimeSpan endTime, int chNum, string title = null) :base(new FrameHeader("CHAP", 0), null)
		{
			ChapterID = $"CH{chNum:D4}";
			StartTime = startTime;
			EndTime = endTime;
			ByteStart = uint.MaxValue;
			ByteEnd = uint.MaxValue;

			if (title is not null)
				TIT2.Create(this, title);
		}

		public override void Render(Stream file)
		{
			file.Write(Encoding.ASCII.GetBytes(ChapterID));
			file.WriteByte(0);
			WriteUInt32BE(file, (uint)Math.Round(StartTime.TotalMilliseconds));
			WriteUInt32BE(file, (uint)Math.Round(EndTime.TotalMilliseconds));
			WriteUInt32BE(file, ByteStart);
			WriteUInt32BE(file, ByteEnd);
		}

		private void WriteUInt32BE(Stream stream, uint value)
			=> stream.Write(stackalloc byte[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value });
	}

	public class TIT2 : Frame
	{
		public override int Size => 1 + Encoding.Unicode.Preamble.Length + Encoding.Unicode.GetByteCount(Title) + 2;
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
			file.Write(UCS2.GetBytes(Title));
		}
	}
}
