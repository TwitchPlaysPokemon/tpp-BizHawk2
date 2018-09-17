using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using BizHawk.Client.Common.Api.Public;
using BizHawk.Emulation.Common;
using System.IO;

namespace BizHawk.Client.EmuHawk.tools.Api
{
	class ClientApi : ApiProvider
	{
		public override IEnumerable<ApiCommand> Commands => new List<ApiCommand>()
		{
			new ApiCommand("Quit", WrapVoid(Quit), new List<ApiParameter>(), "Closes the emulator"),
			new ApiCommand("Pause", WrapVoid(Pause), new List<ApiParameter>(), "Pauses the emulator"),
			new ApiCommand("Play", WrapVoid(Play), new List<ApiParameter>(), "Unpauses the emulator"),
			new ApiCommand("IsPaused", WrapFunc(IsPaused), new List<ApiParameter>(), "Returns true if emulator is paused, otherwise false"),
			new ApiCommand("FlushSaveRAM", WrapVoid(FlushSaveRAM), new List<ApiParameter>(), "Flushes save ram to disk"),
			new ApiCommand("LoadROM", WrapPath(LoadRom), new List<ApiParameter>(){ PathParam }, "Loads the ROM file at the given Path"),
			new ApiCommand("CloseROM", WrapVoid(CloseRom), new List<ApiParameter>(), "Closes the loaded ROM"),
			new ApiCommand("LoadState", WrapPath(LoadState), new List<ApiParameter>(){ PathParam }, "Loads the State file at the given Path"),
			new ApiCommand("SaveState", WrapPath(SaveState, false), new List<ApiParameter>(){ PathParam }, "Saves the current game state to the given Path"),
		};

		private static ApiParameter PathParam = new ApiParameter("Path", "string");

		private static Func<IEnumerable<string>, string, string> WrapFunc<T>(Func<T> innerCall) => (IEnumerable<string> args, string domain) => JsonConvert.SerializeObject(innerCall());
		private static Func<IEnumerable<string>, string, string> WrapVoid(Action innerCall) => (IEnumerable<string> args, string domain) =>
		{
			try
			{
				innerCall();
			}
			catch (Exception e)
			{
				return e.Message;
			}
			return null;
		};
		private static Func<IEnumerable<string>, string, string> WrapPath(Action<string> innerCall, bool fileMustExist = true) => (IEnumerable<string> args, string domain) =>
		{
			var path = string.Join("\\", args);
			try
			{
				if (fileMustExist && !File.Exists(path))
					return $"Could not find file: {path}";
				innerCall(path);
			}
			catch (Exception e)
			{
				return e.Message;
			}
			return null;
		};

		public static void Quit() => GlobalWin.MainForm.CloseEmulator();

		public static bool IsPaused() => GlobalWin.MainForm.EmulatorPaused;

		public static void Pause() => GlobalWin.MainForm.PauseEmulator();

		public static void Play() => GlobalWin.MainForm.UnpauseEmulator();

		public void FlushSaveRAM() => GlobalWin.MainForm.FlushSaveRAM();

		public static void LoadRom(string path) => GlobalWin.MainForm.LoadRom(path, new MainForm.LoadRomArgs { OpenAdvanced = OpenAdvancedSerializer.ParseWithLegacy(path) });

		public static void CloseRom() => GlobalWin.MainForm.CloseRom();

		public void LoadState(string path) => GlobalWin.MainForm.LoadState(path, Path.GetFileName(path), true, true);

		public void SaveState(string path) => GlobalWin.MainForm.SaveState(path, path, true, true);
	}
}
