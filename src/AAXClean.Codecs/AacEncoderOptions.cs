namespace AAXClean.Codecs
{
	public class AacEncodingOptions
	{
		public SampleRate? SampleRate { get; set; }
		public bool? Stereo { get; set; }
		public double? EncoderQuality { get; set; }
		public long? BitRate { get; set; }
	}
}
