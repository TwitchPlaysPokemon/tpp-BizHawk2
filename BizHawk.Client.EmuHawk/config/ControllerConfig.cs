﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using BizHawk.Common;
using BizHawk.Client.Common;
using BizHawk.Emulation.Common;
using BizHawk.Client.EmuHawk.WinFormExtensions;

namespace BizHawk.Client.EmuHawk
{
	public partial class ControllerConfig : Form
	{
		private const int MaxPlayers = 12;
		private static readonly Dictionary<string, Lazy<Bitmap>> ControllerImages = new Dictionary<string, Lazy<Bitmap>>();
		private readonly IEmulator _emulator;
		private readonly Config _config;

		static ControllerConfig()
		{
			ControllerImages.Add("NES Controller", Properties.Resources.NES_Controller);
			ControllerImages.Add("SNES Controller", Properties.Resources.SNES_Controller);
			ControllerImages.Add("Nintendo 64 Controller", Properties.Resources.N64);
			ControllerImages.Add("Gameboy Controller", Properties.Resources.GBController);
			ControllerImages.Add("Gameboy Controller H", Properties.Resources.GBController);
			ControllerImages.Add("Gameboy Controller + Tilt", Properties.Resources.GBController);
			ControllerImages.Add("GBA Controller", Properties.Resources.GBA_Controller);
			ControllerImages.Add("Dual Gameboy Controller", Properties.Resources.GBController);

			ControllerImages.Add("SMS Controller", Properties.Resources.SMSController);
			ControllerImages.Add("GPGX Genesis Controller", Properties.Resources.GENController);
			ControllerImages.Add("Saturn Controller", Properties.Resources.SaturnController);

			ControllerImages.Add("Intellivision Controller", Properties.Resources.IntVController);
			ControllerImages.Add("ColecoVision Basic Controller", Properties.Resources.colecovisioncontroller);
			ControllerImages.Add("Atari 2600 Basic Controller", Properties.Resources.atari_controller);
			ControllerImages.Add("Atari 7800 ProLine Joystick Controller", Properties.Resources.A78Joystick);

			ControllerImages.Add("PC Engine Controller", Properties.Resources.PCEngineController);
			ControllerImages.Add("Commodore 64 Controller", Properties.Resources.C64Joystick);
			ControllerImages.Add("TI83 Controller", Properties.Resources.TI83_Controller);

			ControllerImages.Add("WonderSwan Controller", Properties.Resources.WonderSwanColor);
			ControllerImages.Add("Lynx Controller", Properties.Resources.Lynx);
			ControllerImages.Add("PSX Gamepad Controller", Properties.Resources.PSX_Original_Controller);
			ControllerImages.Add("PSX DualShock Controller", Properties.Resources.psx_dualshock);
			ControllerImages.Add("Apple IIe Keyboard", Properties.Resources.AppleIIKeyboard);
			ControllerImages.Add("VirtualBoy Controller", Properties.Resources.VBoyController);
			ControllerImages.Add("NeoGeo Portable Controller", Properties.Resources.NGPController);
			ControllerImages.Add("MAME Controller", Properties.Resources.ArcadeController);
		}

		protected override void OnActivated(EventArgs e)
		{
			base.OnActivated(e);
			Input.Instance.ControlInputFocus(this, Input.InputFocus.Mouse, true);
		}

		protected override void OnDeactivate(EventArgs e)
		{
			base.OnDeactivate(e);
			Input.Instance.ControlInputFocus(this, Input.InputFocus.Mouse, false);
		}

		private void ControllerConfig_Load(object sender, EventArgs e)
		{
			Text = $"{_emulator.ControllerDefinition.Name} Configuration";
		}

		private void ControllerConfig_FormClosed(object sender, FormClosedEventArgs e)
		{
			Input.Instance.ClearEvents();
		}

		private delegate Control PanelCreator<T>(Dictionary<string, T> settings, List<string> buttons, Size size);

		private Control CreateNormalPanel(Dictionary<string, string> settings, List<string> buttons, Size size)
		{
			var cp = new ControllerConfigPanel { Dock = DockStyle.Fill, AutoScroll = true, Tooltip = toolTip1 };
			cp.LoadSettings(settings, checkBoxAutoTab.Checked, buttons, size.Width, size.Height);
			return cp;
		}

		private static Control CreateAnalogPanel(Dictionary<string, AnalogBind> settings, List<string> buttons, Size size)
		{
			return new AnalogBindPanel(settings, buttons) { Dock = DockStyle.Fill, AutoScroll = true };
		}

		private void LoadToPanel<T>(Control dest, string controllerName, IList<string> controllerButtons, Dictionary<string,string> categoryLabels, IDictionary<string, Dictionary<string, T>> settingsBlock, T defaultValue, PanelCreator<T> createPanel)
		{
			if (!settingsBlock.TryGetValue(controllerName, out var settings))
			{
				settings = new Dictionary<string, T>();
				settingsBlock[controllerName] = settings;
			}

			// check to make sure that the settings object has all of the appropriate bool buttons
			foreach (var button in controllerButtons)
			{
				if (!settings.Keys.Contains(button))
				{
					settings[button] = defaultValue;
				}
			}

			if (controllerButtons.Count == 0)
			{
				return;
			}

			// split the list of all settings into buckets by player number
			var buckets = new List<string>[MaxPlayers + 1];
			var categoryBuckets = new WorkingDictionary<string, List<string>>();
			for (var i = 0; i < buckets.Length; i++)
			{
				buckets[i] = new List<string>();
			}

			// by iterating through only the controller's active buttons, we're silently
			// discarding anything that's not on the controller right now.  due to the way
			// saving works, those entries will still be preserved in the config file, tho
			foreach (var button in controllerButtons)
			{
				int i;
				for (i = MaxPlayers; i > 0; i--)
				{
					if (button.StartsWith($"P{i}"))
					{
						break;
					}
				}

				if (i > MaxPlayers) // couldn't find
				{
					i = 0;
				}

				if (categoryLabels.ContainsKey(button))
				{
					categoryBuckets[categoryLabels[button]].Add(button);
				}
				else
				{
					buckets[i].Add(button);
				}
			}

			if (buckets[0].Count == controllerButtons.Count)
			{
				// everything went into bucket 0, so make no tabs at all
				dest.Controls.Add(createPanel(settings, controllerButtons.ToList(), dest.Size));
			}
			else
			{
				// create multiple player tabs
				var tt = new TabControl { Dock = DockStyle.Fill };
				dest.Controls.Add(tt);
				int pageIdx = 0;
				for (int i = 1; i <= MaxPlayers; i++)
				{
					if (buckets[i].Count > 0)
					{
						string tabName = _emulator.SystemId != "WSWAN" ? $"Player {i}" : i == 1 ? "Normal" : "Rotated"; // hack
						tt.TabPages.Add(tabName);
						tt.TabPages[pageIdx].Controls.Add(createPanel(settings, buckets[i], tt.Size));
						pageIdx++;
					}
				}

				foreach (var cat in categoryBuckets)
				{
					string tabName = cat.Key;
					tt.TabPages.Add(tabName);
					tt.TabPages[pageIdx].Controls.Add(createPanel(settings, cat.Value, tt.Size));

					// ZxHawk hack - it uses multiple categoryLabels
					if (_emulator.SystemId == "ZXSpectrum"
						|| _emulator.SystemId == "AmstradCPC"
						|| _emulator.SystemId == "ChannelF")
					{
						pageIdx++;
					}
				}

				if (buckets[0].Count > 0)
				{
					// ZXHawk needs to skip this bit
					if (_emulator.SystemId == "ZXSpectrum" || _emulator.SystemId == "AmstradCPC" || _emulator.SystemId == "ChannelF")
						return;

					string tabName =
						(_emulator.SystemId == "C64") ? "Keyboard" :
						(_emulator.SystemId == "MAME") ? "Misc" :
						"Console"; // hack
					tt.TabPages.Add(tabName);
					tt.TabPages[pageIdx].Controls.Add(createPanel(settings, buckets[0], tt.Size));
				}
			}
		}

		public ControllerConfig(
			IEmulator emulator,
			Config config)
		{
			_emulator = emulator;
			_config = config;
			
			InitializeComponent();

			SuspendLayout();
			LoadPanels(_config);

			rbUDLRAllow.Checked = _config.AllowUdlr;
			rbUDLRForbid.Checked = _config.ForbidUdlr;
			rbUDLRPriority.Checked = !_config.AllowUdlr && !_config.ForbidUdlr;
			checkBoxAutoTab.Checked = _config.InputConfigAutoTab;

			SetControllerPicture(_emulator.ControllerDefinition.Name);
			ResumeLayout();
		}

		private void LoadPanels(
			IDictionary<string, Dictionary<string, string>> normal,
			IDictionary<string, Dictionary<string, string>> autofire,
			IDictionary<string, Dictionary<string, AnalogBind>> analog)
		{
			LoadToPanel(NormalControlsTab, _emulator.ControllerDefinition.Name, _emulator.ControllerDefinition.BoolButtons, _emulator.ControllerDefinition.CategoryLabels, normal, "", CreateNormalPanel);
			LoadToPanel(AutofireControlsTab, _emulator.ControllerDefinition.Name, _emulator.ControllerDefinition.BoolButtons, _emulator.ControllerDefinition.CategoryLabels, autofire, "", CreateNormalPanel);
			LoadToPanel(AnalogControlsTab, _emulator.ControllerDefinition.Name, _emulator.ControllerDefinition.FloatControls, _emulator.ControllerDefinition.CategoryLabels, analog, new AnalogBind("", 1.0f, 0.1f), CreateAnalogPanel);

			if (AnalogControlsTab.Controls.Count == 0)
			{
				tabControl1.TabPages.Remove(AnalogControlsTab);
			}
		}

		private void LoadPanels(DefaultControls cd)
		{
			LoadPanels(cd.AllTrollers, cd.AllTrollersAutoFire, cd.AllTrollersAnalog);
		}

		private void LoadPanels(Config c)
		{
			LoadPanels(c.AllTrollers, c.AllTrollersAutoFire, c.AllTrollersAnalog);
		}

		private void SetControllerPicture(string controlName)
		{
			ControllerImages.TryGetValue(controlName, out var lazyBmp);
			var bmp = lazyBmp?.Value ?? Properties.Resources.Help;
			pictureBox1.Image = bmp;
			pictureBox1.Size = bmp.Size;
			tableLayoutPanel1.ColumnStyles[1].Width = bmp.Width;

			// Uberhack
			if (controlName == "Commodore 64 Controller")
			{
				var pictureBox2 = new PictureBox
					{
						Image = Properties.Resources.C64Keyboard.Value,
						Size = Properties.Resources.C64Keyboard.Value.Size
					};
				tableLayoutPanel1.ColumnStyles[1].Width = Properties.Resources.C64Keyboard.Value.Width;
				pictureBox1.Height /= 2;
				pictureBox1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
				pictureBox1.Dock = DockStyle.Top;
				pictureBox2.Location = new Point(pictureBox1.Location.X, pictureBox1.Location.Y + pictureBox1.Size.Height + 10);
				tableLayoutPanel1.Controls.Add(pictureBox2, 1, 0);

				pictureBox2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
			}

			if (controlName == "ZXSpectrum Controller")
			{
				pictureBox1.Image = Properties.Resources.ZXSpectrumKeyboards.Value;
				pictureBox1.Size = Properties.Resources.ZXSpectrumKeyboards.Value.Size;
				tableLayoutPanel1.ColumnStyles[1].Width = Properties.Resources.ZXSpectrumKeyboards.Value.Width;
			}

			if (controlName == "ChannelF Controller")
			{

			}

			if (controlName == "AmstradCPC Controller")
			{
#if false
				pictureBox1.Image = Properties.Resources.ZXSpectrumKeyboards.Value;
				pictureBox1.Size = Properties.Resources.ZXSpectrumKeyboards.Value.Size;
				tableLayoutPanel1.ColumnStyles[1].Width = Properties.Resources.ZXSpectrumKeyboards.Value.Width;
#endif
			}
		}

		// lazy methods, but they're not called often and actually
		// tracking all of the ControllerConfigPanels wouldn't be simpler
		private static void SetAutoTab(Control c, bool value)
		{
			if (c is ControllerConfigPanel panel)
			{
				panel.SetAutoTab(value);
			}
			else if (c is AnalogBindPanel)
			{
				// TODO
			}
			else if (c.HasChildren)
			{
				foreach (Control cc in c.Controls)
				{
					SetAutoTab(cc, value);
				}
			}
		}

		private void Save()
		{
			ActOnControlCollection<ControllerConfigPanel>(NormalControlsTab, c => c.Save(_config.AllTrollers[_emulator.ControllerDefinition.Name]));
			ActOnControlCollection<ControllerConfigPanel>(AutofireControlsTab, c => c.Save(_config.AllTrollersAutoFire[_emulator.ControllerDefinition.Name]));
			ActOnControlCollection<AnalogBindPanel>(AnalogControlsTab, c => c.Save(_config.AllTrollersAnalog[_emulator.ControllerDefinition.Name]));
		}

		private void SaveToDefaults(DefaultControls cd)
		{
			ActOnControlCollection<ControllerConfigPanel>(NormalControlsTab, c => c.Save(cd.AllTrollers[_emulator.ControllerDefinition.Name]));
			ActOnControlCollection<ControllerConfigPanel>(AutofireControlsTab, c => c.Save(cd.AllTrollersAutoFire[_emulator.ControllerDefinition.Name]));
			ActOnControlCollection<AnalogBindPanel>(AnalogControlsTab, c => c.Save(cd.AllTrollersAnalog[_emulator.ControllerDefinition.Name]));
		}

		private static void ActOnControlCollection<T>(Control c, Action<T> proc)
			where T : Control
		{
			if (c is T control)
			{
				proc(control);
			}
			else if (c.HasChildren)
			{
				foreach (Control cc in c.Controls)
				{
					ActOnControlCollection(cc, proc);
				}
			}
		}

		private void CheckBoxAutoTab_CheckedChanged(object sender, EventArgs e)
		{
			SetAutoTab(this, checkBoxAutoTab.Checked);
		}

		private void ButtonOk_Click(object sender, EventArgs e)
		{
			_config.AllowUdlr = rbUDLRAllow.Checked;
			_config.ForbidUdlr = rbUDLRForbid.Checked;
			_config.InputConfigAutoTab = checkBoxAutoTab.Checked;

			Save();

			DialogResult = DialogResult.OK;
			Close();
		}

		private void ButtonCancel_Click(object sender, EventArgs e)
		{
			Close();
		}

		private static TabControl GetTabControl(IEnumerable controls)
		{
			return controls?.OfType<TabControl>()
				.Select(c => c)
				.FirstOrDefault();
		}

		private void ButtonLoadDefaults_Click(object sender, EventArgs e)
		{
			tabControl1.SuspendLayout();

			var wasTabbedMain = tabControl1.SelectedTab.Name;
			var tb1 = GetTabControl(NormalControlsTab.Controls);
			var tb2 = GetTabControl(AutofireControlsTab.Controls);
			var tb3 = GetTabControl(AnalogControlsTab.Controls);
			int? wasTabbedPage1 = null;
			int? wasTabbedPage2 = null;
			int? wasTabbedPage3 = null;

			if (tb1?.SelectedTab != null) { wasTabbedPage1 = tb1.SelectedIndex; }
			if (tb2?.SelectedTab != null) { wasTabbedPage2 = tb2.SelectedIndex; }
			if (tb3?.SelectedTab != null) { wasTabbedPage3 = tb3.SelectedIndex; }

			NormalControlsTab.Controls.Clear();
			AutofireControlsTab.Controls.Clear();
			AnalogControlsTab.Controls.Clear();

			// load panels directly from the default config.
			// this means that the changes are NOT committed.  so "Cancel" works right and you
			// still have to hit OK at the end.
			var cd = ConfigService.Load<DefaultControls>(Config.ControlDefaultPath);
			LoadPanels(cd);

			tabControl1.SelectTab(wasTabbedMain);

			if (wasTabbedPage1.HasValue)
			{
				var newTb1 = GetTabControl(NormalControlsTab.Controls);
				newTb1?.SelectTab(wasTabbedPage1.Value);
			}

			if (wasTabbedPage2.HasValue)
			{
				var newTb2 = GetTabControl(AutofireControlsTab.Controls);
				newTb2?.SelectTab(wasTabbedPage2.Value);
			}

			if (wasTabbedPage3.HasValue)
			{
				var newTb3 = GetTabControl(AnalogControlsTab.Controls);
				newTb3?.SelectTab(wasTabbedPage3.Value);
			}

			tabControl1.ResumeLayout();
		}

		private void ButtonSaveDefaults_Click(object sender, EventArgs e)
		{
			// this doesn't work anymore, as it stomps out any defaults for buttons that aren't currently active on the console
			// there are various ways to fix it, each with its own semantic problems
			var result = MessageBox.Show(this, "OK to overwrite defaults for current control scheme?", "Save Defaults", MessageBoxButtons.YesNo);
			if (result == DialogResult.Yes)
			{
				var cd = ConfigService.Load<DefaultControls>(Config.ControlDefaultPath);
				cd.AllTrollers[_emulator.ControllerDefinition.Name] = new Dictionary<string, string>();
				cd.AllTrollersAutoFire[_emulator.ControllerDefinition.Name] = new Dictionary<string, string>();
				cd.AllTrollersAnalog[_emulator.ControllerDefinition.Name] = new Dictionary<string, AnalogBind>();

				SaveToDefaults(cd);

				ConfigService.Save(Config.ControlDefaultPath, cd);
			}
		}

		private void ClearWidgetAndChildren(Control c)
		{
			if (c is InputCompositeWidget widget)
			{
				widget.Clear();
			}

			if (c is InputWidget inputWidget)
			{
				inputWidget.ClearAll();
			}

			if (c is AnalogBindControl control)
			{
				control.Unbind_Click(null, null);
			}

			if (c.Controls().Any())
			{
				foreach (Control child in c.Controls())
				{
					ClearWidgetAndChildren(child);
				}
			}
		}

		private void ClearBtn_Click(object sender, EventArgs e)
		{
			foreach (var c in this.Controls())
			{
				ClearWidgetAndChildren(c);
			}
		}
	}
}
