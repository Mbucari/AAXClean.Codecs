using Mpeg4Lib;
using System.IO;

namespace AAXClean.Codecs;

public class NewAacSplitCallback : INewSplitCallback<NewAacSplitCallback>
{
	public Chapter Chapter { get; }
	public int? TrackNumber { get; set; }
	public int? TrackCount { get; set; }
	public string? TrackTitle { get; set; }
	public Stream? OutputFile { get; set; }
	public AacEncodingOptions? EncodingOptions { get; set; }

	private NewAacSplitCallback(Chapter chapter)
		=> Chapter = chapter;

	public static NewAacSplitCallback Create(Chapter chapter)
	{
		return new NewAacSplitCallback(chapter);
	}
}
