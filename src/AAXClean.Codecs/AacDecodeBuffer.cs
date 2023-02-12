using AAXClean.Codecs.FrameFilters.Audio;
using AAXClean.FrameFilters;
using Mpeg4Lib.Chunks;
using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AAXClean.Codecs
{
	public interface IDecodeBuffer
	{
		long Length { get; }
		NAudio.Wave.WaveFormat WaveFormat { get; }
		TimeSpan Duration { get; }
		int BytesPerFrame { get; }
		int TimeScale { get; }
		bool TryDequeue(out Memory<byte> waveEntry);
		public void SkipToFrame(long nextFrame);
	}

	public class AacDecodeBuffer : IDecodeBuffer
	{
		public NAudio.Wave.WaveFormat WaveFormat { get; }
		public int BytesPerFrame { get; }
		public long Length { get; }
		public int TimeScale { get; }
		public TimeSpan Duration { get; }
		public Mp4File Mp4File { get; }
		public TimeSpan BufferTime
		{
			get => bufferTime;
			set
			{
				bufferTime = value;
				MaxBufferedFrames = (int)Math.Round(bufferTime.TotalSeconds * TimeScale / AAC_SAMPLES_PER_FRAME);
			}
		}

		private CancellationTokenSource CancellationSource;
		private ConcurrentQueue<Memory<byte>> buffer;
		private Task readerTask;
		private TimeSpan bufferTime;
		private int MaxBufferedFrames;
		private bool ended;

		private readonly EventWaitHandle entryAvailable = new(initialState: false, EventResetMode.ManualReset);
		private readonly FrameTransformBase<FrameEntry, FrameEntry> _readAac;
		private readonly AacToWave _aacToWave;
		private readonly List<ChunkEntry> _chunkEntryList;
		private readonly long TotalNumFrames;
		private const int AAC_SAMPLES_PER_FRAME = 1024;

		public AacDecodeBuffer(Mp4File mp4File, TimeSpan bufferTime)
		{
			Mp4File = mp4File;

			_readAac = mp4File.GetAudioFrameFilter();
			_aacToWave = new(mp4File.AscBlob, FrameFilters.Audio.WaveFormatEncoding.Pcm);
			_chunkEntryList = new ChunkEntryList(mp4File.Moov.AudioTrack).ToList();
			TotalNumFrames = Mp4File.Moov.AudioTrack.Mdia.Minf.Stbl.Stsz.SampleCount;

			WaveFormat = _aacToWave.WaveFormat;
			BytesPerFrame = WaveFormat.BlockAlign * 1024;
			Length = WaveFormat.BlockAlign * (long)mp4File.Moov.AudioTrack.Mdia.Mdhd.Duration;
			TimeScale = (int)Mp4File.TimeScale;
			Duration = TimeSpan.FromSeconds(Mp4File.Moov.AudioTrack.Mdia.Mdhd.Duration / (double)TimeScale);
			BufferTime = bufferTime;

			SkipToFrame(0);
		}

		private async Task Reader(long nextFrame)
		{			
			try
			{
				buffer = new();

				foreach (var waveData in GetWaveFramesAsync(nextFrame, TotalNumFrames - nextFrame, CancellationSource.Token))
				{
					while (true)
					{
						if (buffer.Count < MaxBufferedFrames)
						{
							buffer.Enqueue(waveData);
							break;
						}

						await Task.Delay(10, CancellationSource.Token);
					}

					entryAvailable.Set();
				}
				ended = true;
			}
			catch (OperationCanceledException) { }
		}

		public void SkipToFrame(long nextFrame)
		{
			CancellationSource?.Cancel();
			readerTask?.GetAwaiter().GetResult();

			ended = false;
			CancellationSource = new();
			readerTask = Task.Run(() => Reader(nextFrame), CancellationSource.Token);
		}

		public bool TryDequeue(out Memory<byte> waveEntry)
		{
			while (true)
			{
				if (buffer.Any() || ended || entryAvailable.WaitOne(1))
				{
					if (buffer.TryDequeue(out waveEntry))
					{
						entryAvailable.Reset();
						return true;
					}
					return false;
				}
			}
		}

		private IEnumerable<Memory<byte>> GetWaveFramesAsync(long firstFrame, long numFrames, CancellationToken cancellationToken)
		{
			if (numFrames < 1) yield break;

			foreach (var chunkEntry in _chunkEntryList.Where(c => c.FirstFrameIndex + c.FrameSizes.Length >= firstFrame))
			{
				long frameOffset = chunkEntry.ChunkOffset;
				uint frameIndex = chunkEntry.FirstFrameIndex;

				foreach (var frameSize in chunkEntry.FrameSizes)
				{
					if (frameIndex - firstFrame == numFrames || cancellationToken.IsCancellationRequested)
						yield break;

					if (frameIndex >= firstFrame)
					{
						var frameEntry = new FrameEntry
						{
							Chunk = chunkEntry,
							FrameIndex = frameIndex,
							SamplesInFrame = AAC_SAMPLES_PER_FRAME,
							FrameData = new byte[frameSize]
						};

						Mp4File.InputStream.Position = frameOffset;
						Mp4File.InputStream.Read(frameEntry.FrameData.Span);

						var decryptedAac = _readAac.PerformFiltering(frameEntry);
						var waveEntry = _aacToWave.PerformFiltering(decryptedAac);

						yield return waveEntry.FrameData;
					}

					frameOffset += frameSize;
					frameIndex++;
				}
			}
		}
	}
}
