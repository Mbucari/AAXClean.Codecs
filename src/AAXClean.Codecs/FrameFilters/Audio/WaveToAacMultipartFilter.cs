using AAXClean.FrameFilters.Audio;
using Mpeg4Lib.Boxes;
using System;

namespace AAXClean.Codecs.FrameFilters.Audio
{
	internal class WaveToAacMultipartFilter : MultipartFilterBase<WaveEntry, NewAacSplitCallback>
	{
		private Action<NewAacSplitCallback> NewFileCallback { get; }
		protected override int InputBufferSize => 100;

		private bool currentWriterOpen;
		private AacEncodingOptions encodingOptions;
		private Mp4aWriter? mp4writer;
		private FfmpegAacEncoder? aacEncoder;
		private int framesInCurrentChunk = 0;

		private readonly WaveFormat waveFormat;
		private readonly FtypBox ftyp;
		private readonly MoovBox moov;
		private const int FRAMES_PER_CHUNK = 20;

		public WaveToAacMultipartFilter(ChapterInfo splitChapters, FtypBox ftyp, MoovBox moov, WaveFormat waveFormat, AacEncodingOptions encoderOptions, Action<NewAacSplitCallback> newFileCallback)
			: base(splitChapters, waveFormat.SampleRateEnum, waveFormat.Channels == 2)
		{
			this.ftyp = ftyp;
			this.moov = moov;
			this.waveFormat = waveFormat;
			encodingOptions = encoderOptions;
			NewFileCallback = newFileCallback;
		}

		protected override void CloseCurrentWriter()
		{
			if (!currentWriterOpen || aacEncoder is null) return;

			foreach (var flushedFrame in aacEncoder.EncodeFlush())
			{
				mp4writer?.AddFrame(flushedFrame.FrameData.Span, newChunk: false, flushedFrame.SamplesInFrame);
			}
			mp4writer?.Close();
			mp4writer?.OutputFile.Close();
			mp4writer?.Dispose();
			currentWriterOpen = false;
		}

		protected override void WriteFrameToFile(WaveEntry audioFrame, bool _)
		{
			if (aacEncoder is null) return;

			foreach (var encodedAac in aacEncoder.EncodeWave(audioFrame))
			{
				mp4writer?.AddFrame(encodedAac.FrameData.Span, framesInCurrentChunk++ == 0, encodedAac.SamplesInFrame);
				framesInCurrentChunk %= FRAMES_PER_CHUNK;
			}
		}

		protected override void CreateNewWriter(NewAacSplitCallback callback)
		{
			callback.EncodingOptions = encodingOptions;
			NewFileCallback(callback);
			if (callback.OutputFile is not System.IO.Stream outFile)
				throw new InvalidOperationException("Output file stream null");

			encodingOptions = callback.EncodingOptions;
			aacEncoder = new FfmpegAacEncoder(waveFormat, encodingOptions?.BitRate, encodingOptions?.EncoderQuality);
			var ascBytes = aacEncoder.GetAudioSpecificConfig();
			mp4writer = new Mp4aWriter(outFile, ftyp, moov, ascBytes);
			currentWriterOpen = true;
			framesInCurrentChunk = 0;
			mp4writer.RemoveTextTrack();

			if (mp4writer.Moov.ILst is not null)
			{
				var tags = new AppleTags(mp4writer.Moov.ILst);
				if (callback.TrackNumber.HasValue && callback.TrackCount.HasValue)
					tags.Tracks = (callback.TrackNumber.Value, callback.TrackCount.Value);
				tags.Title = callback.TrackTitle ?? tags.Title;
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && !Disposed)
			{
				aacEncoder?.Dispose();
				mp4writer?.Dispose();
			}
			base.Dispose(disposing);
		}
	}
}
