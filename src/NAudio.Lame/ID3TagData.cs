﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace NAudio.Lame
{/// <summary>ID3 tag content</summary>
	public class ID3TagData
	{
		// Standard values:
		/// <summary>Track title (TIT2)</summary>
		public string? Title;
		/// <summary>Artist (TPE1)</summary>
		public string? Artist;
		/// <summary>Composer (TCOM)</summary>
		public string? Composer;
		/// <summary>Album (TALB)</summary>
		public string? Album;
		/// <summary>Year (TYER)</summary>
		public string? Year;
		/// <summary>Comment (COMM)</summary>
		public string? Comment;
		/// <summary>Genre (TCON)</summary>
		public string? Genre;
		/// <summary>Track number (TRCK)</summary>
		public string? Track;

		// Experimental:
		/// <summary>Subtitle (TIT3)</summary>
		public string? Subtitle;
		/// <summary>AlbumArtist (TPE2)</summary>
		public string? AlbumArtist;

		/// <summary>User defined text frames (TXXX)</summary>
		/// <remarks>Stored in ID3v2 tag as one TXXX frame per item.</remarks>
		public Dictionary<string, string?> UserDefinedText { get; set; } = new();
		public Dictionary<string, string?> AdditionalTags { get; set; } = new();
		public List<(TimeSpan start, TimeSpan end, string title)> Chapters { get; } = new();

		/// <summary>Album art - PNG, JPG or GIF file content</summary>
		public byte[]? AlbumArt;

		public ID3TagData() { }
		public ID3TagData(string tool)
		{
			UserDefinedText["TOOL"] = tool;
		}

		/// <summary>
		/// Clear <see cref="UserDefinedText"/> and insert values from collection of "description=text" strings.
		/// </summary>
		/// <param name="data">Collection to load.</param>
		public void SetUDT(IEnumerable<string> data)
		{
			UserDefinedText.Clear();
			foreach (var item in data)
			{
				string key = item.Split('=').First();
				int valuePos = key.Length + 1;
				string val = valuePos > item.Length ? string.Empty : item.Substring(valuePos);
				UserDefinedText[key] = val;
			}
		}
	}
}