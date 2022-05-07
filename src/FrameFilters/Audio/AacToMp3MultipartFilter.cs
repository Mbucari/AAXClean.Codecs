using AAXClean.FrameFilters;
using AAXClean.FrameFilters.Audio;
using NAudio.Lame;
using System;
using System.IO;

namespace AAXClean.Codecs.FrameFilters.Audio
{
	internal sealed class AacToMp3MultipartFilter : MultipartFilterBase<WaveEntry>
	{
		protected override Action<NewSplitCallback> NewFileCallback { get; }

		private readonly WaveFormat waveFormat;
		private LameMP3FileWriter Writer;
		private LameConfig LameConfig;
		private Stream OutputStream;

		public AacToMp3MultipartFilter(ChapterInfo splitChapters, Action<NewSplitCallback> newFileCallback, byte[] audioSpecificConfig, ushort sampleSize, LameConfig lameConfig)
						: base(audioSpecificConfig, splitChapters)
		{

			if (sampleSize != AacToWave.BitsPerSample)
				throw new ArgumentException($"{nameof(AacToMp3Filter)} only supports 16-bit aac streams.");


			waveFormat = new WaveFormat(SampleRate, sampleSize, Channels);

			LameConfig = lameConfig;
			NewFileCallback = newFileCallback;
		}

		protected override void CloseCurrentWriter()
		{
			Writer?.Flush();
			Writer?.Close();
			Writer?.Dispose();
			OutputStream?.Close();
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

		protected override void CreateNewWriter(NewSplitCallback callback)
		{
			callback.UserState = LameConfig;
			NewFileCallback(callback);
			if (callback.UserState is LameConfig lameConfig)
			{
				LameConfig = lameConfig;

				OutputStream = callback.OutputFile;
				Writer = new LameMP3FileWriter(OutputStream, waveFormat, lameConfig);
			}
			else throw new ArgumentException($"{nameof(NewSplitCallback.UserState)} must be {typeof(LameConfig).Name}");
		}
		protected override void Dispose(bool disposing)
		{
			if (!Disposed)
			{
				if (disposing)
				{
					Writer?.Dispose();
				}
				base.Dispose(disposing);
			}
		}
	}
}
