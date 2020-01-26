﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;

using BizHawk.Emulation.Common;
using BizHawk.Client.Common;
using BizHawk.Client.EmuHawk.WinFormExtensions;
using BizHawk.Common;

namespace BizHawk.Client.EmuHawk
{
	public partial class MultiDiskBundler : ToolFormBase, IToolFormAutoConfig
	{
		private XElement _currentXml;

		[RequiredService]
		public IEmulator Emulator { get; set; }

		public MultiDiskBundler()
		{
			InitializeComponent();
		}

		private void MultiGameCreator_Load(object sender, EventArgs e)
		{
			AddButton_Click(null, null);
			AddButton_Click(null, null);

			if (!Global.Game.IsNullInstance() &&  !MainForm.CurrentlyOpenRom.EndsWith(".xml"))
			{
				if (MainForm.CurrentlyOpenRom.Contains("|"))
				{
					var pieces = MainForm.CurrentlyOpenRom.Split('|');

					var directory = Path.GetDirectoryName(pieces[0]) ?? "";
					var filename = Path.ChangeExtension(pieces[1], ".xml");

					NameBox.Text = Path.Combine(directory, filename);
				}
				else
				{
					NameBox.Text = Path.ChangeExtension(MainForm.CurrentlyOpenRom, ".xml");
				}

				if (SystemDropDown.Items.Contains(Emulator.SystemId))
				{
					SystemDropDown.SelectedItem = Emulator.SystemId;
				}

				FileSelectors.First().Path = MainForm.CurrentlyOpenRom;
			}
		}

		#region IToolForm

		public void NewUpdate(ToolFormUpdateType type) { }

		public void UpdateValues()
		{
		}

		public void FastUpdate()
		{
		}

		public void Restart()
		{
		}

		public bool AskSaveChanges() => true;

		public bool UpdateBefore => true;

		#endregion

		private void CancelBtn_Click(object sender, EventArgs e)
		{
			DialogResult = DialogResult.Cancel;
			Close();
		}

		private void SaveRunButton_Click(object sender, EventArgs e)
		{
			if (Recalculate())
			{
				var fileInfo = new FileInfo(NameBox.Text);
				if (fileInfo.Exists)
				{
					var result = MessageBox.Show(this, "File already exists, overwrite?", "File exists", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
					if (result != DialogResult.OK)
					{
						return;
					}
				}

				File.WriteAllText(fileInfo.FullName, _currentXml.ToString());

				DialogResult = DialogResult.OK;
				Close();

				var lra = new MainForm.LoadRomArgs { OpenAdvanced = new OpenAdvanced_OpenRom { Path = fileInfo.FullName } };
				MainForm.LoadRom(fileInfo.FullName, lra);
			}
		}

		private void AddButton_Click(object sender, EventArgs e)
		{
			int start = 3 + (FileSelectorPanel.Controls.Count * 43);

			var groupBox = new GroupBox
			{
				Text = "",
				Location = UIHelper.Scale(new Point(6, start)),
				Size = new Size(FileSelectorPanel.ClientSize.Width - UIHelper.ScaleX(12), UIHelper.ScaleY(41)),
				Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
			};

			var mdf = new MultiDiskFileSelector(this)
			{
				Location = UIHelper.Scale(new Point(7, 12)),
				Width = groupBox.ClientSize.Width - UIHelper.ScaleX(13),
				Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
			};

			mdf.NameChanged += FileSelector_NameChanged;
			mdf.SystemString = SystemDropDown.SelectedText;


			groupBox.Controls.Add(mdf);

			FileSelectorPanel.Controls.Add(groupBox);
		}

		private void btnRemove_Click(object sender, EventArgs e)
		{
			//ToDo:
			//Make this better?
			//We need to have i at 1 and not zero because Controls Count doesn't start at zero (sort of)
			Int32 i = 1;
			//For Each Control box we have, loop
			foreach (Control ctrl in FileSelectorPanel.Controls)
			{
				//if we are at the last box, then remove it.
				if ((i == FileSelectorPanel.Controls.Count))
				{
					ctrl.Dispose();
				}
				//One to our looper
				i++;
			}
		}

		private void FileSelector_NameChanged(object sender, EventArgs e)
		{
			Recalculate();
		}

		private IEnumerable<MultiDiskFileSelector> FileSelectors =>
			FileSelectorPanel.Controls
				.OfType<GroupBox>()
				.SelectMany(g => g.Controls.OfType<MultiDiskFileSelector>());

		private bool Recalculate()
		{
			try
			{
				var names = FileSelectors.Select(f => f.Path).ToList();

				var name = NameBox.Text;

				if (string.IsNullOrWhiteSpace(name))
				{
					throw new Exception("Xml Filename can not be blank");
				}

				if (names.Any(string.IsNullOrWhiteSpace))
				{
					throw new Exception("Rom Names can not be blank");
				}

				var system = SystemDropDown.SelectedItem?.ToString();

				if (string.IsNullOrWhiteSpace(system))
				{
					throw new Exception("System Id can not be blank");
				}

				var basePath = Path.GetDirectoryName(name.Split('|').First());

				if (string.IsNullOrEmpty(basePath))
				{
					var fileInfo = new FileInfo(name);
					basePath = Path.GetDirectoryName(fileInfo.FullName);
				}

				_currentXml = new XElement("BizHawk-XMLGame",
					new XAttribute("System", system),
					new XAttribute("Name", Path.GetFileNameWithoutExtension(name)),
					new XElement("LoadAssets",
						names.Select(n => new XElement(
							"Asset",
							new XAttribute("FileName", GetRelativePath(basePath, n))
						))
					) 
				);

				SaveRunButton.Enabled = true;
				return true;
			}
			catch (Exception)
			{
				_currentXml = null;
				SaveRunButton.Enabled = false;
				return false;
			}
		}

		private void NameBox_TextChanged(object sender, EventArgs e)
		{
			Recalculate();
		}

		private void BrowseBtn_Click(object sender, EventArgs e)
		{
			string filename = "";
			string initialDirectory = PathManager.MakeAbsolutePath(Config.PathEntries.MultiDiskBundlesFragment, "Global_NULL");

			if (!Global.Game.IsNullInstance())
			{
				filename = NameBox.Text;
				if (string.IsNullOrWhiteSpace(filename))
				{
					filename = Path.ChangeExtension(PathManager.FilesystemSafeName(Global.Game), ".xml");
				}

				initialDirectory = Path.GetDirectoryName(filename);
			}

			using var sfd = new SaveFileDialog
			{
				FileName = filename,
				InitialDirectory = initialDirectory,
				Filter = "xml (*.xml)|*.xml|All Files|*.*"
			};

			var result = sfd.ShowHawkDialog();
			if (result != DialogResult.Cancel)
			{
				NameBox.Text = sfd.FileName;
			}
		}

		/// <exception cref="ArgumentException">running on Windows host, and unmanaged call failed</exception>
		/// <exception cref="FileNotFoundException">running on Windows host, and either path is not a regular file or directory</exception>
		/// <remarks>Algorithm for Windows taken from https://stackoverflow.com/a/485516/7467292</remarks>
		public static string GetRelativePath(string fromPath, string toPath)
		{
			if (OSTailoredCode.IsUnixHost)
			{
#if true
				return PathManager.IsSubfolder(toPath, fromPath)
					? "./" + OSTailoredCode.SimpleSubshell("realpath", $"--relative-to=\"{toPath}\" \"{fromPath}\"", $"invalid path {fromPath} or missing realpath binary")
					: fromPath;
#else // written for Unix port but may be useful for .NET Core
				// algorithm taken from https://stackoverflow.com/a/340454/7467292
				var dirSepChar = Path.DirectorySeparatorChar;
				var fromUri = new Uri(fromPath.EndsWith(dirSepChar.ToString()) ? fromPath : fromPath + dirSepChar);
				var toUri = new Uri(toPath.EndsWith(dirSepChar.ToString()) ? toPath : toPath + dirSepChar);
				if (fromUri.Scheme != toUri.Scheme) return toPath;

				var relativePath = Uri.UnescapeDataString(fromUri.MakeRelativeUri(toUri).ToString());
				return (toUri.Scheme.Equals(Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase)
					? relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
					: relativePath
				).TrimEnd(dirSepChar);
#endif
			}

			static FileAttributes GetPathAttribute(string path1)
			{
				var di = new DirectoryInfo(path1.Split('|').First());
				if (di.Exists)
				{
					return FileAttributes.Directory;
				}

				var fi = new FileInfo(path1.Split('|').First());
				if (fi.Exists)
				{
					return FileAttributes.Normal;
				}

				throw new FileNotFoundException();
			}
			var path = new StringBuilder(260 /* = MAX_PATH */);
			return Win32Imports.PathRelativePathTo(path, fromPath, GetPathAttribute(fromPath), toPath, GetPathAttribute(toPath))
				? path.ToString()
				: throw new ArgumentException("Paths must have a common prefix");
		}

		private void SystemDropDown_SelectedIndexChanged(object sender, EventArgs e)
		{
			Recalculate();
			do
			{
				foreach (Control ctrl in FileSelectorPanel.Controls)
				{
					ctrl.Dispose();
				}
			} while (FileSelectorPanel.Controls.Count != 0);

			if (SystemDropDown.SelectedItem.ToString() == "GB")
			{
				AddButton.Enabled = false;
				btnRemove.Enabled = false;
			}
			else
			{
				AddButton.Enabled = true;
				btnRemove.Enabled = true;
			}
			AddButton_Click(null, null);
			AddButton_Click(null, null);
		}
	}
}
