﻿using System;
using System.IO;
using System.Drawing.Imaging;
using System.Windows.Forms;

using BizHawk.Bizware.BizwareGL;
using BizHawk.Emulation.Common;

namespace BizHawk.Client.EmuHawk
{
	/// <summary>
	/// Writes a sequence of 24bpp PNG or JPG files
	/// </summary>
	[VideoWriter("imagesequence", "Image sequence writer", "Writes a sequence of 24bpp PNG or JPG files (default compression quality)")]
	public class ImageSequenceWriter : IDisposable, IVideoWriter
	{
		private string _baseName;
		private int _frame;

		public void SetVideoCodecToken(IDisposable token)
		{
		}

		public void SetDefaultVideoCodecToken()
		{
		}

		public bool UsesAudio => false;

		public bool UsesVideo => true;

		public void OpenFile(string baseName)
		{
			_baseName = baseName;
		}

		public void CloseFile()
		{
		}

		public void SetFrame(int frame)
		{
			// eh? this gets ditched somehow
		}

		public void AddFrame(IVideoProvider source)
		{
			string ext = Path.GetExtension(_baseName) ?? "";
			var name = Path.Combine(Path.GetDirectoryName(_baseName) ?? "", $"{Path.GetFileNameWithoutExtension(_baseName)}_{_frame}{ext}");
			BitmapBuffer bb = new BitmapBuffer(source.BufferWidth, source.BufferHeight, source.GetVideoBuffer());
			using var bmp = bb.ToSysdrawingBitmap();
			if (ext.ToUpper() == ".PNG")
			{
				bmp.Save(name, ImageFormat.Png);
			}
			else if (ext.ToUpper() == ".JPG")
			{
				bmp.Save(name, ImageFormat.Jpeg);
			}

			_frame++;
		}

		public void AddSamples(short[] samples)
		{
		}

		private class CodecToken : IDisposable
		{
			public void Dispose()
			{
			}
		}

		public IDisposable AcquireVideoCodecToken(IWin32Window hwnd)
		{
			return new CodecToken();
		}

		public void SetMovieParameters(int fpsNum, int fpsDen)
		{
		}

		public void SetVideoParameters(int width, int height)
		{
		}

		public void SetAudioParameters(int sampleRate, int channels, int bits)
		{
		}

		public void SetMetaData(string gameName, string authors, ulong lengthMs, ulong rerecords)
		{
		}

		public string DesiredExtension() => "png";

		public void Dispose()
		{
		}
	}
}
