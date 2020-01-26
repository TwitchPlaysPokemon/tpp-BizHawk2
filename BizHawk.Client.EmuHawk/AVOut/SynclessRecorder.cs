using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Text;
using System.Windows.Forms;

using BizHawk.Emulation.Common;
using BizHawk.Bizware.BizwareGL;

namespace BizHawk.Client.EmuHawk
{
	[VideoWriter("syncless", "Syncless Recording", "Writes each frame to a directory as a PNG and WAV pair, identified by frame number. The results can be exported into one video file.")]
	public class SynclessRecorder : IVideoWriter
	{
		public void Dispose()
		{
		}

		public void SetVideoCodecToken(IDisposable token)
		{
		}

		public void SetDefaultVideoCodecToken()
		{
		}

		public void SetFrame(int frame)
		{
			_mCurrFrame = frame;
		}

		private int _mCurrFrame;
		private string _mBaseDirectory, _mFramesDirectory;
		private string _mProjectFile;

		public void OpenFile(string projFile)
		{
			_mProjectFile = projFile;
			_mBaseDirectory = Path.GetDirectoryName(_mProjectFile) ?? "";
			string basename = Path.GetFileNameWithoutExtension(projFile);
			string framesDirFragment = $"{basename}_frames";
			_mFramesDirectory = Path.Combine(_mBaseDirectory, framesDirFragment);
			var sb = new StringBuilder();
			sb.AppendLine("version=1");
			sb.AppendLine($"framesdir={framesDirFragment}");
			File.WriteAllText(_mProjectFile, sb.ToString());
		}

		public void CloseFile()
		{
		}

		public void AddFrame(IVideoProvider source)
		{
			using var bb = new BitmapBuffer(source.BufferWidth, source.BufferHeight, source.GetVideoBuffer());
			string subPath = GetAndCreatePathForFrameNum(_mCurrFrame);
			string path = $"{subPath}.png";
			bb.ToSysdrawingBitmap().Save(path, ImageFormat.Png);
		}

		public void AddSamples(short[] samples)
		{
			string subPath = GetAndCreatePathForFrameNum(_mCurrFrame);
			string path = $"{subPath}.wav";
			var wwv = new WavWriterV();
			wwv.SetAudioParameters(_paramSampleRate, _paramChannels, _paramBits);
			wwv.OpenFile(path);
			wwv.AddSamples(samples);
			wwv.CloseFile();
			wwv.Dispose();
		}

		public bool UsesAudio => true;

		public bool UsesVideo => true;

		private class DummyDisposable : IDisposable
		{
			public void Dispose()
			{
			}
		}

		public IDisposable AcquireVideoCodecToken(IWin32Window hwnd)
		{
			return new DummyDisposable();
		}

		public void SetMovieParameters(int fpsNum, int fpsDen)
		{
			//should probably todo in here
		}

		public void SetVideoParameters(int width, int height)
		{
			// may want to todo
		}

		private int _paramSampleRate, _paramChannels, _paramBits;

		public void SetAudioParameters(int sampleRate, int channels, int bits)
		{
			_paramSampleRate = sampleRate;
			_paramChannels = channels;
			_paramBits = bits;
		}

		public void SetMetaData(string gameName, string authors, ulong lengthMs, ulong rerecords)
		{
			// not needed
		}

		public string DesiredExtension() => "syncless.txt";

		/// <summary>
		/// splits the string into chunks of length s
		/// </summary>
		private static List<string> StringChunkSplit(string s, int len)
		{
			if (len == 0)
			{
				throw new ArgumentException("Invalid len", nameof(len));
			}

			int numChunks = (s.Length + len - 1) / len;
			var output = new List<string>(numChunks);
			for (int i = 0, j = 0; i < numChunks; i++, j += len)
			{
				int todo = len;
				int remain = s.Length - j;
				if (remain < todo)
				{
					todo = remain;
				}

				output.Add(s.Substring(j, todo));
			}

			return output;
		}

		private string GetAndCreatePathForFrameNum(int index)
		{
			string subPath = GetPathFragmentForFrameNum(index);
			string path = _mFramesDirectory;
			path = Path.Combine(path, subPath);
			string fPath = $"{path}.nothing";
			Directory.CreateDirectory(Path.GetDirectoryName(fPath) ?? "");
			return path;
		}

		public static string GetPathFragmentForFrameNum(int index)
		{
			var chunks = StringChunkSplit(index.ToString(), 2);
			string subPath = string.Join("/", chunks);
			return subPath;
		}
	}
}
