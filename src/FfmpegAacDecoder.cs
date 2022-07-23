using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;

namespace AAXClean.Codecs
{
	internal unsafe sealed class FfmpegAacDecoder : IDisposable
	{
		internal const int BITS_PER_SAMPLE = 16;
		public int Channels { get; }
		public int SampleRate { get; }

		private readonly NativeAac AacDecoder;

		private static readonly int[] asc_samplerates = { 96000, 88200, 64000, 48000, 44100, 32000, 24000, 22050, 16000, 12000, 11025, 8000, 7350 };

		public FfmpegAacDecoder(byte[] asc)
		{
			SampleRate = asc_samplerates[(asc[0] & 7) << 1 | asc[1] >> 7];
			Channels = (asc[1] >> 3) & 7;
			AacDecoder = NativeAac.Open(asc, asc.Length);
		}

		public (MemoryHandle, Memory<byte>) DecodeRaw(Span<byte> aacFrame, uint frameDelta)
		{
			int error, frameSize = (int)frameDelta * sizeof(short) * Channels;

			Memory<byte> decoded = new byte[frameSize];

			MemoryHandle handle = decoded.Pin();

			fixed (byte* inBuff = aacFrame)
			{
				byte* outBuff = (byte*)handle.Pointer;

				error = AacDecoder.DecodeFrame(inBuff, aacFrame.Length, outBuff, frameSize);
			}

			if (error != 0)
			{
				throw new Exception($"Error decoding AAC frame. Code {error:X}");
			}

			return (handle, decoded);
		}

		private bool disposed = false;
		public void Dispose()
		{
			if (!disposed)
			{
				AacDecoder?.Close();
			}
		}
		~FfmpegAacDecoder()
		{
			Dispose();
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
		private class NativeAac
		{
			private bool closed = false;
			private DecoderHandle Handle;

			private const string libname = "ffmpegaac";

			[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
			private static extern DecoderHandle aacDecoder_Open(byte[] ASC, int ASCSize);

			[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
			private static extern void aacDecoder_Close(DecoderHandle self);

			[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
			private static extern int aacDecoder_DecodeFrame(DecoderHandle self, byte* pCompressedAudio, int cbInBufferSize, byte* pDecodedAudio, int cbOutBufferSize);

			public static NativeAac Open(byte[] ASC, int ASCSize)
			{
				var aac = new NativeAac();
				aac.Handle = aacDecoder_Open(ASC, ASCSize);
				aac.Handle.CloseHandle = aacDecoder_Close;

				long err = (long)aac.Handle.DangerousGetHandle();

				if (err < 0)
				{
					throw new Exception($"Error opening AAC Decoder. Code {err}");
				}
				return aac;
			}
			public void Close()
			{
				if (!closed)
				{
					aacDecoder_Close(Handle);
					Handle.Dispose();
					closed = true;
				}
			}

			public int DecodeFrame(byte* pCompressedAudio, int cbInBufferSize, byte* pDecodedAudio, int cbOutBufferSize)
				=> aacDecoder_DecodeFrame(Handle, pCompressedAudio, cbInBufferSize, pDecodedAudio, cbOutBufferSize);
		}
	}
}
