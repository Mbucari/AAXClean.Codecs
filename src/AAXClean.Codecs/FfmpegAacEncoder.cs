using AAXClean.Codecs.FrameFilters.Audio;
using AAXClean.FrameFilters;
using System;
using System.Collections.Generic;
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
		public byte[] GetAudioSpecificConfig() => aacEncoder.GetAudioSpecificConfig();

        public FfmpegAacEncoder(WaveFormat inputWaveFormat, long? bitRate, double? quality)
		{
			WaveFormat = inputWaveFormat;
			buffer = new byte[AAC_SAMPLES_PER_FRAME * WaveFormat.BlockAlign];
			aacEncoder = NativeAacEncode.Open(WaveFormat, bitRate ?? 0, quality ?? 0);
		}

		public IEnumerable<FrameEntry> EncodeWave(WaveEntry input)
		{
			if (input.SamplesInFrame > AAC_SAMPLES_PER_FRAME)
				throw new Exception($"Maximum number of samples that can be sent to the encoder at one time is {AAC_SAMPLES_PER_FRAME}");

			int samplesNeeded
				= WaveFormat.Encoding is NAudio.Wave.WaveFormatEncoding.Dts && WaveFormat.Channels == 2
				? SendSamplesPlanarStereo(input.FrameData.Span, input.FrameData2.Span, (int)input.SamplesInFrame)
				: SendSamples(input.FrameData.Span, (int)input.SamplesInFrame);

            if (samplesNeeded > 0) yield break;

			do
			{
				int encodedSize = GetAvailableFrameSize();

				if (encodedSize < 0)
					throw new Exception("Failed to retrieve encoded samples.");
				else if (encodedSize == 0) yield break;

				Memory<byte> encAud = GetEncodedFrame(encodedSize);
				yield return new FrameEntry
				{
					Chunk = input.Chunk,
					FrameIndex = input.FrameIndex,
					SamplesInFrame = AAC_SAMPLES_PER_FRAME,
					FrameData = encAud
				};
			} while (true);
		}

		public IEnumerable<FrameEntry> EncodeFlush()
		{
			int ret = aacEncoder.EncodeFlush();

			if (ret < 0)
				throw new Exception($"Error flusing AAC encoder.");

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
			fixed(byte* buffer1 = frameData)
            {
                ret = aacEncoder.EncodeFrame(buffer1, null, numSamples);
            }

			if (ret < 0)
				throw new Exception("Failed to encode samples.");

			return ret;
		}

		private int SendSamplesPlanarStereo(Span<byte> frameData1, Span<byte> frameData2, int numSamples)
		{
			int ret;
			fixed(byte* buffer1 = frameData1)
			{
				fixed (byte* buffer2 = frameData2)
				{
					ret = aacEncoder.EncodeFrame(buffer1, buffer2, numSamples);
				}
			}

			if (ret < 0)
				throw new Exception("Failed to encode samples.");

			return ret;
		}

		private int GetAvailableFrameSize() => aacEncoder.ReceiveEncodedFrame(null, 0);

		private Memory<byte> GetEncodedFrame(int encodedSize)
		{
			Memory<byte> encAud = new byte[encodedSize];
			fixed (byte* pEncAud = encAud.Span)
			{
				encodedSize = aacEncoder.ReceiveEncodedFrame(pEncAud, encodedSize);
			}
			if (encodedSize != 0)
				throw new Exception("Failed to retrieve encoded samples.");
			return encAud;
		}

		public void Dispose()
		{
			aacEncoder?.Close();
		}

		private class NativeAacEncode
		{
			//Factor for converting quality to global_quality
			private const int FF_QP2LAMBDA = 118;
			private readonly EncoderHandle Handle;
			private NativeAacEncode(EncoderHandle handle) => Handle = handle;

			[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
			private static extern EncoderHandle AacEncoder_Open(AacEncoderOptions* options);

			[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
			private static extern int AacEncoder_EncodeFrame(EncoderHandle self, byte* pWaveAudio1, byte* pWaveAudio2, int nbSamples);

			[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
			private static extern int AacEncoder_ReceiveEncodedFrame(EncoderHandle self, byte* pEncodedAudio, int size);

			[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
			private static extern int AacEncoder_EncodeFlush(EncoderHandle self);

			[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
			private static extern int AacEncoder_GetExtraData(EncoderHandle self, byte* ascBuffer, int* pSize);

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
				var handle = AacEncoder_Open(&options);

				long err = (long)handle.DangerousGetHandle();

				if (err < 0)
				{
					throw new Exception($"Error opening AAC Decoder. Code {err}");
				}
				return new NativeAacEncode(handle);
			}

			public void Close() => Handle.Close();
			public int EncodeFrame(byte* pWaveAudio1, byte* pWaveAudio2, int nbSamples)
				=> AacEncoder_EncodeFrame(Handle, pWaveAudio1, pWaveAudio2, nbSamples);
			public int ReceiveEncodedFrame(byte* pEncodedAudio, int size)
				=> AacEncoder_ReceiveEncodedFrame(Handle, pEncodedAudio, size);
			public int EncodeFlush()
				=> AacEncoder_EncodeFlush(Handle);

			public byte[] GetAudioSpecificConfig()
			{
                var ascSize = AacEncoder_GetExtraData(Handle, null, null);
				var ascBuffer = new byte[ascSize];
                fixed (byte* pAscBuffer = ascBuffer)
				{
                    if (AacEncoder_GetExtraData(Handle, pAscBuffer, &ascSize) != 0)
						throw new Exception("Failed to retrieve Audio Specific Config.");
                }
				return ascBuffer;
            }

			private class EncoderHandle : SafeHandle
			{
				[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
				private static extern int AacEncoder_Close(IntPtr self);
				private EncoderHandle() : base(IntPtr.Zero, true) { }
				public override bool IsInvalid => IsClosed || handle == IntPtr.Zero;
				protected override bool ReleaseHandle()
				{
					AacEncoder_Close(handle);
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
