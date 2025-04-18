using AAXClean.Codecs.FrameFilters.Audio;
using AAXClean.FrameFilters;
using Mpeg4Lib.Descriptors;
using System;
using System.Runtime.InteropServices;

namespace AAXClean.Codecs
{
	internal unsafe sealed class FfmpegAacDecoder : IDisposable
	{
		internal const string libname = "ffmpegaac";
		public WaveFormat WaveFormat { get; }

		private readonly NativeAacDecode aacDecoder;
		private readonly IASC Asc;

		public FfmpegAacDecoder(byte[] asc, WaveFormatEncoding waveFormatEncoding)
			: this(asc)
		{
			WaveFormat = new WaveFormat((SampleRate)Asc.SamplingFrequency, waveFormatEncoding, Asc.ChannelConfiguration == 2);
			aacDecoder = NativeAacDecode.Open(asc, WaveFormat);
		}

		public FfmpegAacDecoder(byte[] asc, WaveFormatEncoding waveFormatEncoding, SampleRate sampleRate, bool stereo)
			: this(asc)
		{
			WaveFormat = new WaveFormat(sampleRate, waveFormatEncoding, stereo);
			aacDecoder = NativeAacDecode.Open(asc, WaveFormat);
		}

		private FfmpegAacDecoder(byte[] asc)
		{
			Asc = AudioSpecificConfig.Parse(asc);
		}

		public WaveEntry DecodeWave(FrameEntry input)
		{
			SendSamples(input.FrameData, input.SamplesInFrame);

			int requiredSamples = GetMaxAvaliableDecodeSize();

			Memory<byte> decoded = new byte[requiredSamples * WaveFormat.BlockAlign];

			if (WaveFormat.Encoding is NAudio.Wave.WaveFormatEncoding.Dts && WaveFormat.Channels == 2)
			{
				int receivedSamples;
				fixed (byte* decodeBuff = decoded.Span)
				{
					receivedSamples = aacDecoder.ReceiveDecodedFrame(decodeBuff, decodeBuff + decoded.Length / 2, requiredSamples);
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
					receivedSamples = aacDecoder.ReceiveDecodedFrame(decodeBuff, null, requiredSamples);
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
			int requiredSamples = GetMaxAvaliableDecodeSize();

			Memory<byte> decoded = new byte[requiredSamples * WaveFormat.BlockAlign];

			if (WaveFormat.Encoding is NAudio.Wave.WaveFormatEncoding.Dts && WaveFormat.Channels == 2)
			{
				int receivedSamples;
				fixed (byte* decodeBuff = decoded.Span)
				{
					receivedSamples = aacDecoder.DecodeFlush(decodeBuff, decodeBuff + decoded.Length / 2, requiredSamples);
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
					receivedSamples = aacDecoder.DecodeFlush(decodeBuff, null, requiredSamples);
				}

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
				ret = aacDecoder.DecodeFrame(inBuff, frameData.Length, numSamples);
			}

			if (ret < 0)
				throw new Exception($"Error decoding AAC frame. Code {ret:X}");
		}

		private int GetMaxAvaliableDecodeSize() => aacDecoder.ReceiveDecodedFrame(null, null, 0);

		public void Dispose()
		{
			aacDecoder?.Close();
		}


		private class NativeAacDecode
		{
			private readonly DecoderHandle Handle;
			private NativeAacDecode(DecoderHandle handle) => Handle = handle;

			[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
			private static extern DecoderHandle AacDecoder_Open(AacDecoderOptions* decoder_options);

			[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
			private static extern int AacDecoder_DecodeFrame(DecoderHandle self, byte* pCompressedAudio, int cbInBufferSize, uint nbSamples);

			[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
			private static extern int AacDecoder_ReceiveDecodedFrame(DecoderHandle self, byte* pDecodedAudio1, byte* pDecodedAudio2, int cbInBufferSize);

			[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
			private static extern int AacDecoder_DecodeFlush(DecoderHandle self, byte* pDecodedAudio1, byte* pDecodedAudio2, int cbInBufferSize);

			public static NativeAacDecode Open(byte[] ASC, WaveFormat waveFormat)
			{
				DecoderHandle handle;
				fixed (byte* asc = ASC)
				{
					AacDecoderOptions options = new()
					{
						asc_size = ASC.Length,
						sample_rate = waveFormat.SampleRate,
						channels = waveFormat.Channels,
						sample_fmt = (int)waveFormat.Encoding,
						ASC = asc
					};
					handle = AacDecoder_Open(&options);
				}

				long err = handle.DangerousGetHandle();

				if (err < 0)
				{
					throw new Exception($"Error opening AAC Decoder. Code {err}");
				}

				return new NativeAacDecode(handle);
			}

			public void Close() => Handle.Close();
			public int DecodeFrame(byte* pCompressedAudio, int cbInputSize, uint nbSamples)
				=> AacDecoder_DecodeFrame(Handle, pCompressedAudio, cbInputSize, nbSamples);
			public int ReceiveDecodedFrame(byte* pDecodedAudio1, byte* pDecodedAudio2, int cbInputSize)
				=> AacDecoder_ReceiveDecodedFrame(Handle, pDecodedAudio1, pDecodedAudio2, cbInputSize);
			public int DecodeFlush(byte* pDecodedAudio1, byte* pDecodedAudio2, int cbInputSize)
				=> AacDecoder_DecodeFlush(Handle, pDecodedAudio1, pDecodedAudio2, cbInputSize);

			private class DecoderHandle : SafeHandle
			{
				[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
				private static extern int AacDecoder_Close(IntPtr self);
				private DecoderHandle() : base(IntPtr.Zero, true) { }
				public override bool IsInvalid => IsClosed || handle == IntPtr.Zero;
				protected override bool ReleaseHandle()
				{
					AacDecoder_Close(handle);
					return true;
				}
			}

			[StructLayout(LayoutKind.Sequential)]
			private struct AacDecoderOptions
			{
				public int asc_size;
				public int sample_rate;
				public int channels;
				public int sample_fmt;
				public byte* ASC;
			}
		}
	}
}
