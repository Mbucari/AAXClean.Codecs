using AAXClean.FrameFilters;
using Mpeg4Lib.Boxes;

namespace AAXClean.Codecs.FrameFilters.Audio
{
	internal sealed class AacToWave : FrameTransformBase<FrameEntry, WaveEntry>
	{
		protected override int InputBufferSize => 300;
		public WaveFormat WaveFormat => AacDecoder.WaveFormat;

		private readonly FfmpegAacDecoder AacDecoder;
		public AacToWave(AudioSampleEntry audioSampleEntry, WaveFormatEncoding waveFormat, SampleRate sampleRate, bool stereo)
		{
			AacDecoder = new FfmpegAacDecoder(audioSampleEntry, waveFormat, sampleRate, stereo);
		}
		public AacToWave(AudioSampleEntry audioSampleEntry, WaveFormatEncoding waveFormat)
		{
			AacDecoder = new FfmpegAacDecoder(audioSampleEntry, waveFormat);
		}

		protected override WaveEntry PerformFinalFiltering() => AacDecoder.DecodeFlush();
		public override WaveEntry PerformFiltering(FrameEntry input) => AacDecoder.DecodeWave(input);

		protected override void Dispose(bool disposing)
		{
			if (disposing && !Disposed)
				AacDecoder.Dispose();
			base.Dispose(disposing);
		}
	}
}
