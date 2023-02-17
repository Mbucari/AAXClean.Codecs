using AAXClean.FrameFilters;
using AAXClean.FrameFilters.Audio;
using Mpeg4Lib.Boxes;
using System;
using System.IO;
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

		private const int FRAMES_PER_CHUNK = 20;
		private int FramesInCurrentChunk = 0;
		public bool Closed { get; private set; }

		public WaveToAacFilter(Stream mp4Output, FtypBox ftyp, MoovBox moov, WaveFormat waveFormat, long? bitrate, double? quality)
		{
			outputFile = mp4Output;
			long audioSize = moov.AudioTrack.Mdia.Minf.Stbl.Stsz.SampleSizes.Cast<long>().Sum();
			Mp4AWriter = new Mp4aWriter(mp4Output, ftyp, moov, audioSize > uint.MaxValue, waveFormat.SampleRate, waveFormat.Channels);
			aacEncoder = new FfmpegAacEncoder(waveFormat, bitrate, quality);
		}
		protected override Task PerformFilteringAsync(WaveEntry input)
		{
			foreach (var encodedAac in aacEncoder.EncodeWave(input))
			{
				Mp4AWriter.AddFrame(encodedAac.FrameData.Span, FramesInCurrentChunk++ == 0);
				FramesInCurrentChunk %= FRAMES_PER_CHUNK;
			}

			return Task.CompletedTask;
		}

		public void SetChapterDelegate(Func<ChapterInfo> getChapterDelegate)
		{
			GetChapterDelegate = getChapterDelegate;
		}

		protected override Task FlushAsync()
		{
			foreach (var flushedFrame in aacEncoder.EncodeFlush())
			{
				Mp4AWriter.AddFrame(flushedFrame.FrameData.Span, newChunk: false);
			}
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
