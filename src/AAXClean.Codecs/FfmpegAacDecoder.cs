using AAXClean.Codecs.FrameFilters.Audio;
using AAXClean.FrameFilters;
using System;
using System.Buffers;
using System.Runtime.InteropServices;

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
			lastFrameNumSamples = (int)Math.Ceiling(input.SamplesInFrame * (double)WaveFormat.SampleRate / inputSampleRate); /* add one frame of headroom to ensure that sw_convert gives us everything*/

			Memory<byte> decoded = new byte[lastFrameNumSamples * WaveFormat.BlockAlign];
			MemoryHandle handle = decoded.Pin();

			int samplesDecoded;

			fixed (byte* inBuff = input.FrameData.Span)
			{
				byte* outBuff = (byte*)handle.Pointer;

				samplesDecoded = aacDecoder.DecodeFrame(inBuff, input.FrameData.Length, outBuff, lastFrameNumSamples);
			}

			if (samplesDecoded <= 0)
			{
				throw new Exception($"Error decoding AAC frame. Code {samplesDecoded:X}");
			}

			int bytesDecoded = samplesDecoded * WaveFormat.BlockAlign;

			return new WaveEntry
			{
				Chunk = input.Chunk,
				SamplesInFrame = (uint)samplesDecoded,
				FrameData = decoded.Slice(0, bytesDecoded),
				hFrameData = handle,
				FrameIndex = input.FrameIndex
			};
		}

		public WaveEntry DecodeFlush()
		{
			Memory<byte> decoded = new byte[lastFrameNumSamples * WaveFormat.BlockAlign];
			MemoryHandle handle = decoded.Pin();

			byte* outBuff = (byte*)handle.Pointer;

			int samplesDecoded = aacDecoder.DecodeFlush(outBuff, lastFrameNumSamples);

			if (samplesDecoded < 0)
			{
				throw new Exception($"Error decoding AAC frame. Code {samplesDecoded:X}");
			}

			int size = samplesDecoded * WaveFormat.BlockAlign;

			return new WaveEntry
			{
				SamplesInFrame = (uint)samplesDecoded,
				FrameData = decoded.Slice(0, size),
				hFrameData = handle
			};
		}

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
			private static extern int aacDecoder_DecodeFrame(DecoderHandle self, byte* pCompressedAudio, int cbInBufferSize, byte* pDecodedAudio, int numOutSamples);

			[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
			private static extern int aacDecoder_DecodeFlush(DecoderHandle self, byte* pDecodedAudio, int numOutSamples);

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
			public int DecodeFrame(byte* pCompressedAudio, int cbInputSize, byte* pDecodedAudio, int numOutSamples)
				=> aacDecoder_DecodeFrame(Handle, pCompressedAudio, cbInputSize, pDecodedAudio, numOutSamples);
			public int DecodeFlush(byte* pDecodedAudio, int numOutSamples)
				=> aacDecoder_DecodeFlush(Handle, pDecodedAudio, numOutSamples);

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
