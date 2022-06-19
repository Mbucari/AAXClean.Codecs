using AAXClean.FrameFilters;
using AAXClean.FrameFilters.Audio;
using NAudio.Lame;
using System;
using System.IO;

namespace AAXClean.Codecs.FrameFilters.Audio
{
	internal sealed class AacToMp3MultipartFilter : MultipartFilterBase<WaveEntry, NewMP3SplitCallback>
	{
		private Action<NewMP3SplitCallback> NewFileCallback { get; }

		private bool CurrentWriterOpen;
		private readonly WaveFormat WaveFormat;
		private LameMP3FileWriter Writer;
		private LameConfig LameConfig;
		private Stream OutputStream;

		public AacToMp3MultipartFilter(ChapterInfo splitChapters, Action<NewMP3SplitCallback> newFileCallback, byte[] audioSpecificConfig, ushort sampleSize, LameConfig lameConfig)
						: base(audioSpecificConfig, splitChapters)
		{

			if (sampleSize != AacToWave.BitsPerSample)
				throw new ArgumentException($"{nameof(AacToMp3Filter)} only supports 16-bit aac streams.");

			WaveFormat = new WaveFormat(SampleRate, sampleSize, Channels);
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

		protected override void WriteFrameToFile(FrameEntry audioFrame, bool newChunk)
		{
			if (audioFrame is WaveEntry wave)
			{
				Writer.Write(wave.FrameData.Span);
				wave.hFrameData.Dispose();
			}
			else throw new ArgumentException($"{nameof(audioFrame)} argument to {this.GetType().Name}.{nameof(WriteFrameToFile)} must be a {nameof(WaveEntry)}");
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
