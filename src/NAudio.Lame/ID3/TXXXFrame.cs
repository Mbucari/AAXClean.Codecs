using System.IO;
using System.Text;

namespace NAudio.Lame.ID3
{
	public class TXXXFrame : Frame
	{
		public override int Size
		{
			get
			{
				var nameSz = Encoding.UTF8.GetByteCount(FieldName);
				var valueSz = Encoding.UTF8.GetByteCount(FieldValue);

				if (nameSz == FieldName.Length && valueSz == FieldValue.Length)
					return 1 + nameSz + 1 + valueSz;
				else
					return 1 + 2 + 2 * FieldName.Length + 2 + 2 + 2 * FieldValue.Length;
			}
		}


		public string FieldName { get; }
		public string FieldValue { get; }
		public TXXXFrame(Stream file, FrameHeader header, Frame parent) : base(header, parent)
		{
			var startPos = file.Position;
			bool unicode = file.ReadByte() == 1;
			FieldName = ReadNullTerminatedString(file, unicode);
			FieldValue = ReadSizeString(file, unicode, (int)(startPos + header.Size - file.Position));
		}

		public override string ToString() => FieldName;

		public override void Render(Stream file)
		{
			var nameSz = Encoding.UTF8.GetByteCount(FieldName);
			var valueSz = Encoding.UTF8.GetByteCount(FieldValue);

			if (nameSz == FieldName.Length && valueSz == FieldValue.Length)
			{
				file.WriteByte(0);
				file.Write(Encoding.ASCII.GetBytes(FieldName));
				file.WriteByte(0);
				file.Write(Encoding.ASCII.GetBytes(FieldValue));
			}
			else
			{

				file.WriteByte(1);
				file.Write(Encoding.Unicode.GetPreamble());
				file.Write(Encoding.Unicode.GetBytes(FieldName));
				file.Write(new byte[2]);
				file.Write(Encoding.Unicode.GetPreamble());
				file.Write(Encoding.Unicode.GetBytes(FieldValue));
			}
		}
	}
}
