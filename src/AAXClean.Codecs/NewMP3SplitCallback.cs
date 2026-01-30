using Mpeg4Lib;
using NAudio.Lame;
using System.IO;

namespace AAXClean.Codecs
{
	public class NewMP3SplitCallback : INewSplitCallback<NewMP3SplitCallback>
	{
		public Chapter Chapter { get; }
		public int? TrackNumber { get; set; }
		public int? TrackCount { get; set; }
		public string? TrackTitle { get; set; }
		public Stream? OutputFile { get; set; }
		public LameConfig? LameConfig { get; set; }

		private NewMP3SplitCallback(Chapter chapter)
			=> Chapter = chapter;

		public static NewMP3SplitCallback Create(Chapter chapter)
		{
			return new NewMP3SplitCallback(chapter);
		}
	}
}
