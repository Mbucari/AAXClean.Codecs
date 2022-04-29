using NAudio.Lame;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AAXClean.Codecs.AudioFilters
{
    internal class LameMP3FileWriterEx : LameMP3FileWriter
    {
		private readonly Action ID3Init;
		private readonly Action<string> ID3SetTitle;
		private readonly Action<string> ID3SetArtist;
		private readonly Action<string> ID3SetAlbum;
		private readonly Action<string> ID3SetYear;
		private readonly Func<string, bool> ID3SetComment;
		private readonly Func<string, bool> ID3SetGenre;
		private readonly Func<string, bool> ID3SetTrack;
		private readonly Func<string, bool> ID3SetFieldValue;
		private readonly Func<byte[], bool> ID3SetAlbumArt;
		private readonly Func<byte[]> ID3GetID3v2Tag;
		private readonly Action<bool> set_ID3WriteTagAutomatic;
		private readonly Stream _outStream;
		private readonly object _lame;
		private readonly IntPtr context;
		internal LameMP3FileWriterEx(Stream outputStream, WaveFormat waveFormat, LameConfig lameConfig)
            :base(outputStream, waveFormat, lameConfig)
        {
			_lame = typeof(LameMP3FileWriter).GetField("_lame", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(this);
			_outStream = typeof(LameMP3FileWriter).GetField("_outStream", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(this) as Stream;

			var typ = _lame.GetType();

			context = (IntPtr)typ.GetField("context", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(_lame);
			ID3Init = typ.GetMethod("ID3Init").CreateDelegate<Action>(_lame);
			ID3SetTitle = typ.GetMethod("ID3SetTitle").CreateDelegate<Action<string>>(_lame);
			ID3SetArtist = typ.GetMethod("ID3SetArtist").CreateDelegate<Action<string>>(_lame);
			ID3SetAlbum = typ.GetMethod("ID3SetAlbum").CreateDelegate<Action<string>>(_lame);
			ID3SetYear = typ.GetMethod("ID3SetYear", new Type[] { typeof(string) }).CreateDelegate<Action<string>>(_lame);
			ID3SetComment = typ.GetMethod("ID3SetComment").CreateDelegate<Func<string, bool>>(_lame);
			ID3SetGenre = typ.GetMethod("ID3SetGenre", new Type[] { typeof(string) }).CreateDelegate<Func<string, bool>>(_lame);
			ID3SetTrack = typ.GetMethod("ID3SetTrack").CreateDelegate<Func<string, bool>>(_lame);
			ID3SetFieldValue = typ.GetMethod("ID3SetFieldValue").CreateDelegate<Func<string, bool>>(_lame);
			ID3SetAlbumArt = typ.GetMethod("ID3SetAlbumArt").CreateDelegate<Func<byte[], bool>>(_lame);
			ID3GetID3v2Tag = typ.GetMethod("ID3GetID3v2Tag").CreateDelegate<Func<byte[]>>(_lame);
			set_ID3WriteTagAutomatic = typ.GetProperty("ID3WriteTagAutomatic").GetSetMethod().CreateDelegate<Action<bool>>(_lame);
		}

		public void ApplyID3Tag(ID3TagData tag, ChapterInfo chapters)
		{
			if (tag == null)
				return;

			ID3Init();

			// Apply standard ID3 fields
			if (!string.IsNullOrEmpty(tag.Title))
				ID3SetTitle(tag.Title);
			if (!string.IsNullOrEmpty(tag.Artist))
				ID3SetArtist(tag.Artist);
			if (!string.IsNullOrEmpty(tag.Album))
				ID3SetAlbum(tag.Album);
			if (!string.IsNullOrEmpty(tag.Year))
				ID3SetYear(tag.Year);
			if (!string.IsNullOrEmpty(tag.Comment))
				ID3SetComment(tag.Comment);
			if (!string.IsNullOrEmpty(tag.Genre))
				ID3SetGenre(tag.Genre);
			if (!string.IsNullOrEmpty(tag.Track))
				ID3SetTrack(tag.Track);

			// Apply standard ID3 fields that are not directly supported by LAME
			if (!string.IsNullOrEmpty(tag.Subtitle))
				ID3SetFieldValue($"TIT3={tag.Subtitle}");
			if (!string.IsNullOrEmpty(tag.AlbumArtist))
				ID3SetFieldValue($"TPE2={tag.AlbumArtist}");

			// Add user-defined tags if present
			foreach (var kv in tag.UserDefinedText)
			{
				ID3SetFieldValue($"TXXX={kv.Key}={kv.Value}");
			}

			// Set the album art if supplied
			if (tag.AlbumArt?.Length > 0)
				ID3SetAlbumArt(tag.AlbumArt);

			// check size of ID3 tag, if too large write it ourselves.
			byte[] data = ID3GetID3v2Tag();
			var tagStr = Encoding.UTF8.GetString(data);
			if (data?.Length >= 32768)
			{
				set_ID3WriteTagAutomatic(false);

				_outStream.Write(data, 0, data.Length);
			}
		}
	}
}
