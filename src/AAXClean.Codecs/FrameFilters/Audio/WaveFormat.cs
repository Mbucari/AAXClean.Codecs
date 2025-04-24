namespace AAXClean.Codecs.FrameFilters.Audio
{
	public enum WaveFormatEncoding : int
	{
		Pcm = 1,
	}

	public class WaveFormat : NAudio.Wave.WaveFormat
	{
		public SampleRate SampleRateEnum { get; }
		public WaveFormat(SampleRate sampleRate, WaveFormatEncoding format, bool stereo)
		{
			SampleRateEnum = sampleRate;
			this.sampleRate = (int)sampleRate;
			channels = (short)(stereo ? 2 : 1);
			bitsPerSample = (short)(format is WaveFormatEncoding.Pcm ? 16 : 32);
			blockAlign = (short)(channels * bitsPerSample / 8);
			averageBytesPerSecond = blockAlign * this.sampleRate;
			waveFormatTag = (NAudio.Wave.WaveFormatEncoding)format;
		}
	}
}
