using AAXClean.FrameFilters;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;

namespace AAXClean.Codecs.FrameFilters.Audio
{
	internal unsafe class SilenceDetectFilter : FrameFinalBase<WaveEntry>
	{
		public List<SilenceEntry> Silences { get; }
		protected override int InputBufferSize => 500;

		private const int VECTOR_COUNT = 16;
		private readonly double SilenceThreshold;
		private readonly TimeSpan MinimumDuration;
		private readonly WaveFormat WaveFormat;
		private readonly Action<SilenceDetectCallback> DetectionCallback;
		private readonly long MinConsecutiveSamples;

		private readonly Vector256<short> MaxAmplitudes;
		private readonly Vector256<short> AllBitsSet = Vector256<short>.AllBitsSet;

		private long currentSample = 0;
		private long lastSilenceStart = 0;
		private long numConsecutiveSilences = 0;

		private readonly Memory<short> buff256;
		private readonly MemoryHandle hbuff256;
		private readonly short* pbuff256;
		public unsafe SilenceDetectFilter(double db, TimeSpan minDuration, WaveFormat waveFormat, Action<SilenceDetectCallback> detectionCallback)
		{
			SilenceThreshold = db;
			MinimumDuration = minDuration;
			WaveFormat = waveFormat;
			DetectionCallback = detectionCallback;
			Silences = new List<SilenceEntry>();
			MinConsecutiveSamples = (long)Math.Round(waveFormat.SampleRate * MinimumDuration.TotalSeconds * waveFormat.Channels);

			short maxAmplitude = (short)Math.Round(Math.Pow(10, SilenceThreshold / 20) * short.MaxValue);

			//Initialize vectors for comparisons
			fixed (short* s = Enumerable.Repeat(maxAmplitude, VECTOR_COUNT).ToArray())
			{
				MaxAmplitudes = Avx.LoadVector256(s);
			}

			//Buffer for storing Vector256<short>
			buff256 = new short[VECTOR_COUNT];
			hbuff256 = buff256.Pin();
			pbuff256 = (short*)hbuff256.Pointer;
		}

		private void CheckAndAddSilence(long lastSilenceStart, long numConsecutiveSilences)
		{
			if (numConsecutiveSilences > MinConsecutiveSamples)
			{
				TimeSpan start = TimeSpan.FromSeconds((double)lastSilenceStart / WaveFormat.Channels / WaveFormat.SampleRate);
				TimeSpan end = TimeSpan.FromSeconds((double)(lastSilenceStart + numConsecutiveSilences) / WaveFormat.Channels / WaveFormat.SampleRate);

				SilenceEntry silence = new(start, end);
				Silences.Add(silence);
				DetectionCallback?.Invoke(new SilenceDetectCallback { SilenceThreshold = SilenceThreshold, MinimumDuration = MinimumDuration, Silence = silence });
			}
		}

		protected override Task FlushAsync()
		{
			CheckAndAddSilence(lastSilenceStart, numConsecutiveSilences);
			return Task.CompletedTask;
		}

		protected override Task PerformFilteringAsync(WaveEntry input)
		{
			short* samples = (short*)input.hFrameData.Pointer;

			for (int i = 0; i < input.SamplesInFrame * WaveFormat.Channels; i += VECTOR_COUNT, currentSample += VECTOR_COUNT)
			{
				var sampleVector = Avx.LoadVector256(samples + i);
				var absVal = Avx2.Abs(sampleVector).AsInt16();
				var greaterThan = Avx2.CompareGreaterThan(MaxAmplitudes, absVal);

				var allSilent = Avx.TestC(greaterThan, AllBitsSet);

				if (allSilent)
				{
					if (numConsecutiveSilences == 0)
					{
						lastSilenceStart = currentSample;
					}

					numConsecutiveSilences += VECTOR_COUNT;
					continue;
				}

				bool allLoud = Avx.TestZ(greaterThan, AllBitsSet);

				if (allLoud)
				{
					if (numConsecutiveSilences != 0)
					{
						CheckAndAddSilence(lastSilenceStart, numConsecutiveSilences);

						numConsecutiveSilences = 0;
					}
					continue;
				}

				Avx.Store(pbuff256, greaterThan);
				Span<short> span = buff256.Span;

				for (int j = 0; j < VECTOR_COUNT; j++)
				{
					if (span[j] == -1)
					{
						//Sample is silent
						if (numConsecutiveSilences == 0)
						{
							lastSilenceStart = currentSample + j;
						}

						numConsecutiveSilences++;
					}
					else if (numConsecutiveSilences != 0)
					{
						CheckAndAddSilence(lastSilenceStart, numConsecutiveSilences);

						numConsecutiveSilences = 0;
					}
				}
			}

			input.Dispose();

			return Task.CompletedTask;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && !Disposed)
				hbuff256.Dispose();
			base.Dispose(disposing);
		}
	}
}
