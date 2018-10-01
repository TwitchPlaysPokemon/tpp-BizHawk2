using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using BizHawk.Client.Common.Api.Public;
using BizHawk.Emulation.Common;
using System.IO;
using System.Windows.Forms;
using BizHawk.Client.Common;
using System.Text.RegularExpressions;
using System.Drawing;

namespace BizHawk.Client.EmuHawk.tools.Api
{
	class ClientApi : ApiProvider
	{
		public override IEnumerable<ApiCommand> Commands => new List<ApiCommand>()
		{
			new ApiCommand("Quit", WrapUITask(WrapVoid(Quit)), new List<ApiParameter>(), "Closes the emulator"),
			new ApiCommand("Pause", WrapUITask(WrapVoid(Pause)), new List<ApiParameter>(), "Pauses the emulator"),
			new ApiCommand("Play", WrapUITask(WrapVoid(Play)), new List<ApiParameter>(), "Unpauses the emulator"),
			new ApiCommand("IsPaused", WrapUITask(WrapFunc(IsPaused)), new List<ApiParameter>(), "Returns true if emulator is paused, otherwise false"),
			new ApiCommand("Mute", WrapUITask(WrapVoid(Mute)), new List<ApiParameter>() { }, "Disables sound"),
			new ApiCommand("Unmute", WrapUITask(WrapVoid(Unmute)), new List<ApiParameter>(), "Enables sound"),
			new ApiCommand("FlushSaveRAM", WrapUITask(WrapVoid(FlushSaveRAM)), new List<ApiParameter>(), "Flushes save ram to disk"),
			new ApiCommand("LoadROM", WrapUITask(WrapPath(LoadRom)), new List<ApiParameter>(){ PathParam }, "Loads the ROM file at the given Path"),
			new ApiCommand("CloseROM", WrapUITask(WrapVoid(CloseRom)), new List<ApiParameter>(), "Closes the loaded ROM"),
			new ApiCommand("LoadState", WrapUITask(WrapPath(LoadState)), new List<ApiParameter>(){ PathParam }, "Loads the State file at the given Path"),
			new ApiCommand("SaveState", WrapUITask(WrapPath(SaveState, false)), new List<ApiParameter>(){ PathParam }, "Saves the current game state to the given Path. Tokens %Timestamp% and %Name% will be replaced with the appropriate values."),
			new ApiCommand("SaveScreenshot", WrapUITask(WrapPath(SaveScreenshot, false)), new List<ApiParameter>(){ PathParam }, "Saves a screenshot to the given Path. Tokens %Timestamp% and %Name% will be replaced with the appropriate values."),
			new ApiCommand("StopDrawing", WrapUITask(WrapVoid(StopDrawing)), new List<ApiParameter>(), "Stops the emulator from writing to the screen"),
			new ApiCommand("StartDrawing", WrapUITask(WrapVoid(StartDrawing)), new List<ApiParameter>(), "Allows the emulator to write to the screen"),
			new ApiCommand("ClearScreen", WrapUITask(WrapVoid(ClearScreen)), new List<ApiParameter>() { ColorParam }, "Blanks the emulator screen"),
			new ApiCommand("Stall", WrapUITask(WrapVoid(Stall)), new List<ApiParameter>() {ColorParam }, "Stalls the emulator on a blank screen"),
			new ApiCommand("Resume", WrapUITask(WrapVoid(Resume)), new List<ApiParameter>(), "Resumes emulation after a Pause or Stall"),
		};

		private static ApiParameter PathParam = new ApiParameter("Path", "string");
		private static ApiParameter ColorParam = new ApiParameter("Color", "hexcode", true);

		private static Func<IEnumerable<string>, string> WrapFunc<T>(Func<T> innerCall) => (IEnumerable<string> args) => JsonConvert.SerializeObject(innerCall());
		private static Func<IEnumerable<string>, string> WrapVoid(Action innerCall) => (IEnumerable<string> args) =>
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
		private static Func<IEnumerable<string>, string> WrapVoid(Action<string> innerCall) => (IEnumerable<string> args) =>
		{
			try
			{
				innerCall(args.FirstOrDefault());
			}
			catch (Exception e)
			{
				return e.Message;
			}
			return null;
		};
		private static Func<IEnumerable<string>, string> WrapPath(Action<string> innerCall, bool fileMustExist = true) => (IEnumerable<string> args) =>
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
		private static Func<IEnumerable<string>, string> WrapUITask(Func<IEnumerable<string>, string> innerCall) => (IEnumerable<string> args) => GlobalWin.MainForm.UIWorker.Invoke(innerCall, args) as string;

		public static void Quit() => GlobalWin.MainForm.CloseEmulator();

		public static bool IsPaused() => GlobalWin.MainForm.EmulatorPaused;

		public static void Pause() => GlobalWin.MainForm.PauseEmulator();

		public static void Play() => GlobalWin.MainForm.UnpauseEmulator();

		public void FlushSaveRAM() => GlobalWin.MainForm.FlushSaveRAM();

		public static void LoadRom(string path) => GlobalWin.MainForm.LoadRom(path, new MainForm.LoadRomArgs { OpenAdvanced = OpenAdvancedSerializer.ParseWithLegacy(path), FromLua = true });

		public static void CloseRom() => GlobalWin.MainForm.CloseRom();

		public void LoadState(string path) => GlobalWin.MainForm.LoadState(path, Path.GetFileName(path), true, true);

		private static readonly Regex timestamp = new Regex("%timestamp%", RegexOptions.IgnoreCase);
		private static readonly Regex gameName = new Regex("%name%", RegexOptions.IgnoreCase);

		private string NormalizePath(string path, string extension, Func<GameInfo, string> resolver)
		{
			if (!(path?.ToLower().EndsWith(extension.ToLower()) ?? false))
			{
				if (string.IsNullOrWhiteSpace(path))
					path = $"{resolver(Global.Game).Replace(Global.Game.Name, "")}\\%timestamp%%name%{extension}";
				else
					path += extension;
			}
			path = timestamp.Replace(path, DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ssZ"));
			path = gameName.Replace(path, Global.Game.Name);
			return path;
		}

		public void SaveState(string path) => GlobalWin.MainForm.SaveState(NormalizePath(path, ".State", PathManager.SaveStatePrefix), path, true, true);

		public void SaveScreenshot(string path) => GlobalWin.MainForm.TakeScreenshot(NormalizePath(path, ".png", PathManager.ScreenshotPrefix));

		public void StopDrawing() => Global.Config.DispSpeedupFeatures = 0;

		public void StartDrawing() => Global.Config.DispSpeedupFeatures = 2;

		public void ClearScreen(string color) => GlobalWin.DisplayManager.Blank(string.IsNullOrWhiteSpace(color) ? null as Color? : Color.FromArgb(int.Parse(color, System.Globalization.NumberStyles.HexNumber)));
		
		public void Stall(string color)
		{
			Pause();
			StopDrawing();
			ClearScreen(color);
		}

		public void Resume()
		{
			StartDrawing();
			Play();
		}

		private void ToggleSound(bool soundEnabled)
		{
			Global.Config.SoundEnabled = soundEnabled;
			GlobalWin.Sound.StopSound();
			GlobalWin.Sound.StartSound();
		}

		public void Mute() => ToggleSound(false);
		public void Unmute() => ToggleSound(true);
	}
}
