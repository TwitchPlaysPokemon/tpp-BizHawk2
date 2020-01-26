﻿using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

using BizHawk.Emulation.Common;

using BizHawk.Client.Common;
using BizHawk.Client.EmuHawk.WinFormExtensions;
using System.Drawing;

namespace BizHawk.Client.EmuHawk
{
	public class ToolFormBase : Form
	{
		public ToolManager Tools { get; set; }
		public Config Config { get; set; }
		public MainForm MainForm { get; set; }

		public static FileInfo OpenFileDialog(string currentFile, string path, string fileType, string fileExt)
		{
			if (!Directory.Exists(path))
			{
				Directory.CreateDirectory(path);
			}

			using var ofd = new OpenFileDialog
			{
				FileName = !string.IsNullOrWhiteSpace(currentFile)
					? Path.GetFileName(currentFile)
					: $"{PathManager.FilesystemSafeName(Global.Game)}.{fileExt}",
				InitialDirectory = path,
				Filter = string.Format("{0} (*.{1})|*.{1}|All Files|*.*", fileType, fileExt),
				RestoreDirectory = true
			};

			var result = ofd.ShowHawkDialog();
			if (result != DialogResult.OK)
			{
				return null;
			}

			return new FileInfo(ofd.FileName);
		}

		public static FileInfo SaveFileDialog(string currentFile, string path, string fileType, string fileExt)
		{
			if (!Directory.Exists(path))
			{
				Directory.CreateDirectory(path);
			}

			using var sfd = new SaveFileDialog
			{
				FileName = !string.IsNullOrWhiteSpace(currentFile)
					? Path.GetFileName(currentFile)
					: $"{PathManager.FilesystemSafeName(Global.Game)}.{fileExt}",
				InitialDirectory = path,
				Filter = string.Format("{0} (*.{1})|*.{1}|All Files|*.*", fileType, fileExt),
				RestoreDirectory = true,
			};

			var result = sfd.ShowHawkDialog();
			if (result != DialogResult.OK)
			{
				return null;
			}

			return new FileInfo(sfd.FileName);
		}

		public static FileInfo GetWatchFileFromUser(string currentFile)
		{
			return OpenFileDialog(currentFile, PathManager.MakeAbsolutePath(Global.Config.PathEntries.WatchPathFragment, null), "Watch Files", "wch");
		}

		public static FileInfo GetWatchSaveFileFromUser(string currentFile)
		{
			return SaveFileDialog(currentFile, PathManager.MakeAbsolutePath(Global.Config.PathEntries.WatchPathFragment, null), "Watch Files", "wch");
		}

		public void ViewInHexEditor(MemoryDomain domain, IEnumerable<long> addresses, WatchSize size)
		{
			Tools.Load<HexEditor>();
			Tools.HexEditor.SetToAddresses(addresses, domain, size);
		}

		protected void GenericDragEnter(object sender, DragEventArgs e)
		{
			e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
		}

		protected void RefreshFloatingWindowControl(bool floatingWindow)
		{
			Owner = floatingWindow ? null : MainForm;
		}

		protected bool IsOnScreen(Point topLeft)
		{
			return Tools.IsOnScreen(topLeft);
		}
	}
}
