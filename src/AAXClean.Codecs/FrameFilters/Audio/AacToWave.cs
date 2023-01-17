using AAXClean.FrameFilters;


namespace AAXClean.Codecs.FrameFilters.Audio
{
	internal sealed class AacToWave : FrameTransformBase<FrameEntry, WaveEntry>
	{
		protected override int InputBufferSize => 300;
		public WaveFormat WaveFormat => AacDecoder.WaveFormat;

		readonly FfmpegAacDecoder AacDecoder;
		public AacToWave(byte[] asc, WaveFormatEncoding waveFormat, SampleRate sampleRate, bool stereo)
		{
			AacDecoder = new FfmpegAacDecoder(asc, waveFormat, sampleRate, stereo);
		}
		public AacToWave(byte[] asc, WaveFormatEncoding waveFormat)
		{
			AacDecoder = new FfmpegAacDecoder(asc, waveFormat);
		}

		protected override WaveEntry PerformFinalFiltering() => AacDecoder.DecodeFlush();
		protected override WaveEntry PerformFiltering(FrameEntry input) => AacDecoder.DecodeWave(input);

		protected override void Dispose(bool disposing)
		{
			if (disposing &&!Disposed)
				AacDecoder.Dispose();
			base.Dispose(disposing);
		}
	}
}
