using AAXClean.FrameFilters;
using AAXClean.FrameFilters.Audio;
using Mpeg4Lib.Boxes;
using System.IO;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AAXClean.Codecs.FrameFilters.Audio
{
	internal class WaveToAacFilter : FrameFinalBase<WaveEntry>
	{
		private readonly FfmpegAacEncoder aacEncoder;
		private readonly Mp4aWriter Mp4AWriter;
		private readonly Stream outputFile;
		private Func<ChapterInfo> GetChapterDelegate;
		public ChapterInfo Chapters => GetChapterDelegate?.Invoke();
		protected override int InputBufferSize => 200;

		int chunkCount = 0;
		public bool Closed { get; private set; }

		private static readonly int[] asc_samplerates = { 96000, 88200, 64000, 48000, 44100, 32000, 24000, 22050, 16000, 12000, 11025, 8000, 7350 };

		public WaveToAacFilter(Stream mp4Output, FtypBox ftyp, MoovBox moov, WaveFormat waveFormat, long bitrate, double quality)
		{
			outputFile = mp4Output;
			long audioSize = moov.AudioTrack.Mdia.Minf.Stbl.Stsz.SampleSizes.Sum(s => (long)s);
			Mp4AWriter = new Mp4aWriter(mp4Output, ftyp, moov, audioSize > uint.MaxValue);
			aacEncoder = new FfmpegAacEncoder(waveFormat, bitrate, quality);

			int sampleRateIndex = Array.IndexOf(asc_samplerates, waveFormat.SampleRate);

			Mp4AWriter.Moov.AudioTrack.Mdia.Mdhd.Timescale = (uint)waveFormat.SampleRate;
			Mp4AWriter.Moov.AudioTrack.Mdia.Minf.Stbl.Stsd.AudioSampleEntry.SampleRate = (ushort)waveFormat.SampleRate;
			Mp4AWriter.Moov.AudioTrack.Mdia.Minf.Stbl.Stsd.AudioSampleEntry.Esds.ES_Descriptor.DecoderConfig.AudioSpecificConfig.SamplingFrequencyIndex = sampleRateIndex;
			Mp4AWriter.Moov.AudioTrack.Mdia.Minf.Stbl.Stsd.AudioSampleEntry.Esds.ES_Descriptor.DecoderConfig.AudioSpecificConfig.ChannelConfiguration = waveFormat.Channels;

			if (Mp4AWriter.Moov.TextTrack is not null)
			{
				Mp4AWriter.Moov.TextTrack.Mdia.Mdhd.Timescale = (uint)waveFormat.SampleRate;
			}
		}
		protected override Task PerformFilteringAsync(WaveEntry input)
		{
			FrameEntry encodedAac = aacEncoder.EncodeWave(input);

			if (encodedAac?.FrameData.Length > 0)
			{
				Mp4AWriter.AddFrame(encodedAac.FrameData.Span, chunkCount++ == 0);
				chunkCount %= 20;
			}
			input.Dispose();

			return Task.CompletedTask;
		}

		public void SetChapterDelegate(Func<ChapterInfo> getChapterDelegate)
		{
			GetChapterDelegate = getChapterDelegate;
		}

		protected override Task FlushAsync()
		{
			var flushedFrame = aacEncoder.EncodeFlush();
			Mp4AWriter.AddFrame(flushedFrame.FrameData.Span, newChunk: false);
			CloseWriter();
			return Task.CompletedTask;
		}

		private void CloseWriter()
		{
			if (Closed) return;
			ChapterInfo chinf = Chapters;
			if (chinf is not null)
			{
				Mp4AWriter.WriteChapters(chinf);
			}
			Mp4AWriter.Close();
			outputFile.Close();
			Closed = true;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && !Disposed)
			{
				aacEncoder?.Dispose();
				Mp4AWriter?.Dispose();
			}
			base.Dispose(disposing);
		}
	}
}
