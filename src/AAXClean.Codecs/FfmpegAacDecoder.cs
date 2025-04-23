using AAXClean.Codecs.FrameFilters.Audio;
using AAXClean.Codecs.Interop;
using AAXClean.FrameFilters;
using Mpeg4Lib.Boxes;
using System;
using System.Diagnostics;

namespace AAXClean.Codecs;

internal unsafe sealed class FfmpegAacDecoder : IDisposable
{
	internal const string libname = "ffmpegaac";
	public WaveFormat WaveFormat { get; }

	private readonly NativeDecode AudioDecoder;

	public FfmpegAacDecoder(AudioSampleEntry audioSampleEntry, WaveFormatEncoding waveFormatEncoding)
	{
		if (audioSampleEntry.Esds is EsdsBox esds)
		{
			var asc = esds.ES_Descriptor.DecoderConfig.AudioSpecificConfig;
			WaveFormat = new WaveFormat((SampleRate)asc.SamplingFrequency, waveFormatEncoding, asc.ChannelConfiguration == 2);
			AudioDecoder = new NativeAacDecode(esds, WaveFormat);
		}
		else if (audioSampleEntry.Dec3 is Dec3Box dec3)
		{
			WaveFormat = new WaveFormat((SampleRate)dec3.SampleRate, waveFormatEncoding, stereo: true);
			AudioDecoder = new NativeEc3Decode(dec3, WaveFormat);
		}
		else
			throw new Exception($"AudioSampleEntry does not contain {nameof(EsdsBox)} or {nameof(Dec3Box)}");
	}

	public FfmpegAacDecoder(AudioSampleEntry audioSampleEntry, WaveFormatEncoding waveFormatEncoding, SampleRate sampleRate, bool stereo)
	{
		WaveFormat = new WaveFormat(sampleRate, waveFormatEncoding, stereo);
		if (audioSampleEntry.Esds is EsdsBox esds)
			AudioDecoder = new NativeAacDecode(esds, WaveFormat);
		else if (audioSampleEntry.Dec3 is Dec3Box dec3)
			AudioDecoder = new NativeEc3Decode(dec3, WaveFormat);
		else
			throw new Exception($"AudioSampleEntry does not contain {nameof(EsdsBox)} or {nameof(Dec3Box)}");
	}

	public WaveEntry DecodeWave(FrameEntry input)
	{
		SendSamples(input.FrameData, input.SamplesInFrame);

		int requiredSamples = GetMaxAvailableDecodeSize();

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

			Debug.Assert(receivedSamples <= requiredSamples);

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

			Debug.Assert(receivedSamples <= requiredSamples);

			return new WaveEntry
			{
				SamplesInFrame = (uint)receivedSamples,
				FrameData = decoded.Slice(0, receivedSamples * WaveFormat.BlockAlign),
			};
		}
	}

	private void SendSamples(ReadOnlyMemory<byte> frameData, uint numSamples)
	{
		int ret;

		fixed (byte* inBuff = frameData.Span)
		{
			ret = AudioDecoder.DecodeFrame(inBuff, frameData.Length, numSamples);
		}

		if (ret < 0)
			throw new Exception($"Error decoding AAC frame. Code {ret:X}");
	}

	private int GetMaxAvailableDecodeSize() => AudioDecoder.ReceiveDecodedFrame(null, null, 0);

	public void Dispose()
	{
		AudioDecoder.Dispose();
	}
}
