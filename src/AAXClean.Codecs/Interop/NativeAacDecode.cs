using AAXClean.Codecs.FrameFilters.Audio;
using Mpeg4Lib.Boxes;
using System;
using System.Runtime.InteropServices;

namespace AAXClean.Codecs.Interop;

internal class NativeAacDecode : NativeDecode
{
	protected override DecoderHandle Handle { get; }

	public unsafe NativeAacDecode(EsdsBox esed, WaveFormat waveFormat)
	{
		ArgumentNullException.ThrowIfNull(esed, nameof(esed));
		ArgumentNullException.ThrowIfNull(waveFormat, nameof(waveFormat));

		var asc = esed.ES_Descriptor.DecoderConfig.AudioSpecificConfig.AscBlob;
		fixed (byte* pAsc = asc)
		{
			AacDecoderOptions options = new()
			{
				output_options = GetOutputOptions(waveFormat),
				asc_size = asc.Length,
				ASC = pAsc
			};
			Handle = Decoder_OpenAac(ref options);
		}

		long err = Handle.DangerousGetHandle();

		if (err < 0)
		{
			throw new Exception($"Error opening AAC Decoder. Code {err}");
		}
	}

	[DllImport(libname, CallingConvention = CallingConvention.StdCall)]
	private static extern DecoderHandle Decoder_OpenAac(ref AacDecoderOptions decoder_options);

	[StructLayout(LayoutKind.Sequential)]
	private unsafe struct AacDecoderOptions
	{
		public OutputOptions output_options;
		public int asc_size;
		public byte* ASC;
	}
}
