﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

using BizHawk.Client.Common;
using BizHawk.Emulation.Common;

namespace BizHawk.Client.EmuHawk
{
	public partial class ToolBox : ToolFormBase, IToolForm
	{
		[RequiredService]
		private IEmulator Emulator { get; set; }

		public ToolBox()
		{
			InitializeComponent();
		}

		private void ToolBox_Load(object sender, EventArgs e)
		{
			Location = new Point(
				Owner.Location.X + Owner.Size.Width,
				Owner.Location.Y
			);
		}

		public void NewUpdate(ToolFormUpdateType type) { }

		public bool AskSaveChanges() => true;
		public bool UpdateBefore => false;
		public void UpdateValues() { }

		public void FastUpdate()
		{
			// Do nothing
		}

		public void Restart()
		{
			SetTools();
			SetSize();

			ToolBoxStrip.Select();
			ToolBoxItems.First().Select();
		}

		private void SetTools()
		{
			ToolBoxStrip.Items.Clear();

			foreach (var t in Assembly.GetAssembly(GetType()).GetTypes())
			{
				if (!typeof(IToolForm).IsAssignableFrom(t))
					continue;
				if (!typeof(Form).IsAssignableFrom(t))
					continue;
				if (typeof(ToolBox).IsAssignableFrom(t))  //yo dawg i head you like toolboxes
					continue;
				if (VersionInfo.DeveloperBuild && t.GetCustomAttributes(false).OfType<ToolAttribute>().Any(a => !a.Released))
					continue;
				if (t == typeof(GBGameGenie)) // Hack, this tool is specific to a system id and a sub-system (gb and gg) we have no reasonable way to declare a dependency like that
					continue;
				if (!ServiceInjector.IsAvailable(Emulator.ServiceProvider, t))
					continue;
//				if (!ApiInjector.IsAvailable(, t))
//					continue;

				var instance = Activator.CreateInstance(t);

				var tsb = new ToolStripButton
				{
					Image = ((Form) instance).Icon.ToBitmap(),
					Text = ((Form) instance).Text,
					DisplayStyle = ((Form) instance).ShowIcon ? ToolStripItemDisplayStyle.Image : ToolStripItemDisplayStyle.Text
				};

				tsb.Click += (o, e) =>
				{
					Tools.Load(t);
					Close();
				};

				ToolBoxStrip.Items.Add(tsb);
			}
		}

		private void SetSize()
		{
			var rows = (int)Math.Ceiling(ToolBoxItems.Count() / 4.0);
			Height = 30 + (rows * 30);
		}

		// Provide LINQ capabilities to an outdated form collection
		private IEnumerable<ToolStripItem> ToolBoxItems => ToolBoxStrip.Items.Cast<ToolStripItem>();

		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			if (keyData == Keys.Escape)
			{
				Close();
				return true;
			}

			return base.ProcessCmdKey(ref msg, keyData);
		}
	}
}
