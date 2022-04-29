using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace AAXClean.Codecs.Test
{
    public static class TestFiles
    {
        private static List<FileStream> OpenTempFiles { get; } = new List<FileStream>();
        private static string HP_WebPath { get; } = "https://drive.google.com/uc?export=download&id=1UIc0ouxIspS2RjGstX1Rzvp3QdeidDrR&confirm=t";

        private static string _HP_BookPath { get; } = @"..\..\..\..\..\..\TestFiles\Harry Potter and the Sorcerer's Stone, Book 1 [B017V4IM1G] - Zero.aax";

        public static string HP_BookPath => FindOrDownload(_HP_BookPath, HP_WebPath);

        private static string FindOrDownload(string path, string url)
        {
            if (!File.Exists(path))
            {
                var dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                using WebClient cli = new();
                cli.DownloadFile(url, path);
            }
            return path;
        }

        public static void CloseAllFiles()
        {
            foreach (var fs in OpenTempFiles)
            {
                fs.Close();
                File.Delete(fs.Name);
            }
            OpenTempFiles.Clear();
        }

        public static FileStream NewTempFile()
        {
            var fs = File.Open(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
            OpenTempFiles.Add(fs);
            return fs;
        }
    }
}
