using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;

namespace AAXClean.Codecs.FrameFilters.Audio
{
	internal sealed unsafe class FfmpegAacDecoder : IDisposable
	{
		internal const int BITS_PER_SAMPLE = 16;

		private const int AAC_FRAME_SIZE = 1024 * BITS_PER_SAMPLE / 8;
		private readonly NativeAac AacDecoder;
		internal int DecodeSize => AAC_FRAME_SIZE * Channels;
		public int Channels { get; }
		public int SampleRate { get; }

		private static readonly int[] asc_samplerates = { 96000, 88200, 64000, 48000, 44100, 32000, 24000, 22050, 16000, 12000, 11025, 8000, 7350 };

		public FfmpegAacDecoder(byte[] asc)
		{
			SampleRate = asc_samplerates[(asc[0] & 7) << 1 | asc[1] >> 7];
			Channels = (asc[1] >> 3) & 7;
			AacDecoder = NativeAac.Open(asc, asc.Length);
		}

		public MemoryHandle DecodeRaw(Span<byte> aacFrame)
		{
			int error, inputSize = aacFrame.Length;

			Memory<byte> decoded = new byte[DecodeSize];

			MemoryHandle handle = decoded.Pin();

			fixed (byte* buff = aacFrame)
			{
				byte* outBuff = (byte*)handle.Pointer;

				error = AacDecoder.DecodeFrame(buff, inputSize, outBuff, DecodeSize);
			}

			if (error != 0)
			{
				throw new Exception($"Error decoding AAC frame. Code {error:X}");
			}

			return handle;
		}

		public Memory<byte> Decode(Span<byte> aacFrame)
		{
			int error, inputSize = aacFrame.Length;

			Memory<byte> decoded = new byte[DecodeSize];

			using MemoryHandle handle = decoded.Pin();

			fixed (byte* buff = aacFrame)
			{
				byte* outBuff = (byte*)handle.Pointer;

				error = AacDecoder.DecodeFrame(buff, inputSize, outBuff, DecodeSize);
			}

			if (error != 0)
			{
				throw new Exception($"Error decoding AAC frame. Code {error:X}");
			}

			return decoded;
		}

		public void Dispose()
		{
			AacDecoder?.Close();
		}

		private class DecoderHandle : SafeHandle
		{
			public Action<DecoderHandle> CloseHandle { get; set; }
			public DecoderHandle() : base(IntPtr.Zero, true) { }
			public override bool IsInvalid => !IsClosed && handle != IntPtr.Zero;
			protected override bool ReleaseHandle()
			{
				CloseHandle(this);
				return true;
			}
		}

		private abstract class NativeAac
		{
			private DecoderHandle Handle;
			private static readonly int bitness = IntPtr.Size * 8;

			static NativeAac()
			{
				string libName = $"ffmpegaac_x{bitness}.dll";

				if (!File.Exists(libName))
				{
					try
					{
						if (bitness == 64)
							File.WriteAllBytes(libName, Properties.Resources.ffmpegx64);
						else
							File.WriteAllBytes(libName, Properties.Resources.ffmpegx86);
					}
					catch (Exception ex)
					{
						throw new DllNotFoundException($"Dould not load {libName}", ex);
					}
				}
			}

			public static NativeAac Open(byte[] ASC, int ASCSize)
			{
				NativeAac aac = bitness == 32 ? new NativeAac32() : new NativeAac64();

				aac.Handle = aac.OpenHandle(ASC, ASCSize);
				aac.Handle.CloseHandle = aac.Close;

				long err = (long)aac.Handle.DangerousGetHandle();

				if (err < 0)
				{
					throw new Exception($"Error opening AAC Decoder. Code {err}");
				}
				return aac;
			}
			public void Close()
			{
				Close(Handle);
				Handle.Dispose();
			}

			public int DecodeFrame(byte* pCompressedAudio, int cbInBufferSize, byte* pDecodedAudio, int cbOutBufferSize)
				=> DecodeFrame(Handle, pCompressedAudio, cbInBufferSize, pDecodedAudio, cbOutBufferSize);

			protected abstract DecoderHandle OpenHandle(byte[] ASC, int ASCSize);
			protected abstract void Close(DecoderHandle self);
			protected abstract int DecodeFrame(DecoderHandle self, byte* pCompressedAudio, int cbInBufferSize, byte* pDecodedAudio, int cbOutBufferSize);
		}

		private class NativeAac32 : NativeAac
		{
			private const string libName = "ffmpegaac_x32.dll";

			[DllImport(libName, CallingConvention = CallingConvention.StdCall)]
			private static extern DecoderHandle aacDecoder_Open(byte[] ASC, int ASCSize);

			[DllImport(libName, CallingConvention = CallingConvention.StdCall)]
			private static extern void aacDecoder_Close(DecoderHandle self);

			[DllImport(libName, CallingConvention = CallingConvention.StdCall)]
			private static extern int aacDecoder_DecodeFrame(DecoderHandle self, byte* pCompressedAudio, int cbInBufferSize, byte* pDecodedAudio, int cbOutBufferSize);

			protected override DecoderHandle OpenHandle(byte[] ASC, int ASCSize)
				=> aacDecoder_Open(ASC, ASCSize);

			protected override void Close(DecoderHandle self)
				=> aacDecoder_Close(self);

			protected override int DecodeFrame(DecoderHandle self, byte* pCompressedAudio, int cbInBufferSize, byte* pDecodedAudio, int cbOutBufferSize)
				=> aacDecoder_DecodeFrame(self, pCompressedAudio, cbInBufferSize, pDecodedAudio, cbOutBufferSize);
		}

		private class NativeAac64 : NativeAac
		{
			private const string libName = "ffmpegaac_x64.dll";

			[DllImport(libName, CallingConvention = CallingConvention.StdCall)]
			private static extern DecoderHandle aacDecoder_Open(byte[] ASC, int ASCSize);

			[DllImport(libName, CallingConvention = CallingConvention.StdCall)]
			private static extern void aacDecoder_Close(DecoderHandle self);

			[DllImport(libName, CallingConvention = CallingConvention.StdCall)]
			private static extern int aacDecoder_DecodeFrame(DecoderHandle self, byte* pCompressedAudio, int cbInBufferSize, byte* pDecodedAudio, int cbOutBufferSize);

			protected override DecoderHandle OpenHandle(byte[] ASC, int ASCSize)
				=> aacDecoder_Open(ASC, ASCSize);

			protected override void Close(DecoderHandle self)
				=> aacDecoder_Close(self);

			protected override int DecodeFrame(DecoderHandle self, byte* pCompressedAudio, int cbInBufferSize, byte* pDecodedAudio, int cbOutBufferSize)
				=> aacDecoder_DecodeFrame(self, pCompressedAudio, cbInBufferSize, pDecodedAudio, cbOutBufferSize);
		}
	}
}
