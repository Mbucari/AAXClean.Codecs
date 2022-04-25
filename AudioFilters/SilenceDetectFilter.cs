using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;

namespace AAXClean.AudioFilters
{
	internal unsafe class SilenceDetectFilter : AudioFilterBase
	{

		public List<SilenceEntry> Silences { get; }

		private const int VECTOR_COUNT = 8;
		private const int BITS_PER_SAMPLE = 16;
		private readonly FfmpegAacDecoder decoder;
		private readonly BlockingCollection<MemoryHandle> waveFrameQueue;
		private readonly Task encoderLoopTask;

		private readonly Vector128<short> maxAmplitudes;
		private readonly Vector128<short> minAmplitudes;
		private readonly Vector128<short> zeros;


		private readonly long numSamples;
		public SilenceDetectFilter(double db, TimeSpan minDuration, byte[] audioSpecificConfig, ushort sampleSize)
		{
			if (BITS_PER_SAMPLE != sampleSize)
				throw new ArgumentException($"{nameof(AacToMp3Filter)} only supports 16-bit aac streams.");

			decoder = new FfmpegAacDecoder(audioSpecificConfig);

			Silences = new List<SilenceEntry>();

			short maxAmplitude = (short)(Math.Pow(10, db / 20) * short.MaxValue);
			short minAmplitude = (short)-maxAmplitude;
			numSamples = (long)Math.Round(decoder.SampleRate * minDuration.TotalSeconds * decoder.Channels);


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

			short[] zeroesArr = new short[VECTOR_COUNT];
			fixed (short* ss = zeroesArr)
			{
				zeros = Sse2.LoadVector128(ss);
			}

			waveFrameQueue = new BlockingCollection<MemoryHandle>(200);
			encoderLoopTask = new Task(SilenceCheckLoop);
			encoderLoopTask.Start();
		}

		private void SilenceCheckLoop()
		{
			long currentSample = 0;
			long lastStart = 0;
			long numConsecutive = 0;

			void CheckAndAddSilence()
			{
				if (numConsecutive > numSamples)
				{
					var start = TimeSpan.FromSeconds((double)lastStart / decoder.Channels / decoder.SampleRate);
					var end = TimeSpan.FromSeconds((double)(lastStart + numConsecutive) / decoder.Channels / decoder.SampleRate);
					Silences.Add(new SilenceEntry(start, end));
				}
			}

			while (waveFrameQueue.TryTake(out MemoryHandle waveFrame, -1))
			{
				short* samples = (short*)waveFrame.Pointer;

				for (int i = 0; i < decoder.DecodeSize / sizeof(short); i += VECTOR_COUNT, currentSample += VECTOR_COUNT)
				{
					var samps = Sse2.LoadVector128(samples + i);
					var comparesLess = Sse2.CompareLessThan(samps, maxAmplitudes);
					var comparesGreater = Sse2.CompareGreaterThan(samps, minAmplitudes);
					var compares = Sse2.And(comparesLess, comparesGreater);

					//loud = 0
					//silent = -1
					var allLoud = Sse41.TestC(zeros, compares); //compares are all 0

					//var anyLoud = !Avx.TestC(compares, ones); //compares have at least one 0

					//Most of the audio will be above "silent", so checking for all silence is a bit of a waste
					//var allSilent = Sse41.TestC(compares, zeros); //compares are all 1

					if (allLoud)
					{
						if (numConsecutive != 0)
						{
							CheckAndAddSilence();

							numConsecutive = 0;
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

					for (int j = 0; j < VECTOR_COUNT; j++)
					{
						bool Silence = compares.GetElement(j) == -1;
						if (!Silence)
						{
							if (numConsecutive != 0)
							{
								CheckAndAddSilence();

								numConsecutive = 0;
							}
						}
						else if (numConsecutive == 0)
						{
							lastStart = currentSample + j;
							numConsecutive++;
						}
						else
						{
							numConsecutive++;
						}
					}
				}

				waveFrame.Dispose();
			}

			CheckAndAddSilence();
		}
		public override bool FilterFrame(uint chunkIndex, uint frameIndex, Span<byte> aacSample)
		{
			waveFrameQueue.Add(decoder.DecodeRaw(aacSample));
			return true;
		}

		public override void Close()
		{
			waveFrameQueue.CompleteAdding();
			encoderLoopTask.Wait();
		}

		protected override void Dispose(bool disposing)
		{
			if (!_disposed)
			{
				base.Dispose(disposing);

				if (disposing)
				{
					decoder?.Dispose();
					waveFrameQueue?.Dispose();
					encoderLoopTask?.Dispose();
				}
			}
		}
	}
}
