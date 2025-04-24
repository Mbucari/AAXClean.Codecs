﻿using AAXClean.Codecs.FrameFilters.Audio;
using System;
using System.Runtime.InteropServices;

namespace AAXClean.Codecs.Interop;

internal unsafe abstract class NativeDecode : IDisposable
{
	protected abstract DecoderHandle Handle { get; }
	protected const string libname = "ffmpegaac";

	[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
	private static extern int Decoder_DecodeFrame(DecoderHandle self, byte* pCompressedAudio, int cbInBufferSize, uint nbSamples);

	[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
	private static extern int Decoder_ReceiveDecodedFrame(DecoderHandle self, byte* pDecodedAudio1, byte* pDecodedAudio2, int cbInBufferSize);

	[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
	private static extern int Decoder_DecodeFlush(DecoderHandle self, byte* pDecodedAudio1, byte* pDecodedAudio2, int cbInBufferSize);

	public int DecodeFrame(byte* pCompressedAudio, int cbInputSize, uint nbSamples)
		=> Decoder_DecodeFrame(Handle, pCompressedAudio, cbInputSize, nbSamples);
	public int ReceiveDecodedFrame(byte* pDecodedAudio1, byte* pDecodedAudio2, int cbInputSize)
		=> Decoder_ReceiveDecodedFrame(Handle, pDecodedAudio1, pDecodedAudio2, cbInputSize);
	public int DecodeFlush(byte* pDecodedAudio1, byte* pDecodedAudio2, int cbInputSize)
		=> Decoder_DecodeFlush(Handle, pDecodedAudio1, pDecodedAudio2, cbInputSize);

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

	[StructLayout(LayoutKind.Sequential)]
	protected struct OutputOptions
	{
		public int out_sample_rate;
		public int out_sample_fmt;
		public int out_channels;
	}

	protected static OutputOptions GetOutputOptions(WaveFormat waveFormat)
	{
		if (waveFormat.Channels is not 1 and not 2)
			throw new ArgumentException("Output wave format must be either mono or stereo.");

		return new OutputOptions
		{
			out_sample_rate = waveFormat.SampleRate,
			out_sample_fmt = (int)waveFormat.Encoding,
			out_channels = waveFormat.Channels,
		};
	}

	protected class DecoderHandle : SafeHandle
	{
		[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
		private static extern int Decoder_Close(IntPtr self);
		private DecoderHandle() : base(IntPtr.Zero, true) { }
		public override bool IsInvalid => IsClosed || handle == IntPtr.Zero;
		protected override bool ReleaseHandle() => Decoder_Close(handle) == 0;
	}
}
