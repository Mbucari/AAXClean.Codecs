using AAXClean.FrameFilters;
using AAXClean.FrameFilters.Audio;
using System.IO;
using System.Threading.Tasks;

namespace AAXClean.Codecs.FrameFilters.Audio
{
	public class WaveToAacFilter : FrameFinalBase<WaveEntry>
	{
		private readonly FfmpegAacEncoder aacEncoder;
		private readonly Mp4aWriter Mp4aWriter;
		private readonly ChapterQueue ChapterQueue;
		protected override int InputBufferSize => 200;

		private const int FRAMES_PER_CHUNK = 20;
		private int FramesInCurrentChunk = 0;
		public bool Closed { get; private set; }

        internal WaveToAacFilter(Stream mp4Output, Mp4File mp4File, ChapterQueue chapterQueue, WaveFormat waveFormat, long? bitrate, double? quality)
		{
			ChapterQueue = chapterQueue;
            aacEncoder = new FfmpegAacEncoder(waveFormat, bitrate, quality);
			var asc = aacEncoder.GetAudioSpecificConfig();
            Mp4aWriter = new Mp4aWriter(mp4Output, mp4File.Ftyp, mp4File.Moov, asc);
		}

		protected override Task PerformFilteringAsync(WaveEntry input)
        {
            foreach (var encodedAac in aacEncoder.EncodeWave(input))
			{
				bool newChunk = FramesInCurrentChunk++ == 0;

				//Write chapters as soon as they're available.
				while (ChapterQueue?.TryGetNextChapter(out var chapterEntry) is true)
				{
					Mp4aWriter.WriteChapter(chapterEntry);
					newChunk = true;
				}

				Mp4aWriter.AddFrame(encodedAac.FrameData.Span, newChunk);
				FramesInCurrentChunk %= FRAMES_PER_CHUNK;
			}

			return Task.CompletedTask;
		}

		protected override Task FlushAsync()
        {
            foreach (var flushedFrame in aacEncoder.EncodeFlush())
			{
				Mp4aWriter.AddFrame(flushedFrame.FrameData.Span, newChunk: false);
			}

			//Write any remaining chapters
			while (ChapterQueue?.TryGetNextChapter(out var chapterEntry) is true)
				Mp4aWriter.WriteChapter(chapterEntry);

			CloseWriter();
			return Task.CompletedTask;
		}

		private void CloseWriter()
		{
			if (Closed) return;
			Mp4aWriter.Close();
			Closed = true;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && !Disposed)
			{
				aacEncoder?.Dispose();
				Mp4aWriter?.Dispose();
			}
			base.Dispose(disposing);
		}
	}
}
