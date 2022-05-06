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
		private const int BITS_PER_SAMPLE = 16;
		private readonly FfmpegAacDecoder AacDecoder;
		private readonly Action<SilenceDetectCallback> DetectionCallback;
		private readonly TimeSpan MinimumDuration;
		private readonly double SilenceThreshold;
		private readonly long MinConsecutiveSamples;
		private readonly Vector128<short> MaxAmplitudes;
		private readonly Vector128<short> MinAmplitudes;
		private readonly Vector128<short> Zeros = Vector128<short>.Zero;
		private long currentSample;
		private long lastSilenceStart;
		private long numConsecutiveSilences;
		private readonly Memory<short> buff128;
		private MemoryHandle hbuff128;
		private readonly short* pbuff128;
		public unsafe SilenceDetectFilter(double db, TimeSpan minDuration, byte[] audioSpecificConfig, ushort sampleSize, Action<SilenceDetectCallback> detectionCallback)
		{
			if (BITS_PER_SAMPLE != sampleSize)
				throw new ArgumentException($"{nameof(AacToMp3Filter)} only supports 16-bit aac streams.");

			DetectionCallback = detectionCallback;
			AacDecoder = new FfmpegAacDecoder(audioSpecificConfig);
			Silences = new List<SilenceEntry>();

			SilenceThreshold = db;
			MinimumDuration = minDuration;

			short maxAmplitude = (short)(Math.Pow(10, SilenceThreshold / 20) * short.MaxValue);
			short minAmplitude = (short)-maxAmplitude;
			MinConsecutiveSamples = (long)Math.Round(AacDecoder.SampleRate * MinimumDuration.TotalSeconds * AacDecoder.Channels);

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

			currentSample = 0;
			lastSilenceStart = 0;
			numConsecutiveSilences = 0;


			//Buffer for storing Vector128<short>
			buff128 = new short[VECTOR_COUNT];
			hbuff128 = buff128.Pin();
			pbuff128 = (short*)hbuff128.Pointer;
		}

		public override Task CompleteAsync()
		{
			base.CompleteAsync().GetAwaiter().GetResult();
			CheckAndAddSilence(lastSilenceStart, numConsecutiveSilences);
			hbuff128.Dispose();
			return Task.Delay(0);
		}

		private void CheckAndAddSilence(long lastSilenceStart, long numConsecutiveSilences)
		{
			if (numConsecutiveSilences > MinConsecutiveSamples)
			{
				TimeSpan start = TimeSpan.FromSeconds((double)lastSilenceStart / AacDecoder.Channels / AacDecoder.SampleRate);
				TimeSpan end = TimeSpan.FromSeconds((double)(lastSilenceStart + numConsecutiveSilences) / AacDecoder.Channels / AacDecoder.SampleRate);

				SilenceEntry silence = new SilenceEntry(start, end);
				Silences.Add(silence);
				if (DetectionCallback != null)
					DetectionCallback(new SilenceDetectCallback { SilenceThreshold = SilenceThreshold, MinimumDuration = MinimumDuration, Silence = silence });
			}
		}

		protected override void PerformFiltering(WaveEntry input)
		{
			short* samples = (short*)input.hFrameData.Pointer;

			for (int i = 0; i < AacDecoder.DecodeSize / sizeof(short); i += VECTOR_COUNT, currentSample += VECTOR_COUNT)
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
