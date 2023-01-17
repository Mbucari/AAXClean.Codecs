using AAXClean.Codecs.FrameFilters.Audio;
using AAXClean.FrameFilters;
using System;
using System.Runtime.InteropServices;

namespace AAXClean.Codecs
{
	internal unsafe sealed class FfmpegAacEncoder : IDisposable
	{
		internal const string libname = FfmpegAacDecoder.libname;
		public WaveFormat WaveFormat { get; }
		private readonly NativeAacEncode aacEncoder;
		private readonly Memory<byte> buffer;
		private const int AAC_SAMPLES_PER_FRAME = 1024;

		public FfmpegAacEncoder(WaveFormat inputWaveFormat, long bitRate, double quality)
		{
			WaveFormat = inputWaveFormat;
			buffer = new byte[AAC_SAMPLES_PER_FRAME * WaveFormat.BlockAlign];
			aacEncoder = NativeAacEncode.Open(WaveFormat, bitRate, quality);
		}

		public FrameEntry EncodeWave(WaveEntry input)
		{
			int bytesEncoded;
			fixed (byte* inData = input.FrameData.Span)
			{
				bytesEncoded = aacEncoder.EncodeFrame(inData, (int)input.SamplesInFrame);
			}

			if (bytesEncoded < 0)
			{
				throw new Exception("Failed to encode samples.");
			}

			Memory<byte> encAud = new byte[bytesEncoded];
			if (bytesEncoded > 0)
			{

				fixed (byte* pEncAud = encAud.Span)
				{
					bytesEncoded = aacEncoder.GetEncodedFrame(pEncAud, bytesEncoded);
				}
			}
			if (bytesEncoded < 0)
			{
				throw new Exception("Failed to retrieve encoded samples.");
			}

			return new FrameEntry
			{
				Chunk = input.Chunk,
				FrameIndex = input.FrameIndex,
				SamplesInFrame = AAC_SAMPLES_PER_FRAME,
				FrameData = encAud
			};
		}

		public FrameEntry EncodeFlush()
		{
			Memory<byte> decoded = new byte[4096];

			int bytesEncoded = 0;
			fixed (byte* pEncAud = decoded.Span)
			{
				bytesEncoded = aacEncoder.EncodeFlush(pEncAud, decoded.Length);
			}

			if (bytesEncoded < 0)
			{
				throw new Exception($"Error flusing AAC encoder.");
			}

			return new FrameEntry
			{
				SamplesInFrame = AAC_SAMPLES_PER_FRAME,
				FrameData = decoded.Slice(0, bytesEncoded)
			};
		}

		private bool disposed = false;
		public void Dispose()
		{
			if (!disposed)
			{
				aacEncoder?.Close();
				disposed = true;
			}
			GC.SuppressFinalize(this);
		}

		private class NativeAacEncode
		{
			//Factor for converting quality to global_quality
			private const int FF_QP2LAMBDA = 118;
			private readonly EncoderHandle Handle;
			private NativeAacEncode(EncoderHandle handle) => Handle = handle;

			[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
			private static extern EncoderHandle aacEncoder_Open(AacEncoderOptions* options);

			[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
			private static extern int aacEncoder_EncodeFrame(EncoderHandle self, byte* pWaveAudio, int nbSamples);

			[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
			private static extern int aacEncoder_GetEncodedFrame(EncoderHandle self, byte* pEncodedAudio, int size);

			[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
			private static extern int aacEncoder_EncodeFlush(EncoderHandle self, byte* pEncodedAudio, int numOutSamples);

			public static NativeAacEncode Open(WaveFormat waveFormat, long bitRate, double quality)
			{
				AacEncoderOptions options = new()
				{
					bit_rate = bitRate,
					global_quality = (int)(FF_QP2LAMBDA * quality),
					sample_rate = waveFormat.SampleRate,
					channels = waveFormat.Channels,
					sample_fmt = (int)waveFormat.Encoding
				};
				var handle = aacEncoder_Open(&options);

				long err = (long)handle.DangerousGetHandle();

				if (err < 0)
				{
					throw new Exception($"Error opening AAC Decoder. Code {err}");
				}
				return new NativeAacEncode(handle);
			}

			public void Close() => Handle.Close();
			public int EncodeFrame(byte* pWaveAudio, int nbSamples)
				=> aacEncoder_EncodeFrame(Handle, pWaveAudio, nbSamples);
			public int GetEncodedFrame(byte* pEncodedAudio, int size)
				=> aacEncoder_GetEncodedFrame(Handle, pEncodedAudio, size);
			public int EncodeFlush(byte* pDecodedAudio, int numOutSamples)
				=> aacEncoder_EncodeFlush(Handle, pDecodedAudio, numOutSamples);

			private class EncoderHandle : SafeHandle
			{
				[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
				private static extern int aacEncoder_Close(IntPtr self);
				private EncoderHandle() : base(IntPtr.Zero, true) { }
				public override bool IsInvalid => IsClosed || handle == IntPtr.Zero;
				protected override bool ReleaseHandle()
				{
					aacEncoder_Close(handle);
					return true;
				}
			}

			[StructLayout(LayoutKind.Sequential)]
			private struct AacEncoderOptions
			{
				public long bit_rate;
				public int global_quality;
				public int sample_rate;
				public int channels;
				public int sample_fmt;
			}
		}
	}
}
