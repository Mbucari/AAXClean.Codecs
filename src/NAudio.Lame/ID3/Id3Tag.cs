using System.IO;
using System.Linq;

namespace NAudio.Lame.ID3
{
	public class Id3Tag : Frame
	{
		public override int Size =>  Children.Sum(f => f.Size + 10);
		public int RenderSize => 10 + Size;

		internal Id3Tag(Stream file, Header header) : base(header, null)
		{
			LoadChildren(file);
		}

		public static Id3Tag? Create(Stream stream) => TagFactory.CreateTag(stream, null) is Id3Tag id3 ? id3 : null;

		public void Add(Frame frame) => Children.Add(frame);

		public override void Render(Stream file) { }
	}
}
