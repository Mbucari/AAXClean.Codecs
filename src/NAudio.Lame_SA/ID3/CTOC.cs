using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NAudio.Lame.ID3
{
	public class CTOC : Frame
	{
		public override int Size => Encoding.ASCII.GetByteCount(ElementID) + 3 + Chapters.Sum(c => Encoding.ASCII.GetByteCount(c.ChapterID) + 1);
		public string ElementID { get; private init; }
		public ChapterFlags ChapterFlags { get; private init; }
		public List<CHAP> Chapters { get; } = new();
		public void Add(CHAP chapter) => Chapters.Add(chapter);

		public CTOC(ChapterFlags chapterFlags) : base(new FrameHeader("CTOC", 0), null)
		{
			ChapterFlags = chapterFlags;
			ElementID = "TOC1";
		}

		public override void Render(Stream file)
		{
			file.Write(Encoding.ASCII.GetBytes(ElementID));
			file.WriteByte(0);
			file.WriteByte((byte)ChapterFlags);
			file.WriteByte((byte)Chapters.Count);

			foreach (var ch in Chapters)
			{
				file.Write(Encoding.ASCII.GetBytes(ch.ChapterID));
				file.WriteByte(0);
			}
		}
	}
}
