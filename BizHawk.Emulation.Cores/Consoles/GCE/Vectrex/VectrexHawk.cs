﻿using System;

using BizHawk.Common.BufferExtensions;
using BizHawk.Emulation.Common;
using BizHawk.Emulation.Cores.Components.MC6809;

namespace BizHawk.Emulation.Cores.Consoles.Vectrex
{
	[Core(
		"VectrexHawk",
		"",
		isPorted: false,
		isReleased: true)]
	[ServiceNotApplicable(typeof(IDriveLight))]
	public partial class VectrexHawk : IEmulator, ISaveRam, IDebuggable, IStatable, IInputPollable, IRegionable, 
	ISettable<VectrexHawk.VectrexSettings, VectrexHawk.VectrexSyncSettings>
	{
		public byte[] RAM = new byte[0x400];


		public byte[] _bios, minestorm;
		public readonly byte[] _rom;	

		private int _frame = 0;

		public MapperBase mapper;

		private readonly ITraceable _tracer;

		public MC6809 cpu;
		public PPU ppu;
		public Audio audio;
		public SerialPort serialport;

		[CoreConstructor("VEC")]
		public VectrexHawk(CoreComm comm, GameInfo game, byte[] rom, /*string gameDbFn,*/ object settings, object syncSettings)
		{
			var ser = new BasicServiceProvider(this);

			cpu = new MC6809
			{
				ReadMemory = ReadMemory,
				WriteMemory = WriteMemory,
				PeekMemory = PeekMemory,
				DummyReadMemory = ReadMemory,
				OnExecFetch = ExecFetch,
			};

			audio = new Audio();
			ppu = new PPU();
			serialport = new SerialPort();

			CoreComm = comm;

			_settings = (VectrexSettings)settings ?? new VectrexSettings();
			_syncSettings = (VectrexSyncSettings)syncSettings ?? new VectrexSyncSettings();
			_controllerDeck = new VectrexHawkControllerDeck(_syncSettings.Port1, _syncSettings.Port2);

			byte[] Bios = null;
			byte[] Mine = null;

			Bios = comm.CoreFileProvider.GetFirmware("Vectrex", "Bios", true, "BIOS Not Found, Cannot Load");			
			_bios = Bios;

			Mine = comm.CoreFileProvider.GetFirmware("Vectrex", "Minestorm", true, "Minestorm Not Found, Cannot Load");
			minestorm = Mine;

			Console.WriteLine("SHA1:" + rom.HashSHA1(0, rom.Length));

			_rom = rom;

			// If the game is minstorm, then no cartridge is inserted, retun 0xFF
			if (rom.HashSHA1(0, rom.Length) == "65D07426B520DDD3115D40F255511E0FD2E20AE7")
			{
				_rom  = new byte[0x8000];

				for (int i = 0; i < 0x8000; i++)
				{
					_rom[i] = 0xFF;
				}
			}

			// mirror games that are too small
			if (_rom.Length < 0x8000)
			{
				_rom = new byte[0x8000];

				for (int i = 0; i < 0x8000 / rom.Length; i++)
				{
					for (int j = 0; j < rom.Length; j++)
					{
						_rom[j + i * rom.Length] = rom[j];
					}
				}
			}

			// RAM appears to power up to either random values or 0xFF, otherwise all the asteroids in minestorm are on the same side if RAM[0x7E]=0
			for (int i = 0; i < RAM.Length; i++)
			{
				RAM[i] = 0xFF;
			}

			Setup_Mapper();

			_frameHz = 50;

			audio.Core = this;
			ppu.Core = this;
			serialport.Core = this;

			ser.Register<IVideoProvider>(this);
			ser.Register<ISoundProvider>(audio);
			ServiceProvider = ser;

			_settings = (VectrexSettings)settings ?? new VectrexSettings();
			_syncSettings = (VectrexSyncSettings)syncSettings ?? new VectrexSyncSettings();

			_tracer = new TraceBuffer { Header = cpu.TraceHeader };
			ser.Register<ITraceable>(_tracer);

			SetupMemoryDomains();
			HardReset();

			cpu.SetCallbacks(ReadMemory, PeekMemory, PeekMemory, WriteMemory);
		}

		public DisplayType Region => DisplayType.NTSC;

		private readonly VectrexHawkControllerDeck _controllerDeck;

		public void HardReset()
		{
			Register_Reset();
			ppu.Reset();
			audio.Reset();
			serialport.Reset();
			cpu.Reset();

			RAM = new byte[0x400];

			_vidbuffer = new int[VirtualWidth * VirtualHeight];
			_framebuffer = new int[VirtualWidth * VirtualHeight];
		}

		public void SoftReset()
		{
			Register_Reset();
			ppu.Reset();
			audio.Reset();
			serialport.Reset();
			cpu.Reset();

			_vidbuffer = new int[VirtualWidth * VirtualHeight];
			_framebuffer = new int[VirtualWidth * VirtualHeight];
		}

		private void ExecFetch(ushort addr)
		{
			uint flags = (uint)MemoryCallbackFlags.AccessExecute;
			MemoryCallbacks.CallMemoryCallbacks(addr, 0, flags, "System Bus");
		}

		private void Setup_Mapper()
		{
			mapper = new MapperDefault();
			mapper.Core = this;
		}
	}
}
