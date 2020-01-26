﻿using System;
using System.Windows.Forms;

using BizHawk.Emulation.Cores.Computers.AmstradCPC;

namespace BizHawk.Client.EmuHawk
{
	public partial class AmstradCpcPokeMemory : Form
	{
		private readonly MainForm _mainForm;
		private readonly AmstradCPC _cpc;

		public AmstradCpcPokeMemory(MainForm mainForm, AmstradCPC cpc)
		{
			_mainForm = mainForm;
			_cpc = cpc;
			InitializeComponent();
		}

		private void OkBtn_Click(object sender, EventArgs e)
		{
			var addr = (ushort)numericUpDownAddress.Value;
			var val = (byte)numericUpDownByte.Value;

			_cpc.PokeMemory(addr, val);

			DialogResult = DialogResult.OK;
			Close();
		}

		private void CancelBtn_Click(object sender, EventArgs e)
		{
			_mainForm.AddOnScreenMessage("POKE memory aborted");
			DialogResult = DialogResult.Cancel;
			Close();
		}
	}
}
