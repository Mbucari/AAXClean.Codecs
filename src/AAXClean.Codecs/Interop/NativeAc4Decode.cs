using AAXClean.Codecs.FrameFilters.Audio;
using Mpeg4Lib.Boxes;
using System;
using System.Runtime.InteropServices;

namespace AAXClean.Codecs.Interop;

internal unsafe class NativeAc4Decode : NativeDecode
{
	protected override DecoderHandle Handle { get; }

	public NativeAc4Decode(Dac4Box dec3, WaveFormat waveFormat)
	{
		ArgumentNullException.ThrowIfNull(dec3, nameof(dec3));
		ArgumentNullException.ThrowIfNull(waveFormat, nameof(waveFormat));

		OutputOptions options = GetOutputOptions(waveFormat);
		Handle = Decoder_OpenAC4(ref options);
	}

	[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
	private static extern DecoderHandle Decoder_OpenAC4(ref OutputOptions decoder_options);

	[StructLayout(LayoutKind.Sequential)]
	private struct Ac4DecoderOptions
	{
		public OutputOptions output_options;
		public int in_sample_rate;
		public byte in_subwoofer;
		public byte in_audio_coding_mode;
	}
}
