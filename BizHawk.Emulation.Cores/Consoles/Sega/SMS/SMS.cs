﻿using System;

using BizHawk.Common.StringExtensions;
using BizHawk.Emulation.Common;
using BizHawk.Emulation.Cores.Components;
using BizHawk.Emulation.Cores.Components.Z80A;

/*****************************************************
  TODO: 
  + HCounter (Manually set for light phaser emulation... should be only case it's polled)
  + Try to clean up the organization of the source code. 
  + Mode 1 not implemented in VDP TMS modes. (I dont have a test case in SG1000 or Coleco)
 
**********************************************************/

namespace BizHawk.Emulation.Cores.Sega.MasterSystem
{
	[Core(
		"SMSHawk",
		"Vecna",
		isPorted: false,
		isReleased: true)]
	[ServiceNotApplicable(typeof(IDriveLight))]
	public partial class SMS : IEmulator, ISaveRam, IStatable, IInputPollable, IRegionable,
		IDebuggable, ISettable<SMS.SMSSettings, SMS.SMSSyncSettings>, ICodeDataLogger
	{
		[CoreConstructor("SMS", "SG", "GG")]
		public SMS(CoreComm comm, GameInfo game, byte[] rom, object settings, object syncSettings)
		{
			ServiceProvider = new BasicServiceProvider(this);
			Settings = (SMSSettings)settings ?? new SMSSettings();
			SyncSettings = (SMSSyncSettings)syncSettings ?? new SMSSyncSettings();
			CoreComm = comm;
			MemoryCallbacks = new MemoryCallbackSystem(new[] { "System Bus" });

			IsGameGear = game.System == "GG";
			IsGameGear_C = game.System == "GG";
			IsSG1000 = game.System == "SG";
			RomData = rom;

			if (RomData.Length % BankSize != 0)
			{
				Array.Resize(ref RomData, ((RomData.Length / BankSize) + 1) * BankSize);
			}

			RomBanks = (byte)(RomData.Length / BankSize);

			Region = DetermineDisplayType(SyncSettings.DisplayType, game.Region);
			if (game["PAL"] && Region != DisplayType.PAL)
			{
				Region = DisplayType.PAL;
				CoreComm.Notify("Display was forced to PAL mode for game compatibility.");
			}

			if (IsGameGear)
			{
				Region = DisplayType.NTSC; // all game gears run at 60hz/NTSC mode
			}

			RegionStr = SyncSettings.ConsoleRegion;
			if (RegionStr == "Auto")
			{
				RegionStr = DetermineRegion(game.Region);
			}

			if (game["Japan"] && RegionStr != "Japan")
			{
				RegionStr = "Japan";
				CoreComm.Notify("Region was forced to Japan for game compatibility.");
			}

			if (game["Korea"] && RegionStr != "Korea")
			{
				RegionStr = "Korea";
				CoreComm.Notify("Region was forced to Korea for game compatibility.");
			}

			if ((game.NotInDatabase || game["FM"]) && SyncSettings.EnableFM && !IsGameGear)
			{
				HasYM2413 = true;
			}

			Cpu = new Z80A()
			{
				ReadHardware = ReadPort,
				WriteHardware = WritePort,
				FetchMemory = FetchMemory,
				ReadMemory = ReadMemory,
				WriteMemory = WriteMemory,
				MemoryCallbacks = MemoryCallbacks,
				OnExecFetch = OnExecMemory
			};

			if (game["GG_in_SMS"])
			{
				// skip setting the BIOS because this is a game gear game that puts the system
				// in SMS compatibility mode (it will fail the check sum if played on an actual SMS though.)
				IsGameGear = false;
				IsGameGear_C = true;
				game.System = "GG";
				Console.WriteLine("Using SMS Compatibility mode for Game Gear System");
			}

			Vdp = new VDP(this, Cpu, IsGameGear ? VdpMode.GameGear : VdpMode.SMS, Region);
			(ServiceProvider as BasicServiceProvider).Register<IVideoProvider>(Vdp);
			PSG = new SN76489sms();
			YM2413 = new YM2413();
			//SoundMixer = new SoundMixer(YM2413, PSG);
			if (HasYM2413 && game["WhenFMDisablePSG"])
			{
				disablePSG = true;
			}

			blip_L.SetRates(3579545, 44100);
			blip_R.SetRates(3579545, 44100);

			(ServiceProvider as BasicServiceProvider).Register<ISoundProvider>(this);

			SystemRam = new byte[0x2000];

			if (game["CMMapper"])
				InitCodeMastersMapper();
			else if (game["CMMapperWithRam"])
				InitCodeMastersMapperRam();
			else if (game["ExtRam"])
				InitExt2kMapper(int.Parse(game.OptionValue("ExtRam")));
			else if (game["KoreaMapper"])
				InitKoreaMapper();
			else if (game["MSXMapper"])
				InitMSXMapper();
			else if (game["NemesisMapper"])
				InitNemesisMapper();
			else if (game["TerebiOekaki"])
				InitTerebiOekaki();
			else if (game["EEPROM"])
				InitEEPROMMapper();
			else
				InitSegaMapper();

			if (Settings.ForceStereoSeparation && !IsGameGear)
			{
				if (game["StereoByte"])
				{
					ForceStereoByte = byte.Parse(game.OptionValue("StereoByte"));
				}

				PSG.Set_Panning(ForceStereoByte);
			}

			if (SyncSettings.AllowOverlock && game["OverclockSafe"])
				Vdp.IPeriod = 512;

			if (Settings.SpriteLimit)
				Vdp.SpriteLimit = true;

			if (game["3D"])
				IsGame3D = true;

			if (game["BIOS"])
			{
				Port3E = 0xF7; // Disable cartridge, enable BIOS rom
				InitBiosMapper();
			}
			else if ((game.System == "SMS") && !game["GG_in_SMS"])
			{
				BiosRom = comm.CoreFileProvider.GetFirmware("SMS", RegionStr, false);

				if (BiosRom == null)
				{
					throw new MissingFirmwareException("No BIOS found");
				}				
				else if (!game["RequireBios"] && !SyncSettings.UseBIOS)
				{
					// we are skipping the BIOS
					// but only if it won't break the game
				}
				else
				{
					Port3E = 0xF7;
				}
			}

			if (game["SRAM"])
			{
				SaveRAM = new byte[int.Parse(game.OptionValue("SRAM"))];
				Console.WriteLine(SaveRAM.Length);
			}			
			else if (game.NotInDatabase)
				SaveRAM = new byte[0x8000];

			SetupMemoryDomains();

			//this manages the linkage between the cpu and mapper callbacks so it needs running before bootup is complete
			((ICodeDataLogger)this).SetCDL(null);

			InputCallbacks = new InputCallbackSystem();

			Tracer = new TraceBuffer { Header = Cpu.TraceHeader };

			var serviceProvider = ServiceProvider as BasicServiceProvider;
			serviceProvider.Register<ITraceable>(Tracer);
			serviceProvider.Register<IDisassemblable>(Cpu);
			Vdp.ProcessOverscan();

			Cpu.ReadMemory = ReadMemory;
			Cpu.WriteMemory = WriteMemory;

			// Z80 SP initialization
			// stops a few SMS and GG games from crashing
			Cpu.Regs[Cpu.SPl] = 0xF0;
			Cpu.Regs[Cpu.SPh] = 0xDF;
		}

		public void HardReset()
		{

		}

		// Constants
		private const int BankSize = 16384;

		// ROM
		public byte[] RomData;
		private byte RomBank0, RomBank1, RomBank2, RomBank3;
		private byte Bios_bank;
		private byte RomBanks;
		private byte[] BiosRom;

		// Machine resources
		public Z80A Cpu;
		public byte[] SystemRam;
		public VDP Vdp;
		public SN76489sms PSG;
		private YM2413 YM2413;
		public bool IsGameGear { get; set; }
		public bool IsGameGear_C { get; set; }
		public bool IsSG1000 { get; set; }

		private bool HasYM2413 = false;
		private bool disablePSG = false;
		private bool PortDEEnabled = false;
		private IController _controller = NullController.Instance;

		private int _frame = 0;

		private byte Port01 = 0xFF;
		public byte Port02 = 0xFF;
		public byte Port03 = 0x00;
		public byte Port04 = 0xFF;
		public byte Port05 = 0x00;
		private byte Port3E = 0xAF;
		private byte Port3F = 0xFF;
		private byte PortDE = 0x00;

		private byte ForceStereoByte = 0xAD;
		private bool IsGame3D = false;

		// Linked Play Only
		public bool start_pressed;
		public byte cntr_rd_0;
		public byte cntr_rd_1;
		public byte cntr_rd_2;
		public bool stand_alone = true;
		public bool p3_write;
		public bool p4_read;

		public DisplayType Region { get; set; }

		private readonly ITraceable Tracer;

		string DetermineRegion(string gameRegion)
		{
			if (gameRegion == null)
				return "Export";
			if (gameRegion.IndexOf("USA") >= 0)
				return "Export";
			if (gameRegion.IndexOf("Europe") >= 0)
				return "Export";
			if (gameRegion.IndexOf("World") >= 0)
				return "Export";
			if (gameRegion.IndexOf("Brazil") >= 0)
				return "Export";
			if (gameRegion.IndexOf("Australia") >= 0)
				return "Export";
			if (gameRegion.IndexOf("Korea") >= 0)
				return "Korea";
			return "Japan";
		}

		private DisplayType DetermineDisplayType(string display, string region)
		{
			if (display == "NTSC") return DisplayType.NTSC;
			if (display == "PAL") return DisplayType.PAL;
			if (region != null && region == "Europe") return DisplayType.PAL;
			return DisplayType.NTSC;
		}

		public byte ReadMemory(ushort addr)
		{
			uint flags = (uint)(MemoryCallbackFlags.AccessRead);
			MemoryCallbacks.CallMemoryCallbacks(addr, 0, flags, "System Bus");

			return ReadMemoryMapper(addr);
		}

		public void WriteMemory(ushort addr, byte value)
		{
			WriteMemoryMapper(addr, value);

			uint flags = (uint)(MemoryCallbackFlags.AccessWrite);
			MemoryCallbacks.CallMemoryCallbacks(addr, value, flags, "System Bus");
		}

		public byte FetchMemory(ushort addr)
		{
			return ReadMemoryMapper(addr);
		}

		private void OnExecMemory(ushort addr)
		{
			uint flags = (uint)(MemoryCallbackFlags.AccessExecute);
			MemoryCallbacks.CallMemoryCallbacks(addr, 0, flags, "System Bus");
		}

		/// <summary>
		/// The ReadMemory callback for the mapper
		/// </summary>
		private Func<ushort, byte> ReadMemoryMapper;

		/// <summary>
		/// The WriteMemory callback for the wrapper
		/// </summary>
		private Action<ushort, byte> WriteMemoryMapper;

		/// <summary>
		/// A dummy FetchMemory that simply reads the memory
		/// </summary>
		private byte FetchMemory_StubThunk(ushort address)
		{
			return ReadMemory(address);
		}

		private byte ReadPort(ushort port)
		{
			port &= 0xFF;
			if (port < 0x40) // General IO ports
			{
				
				switch (port)
				{
					case 0x00: if (stand_alone) { return ReadPort0(); } else { _lagged = false; return cntr_rd_0; }
					case 0x01: return Port01;
					case 0x02: return Port02;
					case 0x03: return Port03;
					case 0x04: p4_read = true; return Port04;
					case 0x05: return Port05;
					case 0x06: return 0xFF;
					case 0x3E: return Port3E;
					default: return 0xFF;
				}
			}
			if (port < 0x80)  // VDP Vcounter/HCounter
			{
				if ((port & 1) == 0)
					return Vdp.ReadVLineCounter();
				else
					return Vdp.ReadHLineCounter();
			}
			if (port < 0xC0) // VDP data/control ports
			{
				if ((port & 1) == 0)
					return Vdp.ReadData();
				else
					return Vdp.ReadVdpStatus();
			}
			switch (port) 
			{
				case 0xC0:
				case 0xDC: if (stand_alone) { return ReadControls1(); } else { _lagged = false; return cntr_rd_1; }
				case 0xC1:
				case 0xDD: if (stand_alone) { return ReadControls2(); } else { _lagged = false; return cntr_rd_2; }
				case 0xDE: return PortDEEnabled ? PortDE : (byte)0xFF;
				case 0xF2: return HasYM2413 ? YM2413.DetectionValue : (byte)0xFF;
				default: return 0xFF;
			}
		}

		private void WritePort(ushort port, byte value)
		{
			port &= 0xFF;
			if (port < 0x40) // general IO ports
			{
				switch (port & 0xFF)
				{
					case 0x01: Port01 = value; break;
					case 0x02: Port02 = value; break;
					case 0x03: p3_write = true; Port03 = value; break;
					case 0x04: /*Port04 = value;*/ break; // receive port, not sure what writing does
					case 0x05: Port05 = (byte)(value & 0xF8); break;
					case 0x06: PSG.Set_Panning(value); break;
					case 0x3E: Port3E = value; break;
					case 0x3F: Port3F = value; break;
				}
			}
			else if (port < 0x80) // PSG
				PSG.WriteReg(value);
			else if (port < 0xC0) // VDP
			{
				if ((port & 1) == 0)
					Vdp.WriteVdpData(value);
				else
					Vdp.WriteVdpControl(value);
			}
			else if (port == 0xDE && PortDEEnabled) PortDE = value;
			else if (port == 0xF0 && HasYM2413) YM2413.RegisterLatch = value;
			else if (port == 0xF1 && HasYM2413) YM2413.Write(value);
			else if (port == 0xF2 && HasYM2413) YM2413.DetectionValue = value;
		}

		public string _region;
		public string RegionStr
		{
			get => _region;
			set
			{
				if (value.NotIn(validRegions))
				{
					throw new Exception("Passed value " + value + " is not a valid region!");
				}

				_region = value;
			}
		}
		
		private readonly string[] validRegions = { "Export", "Japan", "Korea" , "Auto"  };
	}
}
