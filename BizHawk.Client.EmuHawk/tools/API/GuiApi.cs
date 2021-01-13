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
using System.Reflection;

namespace BizHawk.Client.EmuHawk.tools.Api
{
	class GuiApi : ApiProvider
	{
		[RequiredService]
		private IEmulator Emulator { get; set; }
		private ApiContainer APIs { get; set; }
		private IGui Gui => APIs.Gui;

		//Stolen from Lua library
		static ApiContainer InitApiHawkContainerInstance(IEmulatorServiceProvider sp, Action<string> logCallback)
		{
			var ctorParamTypes = new[] { typeof(Action<string>) };
			var ctorParams = new object[] { logCallback };
			var libDict = new Dictionary<Type, IExternalApi>();
			foreach (var api in Assembly.GetAssembly(typeof(EmuApi)).GetTypes()
				.Concat(Assembly.GetAssembly(typeof(ToolApi)).GetTypes())
				.Where(t => t.IsSealed && typeof(IExternalApi).IsAssignableFrom(t) && ServiceInjector.IsAvailable(sp, t)))
			{
				var ctorWithParams = api.GetConstructor(ctorParamTypes);
				var instance = (IExternalApi)(ctorWithParams == null ? Activator.CreateInstance(api) : ctorWithParams.Invoke(ctorParams));
				ServiceInjector.UpdateServices(sp, instance);
				libDict.Add(api, instance);
			}
			return new ApiContainer(libDict);
		}

		public override void Update()
		{
			if (Emulator != null)
				APIs = InitApiHawkContainerInstance(Emulator.ServiceProvider, (string message) => throw new Exception(message));
		}


		public override IEnumerable<ApiCommand> Commands => new List<ApiCommand>()
		{
			new ApiCommand("ClearCanvas", WrapGUI(ClearDrawing), new List<ApiParameter>(){SurfaceParam}, "Clears the canvas on the specified Surface"),
			new ApiCommand("CreateCanvas", WrapGUI(StartDrawing), new List<ApiParameter>(){SurfaceParam}, "Starts a new drawing canvas on the provided Surface (native if none is provided)"),
			new ApiCommand("DisplayCanvas", WrapGUI(FinishDrawing), new List<ApiParameter>(), "Finalizes the canvas on the current Surface and displays it"),
			new ApiCommand("DrawImage", WrapDrawImage(DrawImage), new List<ApiParameter>(){XParam, YParam, WidthParam,HeightParam,PathParam}, "Draws the image at Path onto the current canvas at (X, Y), optionally stretching/squishing to (Width, Height)"),
			new ApiCommand("GetDimensions", WrapGUI(GetDimensions), new List<ApiParameter>(), "Returns the width and height of the current drawing surface in pixels"),
		};


		private static ApiParameter SurfaceParam = new ApiParameter("Surface", "\"native\"|\"emu\"", isPrepend: true, optional: true);
		private static ApiParameter PathParam = new ApiParameter("Path", "string");
		private static ApiParameter XParam = new ApiParameter("X", "int(dec)");
		private static ApiParameter YParam = new ApiParameter("Y", "int(dec)");
		private static ApiParameter WidthParam = new ApiParameter("Width", "int(dec)", optional: true);
		private static ApiParameter HeightParam = new ApiParameter("Height", "int(dec)", optional: true);

		private Func<IEnumerable<string>, string> WrapGUI<T>(Func<T> innerCall) => (IEnumerable<string> args) =>
		{
			try
			{
				if (!Gui.HasGUISurface)
					return "Must create a canvas first";
				return JsonConvert.SerializeObject(innerCall());
			}
			catch (Exception e)
			{
				return e.Message;
			}
		};
		private Func<IEnumerable<string>, string, string> WrapGUI(Action<string> innerCall) => (IEnumerable<string> args, string surface) =>
		{
			try
			{
				innerCall(surface?.ToLower() ?? "native");
			}
			catch (Exception e)
			{
				return e.Message;
			}
			return null;
		};
		private Func<IEnumerable<string>, string, string> WrapDrawImage(Action<string, int, int, int?, int?> innerCall, bool fileMustExist = true, bool clearFirst = false) => (IEnumerable<string> args, string surface) =>
		{
			try
			{
				if (!Gui.HasGUISurface)
					return "Must create a canvas first";
				Queue<string> argsQueue = new Queue<string>(args);
				int? width = null;
				int? height = null;
				var valid = true;
				valid &= int.TryParse(argsQueue.Dequeue(), out int x);
				valid &= int.TryParse(argsQueue.Dequeue(), out int y);
				if (!valid)
					return "Missing X and Y coordinates (in decimal)";
				if (int.TryParse(argsQueue.Peek(), out int possibleWidth))
				{
					width = possibleWidth;
					argsQueue.Dequeue();
					valid = int.TryParse(argsQueue.Dequeue(), out int possibleHeight);
					if (!valid)
						return "If providing Width, must also provide Height";
					height = possibleHeight;
				}
				var path = string.Join("\\", argsQueue);
				if (fileMustExist && !File.Exists(path))
					return $"Could not find file: {path}";
				innerCall(path, x, y, width, height);
			}
			catch (Exception e)
			{
				return e.Message;
			}
			return null;
		};

		public Size GetDimensions() => Gui.DisplaySize;

		public void DrawImage(string path, int x, int y, int? width, int? height) => Gui.DrawImage(path, x, y, width, height, false);

		public void ClearDrawing(string surface)
		{
			Gui.DrawNew(surface, true);
			Gui.DrawNew(surface, true);
			Gui.DrawFinish();
		}

		public void StartDrawing(string surface) => Gui.DrawNew(surface, true);

		public void FinishDrawing(string surface) => Gui.DrawFinish();
	}
}
