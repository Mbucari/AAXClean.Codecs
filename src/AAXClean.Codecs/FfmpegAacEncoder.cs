using AAXClean.Codecs.FrameFilters.Audio;
using AAXClean.Codecs.Interop;
using AAXClean.FrameFilters;
using System;
using System.Collections.Generic;

namespace AAXClean.Codecs;

internal unsafe sealed class FfmpegAacEncoder : IDisposable
{
	internal const string libname = FfmpegAacDecoder.libname;
	public WaveFormat WaveFormat { get; }
	private readonly NativeAacEncode AacEncoder;
	private const int AAC_SAMPLES_PER_FRAME = 1024;
	public byte[] GetAudioSpecificConfig() => AacEncoder.GetAudioSpecificConfig();

	public FfmpegAacEncoder(WaveFormat inputWaveFormat, long? bitRate, double? quality)
	{
		if (inputWaveFormat.Channels > 2)
			throw new ArgumentException("AAC encoder only supports mono or stereo wave formats.", nameof(inputWaveFormat));
		if (inputWaveFormat.Encoding != NAudio.Wave.WaveFormatEncoding.Pcm)
			throw new ArgumentException("AAC encoder only supports PCM wave formats.", nameof(inputWaveFormat));

		WaveFormat = inputWaveFormat;
		AacEncoder = new NativeAacEncode(WaveFormat, bitRate ?? 0, quality ?? 0);
	}

	public IEnumerable<FrameEntry> EncodeWave(WaveEntry input)
	{
		int startIndex = 0;
		var frameSize = (int)input.SamplesInFrame;

		//It's possible that a frame may be larger than AAC_SAMPLES_PER_FRAME
		//Send a maximum of AAC_SAMPLES_PER_FRAME at a time to the encoder.
		while (frameSize > 0)
		{
			int toSend = Math.Min(frameSize, AAC_SAMPLES_PER_FRAME);

			int samplesNeeded = SendSamples(input.FrameData.Slice(startIndex, toSend).Span, toSend);
			startIndex += toSend;
			frameSize -= toSend;

			if (samplesNeeded == 0)
			{
				int encodedSize;
				while ((encodedSize = GetAvailableFrameSize()) > 0)
				{
					Memory<byte> encAud = GetEncodedFrame(encodedSize);
					yield return new FrameEntry
					{
						Chunk = input.Chunk,
						SamplesInFrame = AAC_SAMPLES_PER_FRAME,
						FrameData = encAud
					};
				}
				if (encodedSize < 0)
					throw new Exception("Failed to retrieve encoded samples.");
			}
		}
	}

	public IEnumerable<FrameEntry> EncodeFlush()
	{
		int ret = AacEncoder.EncodeFlush();

		if (ret < 0)
			throw new Exception($"Error flushing AAC encoder.");

		do
		{
			int encodedSize = GetAvailableFrameSize();

			if (encodedSize < 0)
				throw new Exception("Failed to retrieve encoded samples.");
			else if (encodedSize == 0) yield break;

			Memory<byte> encAud = GetEncodedFrame(encodedSize);
			yield return new FrameEntry
			{
				SamplesInFrame = AAC_SAMPLES_PER_FRAME,
				FrameData = encAud
			};
		} while (true);
	}

	private int SendSamples(Span<byte> frameData, int numSamples)
	{
		int ret;
		fixed (byte* buffer1 = frameData)
		{
			ret = AacEncoder.EncodeFrame(buffer1, null, numSamples);
		}

		if (ret < 0)
			throw new Exception("Failed to encode samples.");

		return ret;
	}

	private int SendSamplesPlanarStereo(Span<byte> frameData1, Span<byte> frameData2, int numSamples)
	{
		int ret;
		fixed (byte* buffer1 = frameData1)
		{
			fixed (byte* buffer2 = frameData2)
			{
				ret = AacEncoder.EncodeFrame(buffer1, buffer2, numSamples);
			}
		}

		if (ret < 0)
			throw new Exception("Failed to encode samples.");

		return ret;
	}

	private int GetAvailableFrameSize() => AacEncoder.ReceiveEncodedFrame(null, 0);

	private Memory<byte> GetEncodedFrame(int encodedSize)
	{
		Memory<byte> encAud = new byte[encodedSize];
		fixed (byte* pEncAud = encAud.Span)
		{
			encodedSize = AacEncoder.ReceiveEncodedFrame(pEncAud, encodedSize);
		}
		if (encodedSize != 0)
			throw new Exception("Failed to retrieve encoded samples.");
		return encAud;
	}

	public void Dispose()
	{
		AacEncoder.Dispose();
	}
}
