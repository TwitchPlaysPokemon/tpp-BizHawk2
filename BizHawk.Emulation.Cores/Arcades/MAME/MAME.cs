﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;
using System.Dynamic;

using BizHawk.Common;
using BizHawk.Emulation.Common;
using BizHawk.Emulation.Common.IEmulatorExtensions;

namespace BizHawk.Emulation.Cores.Arcades.MAME
{
	[Core(
		name: "MAME",
		author: "MAMEDev",
		isPorted: true,
		portedVersion: "0.217",
		portedUrl: "https://github.com/mamedev/mame.git",
		singleInstance: false)]
	public partial class MAME : IEmulator, IVideoProvider, ISoundProvider, ISettable<object, MAME.SyncSettings>
	{
		public MAME(CoreComm comm, string dir, string file, object syncSettings, out string gamename)
		{
			ServiceProvider = new BasicServiceProvider(this);

			CoreComm = comm;
			_gameDirectory = dir;
			_gameFilename = file;
			_mameThread = new Thread(ExecuteMAMEThread);

			AsyncLaunchMAME();

			_syncSettings = (SyncSettings)syncSettings ?? new SyncSettings();
			_syncSettings.ExpandoSettings = new ExpandoObject();
			var dynamicObject = (IDictionary<string, object>)_syncSettings.ExpandoSettings;
			dynamicObject.Add("OKAY", 1);
			gamename = _gameName;

			if (_loadFailure != "")
			{
				Dispose();
				throw new Exception("\n\n" + _loadFailure);
			}
		}

		#region Utility

		/* strings and MAME
		 * 
		 * MAME's luaengine uses lua strings to return C strings as well as
		 * binary buffers. You're meant to know which you're going to get and
		 * handle that accordingly.
		 * 
		 * When we want to get a C string, we Marshal.PtrToStringAnsi().
		 * With buffers, we Marshal.Copy() to our new buffer.
		 * MameGetString() only covers the former because it's the same steps
		 * every time, while buffers use to need aditional logic.
		 * 
		 * In both cases MAME wants us to manually free the string buffer. It's
		 * made that way to make the buffer persist actoss C API calls.
		 * 
		 */
		private static string MameGetString(string command)
		{
			IntPtr ptr = LibMAME.mame_lua_get_string(command, out var lengthInBytes);

			if (ptr == IntPtr.Zero)
			{
				Console.WriteLine("LibMAME ERROR: string buffer pointer is null");
				return "";
			}

			var ret = Marshal.PtrToStringAnsi(ptr, lengthInBytes);

			if (!LibMAME.mame_lua_free_string(ptr))
			{
				Console.WriteLine("LibMAME ERROR: string buffer wasn't freed");
			}

			return ret;
		}

		#endregion

		#region Properties

		public CoreComm CoreComm { get; private set; }
		public IEmulatorServiceProvider ServiceProvider { get; private set; }
		public ControllerDefinition ControllerDefinition => MAMEController;
		public string SystemId => "MAME";
		public int[] GetVideoBuffer() => _frameBuffer;
		public bool DeterministicEmulation => true;
		public bool CanProvideAsync => false;
		public SyncSoundMode SyncMode => SyncSoundMode.Sync;
		public int BackgroundColor => 0;
		public int Frame { get; private set; }
		public int VirtualWidth { get; private set; } = 320;
		public int VirtualHeight { get; private set; } = 240;
		public int BufferWidth { get; private set; } = 320;
		public int BufferHeight { get; private set; } = 240;
		public int VsyncNumerator { get; private set; } = 60;
		public int VsyncDenominator { get; private set; } = 1;

		#endregion

		#region Fields

		private SyncSettings _syncSettings;
		private Thread _mameThread;
		private ManualResetEvent _mameStartupComplete = new ManualResetEvent(false);
		private ManualResetEvent _mameFrameComplete = new ManualResetEvent(false);
		private AutoResetEvent _mamePeriodicComplete = new AutoResetEvent(false);
		private AutoResetEvent _memoryAccessComplete = new AutoResetEvent(false);
		private SortedDictionary<string, string> _fieldsPorts = new SortedDictionary<string, string>();
		private IController _controller = NullController.Instance;
		private IMemoryDomains _memoryDomains;
		private int _systemBusAddressShift = 0;
		private bool _memAccess = false;
		private int[] _frameBuffer = new int[0];
		private Queue<short> _audioSamples = new Queue<short>();
		private decimal _dAudioSamples = 0;
		private int _sampleRate = 44100;
		private int _numSamples = 0;
		private bool _paused = true;
		private bool _exiting = false;
		private bool _frameDone = true;
		private string _gameDirectory;
		private string _gameFilename;
		private string _gameName = "Arcade";
		private string _loadFailure = "";
		private LibMAME.PeriodicCallbackDelegate _periodicCallback;
		private LibMAME.SoundCallbackDelegate _soundCallback;
		private LibMAME.BootCallbackDelegate _bootCallback;
		private LibMAME.LogCallbackDelegate _logCallback;

		#endregion

		#region IEmulator

		public bool FrameAdvance(IController controller, bool render, bool renderSound = true)
		{
			if (_exiting)
			{
				return false;
			}

			_controller = controller;
			_paused = false;
			_frameDone = false;

			for (; _frameDone == false;)
			{
				_mameFrameComplete.WaitOne();
			}

			Frame++;

			return true;
		}

		public void ResetCounters()
		{
			Frame = 0;
		}

		public void Dispose()
		{
			_exiting = true;
			_mameThread.Join();
		}

		#endregion

		#region ISettable

		public object GetSettings() => null;
		public bool PutSettings(object o) => false;

		public SyncSettings GetSyncSettings()
		{
			return _syncSettings.Clone();
		}

		public bool PutSyncSettings(SyncSettings o)
		{
			bool ret = SyncSettings.NeedsReboot(o, _syncSettings);
			_syncSettings = o;
			return ret;
		}

		public class SyncSettings
		{
			public static bool NeedsReboot(SyncSettings x, SyncSettings y)
			{
				return !DeepEquality.DeepEquals(x, y);
			}

			public SyncSettings Clone()
			{
				return (SyncSettings)MemberwiseClone();
			}

			public ExpandoObject ExpandoSettings { get; set; }
		}

		#endregion

		#region ISoundProvider

		public void SetSyncMode(SyncSoundMode mode)
		{
			if (mode == SyncSoundMode.Async)
			{
				throw new NotSupportedException("Async mode is not supported.");
			}
		}

		/*
		 * GetSamplesSync() and MAME
		 * 
		 * MAME generates samples 50 times per second, regardless of the VBlank
		 * rate of the emulated machine. It then uses complicated logic to
		 * output the required amount of audio to the OS driver and to the AVI,
		 * where it's meant to tie flashed samples to video frame duration.
		 * 
		 * I'm doing my own logic here for now. I grab MAME's audio buffer
		 * whenever it's filled (MAMESoundCallback()) and enqueue it.
		 * 
		 * Whenever Hawk wants new audio, I dequeue it, but with a little quirk.
		 * Since sample count per frame may not align with frame duration, I
		 * subtract the entire decimal fraction of "required" samples from total
		 * samples. I check if the fractional reminder of total samples is > 0.5
		 * by rounding it. I invert it to see what number I should add to the
		 * integer representation of "required" samples, to compensate for
		 * misalignment between fractional and integral "required" samples.
		 * 
		 * TODO: Figure out how MAME does this and maybe use their method instead.
		 */
		public void GetSamplesSync(out short[] samples, out int nsamp)
		{
			decimal dSamplesPerFrame = (decimal)_sampleRate * VsyncDenominator / VsyncNumerator;

			if (_audioSamples.Any())
			{
				_dAudioSamples -= dSamplesPerFrame;
				int remainder = (int)Math.Round(_dAudioSamples - Math.Truncate(_dAudioSamples)) ^ 1;
				nsamp = (int)Math.Round(dSamplesPerFrame) + remainder;
			}
			else
			{
				nsamp = (int)Math.Round(dSamplesPerFrame);
			}

			samples = new short[nsamp * 2];

			for (int i = 0; i < nsamp * 2; i++)
			{
				if (_audioSamples.Any())
				{
					samples[i] = _audioSamples.Dequeue();
				}
				else
				{
					samples[i] = 0;
				}
			}
		}

		public void GetSamplesAsync(short[] samples)
		{
			throw new InvalidOperationException("Async mode is not supported.");
		}

		public void DiscardSamples()
		{
			_audioSamples.Clear();
		}

		#endregion

		#region IMemoryDomains

		private void InitMemoryDomains()
		{
			var domains = new List<MemoryDomain>();

			_systemBusAddressShift = LibMAME.mame_lua_get_int(MAMELuaCommand.GetSpaceAddressShift);
			var size = (long)LibMAME.mame_lua_get_double(MAMELuaCommand.GetSpaceAddressMask) + 1;
			var dataWidth = LibMAME.mame_lua_get_int(MAMELuaCommand.GetSpaceDataWidth) >> 3; // mame returns in bits
			var endianString = MameGetString(MAMELuaCommand.GetSpaceEndianness);

			MemoryDomain.Endian endian = MemoryDomain.Endian.Unknown;

			if (endianString == "little")
			{
				endian = MemoryDomain.Endian.Little;
			}
			else if (endianString == "big")
			{
				endian = MemoryDomain.Endian.Big;
			}

			var mapCount = LibMAME.mame_lua_get_int(MAMELuaCommand.GetSpaceMapCount);

			for (int i = 1; i <= mapCount; i++)
			{
				var read = MameGetString($"return { MAMELuaCommand.SpaceMap }[{ i }].readtype");
				var write = MameGetString($"return { MAMELuaCommand.SpaceMap }[{ i }].writetype");

				if (read == "ram" && write == "ram" /* || read == "rom" */)
				{
					var firstOffset = LibMAME.mame_lua_get_int($"return { MAMELuaCommand.SpaceMap }[{ i }].offset");
					var lastOffset = LibMAME.mame_lua_get_int($"return { MAMELuaCommand.SpaceMap }[{ i }].endoff");
					var name = $"{ read.ToUpper() } { firstOffset:X}-{ lastOffset:X}";

					domains.Add(new MemoryDomainDelegate(name, lastOffset - firstOffset + 1, endian,
						delegate (long addr)
						{
							if (addr < 0 || addr >= size)
							{
								throw new ArgumentOutOfRangeException();
							}

							_memAccess = true;
							_mamePeriodicComplete.WaitOne();
							addr += firstOffset;
							var val = (byte)LibMAME.mame_lua_get_int($"{ MAMELuaCommand.GetSpace }:read_u8({ addr << _systemBusAddressShift })");
							_memoryAccessComplete.Set();
							_memAccess = false;
							return val;
						},
						read == "rom" ? (Action<long, byte>)null : delegate (long addr, byte val)
						{
							if (addr < 0 || addr >= size)
							{
								throw new ArgumentOutOfRangeException();
							}

							_memAccess = true;
							_mamePeriodicComplete.WaitOne();
							addr += firstOffset;
							LibMAME.mame_lua_execute($"{ MAMELuaCommand.GetSpace }:write_u8({ addr << _systemBusAddressShift }, { val })");
							_memoryAccessComplete.Set();
							_memAccess = false;
						}, dataWidth));
				}
			}

			domains.Add(new MemoryDomainDelegate("System Bus", size, endian,
				delegate (long addr)
				{
					if (addr < 0 || addr >= size)
					{
						throw new ArgumentOutOfRangeException();
					}

					_memAccess = true;
					_mamePeriodicComplete.WaitOne();
					var val = (byte)LibMAME.mame_lua_get_int($"{ MAMELuaCommand.GetSpace }:read_u8({ addr << _systemBusAddressShift })");
					_memoryAccessComplete.Set();
					_memAccess = false;
					return val;
				},
				null, dataWidth));

			_memoryDomains = new MemoryDomainList(domains);
			(ServiceProvider as BasicServiceProvider).Register<IMemoryDomains>(_memoryDomains);
		}

		#endregion

		#region Launchers

		private void AsyncLaunchMAME()
		{
			_mameThread.Start();
			_mameStartupComplete.WaitOne();
		}

		private void ExecuteMAMEThread()
		{
			// dodge GC
			_periodicCallback = MAMEPeriodicCallback;
			_soundCallback = MAMESoundCallback;
			_bootCallback = MAMEBootCallback;
			_logCallback = MAMELogCallback;

			LibMAME.mame_set_periodic_callback(_periodicCallback);
			LibMAME.mame_set_sound_callback(_soundCallback);
			LibMAME.mame_set_boot_callback(_bootCallback);
			LibMAME.mame_set_log_callback(_logCallback);

			// https://docs.mamedev.org/commandline/commandline-index.html
			string[] args =
			{
				 "mame"                                 // dummy, internally discarded by index, so has to go first
				, _gameFilename                         // no dash for rom names
				, "-noreadconfig"                       // forbid reading any config files
				, "-norewind"                           // forbid rewind savestates (captured upon frame advance)
				, "-skip_gameinfo"                      // forbid this blocking screen that requires user input
				, "-nothrottle"                         // forbid throttling to "real" speed of the device
				, "-update_in_pause"                    // ^ including frame-advancing
				, "-rompath",            _gameDirectory // mame doesn't load roms from full paths, only from dirs to scan
				, "-volume",                     "-32"  // lowest attenuation means mame osd remains silent
				, "-output",                 "console"  // print everything to hawk console
				, "-samplerate", _sampleRate.ToString() // match hawk samplerate
				, "-video",                     "none"  // forbid mame window altogether
				, "-keyboardprovider",          "none"
				, "-mouseprovider",             "none"
				, "-lightgunprovider",          "none"
				, "-joystickprovider",          "none"
			};

			LibMAME.mame_launch(args.Length, args);
		}

		#endregion

		#region Updaters

		private void UpdateFramerate()
		{
			VsyncNumerator = 1000000000;
			long refresh = (long)LibMAME.mame_lua_get_double(MAMELuaCommand.GetRefresh);
			VsyncDenominator = (int)(refresh / 1000000000);
		}

		private void UpdateAspect()
		{
			int x = (int)LibMAME.mame_lua_get_double(MAMELuaCommand.GetBoundX);
			int y = (int)LibMAME.mame_lua_get_double(MAMELuaCommand.GetBoundY);
			VirtualHeight = BufferWidth > BufferHeight * x / y
				? BufferWidth * y / x
				: BufferHeight;
			VirtualWidth = VirtualHeight * x / y;
		}

		private void UpdateVideo()
		{
			BufferWidth = LibMAME.mame_lua_get_int(MAMELuaCommand.GetWidth);
			BufferHeight = LibMAME.mame_lua_get_int(MAMELuaCommand.GetHeight);
			int expectedSize = BufferWidth * BufferHeight;
			int bytesPerPixel = 4;
			IntPtr ptr = LibMAME.mame_lua_get_string(MAMELuaCommand.GetPixels, out var lengthInBytes);

			if (ptr == IntPtr.Zero)
			{
				Console.WriteLine("LibMAME ERROR: frame buffer pointer is null");
				return;
			}

			if (expectedSize * bytesPerPixel != lengthInBytes)
			{
				Console.WriteLine(
					"LibMAME ERROR: frame buffer has wrong size\n" +
					$"width:    { BufferWidth                  } pixels\n" +
					$"height:   { BufferHeight                 } pixels\n" +
					$"expected: { expectedSize * bytesPerPixel } bytes\n" +
					$"received: { lengthInBytes                } bytes\n");
				return;
			}

			_frameBuffer = new int[expectedSize];
			Marshal.Copy(ptr, _frameBuffer, 0, expectedSize);

			if (!LibMAME.mame_lua_free_string(ptr))
			{
				Console.WriteLine("LibMAME ERROR: frame buffer wasn't freed");
			}
		}

		private void UpdateInput()
		{
			foreach (var fieldPort in _fieldsPorts)
			{
				LibMAME.mame_lua_execute(
					"manager:machine():ioport()" +
					$".ports  [\"{ fieldPort.Value }\"]" +
					$".fields [\"{ fieldPort.Key   }\"]" +
					$":set_value({ (_controller.IsPressed(fieldPort.Key) ? 1 : 0) })");
			}
		}

		private void Update()
		{
			UpdateFramerate();
			UpdateVideo();
			UpdateAspect();
			UpdateInput();
		}

		private void UpdateGameName()
		{
			_gameName = MameGetString(MAMELuaCommand.GetGameName);
		}

		private void CheckVersions()
		{
			var mameVersion = MameGetString(MAMELuaCommand.GetVersion);
			var version = this.Attributes().PortedVersion;
			Debug.Assert(version == mameVersion,
				"MAME versions desync!\n\n" +
				$"MAME is { mameVersion }\n" +
				$"MAMEHawk is { version }");
		}

		#endregion

		#region Callbacks

		/*
		 * FrameAdvance() and MAME
		 * 
		 * MAME fires the periodic callback on every video and debugger update,
		 * which happens every VBlank and also repeatedly at certain time
		 * intervals while paused. Since MAME's luaengine runs in a separate
		 * thread, it's only safe to update everything we need per frame during
		 * this callback, when it's explicitly waiting for further lua commands.
		 * 
		 * If we disable throttling and pass -update_in_pause, there will be no
		 * delay between video updates. This allows to run at full speed while
		 * frame-stepping.
		 * 
		 * MAME only captures new frame data once per VBlank, while unpaused.
		 * But it doesn't have an exclusive VBlank callback we could attach to.
		 * It has a LUA_ON_FRAME_DONE callback, but that fires even more
		 * frequently and updates all sorts of other non-video stuff, and we
		 * need none of that here.
		 * 
		 * So we filter out all the calls that happen while paused (non-VBlank
		 * updates). Then, when Hawk asks us to advance a frame, we virtually
		 * unpause and declare the new frame unfinished. This informs MAME that
		 * it should advance one frame internally. Hawk starts waiting for the
		 * MAME thread to complete the request.
		 * 
		 * After MAME's done advancing, it fires the periodic callback again.
		 * That's when we update everything and declare the new frame finished,
		 * filtering out any further updates again. Then we allow Hawk to
		 * complete frame-advancing.
		 */
		private void MAMEPeriodicCallback()
		{
			if (_exiting)
			{
				LibMAME.mame_lua_execute(MAMELuaCommand.Exit);
				_exiting = false;
			}
			
			if (_memAccess)
			{
				_mamePeriodicComplete.Set();
				_memoryAccessComplete.WaitOne();
				return;
			}

			//int MAMEFrame = LibMAME.mame_lua_get_int(MAMELuaCommand.GetFrameNumber);

			if (!_paused)
			{
				LibMAME.mame_lua_execute(MAMELuaCommand.Step);
				_frameDone = false;
				_paused = true;
			}
			else if (!_frameDone)
			{
				Update();
				_frameDone = true;
				_mameFrameComplete.Set();
			}
		}

		private void MAMESoundCallback()
		{
			int bytesPerSample = 2;
			IntPtr ptr = LibMAME.mame_lua_get_string(MAMELuaCommand.GetSamples, out var lengthInBytes);

			if (ptr == IntPtr.Zero)
			{
				Console.WriteLine("LibMAME ERROR: audio buffer pointer is null");
				return;
			}

			_numSamples = lengthInBytes / bytesPerSample;

			unsafe
			{
				short* pSample = (short*)ptr.ToPointer();
				for (int i = 0; i < _numSamples; i++)
				{
					_audioSamples.Enqueue(*(pSample + i));
					_dAudioSamples++;
				}
			}

			if (!LibMAME.mame_lua_free_string(ptr))
			{
				Console.WriteLine("LibMAME ERROR: audio buffer wasn't freed");
			}
		}

		private void MAMEBootCallback()
		{
			LibMAME.mame_lua_execute(MAMELuaCommand.Pause);

			CheckVersions();
			GetInputFields();
			Update();
			UpdateGameName();
			InitMemoryDomains();

			_mameStartupComplete.Set();
		}
		
		private void MAMELogCallback(LibMAME.OutputChannel channel, int size, string data)
		{
			if (data.Contains("NOT FOUND"))
			{
				_loadFailure = data;
			}

			if (data.Contains("Fatal error"))
			{
				_mameStartupComplete.Set();
				_loadFailure += data;
			}

			// mame sends osd_output_channel casted to int, we implicitly cast it back
			if (!data.Contains("pause = "))
			{
				Console.WriteLine(
					$"[MAME { channel.ToString() }] " +
					$"{ data.Replace('\n', ' ') }");
			}
		}

		#endregion

		#region Input

		public static ControllerDefinition MAMEController = new ControllerDefinition
		{
			Name = "MAME Controller",
			BoolButtons = new List<string>()
		};

		private void GetInputFields()
		{
			string inputFields = MameGetString(MAMELuaCommand.GetInputFields);
			string[] portFields = inputFields.Split(';');
			MAMEController.BoolButtons.Clear();

			foreach (string portField in portFields)
			{
				if (portField != string.Empty)
				{
					string[] substrings = portField.Split(',');
					string tag = substrings.First();
					string field = substrings.Last();

					_fieldsPorts.Add(field, tag);
					MAMEController.BoolButtons.Add(field);
				}
			}
		}

		#endregion

		#region Lua Commands

		private class MAMELuaCommand
		{
			// commands
			public const string Step = "emu.step()";
			public const string Pause = "emu.pause()";
			public const string Unpause = "emu.unpause()";
			public const string Exit = "manager:machine():exit()";

			// getters
			public const string GetVersion = "return emu.app_version()";
			public const string GetGameName = "return manager:machine():system().description";
			public const string GetPixels = "return manager:machine():video():pixels()";
			public const string GetSamples = "return manager:machine():sound():samples()";
			public const string GetFrameNumber = "return select(2, next(manager:machine().screens)):frame_number()";
			public const string GetRefresh = "return select(2, next(manager:machine().screens)):refresh_attoseconds()";
			public const string GetWidth = "return (select(1, manager:machine():video():size()))";
			public const string GetHeight = "return (select(2, manager:machine():video():size()))";

			// memory space
			public const string GetSpace = "return manager:machine().devices[\":maincpu\"].spaces[\"program\"]";
			public const string GetSpaceMapCount = "return #manager:machine().devices[\":maincpu\"].spaces[\"program\"].map";
			public const string SpaceMap = "manager:machine().devices[\":maincpu\"].spaces[\"program\"].map";
			public const string GetSpaceAddressMask = "return manager:machine().devices[\":maincpu\"].spaces[\"program\"].address_mask";
			public const string GetSpaceAddressShift = "return manager:machine().devices[\":maincpu\"].spaces[\"program\"].shift";
			public const string GetSpaceDataWidth = "return manager:machine().devices[\":maincpu\"].spaces[\"program\"].data_width";
			public const string GetSpaceEndianness = "return manager:machine().devices[\":maincpu\"].spaces[\"program\"].endianness";

			// complex stuff
			public const string GetBoundX =
				"local x0,x1,y0,y1 = manager:machine():render():ui_target():view_bounds() " +
				"return x1-x0";
			public const string GetBoundY =
				"local x0,x1,y0,y1 = manager:machine():render():ui_target():view_bounds() " +
				"return y1-y0";
			public const string GetInputFields =
				"final = {} " +
				"for tag, _ in pairs(manager:machine():ioport().ports) do " +
					"for name, field in pairs(manager:machine():ioport().ports[tag].fields) do " +
						"if field.type_class ~= \"dipswitch\" then " +
							"table.insert(final, string.format(\"%s,%s;\", tag, name)) " +
						"end " +
					"end " +
				"end " +
				"table.sort(final) " +
				"return table.concat(final)";
		}

		#endregion
	}
}
