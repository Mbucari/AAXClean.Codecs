using AAXClean.Codecs.FrameFilters.Audio;
using System;
using System.Runtime.InteropServices;

namespace AAXClean.Codecs.Interop;

internal unsafe class NativeAacEncode : IDisposable
{
	protected const string libname = "aaxcleannative";
	//Factor for converting quality to global_quality
	private const int FF_QP2LAMBDA = 118;
	private EncoderHandle Handle { get; }

	[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
	private static extern EncoderHandle AacEncoder_Open(ref AacEncoderOptions options);

	[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
	private static extern int AacEncoder_EncodeFrame(EncoderHandle self, byte* pWaveAudio1, byte* pWaveAudio2, int nbSamples);

	[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
	private static extern int AacEncoder_ReceiveEncodedFrame(EncoderHandle self, byte* pEncodedAudio, int size);

	[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
	private static extern int AacEncoder_EncodeFlush(EncoderHandle self);

	[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
	private static extern int AacEncoder_GetExtraData(EncoderHandle self, byte* ascBuffer, int* pSize);

	public NativeAacEncode(WaveFormat waveFormat, long bitRate, double quality)
	{
		AacEncoderOptions options = new()
		{
			bit_rate = bitRate,
			global_quality = (int)(FF_QP2LAMBDA * quality),
			sample_rate = waveFormat.SampleRate,
			channels = waveFormat.Channels,
			sample_fmt = (int)waveFormat.Encoding
		};
		Handle = AacEncoder_Open(ref options);

		long err = Handle.DangerousGetHandle();

		if (err < 0)
		{
			throw new Exception($"Error opening AAC Decoder. Code {err}");
		}
	}

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

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (disposing && !Handle.IsClosed)
			Handle.Close();
	}

	private class EncoderHandle : SafeHandle
	{
		[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
		private static extern int AacEncoder_Close(IntPtr self);
		private EncoderHandle() : base(IntPtr.Zero, true) { }
		public override bool IsInvalid => IsClosed || handle == IntPtr.Zero;
		protected override bool ReleaseHandle() => AacEncoder_Close(handle) == 0;
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
