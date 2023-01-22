using AAXClean.FrameFilters.Audio;
using Mpeg4Lib.Boxes;
using System;

namespace AAXClean.Codecs.FrameFilters.Audio
{
	internal class WaveToAacMultipartFilter : MultipartFilterBase<WaveEntry, NewAacSplitCallback>
	{
		private Action<NewAacSplitCallback> NewFileCallback { get; }
		protected override int InputBufferSize => 100;

		private bool CurrentWriterOpen;
		private readonly WaveFormat WaveFormat;
		private AacEncodingOptions EncodingOptions;
		private readonly FtypBox Ftyp;
		private readonly MoovBox Moov;
		private Mp4aWriter Mp4writer;
		private FfmpegAacEncoder aacEncoder;
		private const int FRAMES_PER_CHUNK = 20;
		private int FramesInCurrentChunk = 0;

		public WaveToAacMultipartFilter(ChapterInfo splitChapters, FtypBox ftyp, MoovBox moov, WaveFormat waveFormat, AacEncodingOptions encoderOptions, Action<NewAacSplitCallback> newFileCallback)
			:base(splitChapters, waveFormat.SampleRateEnum, waveFormat.Channels == 2)
		{
			Ftyp = ftyp;
			Moov = moov;
			WaveFormat = waveFormat;
			EncodingOptions = encoderOptions;
			NewFileCallback = newFileCallback;
		}

		protected override void CloseCurrentWriter()
		{
			if (!CurrentWriterOpen) return;

			foreach (var flushedFrame in aacEncoder.EncodeFlush())
			{
				Mp4writer.AddFrame(flushedFrame.FrameData.Span, newChunk: false);
			}
			Mp4writer?.Close();
			Mp4writer?.OutputFile.Close();
			Mp4writer?.Dispose();
			CurrentWriterOpen = false;
		}

		protected override void WriteFrameToFile(WaveEntry audioFrame, bool _)
		{
			foreach (var encodedAac in aacEncoder.EncodeWave(audioFrame))
			{
				Mp4writer.AddFrame(encodedAac.FrameData.Span, FramesInCurrentChunk++ == 0);
				FramesInCurrentChunk %= FRAMES_PER_CHUNK;
			}
		}

		protected override void CreateNewWriter(NewAacSplitCallback callback)
		{
			callback.EncodingOptions = EncodingOptions;
			NewFileCallback(callback);
			EncodingOptions = callback.EncodingOptions;
			Mp4writer = new Mp4aWriter(callback.OutputFile, Ftyp, Moov, false, WaveFormat.SampleRate, WaveFormat.Channels);
			CurrentWriterOpen = true;
			FramesInCurrentChunk = 0;
			Mp4writer.RemoveTextTrack();
			
			if (Mp4writer.Moov.ILst is not null)
			{
				var tags = new AppleTags(Mp4writer.Moov.ILst);
				if (callback.TrackNumber.HasValue && callback.TrackCount.HasValue)
					tags.Tracks = (callback.TrackNumber.Value, callback.TrackCount.Value);
				tags.Title = callback.TrackTitle ?? tags.Title;
			}
			aacEncoder = new FfmpegAacEncoder(WaveFormat, EncodingOptions?.BitRate, EncodingOptions?.EncoderQuality);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && !Disposed)
			{
				aacEncoder?.Dispose();
				Mp4writer?.Dispose();
			}
			base.Dispose(disposing);
		}
	}
}
