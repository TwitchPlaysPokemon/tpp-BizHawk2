﻿using System;
using BizHawk.Emulation.Common;

namespace BizHawk.Emulation.Cores.Computers.AmstradCPC
{
	/// <summary>
	/// CPCHawk: Core Class
	/// * IInputPollable *
	/// </summary>
	public partial class AmstradCPC : IInputPollable
	{
		public int LagCount
		{
			get => _lagCount;
			set => _lagCount = value;
		}

		public bool IsLagFrame
		{
			get => _isLag;
			set => _isLag = value;
		}

		public IInputCallbackSystem InputCallbacks { get; }

		private int _lagCount = 0;
		private bool _isLag = false;
	}
}
