using System.IO;

namespace NAudio.Lame.ID3
{
	internal class TagFactory
	{
		public static Frame CreateTag(Stream file, Frame? parent)
		{
			var header = Header.Create(file);

			if (header is Id3Header id3Header && header.Identifier is "ID3") return new Id3Tag(file, id3Header);
			if (header is not FrameHeader frameHeader) throw new InvalidDataException($"Header must be a {nameof(Id3Header)} or {nameof(FrameHeader)}");
			if (header.Identifier.StartsWith('T') && header.Identifier is not "TXXX") return new TEXTFrame(file, frameHeader, parent);

			return frameHeader.Identifier switch
			{
				"TXXX" => new TXXXFrame(file, frameHeader, parent),
				"APIC" => new APICFrame(file, frameHeader, parent),
				"CHAP" => new CHAPFrame(file, frameHeader, parent),
				"CTOC" => new CTOCFrame(file, frameHeader, parent),
				_ => new UnknownFrame(file, frameHeader, parent),
			};
		}
	}
}
