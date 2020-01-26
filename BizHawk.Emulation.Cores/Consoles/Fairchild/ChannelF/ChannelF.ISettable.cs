﻿using System;
using System.ComponentModel;
using BizHawk.Common;
using BizHawk.Common.BufferExtensions;
using BizHawk.Emulation.Common;
using BizHawk.Emulation.Cores.Nintendo.NES;

namespace BizHawk.Emulation.Cores.Consoles.ChannelF
{
	public partial class ChannelF : ISettable<ChannelF.ChannelFSettings, ChannelF.ChannelFSyncSettings>
	{
		internal ChannelFSettings Settings = new ChannelFSettings();
		internal ChannelFSyncSettings SyncSettings = new ChannelFSyncSettings();

		public ChannelFSettings GetSettings()
		{
			return Settings.Clone();
		}

		public ChannelFSyncSettings GetSyncSettings()
		{
			return SyncSettings.Clone();
		}

		public bool PutSettings(ChannelFSettings o)
		{
			Settings = o;
			return false;
		}

		public bool PutSyncSettings(ChannelFSyncSettings o)
		{
			bool ret = ChannelFSyncSettings.NeedsReboot(SyncSettings, o);
			SyncSettings = o;
			return ret;
		}

		public class ChannelFSettings
		{
			[DisplayName("Default Background Color")]
			[Description("The default BG color")]
			[DefaultValue(0)]
			public int BackgroundColor { get; set; }

			public ChannelFSettings Clone()
			{
				return (ChannelFSettings)MemberwiseClone();
			}

			public ChannelFSettings()
			{
				SettingsUtil.SetDefaultValues(this);
			}
		}

		public class ChannelFSyncSettings
		{
			[DisplayName("Deterministic Emulation")]
			[Description("If true, the core agrees to behave in a completely deterministic manner")]
			[DefaultValue(true)]
			public bool DeterministicEmulation { get; set; }

			public ChannelFSyncSettings Clone()
			{
				return (ChannelFSyncSettings) MemberwiseClone();
			}

			public ChannelFSyncSettings()
			{
				SettingsUtil.SetDefaultValues(this);
			}

			public static bool NeedsReboot(ChannelFSyncSettings x, ChannelFSyncSettings y)
			{
				return !DeepEquality.DeepEquals(x, y);
			}
		}
	}
}
