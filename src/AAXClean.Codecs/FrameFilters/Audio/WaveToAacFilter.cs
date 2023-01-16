using AAXClean.FrameFilters;
using AAXClean.FrameFilters.Audio;
using Mpeg4Lib.Boxes;
using System.IO;
using System;
using System.Linq;
using NAudio.Codecs;

namespace AAXClean.Codecs.FrameFilters.Audio
{
	internal class WaveToAacFilter : FrameFinalBase<WaveEntry>
	{
		private readonly FfmpegAacEncoder aacEncoder;
		private readonly Mp4aWriter mp4AWriter;
		private readonly Stream outputFile;

		int chunkCount = 0;
		public bool Closed { get; private set; }

		private static readonly int[] asc_samplerates = { 96000, 88200, 64000, 48000, 44100, 32000, 24000, 22050, 16000, 12000, 11025, 8000, 7350 };

		public WaveToAacFilter(Stream mp4Output, FtypBox ftyp, MoovBox moov, WaveFormat waveFormat)
		{
			outputFile = mp4Output;
			long audioSize = moov.AudioTrack.Mdia.Minf.Stbl.Stsz.SampleSizes.Sum(s => (long)s);
			mp4AWriter = new Mp4aWriter(mp4Output, ftyp, moov, audioSize > uint.MaxValue);
			aacEncoder = new FfmpegAacEncoder(waveFormat);
			mp4AWriter.RemoveTextTrack();

			int sampleRateIndex = Array.IndexOf(asc_samplerates, waveFormat.SampleRate);

			mp4AWriter.Moov.AudioTrack.Mdia.Mdhd.Timescale = (uint)waveFormat.SampleRate;
			mp4AWriter.Moov.AudioTrack.Mdia.Minf.Stbl.Stsd.AudioSampleEntry.SampleRate = (ushort)waveFormat.SampleRate;
			mp4AWriter.Moov.AudioTrack.Mdia.Minf.Stbl.Stsd.AudioSampleEntry.Esds.ES_Descriptor.DecoderConfig.AudioSpecificConfig.SamplingFrequencyIndex = sampleRateIndex;
			mp4AWriter.Moov.AudioTrack.Mdia.Minf.Stbl.Stsd.AudioSampleEntry.Esds.ES_Descriptor.DecoderConfig.AudioSpecificConfig.ChannelConfiguration = waveFormat.Channels;
		}
		protected override void PerformFiltering(WaveEntry input)
		{
			FrameEntry encodedAac = aacEncoder.EncodeWave(input);

			if (encodedAac?.FrameData.Length > 0)
			{
				mp4AWriter.AddFrame(encodedAac.FrameData.Span, chunkCount++ == 0);
				chunkCount %= 20;
			}
			input.hFrameData.Dispose();
		}

		protected override void Flush()
		{
			var flushedFrame = aacEncoder.EncodeFlush();
			mp4AWriter.AddFrame(flushedFrame.FrameData.Span, newChunk: false);
			CloseWriter();
		}

		private void CloseWriter()
		{
			if (Closed) return;
			mp4AWriter.Close();
			outputFile.Close();
			Closed = true;
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if (disposing)
			{
				aacEncoder?.Dispose();
				mp4AWriter?.Dispose();
			}
		}
	}
}
