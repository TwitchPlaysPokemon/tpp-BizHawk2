﻿using BizHawk.Emulation.Common;
using BizHawk.Emulation.Cores.Sega.MasterSystem;

namespace BizHawk.Emulation.Cores.Sega.GGHawkLink
{
	[Core(
		"GGHawkLink",
		"",
		isPorted: false,
		isReleased: false)]
	[ServiceNotApplicable(typeof(IDriveLight))]
	public partial class GGHawkLink : IEmulator, ISaveRam, IDebuggable, IStatable, IInputPollable, IRegionable, ILinkable,
	ISettable<GGHawkLink.GGLinkSettings, GGHawkLink.GGLinkSyncSettings>
	{
		// we want to create two GG instances that we will run concurrently
		public SMS L;
		public SMS R;

		// if true, the link cable is currently connected
		private bool _cableconnected = true;

		// if true, the link cable toggle signal is currently asserted
		private bool _cablediscosignal = false;

		private bool do_r_next = false;

		public GGHawkLink(CoreComm comm, GameInfo game_L, byte[] rom_L, GameInfo game_R, byte[] rom_R, /*string gameDbFn,*/ object settings, object syncSettings)
		{
			var ser = new BasicServiceProvider(this);

			linkSettings = (GGLinkSettings)settings ?? new GGLinkSettings();
			linkSyncSettings = (GGLinkSyncSettings)syncSettings ?? new GGLinkSyncSettings();
			_controllerDeck = new GGHawkLinkControllerDeck(GGHawkLinkControllerDeck.DefaultControllerName, GGHawkLinkControllerDeck.DefaultControllerName);

			CoreComm = comm;

			var temp_set_L = new SMS.SMSSettings();
			var temp_set_R = new SMS.SMSSettings();

			var temp_sync_L = new SMS.SMSSyncSettings();
			var temp_sync_R = new SMS.SMSSyncSettings();

			L = new SMS(new CoreComm(comm.ShowMessage, comm.Notify) { CoreFileProvider = comm.CoreFileProvider },
				game_L, rom_L, temp_set_L, temp_sync_L);

			R = new SMS(new CoreComm(comm.ShowMessage, comm.Notify) { CoreFileProvider = comm.CoreFileProvider },
				game_R, rom_R, temp_set_R, temp_sync_R);

			ser.Register<IVideoProvider>(this);
			ser.Register<ISoundProvider>(this); 

			_tracer = new TraceBuffer { Header = L.Cpu.TraceHeader };
			ser.Register<ITraceable>(_tracer);

			ServiceProvider = ser;

			SetupMemoryDomains();

			HardReset();

			L.stand_alone = false;
			R.stand_alone = false;
		}

		public void HardReset()
		{
			L.HardReset();
			R.HardReset();
		}

		public DisplayType Region => DisplayType.NTSC;

		public int _frame = 0;

		private readonly GGHawkLinkControllerDeck _controllerDeck;

		private readonly ITraceable _tracer;

		public bool LinkConnected
		{
			get => _cableconnected;
			set => _cableconnected = value;
		}

		private void ExecFetch(ushort addr)
		{
			uint flags = (uint)MemoryCallbackFlags.AccessExecute;
			MemoryCallbacks.CallMemoryCallbacks(addr, 0, flags, "System Bus");
		}
	}
}
