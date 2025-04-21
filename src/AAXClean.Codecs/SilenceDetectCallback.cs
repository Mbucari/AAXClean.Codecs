using System;

namespace AAXClean.Codecs
{
	public class SilenceDetectCallback
	{
		public double SilenceThreshold { get; }
		public TimeSpan MinimumDuration { get; }
		public SilenceEntry Silence { get; }

		internal SilenceDetectCallback(double silenceThreshold, TimeSpan minimumDuration, SilenceEntry silence)
		{
			SilenceThreshold = silenceThreshold;
			MinimumDuration = minimumDuration;
			Silence = silence;
		}
	}
}
