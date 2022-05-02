namespace AAXClean.Codecs.FrameFilters.Audio
{
	internal class WaveFormat : NAudio.Wave.WaveFormat
	{
		public WaveFormat(int sampleRate, int bitsPerSample, int channels)
		{
			this.sampleRate = sampleRate;
			this.channels = (short)channels;
			this.bitsPerSample = (short)(bitsPerSample);
			blockAlign = (short)(channels * this.bitsPerSample / 8);
			averageBytesPerSecond = blockAlign * sampleRate;
			waveFormatTag = NAudio.Wave.WaveFormatEncoding.Pcm;
		}
	}
}
