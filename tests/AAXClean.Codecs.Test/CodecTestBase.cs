using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace AAXClean.Codecs.Test
{
	public abstract class CodecTestBase
	{
		private AaxFile _aax;
		public AaxFile Aax
		{
			get
			{
				if (_aax is null)
				{
					_aax = new AaxFile(File.Open(AaxFile, FileMode.Open, FileAccess.Read, FileShare.Read));
					_aax.SetDecryptionKey(new byte[16], new byte[16]);
				}
				return _aax;
			}
		}
		public abstract string AaxFile { get; }
		public abstract int ChapterCount { get; }
		public abstract TimeSpan SilenceDuration { get; }
		public abstract List<(TimeSpan start, TimeSpan end)> SilenceTimes { get; }
		public abstract double SilenceThreshold { get; }

		[TestMethod]
		public async Task _0_SilenceDetection()
		{
			try
			{
				int silEndex = 0;
				void SilenceDetected(SilenceDetectCallback callback)
				{
#if !DEBUG
					Assert.AreEqual(callback.Silence.SilenceStart, SilenceTimes[silEndex].start);
					Assert.AreEqual(callback.Silence.SilenceEnd, SilenceTimes[silEndex].end);
#endif
					silEndex++;
				}
				List<SilenceEntry> silecnes = (await Aax.DetectSilenceAsync(SilenceThreshold, SilenceDuration, SilenceDetected)).ToList();

#if !DEBUG
				Assert.AreEqual(SilenceTimes.Count, silecnes.Count);

				for (int i = 0; i < silecnes.Count; i++)
				{
					Assert.AreEqual(SilenceTimes[i].start, silecnes[i].SilenceStart);
					Assert.AreEqual(SilenceTimes[i].end, silecnes[i].SilenceEnd);
				}
#else
				System.Text.StringBuilder sb = new System.Text.StringBuilder();
				foreach (var sil in silecnes)
					sb.AppendLine($"(TimeSpan.FromTicks({sil.SilenceStart.Ticks}), TimeSpan.FromTicks({sil.SilenceEnd.Ticks})),");
#endif
			}
			catch (Exception ex)
			{
				Assert.Fail($"Silence detection failed: {ex.Message}");
			}
			finally
			{
				Aax.InputStream.Close();
			}
		}

		[TestMethod]
		public async Task _1_ConvertMp3Single()
		{
			try
			{
				FileStream tempfile = TestFiles.NewTempFile();
				await Aax.ConvertToMp3Async(tempfile, new NAudio.Lame.LameConfig { Preset = NAudio.Lame.LAMEPreset.STANDARD_FAST, Mode = NAudio.Lame.MPEGMode.Mono });

				using SHA1 sha = SHA1.Create();

				FileStream mp4file = File.OpenRead(tempfile.Name);
				int read;
				byte[] buff = new byte[4 * 1024 * 1024];

				while ((read = mp4file.Read(buff)) == buff.Length)
				{
					sha.TransformBlock(buff, 0, read, null, 0);
				}
				mp4file.Close();
			}
			finally
			{
				TestFiles.CloseAllFiles();
				Aax.InputStream.Close();
			}
		}

		[TestMethod]
		public async Task _2_ConvertMp3SingleIndirect()
		{
			try
			{
				FileStream m4bfile = TestFiles.NewTempFile();
				FileStream mp3File = TestFiles.NewTempFile();

				await Aax.ConvertToMp4aAsync(m4bfile);
				Aax.InputStream.Close();

				Mp4File mp4 = new Mp4File(m4bfile.Name);

				await mp4.ConvertToMp3Async(mp3File, new NAudio.Lame.LameConfig { Preset = NAudio.Lame.LAMEPreset.STANDARD_FAST, Mode = NAudio.Lame.MPEGMode.Mono });
				mp4.InputStream.Close();
			}
			finally
			{
				TestFiles.CloseAllFiles();
			}
		}

		[TestMethod]
		public async Task _3_ConvertMp3Multiple()
		{
			try
			{
				List<string> tempFiles = new();
				void NewSplit(INewSplitCallback callback)
				{
					callback.OutputFile = TestFiles.NewTempFile();
					tempFiles.Add(((FileStream)callback.OutputFile).Name);
				}

				await Aax.ConvertToMultiMp3Async(Aax.GetChaptersFromMetadata(), NewSplit, new NAudio.Lame.LameConfig { Preset = NAudio.Lame.LAMEPreset.STANDARD_FAST, Mode = NAudio.Lame.MPEGMode.Mono });
				Assert.AreEqual(ChapterCount, tempFiles.Count);
			}
			finally
			{
				TestFiles.CloseAllFiles();
				Aax.InputStream.Close();
			}
		}

		[TestMethod]
		public async Task _4_ConvertMp4ReencodeSingle()
		{
			try
			{
				FileStream tempfile = TestFiles.NewTempFile();
				var options = new AacEncodingOptions
				{
					BitRate = 30000,
					Stereo = false,
					SampleRate = SampleRate.Hz_16000
				};
				await Aax.ConvertToMp4aAsync(tempfile, options, Aax.GetChaptersFromMetadata());

			}
			finally
			{
				TestFiles.CloseAllFiles();
				Aax.InputStream.Close();
			}
		}
		[TestMethod]
		public async Task _5_ConvertMp4ReencodeMultiple()
		{
			try
			{
				List<string> tempFiles = new();
				void NewSplit(INewSplitCallback callback)
				{
					callback.OutputFile = TestFiles.NewTempFile();
					tempFiles.Add(((FileStream)callback.OutputFile).Name);
				}

				await Aax.ConvertToMultiMp4aAsync(Aax.GetChaptersFromMetadata(), NewSplit, new AacEncodingOptions { BitRate = 30000, EncoderQuality = 0.6, Stereo = false, SampleRate = SampleRate.Hz_16000 });
				Assert.AreEqual(ChapterCount, tempFiles.Count);
			}
			finally
			{
				TestFiles.CloseAllFiles();
				Aax.InputStream.Close();
			}
		}
		[TestMethod]
		public async Task _6_TestCancelSingleMp3()
		{
			var aaxFile = Aax;
			try
			{
				FileStream tempfile = TestFiles.NewTempFile();

				var convertTask = aaxFile.ConvertToMp3Async(tempfile);
				convertTask.Start();

				await Task.Delay(100);
				await convertTask.CancelAsync();
				await convertTask;
				Assert.IsTrue(convertTask.IsCanceled);

				TestFiles.CloseAllFiles();
				Aax.InputStream.Close();
			}
			finally
			{
				TestFiles.CloseAllFiles();
				aaxFile.InputStream.Close();
			}
		}

		[TestMethod]
		public async Task _7_TestCancelMultiMp3()
		{
			var aaxFile = Aax;
			try
			{
				FileStream tempfile = TestFiles.NewTempFile();

				void NewSplit(INewSplitCallback callback)
				{
					callback.OutputFile = TestFiles.NewTempFile();
				}

				var convertTask = aaxFile.ConvertToMultiMp3Async(Aax.GetChaptersFromMetadata(), NewSplit);
				convertTask.Start();
				await Task.Delay(100);
				await convertTask.CancelAsync();

				await convertTask;
				Assert.IsTrue(convertTask.IsCanceled);

				TestFiles.CloseAllFiles();
				Aax.InputStream.Close();
			}
			finally
			{
				TestFiles.CloseAllFiles();
				aaxFile.InputStream.Close();
			}
		}
	}
}
