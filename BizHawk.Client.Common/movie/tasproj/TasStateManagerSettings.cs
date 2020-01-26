﻿using System.ComponentModel;
using Newtonsoft.Json;

namespace BizHawk.Client.Common
{
	public class TasStateManagerSettings
	{
		public TasStateManagerSettings()
		{
			DiskSaveCapacityMb = 512;
			CapacityMb = 512;
			DiskCapacityMb = 1; // not working yet
			MemStateGapDivider = 64;
			FileStateGap = 4;
		}

		public TasStateManagerSettings(TasStateManagerSettings settings)
		{
			DiskSaveCapacityMb = settings.DiskSaveCapacityMb;
			CapacityMb = settings.CapacityMb;
			DiskCapacityMb = settings.DiskCapacityMb;
			MemStateGapDivider = settings.MemStateGapDivider;
			FileStateGap = settings.FileStateGap;
		}

		/// <summary>
		/// Whether or not to save state history information to disk
		/// </summary>
		[DisplayName("Save History")]
		[Description("Whether or not to use savestate history")]
		public bool SaveStateHistory => DiskSaveCapacityMb != 0;

		/// <summary>
		/// Gets or sets the size limit to use when saving the TAS project to disk.
		/// </summary>
		[DisplayName("Save Capacity (in megabytes)")]
		[Description("The size limit to use when saving the tas project to disk.")]
		public int DiskSaveCapacityMb { get; set; }

		/// <summary>
		/// Gets or sets the total amount of memory to devote to state history in megabytes
		/// </summary>
		[DisplayName("Capacity (in megabytes)")]
		[Description("The size limit of the state history buffer.  When this limit is reached it will start moving to disk.")]
		public int CapacityMb { get; set; }

		/// <summary>
		/// Gets or sets the total amount of disk space to devote to state history in megabytes
		/// </summary>
		[DisplayName("Disk Capacity (in megabytes)")]
		[Description("The size limit of the state history buffer on the disk.  When this limit is reached it will start removing previous savestates")]
		public int DiskCapacityMb { get; set; }

		/// <summary>
		/// Gets or sets the divider that determines memory state gap
		/// </summary>
		[DisplayName("Divider for memory state interval")]
		[Description("The actual state gap in frames is calculated as ExpectedStateSize / div / 1024")]
		public int MemStateGapDivider { get; set; }

		/// <summary>
		/// Gets or sets the amount of states to skip during project saving
		/// </summary>
		[DisplayName("State interval for .tasproj")]
		[Description("The actual state gap in frames is calculated as Nth power on 2")]
		public int FileStateGap { get; set; }

		/// <summary>
		/// The memory state capacity in bytes.
		/// </summary>
		[JsonIgnore]
		[Browsable(false)]
		public ulong Cap => (ulong)CapacityMb * 1024UL * 1024UL;
	}
}
