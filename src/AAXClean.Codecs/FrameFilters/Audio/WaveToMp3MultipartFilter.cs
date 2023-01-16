using AAXClean.FrameFilters.Audio;
using NAudio.Lame;
using System;
using System.IO;

namespace AAXClean.Codecs.FrameFilters.Audio
{
	internal sealed class WaveToMp3MultipartFilter : MultipartFilterBase<WaveEntry, NewMP3SplitCallback>
	{
		private Action<NewMP3SplitCallback> NewFileCallback { get; }

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
			NewFileCallback = newFileCallback;
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
		{
			Writer.Write(audioFrame.FrameData.Span);
			audioFrame.hFrameData.Dispose();
		}

		protected override void CreateNewWriter(NewMP3SplitCallback callback)
		{
			CurrentWriterOpen = true;
			callback.LameConfig = LameConfig;

			NewFileCallback(callback);
			LameConfig.ID3.Track = callback.TrackNumber.ToString();
			LameConfig.ID3.Title = callback.TrackTitle ?? LameConfig.ID3.Title;
			OutputStream = callback.OutputFile;
			Writer = new LameMP3FileWriter(OutputStream, WaveFormat, LameConfig);
		}
		protected override void Dispose(bool disposing)
		{
			if (!Disposed)
			{
				if (disposing && CurrentWriterOpen)
				{
					CloseCurrentWriter();
				}
				base.Dispose(disposing);
			}
		}
	}
}
