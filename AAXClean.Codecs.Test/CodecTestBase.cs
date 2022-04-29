using Microsoft.VisualStudio.TestTools.UnitTesting;
using AAXClean;
using System.IO;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Security.Cryptography;

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
        public abstract string SingleMp3Hash { get; }
        public abstract TimeSpan SilenceDuration { get; }
        public abstract List<(TimeSpan start, TimeSpan end)> SilenceTimes { get; }
        public abstract List<string> MultiMp3Hashes { get; }
        public abstract double SilenceThreshold { get; }

        [TestMethod]
        public void _0_SilenceDetection()
        {
            try
            {
                int silEndex = 0;
                void SilenceDetected(SilenceDetectCallback callback)
                {
                    Assert.AreEqual(callback.Silence.SilenceStart, SilenceTimes[silEndex].start);
                    Assert.AreEqual(callback.Silence.SilenceEnd, SilenceTimes[silEndex].end);
                    silEndex++;
                }
                var silecnes = Aax.DetectSilence(SilenceThreshold, SilenceDuration, SilenceDetected).ToList();

                Assert.AreEqual(silecnes.Count, SilenceTimes.Count);

                for (int i = 0; i < silecnes.Count; i++)
                {
                    Assert.AreEqual(silecnes[i].SilenceStart, SilenceTimes[i].start);
                    Assert.AreEqual(silecnes[i].SilenceEnd, SilenceTimes[i].end);
                }

#if DEBUG
                StringBuilder sb = new StringBuilder();
                foreach (var sil in silecnes)
                    sb.AppendLine($"(TimeSpan.FromTicks({sil.SilenceStart.Ticks}), TimeSpan.FromTicks({sil.SilenceEnd.Ticks})),");
#endif
            }
            finally
            {
                Aax.Close();
            }
        }
        [TestMethod]
        public void _1_ConvertMp3Single()
        {
            try
            {
                var tempfile = TestFiles.NewTempFile();
                var result = Aax.ConvertToMp3(tempfile, new NAudio.Lame.LameConfig { Preset = NAudio.Lame.LAMEPreset.STANDARD_FAST, Mode = NAudio.Lame.MPEGMode.Mono });

                Assert.AreEqual(result, ConversionResult.NoErrorsDetected);

                using var sha = SHA1.Create();

                var mp4file = File.OpenRead(tempfile.Name);
                int read;
                byte[] buff = new byte[4 * 1024 * 1024];

                while ((read = mp4file.Read(buff)) == buff.Length)
                {
                    sha.TransformBlock(buff, 0, read, null, 0);
                }
                mp4file.Close();
                sha.TransformFinalBlock(buff, 0, read);
                var fileHash = string.Join("", sha.Hash.Select(b => b.ToString("x2")));

                Assert.AreEqual(SingleMp3Hash, fileHash);
            }
            finally
            {
                TestFiles.CloseAllFiles();
                Aax.Close();
            }
        }
        [TestMethod]
        public void _2_ConvertMp3Multiple()
        {
            try
            {
                List<string> tempFiles = new();
                void NewSplit(NewSplitCallback callback)
                {
                    callback.OutputFile = TestFiles.NewTempFile();
                    tempFiles.Add(((FileStream)callback.OutputFile).Name);
                }

                Aax.ConvertToMultiMp3(Aax.GetChapterInfo(), NewSplit, new NAudio.Lame.LameConfig { Preset = NAudio.Lame.LAMEPreset.STANDARD_FAST, Mode = NAudio.Lame.MPEGMode.Mono });
#if !DEBUG
                Assert.AreEqual(MultiM4bHashes.Count, tempFiles.Count);
#endif
                using var sha = SHA1.Create();
                List<string> hashes = new();

                foreach (var tmp in tempFiles)
                {
                    sha.ComputeHash(File.ReadAllBytes(tmp));
                    hashes.Add(string.Join("", sha.Hash.Select(b => b.ToString("x2"))));
                }
#if DEBUG
                var hs = new StringBuilder();

                foreach (var h in hashes)
                {
                    hs.AppendLine($"\"{h}\",");
                }
#endif

                for (int i = 0; i < tempFiles.Count; i++)
                {
                    Assert.AreEqual(MultiMp3Hashes[i], hashes[i]);
                }
            }
            finally
            {
                TestFiles.CloseAllFiles();
                Aax.Close();
            }
        }
    }
}
