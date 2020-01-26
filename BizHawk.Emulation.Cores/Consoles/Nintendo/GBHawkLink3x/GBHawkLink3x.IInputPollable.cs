﻿using BizHawk.Emulation.Common;

namespace BizHawk.Emulation.Cores.Nintendo.GBHawkLink3x
{
	public partial class GBHawkLink3x : IInputPollable
	{
		public int LagCount
		{
			get => _lagcount;
			set => _lagcount = value;
		}

		public bool IsLagFrame
		{
			get => _islag;
			set => _islag = value;
		}

		public IInputCallbackSystem InputCallbacks { get; } = new InputCallbackSystem();

		public bool _islag = true;
		private int _lagcount;
	}
}
