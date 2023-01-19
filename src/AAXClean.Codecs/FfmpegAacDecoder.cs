using AAXClean.Codecs.FrameFilters.Audio;
using AAXClean.FrameFilters;
using NAudio.Codecs;
using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

namespace AAXClean.Codecs
{
	internal unsafe sealed class FfmpegAacDecoder : IDisposable
	{
		internal const string libname = "ffmpegaac";
		public WaveFormat WaveFormat { get; }

		private int lastFrameNumSamples;
		private readonly NativeAacDecode aacDecoder;
		private readonly int inputSampleRate;

		private static readonly int[] asc_samplerates = { 96000, 88200, 64000, 48000, 44100, 32000, 24000, 22050, 16000, 12000, 11025, 8000, 7350 };

		public FfmpegAacDecoder(byte[] asc, WaveFormatEncoding waveFormatEncoding)
		{
			inputSampleRate = asc_samplerates[(asc[0] & 7) << 1 | asc[1] >> 7];
			var inputChannels = (asc[1] >> 3) & 7;

			WaveFormat = new WaveFormat((SampleRate)inputSampleRate, waveFormatEncoding, inputChannels == 2);
			aacDecoder = NativeAacDecode.Open(asc, WaveFormat);
		}

		public FfmpegAacDecoder(byte[] asc, WaveFormatEncoding waveFormatEncoding, SampleRate sampleRate, bool stereo)
		{
			inputSampleRate = asc_samplerates[(asc[0] & 7) << 1 | asc[1] >> 7];

			WaveFormat = new WaveFormat(sampleRate, waveFormatEncoding, stereo);
			aacDecoder = NativeAacDecode.Open(asc, WaveFormat);
		}

		public WaveEntry DecodeWave(FrameEntry input)
		{
			SendSamples(input.FrameData, (int)input.SamplesInFrame);

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
					FrameIndex = input.FrameIndex
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
					FrameData = decoded.Slice(0, receivedSamples * WaveFormat.BlockAlign),
					FrameIndex = input.FrameIndex
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

		private void SendSamples(Memory<byte> frameData, int numSamples)
		{
			int ret;

			fixed (byte* inBuff = frameData.Span)
			{
				ret = aacDecoder.DecodeFrame(inBuff, frameData.Length);
			}

			if (ret < 0)
				throw new Exception($"Error decoding AAC frame. Code {ret:X}");
		}

		private int GetMaxAvaliableDecodeSize() => aacDecoder.ReceiveDecodedFrame(null, null, 0);


		private bool disposed = false;
		public void Dispose()
		{
			if (!disposed)
			{
				aacDecoder?.Close();
				disposed = true;
			}
			GC.SuppressFinalize(this);
		}

		private class NativeAacDecode
		{
			private readonly DecoderHandle Handle;
			private NativeAacDecode(DecoderHandle handle) => Handle = handle;

			[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
			private static extern DecoderHandle aacDecoder_Open(AacDecoderOptions* decoder_options);

			[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
			private static extern int aacDecoder_DecodeFrame(DecoderHandle self, byte* pCompressedAudio, int cbInBufferSize);

			[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
			private static extern int aacDecoder_ReceiveDecodedFrame(DecoderHandle self, byte* pDecodedAudio1, byte* pDecodedAudio2, int cbInBufferSize);

			[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
			private static extern int aacDecoder_DecodeFlush(DecoderHandle self, byte* pDecodedAudio1, byte* pDecodedAudio2, int cbInBufferSize);

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
					handle = aacDecoder_Open(&options);
				}

				long err = (long)handle.DangerousGetHandle();

				if (err < 0)
				{
					throw new Exception($"Error opening AAC Decoder. Code {err}");
				}

				return new NativeAacDecode(handle);
			}

			public void Close() => Handle.Close();
			public int DecodeFrame(byte* pCompressedAudio, int cbInputSize)
				=> aacDecoder_DecodeFrame(Handle, pCompressedAudio, cbInputSize);
			public int ReceiveDecodedFrame(byte* pDecodedAudio1, byte* pDecodedAudio2, int cbInputSize)
				=> aacDecoder_ReceiveDecodedFrame(Handle, pDecodedAudio1, pDecodedAudio2, cbInputSize);
			public int DecodeFlush(byte* pDecodedAudio1, byte* pDecodedAudio2, int cbInputSize)
				=> aacDecoder_DecodeFlush(Handle, pDecodedAudio1, pDecodedAudio2, cbInputSize);

			private class DecoderHandle : SafeHandle
			{
				[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
				private static extern int aacDecoder_Close(IntPtr self);
				private DecoderHandle() : base(IntPtr.Zero, true) { }
				public override bool IsInvalid => IsClosed || handle == IntPtr.Zero;
				protected override bool ReleaseHandle()
				{
					aacDecoder_Close(handle);
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
