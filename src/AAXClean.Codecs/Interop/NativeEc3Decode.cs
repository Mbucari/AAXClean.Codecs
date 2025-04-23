using AAXClean.Codecs.FrameFilters.Audio;
using Mpeg4Lib.Boxes;
using System;
using System.Runtime.InteropServices;

namespace AAXClean.Codecs.Interop;

internal unsafe class NativeEc3Decode : NativeDecode
{
	protected override DecoderHandle Handle { get; }

	public NativeEc3Decode(Dec3Box dec3, WaveFormat waveFormat)
	{
		ArgumentNullException.ThrowIfNull(dec3, nameof(dec3));
		ArgumentNullException.ThrowIfNull(waveFormat, nameof(waveFormat));
		ArgumentOutOfRangeException.ThrowIfGreaterThan(dec3.acmod, 7, nameof(dec3.acmod));

		Ec3DecoderOptions options = new()
		{
			output_options = GetOutputOptions(waveFormat),
			in_sample_rate = dec3.SampleRate,
			in_subwoofer = (byte)(dec3.lfeon ? 1 : 0),
			in_audio_coding_mode = dec3.acmod,
		};
		Handle = Decoder_OpenEC3(ref options);
	}

	[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
	private static extern DecoderHandle Decoder_OpenEC3(ref Ec3DecoderOptions decoder_options);

	[StructLayout(LayoutKind.Sequential)]
	private struct Ec3DecoderOptions
	{
		public OutputOptions output_options;
		public int in_sample_rate;
		public byte in_subwoofer;
		public byte in_audio_coding_mode;
	}
}
