using NAudio.Lame;

namespace AAXClean.Codecs
{
	public class NewMP3SplitCallback : NewSplitCallback
	{
		public LameConfig LameConfig { get; set; }
	}
}
