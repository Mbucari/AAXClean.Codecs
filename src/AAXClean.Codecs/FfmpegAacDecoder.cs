using AAXClean.Codecs.FrameFilters.Audio;
using AAXClean.Codecs.Interop;
using AAXClean.FrameFilters;
using Mpeg4Lib.Boxes;
using System;
using System.Diagnostics;

namespace AAXClean.Codecs;

internal unsafe sealed class FfmpegAacDecoder : IDisposable
{
	internal const string libname = "aaxcleannative";
	public WaveFormat WaveFormat { get; }

	private readonly NativeDecode AudioDecoder;

	private int NumberOfSamplesSkipped = 0;
	private int MaxSamplesToSkip { get; }
	private static TimeSpan MaxTimeToSkip { get; } = TimeSpan.FromSeconds(1);

	public FfmpegAacDecoder(AudioSampleEntry audioSampleEntry, WaveFormatEncoding waveFormatEncoding)
	{
		if (audioSampleEntry.Esds is EsdsBox esds)
		{
			var asc = esds.ES_Descriptor.DecoderConfig.AudioSpecificConfig;
			MaxSamplesToSkip = GetMaxNumberOfSamplesToSkip(esds);
			WaveFormat = new WaveFormat((SampleRate)asc.SamplingFrequency, waveFormatEncoding, asc.ChannelConfiguration == 2);
			AudioDecoder = new NativeAacDecode(esds, WaveFormat);
		}
		else if (audioSampleEntry.Dec3 is Dec3Box dec3)
		{
			WaveFormat = new WaveFormat((SampleRate)dec3.SampleRate, waveFormatEncoding, stereo: true);
			AudioDecoder = new NativeEc3Decode(dec3, WaveFormat);
		}
		else if (audioSampleEntry.Dac4 is Dac4Box dac4)
		{
			WaveFormat = new WaveFormat((SampleRate?)dac4.SampleRate ?? SampleRate.Hz_44100, waveFormatEncoding, stereo: true);
			AudioDecoder = new NativeAc4Decode(dac4, WaveFormat);
		}
		else
			throw new Exception($"AudioSampleEntry does not contain {nameof(EsdsBox)} or {nameof(Dec3Box)}");
	}

	public FfmpegAacDecoder(AudioSampleEntry audioSampleEntry, WaveFormatEncoding waveFormatEncoding, SampleRate sampleRate, bool stereo)
	{
		WaveFormat = new WaveFormat(sampleRate, waveFormatEncoding, stereo);
		if (audioSampleEntry.Esds is EsdsBox esds)
		{
			MaxSamplesToSkip = GetMaxNumberOfSamplesToSkip(esds);
			AudioDecoder = new NativeAacDecode(esds, WaveFormat);
		}
		else if (audioSampleEntry.Dec3 is Dec3Box dec3)
			AudioDecoder = new NativeEc3Decode(dec3, WaveFormat);
		else if (audioSampleEntry.Dac4 is Dac4Box dac4)
			AudioDecoder = new NativeAc4Decode(dac4, WaveFormat);
		else
			throw new Exception($"AudioSampleEntry does not contain {nameof(EsdsBox)} or {nameof(Dec3Box)}");

		Console.WriteLine("Opened Decoder");
	}

	private static int GetMaxNumberOfSamplesToSkip(EsdsBox esds)
		=> esds.ES_Descriptor.DecoderConfig.AudioSpecificConfig.AudioObjectType == 42
		? (int)(esds.ES_Descriptor.DecoderConfig.AudioSpecificConfig.SamplingFrequency * MaxTimeToSkip.TotalSeconds)
		: 0;

	public WaveEntry DecodeWave(FrameEntry input)
	{
		if (!SendSamples(input.FrameData))
		{
			if (NumberOfSamplesSkipped + (int)input.SamplesInFrame < MaxSamplesToSkip)
			{
				NumberOfSamplesSkipped += (int)input.SamplesInFrame;
			}
			else
				throw new Exception($"Error decoding AAC frame even after skipping {NumberOfSamplesSkipped} samples");

			//Failed to decode the frame. May need to skip to seed the decoder
			//for some number of frames before trying to receive decoded data.
			return new WaveEntry
			{
				Chunk = input.Chunk,
				SamplesInFrame = 0,
				FrameData = Memory<byte>.Empty,
			};
		}

		int requiredSamples = GetMaxAvailableDecodeSize();
		if (requiredSamples == 0)
		{
			return new WaveEntry
			{
				Chunk = input.Chunk,
				SamplesInFrame = 0,
				FrameData = Memory<byte>.Empty,
			};
		}

		Memory<byte> decoded = new byte[requiredSamples * WaveFormat.BlockAlign];

		if (WaveFormat.Encoding is NAudio.Wave.WaveFormatEncoding.Dts && WaveFormat.Channels == 2)
		{
			int receivedSamples;
			fixed (byte* decodeBuff = decoded.Span)
			{
				receivedSamples = AudioDecoder.ReceiveDecodedFrame(decodeBuff, decodeBuff + decoded.Length / 2, requiredSamples);
			}

			return new WaveEntry
			{
				Chunk = input.Chunk,
				SamplesInFrame = (uint)receivedSamples,
				FrameData = decoded.Slice(0, receivedSamples * WaveFormat.BlockAlign / 2),
				FrameData2 = decoded.Slice(requiredSamples * WaveFormat.BlockAlign / 2, receivedSamples * WaveFormat.BlockAlign / 2),
			};
		}
		else
		{
			int receivedSamples;
			fixed (byte* decodeBuff = decoded.Span)
			{
				receivedSamples = AudioDecoder.ReceiveDecodedFrame(decodeBuff, null, requiredSamples);
			}

			return new WaveEntry
			{
				Chunk = input.Chunk,
				SamplesInFrame = (uint)receivedSamples,
				FrameData = decoded.Slice(0, receivedSamples * WaveFormat.BlockAlign)
			};
		}
	}

	public WaveEntry DecodeFlush()
	{
		int requiredSamples = GetMaxAvailableDecodeSize();

		Memory<byte> decoded = new byte[requiredSamples * WaveFormat.BlockAlign];

		if (WaveFormat.Encoding is NAudio.Wave.WaveFormatEncoding.Dts && WaveFormat.Channels == 2)
		{
			int receivedSamples;
			fixed (byte* decodeBuff = decoded.Span)
			{
				receivedSamples = AudioDecoder.DecodeFlush(decodeBuff, decodeBuff + decoded.Length / 2, requiredSamples);
			}

			return new WaveEntry
			{
				SamplesInFrame = (uint)receivedSamples,
				FrameData = decoded.Slice(0, receivedSamples * WaveFormat.BlockAlign / 2),
				FrameData2 = decoded.Slice(requiredSamples * WaveFormat.BlockAlign / 2, receivedSamples * WaveFormat.BlockAlign / 2),
			};
		}
		else
		{
			int receivedSamples;
			fixed (byte* decodeBuff = decoded.Span)
			{
				receivedSamples = AudioDecoder.DecodeFlush(decodeBuff, null, requiredSamples);
			}

			return new WaveEntry
			{
				SamplesInFrame = (uint)receivedSamples,
				FrameData = decoded.Slice(0, receivedSamples * WaveFormat.BlockAlign),
			};
		}
	}

	private bool SendSamples(ReadOnlyMemory<byte> frameData)
	{
		int ret;

		fixed (byte* inBuff = frameData.Span)
		{
			ret = AudioDecoder.DecodeFrame(inBuff, frameData.Length);
		}

		if (ret >= 0)
		{
			return true;
		}
		else if (ret == -1313558101)
		{
			return false;
		}
		
		throw new Exception($"Error decoding AAC frame. Code {NativeDecode.GetFFmpegErrorString(ret)}");
	}

	private int GetMaxAvailableDecodeSize() => AudioDecoder.ReceiveDecodedFrame(null, null, 0);

	public void Dispose()
	{
		AudioDecoder.Dispose();
	}
}
