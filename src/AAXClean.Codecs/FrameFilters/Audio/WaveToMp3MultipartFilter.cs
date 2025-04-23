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
		private LameMP3FileWriter? Writer;
		private LameConfig LameConfig;
		private Stream? OutputStream;

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
			=> Writer?.Write(audioFrame.FrameData.Span);

		protected override void CreateNewWriter(NewMP3SplitCallback callback)
		{
			callback.LameConfig = LameConfig;
			newFileCallback(callback);

			if (callback.OutputFile is not Stream outFile)
				throw new InvalidOperationException("Output file stream null");

			LameConfig = callback.LameConfig;
			CurrentWriterOpen = true;
			if (LameConfig.ID3 is ID3TagData tagData)
			{
				tagData.Track = $"{callback.TrackNumber}/{callback.TrackCount}";
				tagData.Title = callback.TrackTitle ?? tagData.Title;
			}
			OutputStream = outFile;
			Writer = new LameMP3FileWriter(outFile, WaveFormat, LameConfig);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && !Disposed && CurrentWriterOpen)
				CloseCurrentWriter();
			base.Dispose(disposing);
		}
	}
}
