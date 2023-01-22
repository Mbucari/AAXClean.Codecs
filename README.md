# AAXClean.Codecs
Converts and filters aac audio from [AAXClean](https://github.com/Mbucari/AAXClean).

**Now Cross-Platform!**

## Nuget
Include the [AAXClean.Codecs](https://www.nuget.org/packages/AAXClean.Codecs/) NuGet package to your project.

## Usage:

```C#
using AAXClean.Codecs

var audible_key = "aa0b0c0d0e0f1a1b1c1d1e1f2a2b2c2d";
var audible_iv = "ce2f3a3b3c3d3e3f4a4b4c4d4e4f5a5b";
aaxcFile.SetDecryptionKey(audible_key, audible_iv);
```
### Convert to Mp3:
```C#
await aaxcFile.ConvertToMp3Async(File.Open(@"C:\Decrypted book.mp3", FileMode.OpenOrCreate, FileAccess.ReadWrite));
```
Note that the output stream must be Readable, Writable and Seekable for the mp3 Xing header to be written. See [NAudio.Lame #24](https://github.com/Corey-M/NAudio.Lame/issues/24)

### Detect Silence
```C#
await aaxcFile.DetectSilenceAsync(-30, TimeSpan.FromSeconds(0.25));
```


### Conversion Usage:
```C#
var mp4File = new Mp4File(File.OpenRead(@"C:\Decrypted book.m4b"));
await mp4File.ConvertToMp3Async(File.OpenWrite(@"C:\Decrypted book.mp3"));
```
### Multipart Conversion Example:
Note that the input stream needs to be seekable to call GetChapterInfo()

```C#
var chapters = aaxcFile.GetChaptersFromMetadata();
await aaxcFile.ConvertToMultiMp4aAsync(chapters, NewSplit);
            
private static void NewSplit(NewSplitCallback newSplitCallback)
{
	string dir = @"C:\book split\";

	string fileName = newSplitCallback.Chapter.Title.Replace(":", "") + ".m4b";

	newSplitCallback.OutputFile = File.OpenWrite(Path.Combine(dir, fileName));
}
```
