using AAXClean.AudioFilters;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;

namespace AAXClean.Codecs.AudioFilters
{
	internal unsafe class SilenceDetectFilter : AudioFilterBase
	{
		public List<SilenceEntry> Silences { get; }

		private const int VECTOR_COUNT = 8;
		private const int BITS_PER_SAMPLE = 16;
		private readonly FfmpegAacDecoder decoder;
		private readonly Action<SilenceDetectCallback> DetectionCallback;
		private readonly BlockingCollection<MemoryHandle> waveFrameQueue;
		private readonly Task encoderLoopTask;
		private readonly TimeSpan MinDuration;
		private readonly double SilenceThreshold;

		private readonly Vector128<short> maxAmplitudes;
		private readonly Vector128<short> minAmplitudes;
		private readonly Vector128<short> zeros = Vector128<short>.Zero;

		private readonly long numSamples;

		public unsafe SilenceDetectFilter(double db, TimeSpan minDuration, byte[] audioSpecificConfig, ushort sampleSize, Action<SilenceDetectCallback> detectionCallback)
		{
			if (BITS_PER_SAMPLE != sampleSize)
				throw new ArgumentException($"{nameof(AacToMp3Filter)} only supports 16-bit aac streams.");

			SilenceThreshold = db;
			MinDuration = minDuration;

			DetectionCallback = detectionCallback;

			decoder = new FfmpegAacDecoder(audioSpecificConfig);

			Silences = new List<SilenceEntry>();

			short maxAmplitude = (short)(Math.Pow(10, SilenceThreshold / 20) * short.MaxValue);
			short minAmplitude = (short)-maxAmplitude;
			numSamples = (long)Math.Round(decoder.SampleRate * MinDuration.TotalSeconds * decoder.Channels);

			//Initialize vectors for comparisons
			short[] sbytes = new short[VECTOR_COUNT];

			for (int i = 0; i < sbytes.Length; i++)
				sbytes[i] = maxAmplitude;

			fixed (short* s = sbytes)
			{
				maxAmplitudes = Sse2.LoadVector128(s);
			}

			for (int i = 0; i < sbytes.Length; i++)
				sbytes[i] = minAmplitude;

			fixed (short* s = sbytes)
			{
				minAmplitudes = Sse2.LoadVector128(s);
			}

			waveFrameQueue = new BlockingCollection<MemoryHandle>(200);
			encoderLoopTask = new Task(SilenceCheckLoop);
			encoderLoopTask.Start();
		}

		/// <summary>
		/// HIGHLY optimized loop to detect silence in audio stream.
		/// </summary>
		private unsafe void SilenceCheckLoop()
		{
			long currentSample = 0;
			long lastSilenceStart = 0;
			long numConsecutiveSilences = 0;

			//Buffer for storing Vector128<short>
			Memory<short> buff128 = new short[VECTOR_COUNT];
			Span<short> buff128Span = buff128.Span;
			using var hbuff128 = buff128.Pin();
			short* pbuff128 = (short*)hbuff128.Pointer;

			while (waveFrameQueue.TryTake(out MemoryHandle waveFrame, -1))
			{
				short* samples = (short*)waveFrame.Pointer;

				for (int i = 0; i < decoder.DecodeSize / sizeof(short); i += VECTOR_COUNT, currentSample += VECTOR_COUNT)
				{
					//2x compares and an AND is ~3% faster than Abs and 1x compare
					//And for whatever reason, equivalent method with Avx 256-bit vectors is slightly slower.
					var samps = Sse2.LoadVector128(samples + i);
					var comparesLess = Sse2.CompareLessThan(samps, maxAmplitudes);
					var comparesGreater = Sse2.CompareGreaterThan(samps, minAmplitudes);
					var compares = Sse2.And(comparesLess, comparesGreater);

					//loud = 0
					//silent = -1
					var allLoud = Sse41.TestC(zeros, compares); //compares are all 0

					//var anyLoud = !Avx.TestC(compares, ones); //compares have at least one 0

					//Most of the audio will be above "silent", so checking
					//for all silence may be a bit of a waste
					//var allSilent = Sse41.TestC(compares, ones); //compares are all -1

					if (allLoud)
					{
						if (numConsecutiveSilences != 0)
						{
							CheckAndAddSilence(lastSilenceStart, numConsecutiveSilences);

							numConsecutiveSilences = 0;
						}
						continue;
					}

					/*
					else if (allSilent)
					{
						if (numConsecutive == 0)
						{
							lastStart = currentSample;
						}
						numConsecutive += VECTOR_COUNT;
						continue;
					}
					*/

					Sse2.Store(pbuff128, compares);

					for (int j = 0; j < VECTOR_COUNT; j++)
					{
						bool Silence = buff128Span[j] == - 1;
						if (!Silence)
						{
							if (numConsecutiveSilences != 0)
							{
								CheckAndAddSilence(lastSilenceStart, numConsecutiveSilences);

								numConsecutiveSilences = 0;
							}
						}
						else
						{
							if (numConsecutiveSilences == 0)
							{
								lastSilenceStart = currentSample + j;
							}

							numConsecutiveSilences++;
						}
					}
				}

				waveFrame.Dispose();
			}

			CheckAndAddSilence(lastSilenceStart, numConsecutiveSilences);
		}

		private void CheckAndAddSilence(long lastSilenceStart, long numConsecutiveSilences)
		{
			if (numConsecutiveSilences > numSamples)
			{
				var start = TimeSpan.FromSeconds((double)lastSilenceStart / decoder.Channels / decoder.SampleRate);
				var end = TimeSpan.FromSeconds((double)(lastSilenceStart + numConsecutiveSilences) / decoder.Channels / decoder.SampleRate);

				var silence = new SilenceEntry(start, end);
				Silences.Add(silence);
				DetectionCallback?.Invoke(new SilenceDetectCallback(SilenceThreshold, MinDuration, silence));
			}
		}

		public override bool FilterFrame(uint chunkIndex, uint frameIndex, Span<byte> aacSample)
		{
			waveFrameQueue.Add(decoder.DecodeRaw(aacSample));
			return true;
		}

		public override void Close()
		{
			if (Closed) return;
			waveFrameQueue.CompleteAdding();
			encoderLoopTask.Wait();
			Closed = true;
		}

		protected override void Dispose(bool disposing)
		{
			if (!Disposed)
			{
				if (disposing)
				{
					Close();
					decoder?.Dispose();
					waveFrameQueue?.Dispose();
					encoderLoopTask?.Dispose();
				}
				base.Dispose(disposing);
			}
		}
	}
}
