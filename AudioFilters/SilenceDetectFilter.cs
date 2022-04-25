using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;

namespace AAXClean.AudioFilters
{
	internal unsafe class SilenceDetectFilter : AudioFilter
	{
		private class WaveFrame
        {
			public uint FrameIndex { get; }
			public short[] Samples { get; }

			public WaveFrame(uint frameIndex, short[] frame)
            {
				FrameIndex = frameIndex;
				Samples = frame;
			}
		}
		public List<SilenceEntry> Silences { get; }

		private const int BITS_PER_SAMPLE = 16;
		private readonly AacDecoder decoder;
		private readonly BlockingCollection<WaveFrame> waveFrameQueue;
		private readonly Task encoderLoopTask;

		private readonly Vector128<short> maxAmplitudes;
		private readonly Vector128<short> minAmplitudes;
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


			short[] sbytes = new short[8];
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

			waveFrameQueue = new BlockingCollection<WaveFrame>(200);
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

			while (waveFrameQueue.TryTake(out WaveFrame waveFrame, -1))
			{
				for (int i = 0; i < waveFrame.Samples.Length; i += 8, currentSample += 8)
				{
					fixed (short* samples = waveFrame.Samples)
					{
						var samps = Sse2.LoadVector128(samples + i);
						var comparesLess = Sse2.CompareLessThan(samps, maxAmplitudes);
						var comparesGreater = Sse2.CompareGreaterThan(samps, minAmplitudes);
						var compares = Sse2.And(comparesLess, comparesGreater);

						for (int j = 0; j < 8; j++, currentSample++)
                        {
							bool Silence = compares.GetElement(j) == -1;
							if (!Silence)
							{
								CheckAndAddSilence();

								numConsecutive = 0;
							}
							else if (numConsecutive == 0)
							{
								lastStart = currentSample;
								numConsecutive++;
							}
							else
							{
								numConsecutive++;
							}
						}
					}
				}
			}

			CheckAndAddSilence();			
		}
		public override bool FilterFrame(uint chunkIndex, uint frameIndex, Span<byte> aacSample)
		{
			var waveFrame = decoder.DecodeShort(aacSample);
			waveFrameQueue.Add(new WaveFrame(frameIndex, waveFrame.ToArray()));
			return true;
		}

		public override void Close()
		{
			waveFrameQueue.CompleteAdding();
			encoderLoopTask.Wait();
		}

		protected override void Dispose(bool disposing)
		{
			decoder?.Dispose();
			base.Dispose(disposing);
		}
	}
}
