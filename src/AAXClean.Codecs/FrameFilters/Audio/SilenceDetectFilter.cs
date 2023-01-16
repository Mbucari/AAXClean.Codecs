using AAXClean.FrameFilters;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;

namespace AAXClean.Codecs.FrameFilters.Audio
{
	internal unsafe class SilenceDetectFilter : FrameFinalBase<WaveEntry>
	{
		public List<SilenceEntry> Silences { get; }

		private const int VECTOR_COUNT = 8;
		private readonly double SilenceThreshold;
		private readonly TimeSpan MinimumDuration;
		private readonly int Channels;
		private readonly int SampleRate;
		private readonly Action<SilenceDetectCallback> DetectionCallback;
		private readonly long MinConsecutiveSamples;

		private readonly Vector128<short> MaxAmplitudes;
		private readonly Vector128<short> MinAmplitudes;
		private readonly Vector128<short> Zeros = Vector128<short>.Zero;

		private long currentSample = 0;
		private long lastSilenceStart = 0;
		private long numConsecutiveSilences = 0;

		private readonly Memory<short> buff128;
		private readonly MemoryHandle hbuff128;
		private readonly short* pbuff128;
		public unsafe SilenceDetectFilter(double db, TimeSpan minDuration, byte[] audioSpecificConfig, int channels, int sampleRate, Action<SilenceDetectCallback> detectionCallback)
		{
			SilenceThreshold = db;
			MinimumDuration = minDuration;
			Channels = channels;
			SampleRate = sampleRate;
			DetectionCallback = detectionCallback;
			Silences = new List<SilenceEntry>();
			MinConsecutiveSamples = (long)Math.Round(SampleRate * MinimumDuration.TotalSeconds * Channels);

			short maxAmplitude = (short)(Math.Pow(10, SilenceThreshold / 20) * short.MaxValue);
			short minAmplitude = (short)-maxAmplitude;

			//Initialize vectors for comparisons
			short[] sbytes = new short[VECTOR_COUNT];

			for (int i = 0; i < sbytes.Length; i++)
				sbytes[i] = maxAmplitude;

			fixed (short* s = sbytes)
			{
				MaxAmplitudes = Sse2.LoadVector128(s);
			}

			for (int i = 0; i < sbytes.Length; i++)
				sbytes[i] = minAmplitude;

			fixed (short* s = sbytes)
			{
				MinAmplitudes = Sse2.LoadVector128(s);
			}

			//Buffer for storing Vector128<short>
			buff128 = new short[VECTOR_COUNT];
			hbuff128 = buff128.Pin();
			pbuff128 = (short*)hbuff128.Pointer;
		}

		private void CheckAndAddSilence(long lastSilenceStart, long numConsecutiveSilences)
		{
			if (numConsecutiveSilences > MinConsecutiveSamples)
			{
				TimeSpan start = TimeSpan.FromSeconds((double)lastSilenceStart / Channels / SampleRate);
				TimeSpan end = TimeSpan.FromSeconds((double)(lastSilenceStart + numConsecutiveSilences) / Channels / SampleRate);

				SilenceEntry silence = new(start, end);
				Silences.Add(silence);
				DetectionCallback?.Invoke(new SilenceDetectCallback { SilenceThreshold = SilenceThreshold, MinimumDuration = MinimumDuration, Silence = silence });
			}
		}

		protected override void Flush()
		{
			CheckAndAddSilence(lastSilenceStart, numConsecutiveSilences);
			hbuff128.Dispose();
		}

		protected override void PerformFiltering(WaveEntry input)
		{
			short* samples = (short*)input.hFrameData.Pointer;

			for (int i = 0; i < input.SamplesInFrame * Channels; i += VECTOR_COUNT, currentSample += VECTOR_COUNT)
			{
				//2x compares and an AND is ~3% faster than Abs and 1x compare
				//And for whatever reason, equivalent method with Avx 256-bit vectors is slightly slower.
				Vector128<short> samps = Sse2.LoadVector128(samples + i);
				Vector128<short> comparesLess = Sse2.CompareLessThan(samps, MaxAmplitudes);
				Vector128<short> comparesGreater = Sse2.CompareGreaterThan(samps, MinAmplitudes);
				Vector128<short> compares = Sse2.And(comparesLess, comparesGreater);

				//loud = 0
				//silent = -1
				bool allLoud = Sse41.TestC(Zeros, compares); //compares are all 0

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

				Sse2.Store(pbuff128, compares);
				Span<short> span = buff128.Span;

				for (int j = 0; j < VECTOR_COUNT; j++)
				{
					bool Silence = span[j] == -1;
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

			input.hFrameData.Dispose();
		}
	}
}
