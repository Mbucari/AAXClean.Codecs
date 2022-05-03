using AAXClean.FrameFilters.Audio;
using NAudio.Lame;
using System;

namespace AAXClean.Codecs.FrameFilters.Audio
{
	internal sealed class AacToMp3MultipartFilter : MultipartFilterBase
	{
		protected override Action<NewSplitCallback> NewFileCallback { get; }

		private readonly byte[] ASC;
		private readonly ushort SampleSize;
		private LameConfig LameConfig;
		private AacToMp3Filter AacToMp3Filter;

		public AacToMp3MultipartFilter(ChapterInfo splitChapters, Action<NewSplitCallback> newFileCallback, byte[] audioSpecificConfig, ushort sampleSize, LameConfig lameConfig)
						: base(audioSpecificConfig, splitChapters)
		{
			LameConfig = lameConfig;
			ASC = audioSpecificConfig;
			SampleSize = sampleSize;
			LameConfig = lameConfig;
			NewFileCallback = newFileCallback;
		}

		protected override void CloseCurrentWriter() => AacToMp3Filter?.Dispose();

		protected override void WriteFrameToFile(Span<byte> audioFrame, bool newChunk) => AacToMp3Filter.FilterFrame(default, 0, 0, audioFrame);

		protected override void CreateNewWriter(NewSplitCallback callback)
		{
			callback.UserState = LameConfig;
			NewFileCallback(callback);
			if (callback.UserState is LameConfig lameConfig)
			{
				LameConfig = lameConfig;
				AacToMp3Filter = new AacToMp3Filter(callback.OutputFile, ASC, SampleSize, lameConfig);
			}
			else throw new ArgumentException($"{nameof(NewSplitCallback.UserState)} must be {typeof(LameConfig).Name}");
		}
		protected override void Dispose(bool disposing)
		{
			AacToMp3Filter?.Dispose();
			base.Dispose(disposing);
		}
	}
}
