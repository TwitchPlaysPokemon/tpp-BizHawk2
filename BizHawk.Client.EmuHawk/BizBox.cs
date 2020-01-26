﻿using BizHawk.Emulation.Common;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace BizHawk.Client.EmuHawk
{
	public partial class BizBox : Form
	{
		public BizBox()
		{
			InitializeComponent();
		}

		private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			linkLabel1.LinkVisited = true;
			System.Diagnostics.Process.Start("http://tasvideos.org/Bizhawk.html");
		}

		private void OK_Click(object sender, EventArgs e)
		{
			Close();
		}

		private void BizBox_Load(object sender, EventArgs e)
		{
			string mainVersion = VersionInfo.Mainversion;
			if (IntPtr.Size == 8)
			{
				mainVersion += " (x64)";
			}

			DeveloperBuildLabel.Visible = VersionInfo.DeveloperBuild;

			Text = VersionInfo.DeveloperBuild
				? $" BizHawk  (GIT {SubWCRev.GIT_BRANCH}#{SubWCRev.GIT_SHORTHASH})"
				: $"Version {mainVersion} (GIT {SubWCRev.GIT_BRANCH}#{SubWCRev.GIT_SHORTHASH})";

			VersionLabel.Text = $"Version {mainVersion}";
			DateLabel.Text = VersionInfo.RELEASEDATE;

			var cores = Assembly
				.Load("BizHawk.Emulation.Cores")
				.GetTypes()
				.Where(t => typeof(IEmulator).IsAssignableFrom(t))
				.Select(t => t.GetCustomAttributes(false).OfType<CoreAttribute>().FirstOrDefault())
				.Where(a => a != null)
				.Where(a => a.Released)
				.OrderByDescending(a => a.CoreName.ToLower());

			foreach (var core in cores)
			{
				CoreInfoPanel.Controls.Add(new BizBoxInfoControl(core)
				{
					Dock = DockStyle.Top
				});
			}

			linkLabel2.Text = $"Commit # {SubWCRev.GIT_SHORTHASH}";
		}

		private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			Process.Start($"https://github.com/TASVideos/BizHawk/commit/{SubWCRev.GIT_SHORTHASH}");
		}

		private void btnCopyHash_Click(object sender, EventArgs e)
		{
			Clipboard.SetText(SubWCRev.GIT_SHORTHASH);
		}

		private void linkLabel3_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			Process.Start("https://github.com/TASVideos/BizHawk/graphs/contributors");
		}
	}
}
