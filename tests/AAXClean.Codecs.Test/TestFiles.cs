using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace AAXClean.Codecs.Test
{
	public static class TestFiles
	{
		private static readonly List<FileStream> OpenTempFiles = new List<FileStream>();
		private const string TEST_FILE_DIR = @"..\..\..\..\..\..\TestFiles";
		private const string HP_URL = "https://drive.google.com/uc?export=download&id=1UIc0ouxIspS2RjGstX1Rzvp3QdeidDrR&confirm=t";
		private static readonly string HP_FILENAME = Path.Combine(TEST_FILE_DIR, "HP_Zero.aax");
		public static string HP_BookPath => FindOrDownload(HP_FILENAME, HP_URL);

		private static string FindOrDownload(string path, string url)
		{
			if (!File.Exists(path))
			{
				if (!Directory.Exists(TEST_FILE_DIR))
					Directory.CreateDirectory(TEST_FILE_DIR);
				using WebClient cli = new();
				cli.DownloadFile(url, path);
			}
			return path;
		}

		public static void CloseAllFiles()
		{
			foreach (FileStream fs in OpenTempFiles.ToList())
			{
				fs.Close();
				File.Delete(fs.Name);
			}
			OpenTempFiles.Clear();
		}

		public static FileStream NewTempFile()
		{
			FileStream fs = File.Open(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
			OpenTempFiles.Add(fs);
			return fs;
		}
	}
}
