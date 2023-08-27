using AAXClean.FrameFilters.Audio;
using NAudio.Lame;
using System;
using System.IO;

namespace AAXClean.Codecs.FrameFilters.Audio
{
	internal sealed class WaveToMp3MultipartFilter : MultipartFilterBase<WaveEntry, NewMP3SplitCallback>
	{
		protected override int InputBufferSize => 100;

		private readonly Action<NewMP3SplitCallback> newFileCallback;
		private bool CurrentWriterOpen;
		private readonly WaveFormat WaveFormat;
		private LameMP3FileWriter Writer;
		private LameConfig LameConfig;
		private Stream OutputStream;

		public WaveToMp3MultipartFilter(ChapterInfo splitChapters, WaveFormat waveFormat, LameConfig lameConfig, Action<NewMP3SplitCallback> newFileCallback)
			: base(splitChapters, waveFormat.SampleRateEnum, waveFormat.Channels == 2)
		{
			WaveFormat = waveFormat;
			LameConfig = lameConfig;
			this.newFileCallback = newFileCallback;
		}

		protected override void CloseCurrentWriter()
		{
			if (!CurrentWriterOpen) return;

			Writer?.Flush();
			Writer?.Close();
			Writer?.Dispose();
			OutputStream?.Close();
			OutputStream?.Dispose();
			CurrentWriterOpen = false;
		}

		protected override void WriteFrameToFile(WaveEntry audioFrame, bool newChunk)
			=> Writer.Write(audioFrame.FrameData.Span);

		protected override void CreateNewWriter(NewMP3SplitCallback callback)
		{
			callback.LameConfig = LameConfig;
			newFileCallback(callback);
			LameConfig = callback.LameConfig;
			CurrentWriterOpen = true;

			LameConfig.ID3.Track = $"{callback.TrackNumber}/{callback.TrackCount}";
			LameConfig.ID3.Title = callback.TrackTitle ?? LameConfig.ID3.Title;
			OutputStream = callback.OutputFile;
			Writer = new LameMP3FileWriter(OutputStream, WaveFormat, LameConfig);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && !Disposed && CurrentWriterOpen)
				CloseCurrentWriter();
			base.Dispose(disposing);
		}
	}
}
