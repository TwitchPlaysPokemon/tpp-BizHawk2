﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ComponentModel;
using System.Windows.Forms;

using BizHawk.Client.ApiHawk;
using BizHawk.Client.Common;
using BizHawk.Client.EmuHawk.CoreExtensions;
using BizHawk.Common;
using BizHawk.Common.ReflectionExtensions;
using BizHawk.Emulation.Common;
using BizHawk.Emulation.Common.IEmulatorExtensions;

namespace BizHawk.Client.EmuHawk
{
	public class ToolManager
	{
		private readonly MainForm _owner;
		private readonly Config _config;
		private IExternalApiProvider _apiProvider;
		private IEmulator _emulator;

		// TODO: merge ToolHelper code where logical
		// For instance, add an IToolForm property called UsesCheats, so that a UpdateCheatRelatedTools() method can update all tools of this type
		// Also a UsesRam, and similar method
		private readonly List<IToolForm> _tools = new List<IToolForm>();

		/// <summary>
		/// Initializes a new instance of the <see cref="ToolManager"/> class.
		/// </summary>
		public ToolManager(MainForm owner, Config config, IEmulator emulator)
		{
			_owner = owner;
			_config = config;
			_emulator = emulator;
			_apiProvider = ApiManager.Restart(_emulator.ServiceProvider);
		}

		/// <summary>
		/// Loads the tool dialog T (T must implements <see cref="IToolForm"/>) , if it does not exist it will be created, if it is already open, it will be focused
		/// This method should be used only if you can't use the generic one
		/// </summary>
		/// <param name="toolType">Type of tool you want to load</param>
		/// <param name="focus">Define if the tool form has to get the focus or not (Default is true)</param>
		/// <returns>An instantiated <see cref="IToolForm"/></returns>
		/// <exception cref="ArgumentException">Raised if <paramref name="toolType"/> can't cast into IToolForm </exception>
		internal IToolForm Load(Type toolType, bool focus = true)
		{
			if (!typeof(IToolForm).IsAssignableFrom(toolType))
			{
				throw new ArgumentException($"Type {toolType.Name} does not implement {nameof(IToolForm)}.");
			}
			
			// The type[] in parameter is used to avoid an ambiguous name exception
			MethodInfo method = GetType().GetMethod("Load", new Type[] { typeof(bool) }).MakeGenericMethod(toolType);
			return (IToolForm)method.Invoke(this, new object[] { focus });
		}

		// If the form inherits ToolFormBase, it will set base properties such as Tools, Config, etc
		private void SetBaseProperties(IToolForm form)
		{
			if (form is ToolFormBase tool)
			{
				tool.Tools = this;
				tool.Config = _config;
				tool.MainForm = _owner;
			}
		}

		/// <summary>
		/// Loads the tool dialog T (T must implement <see cref="IToolForm"/>) , if it does not exist it will be created, if it is already open, it will be focused
		/// </summary>
		/// <typeparam name="T">Type of tool you want to load</typeparam>
		/// <param name="focus">Define if the tool form has to get the focus or not (Default is true)</param>
		/// <returns>An instantiated <see cref="IToolForm"/></returns>
		public T Load<T>(bool focus = true)
			where T : class, IToolForm
		{
			return Load<T>("", focus);
		}

		/// <summary>
		/// Loads the tool dialog T (T must implement <see cref="IToolForm"/>) , if it does not exist it will be created, if it is already open, it will be focused
		/// </summary>
		/// <typeparam name="T">Type of tool you want to load</typeparam>
		/// <param name="toolPath">Path to the .dll of the external tool</param>
		/// <param name="focus">Define if the tool form has to get the focus or not (Default is true)</param>
		/// <returns>An instantiated <see cref="IToolForm"/></returns>
		public T Load<T>(string toolPath, bool focus = true)
			where T : class, IToolForm
		{
			bool isExternal = typeof(T) == typeof(IExternalToolForm);

			if (!IsAvailable<T>() && !isExternal)
			{
				return null;
			}

			T existingTool;
			if (isExternal)
			{
				existingTool = (T)_tools.FirstOrDefault(t => t is T && t.GetType().Assembly.Location == toolPath);
			}
			else
			{
				existingTool = (T)_tools.FirstOrDefault(t => t is T);
			}

			if (existingTool != null)
			{
				if (existingTool.IsDisposed)
				{
					_tools.Remove(existingTool);
				}
				else
				{
					if (focus)
					{
						existingTool.Show();
						existingTool.Focus();
					}

					return existingTool;
				}
			}

			IToolForm newTool = CreateInstance<T>(toolPath);

			if (newTool == null)
			{
				return null;
			}

			if (newTool is Form form)
			{
				form.Owner = _owner;
			}

			if (isExternal)
			{
				ApiInjector.UpdateApis(_apiProvider, newTool);
			}

			ServiceInjector.UpdateServices(_emulator.ServiceProvider, newTool);
			SetBaseProperties(newTool);
			string toolType = typeof(T).ToString();

			// auto settings
			if (newTool is IToolFormAutoConfig tool)
			{
				if (!_config.CommonToolSettings.TryGetValue(toolType, out var settings))
				{
					settings = new ToolDialogSettings();
					_config.CommonToolSettings[toolType] = settings;
				}

				AttachSettingHooks(tool, settings);
			}

			// custom settings
			if (HasCustomConfig(newTool))
			{
				if (!_config.CustomToolSettings.TryGetValue(toolType, out var settings))
				{
					settings = new Dictionary<string, object>();
					_config.CustomToolSettings[toolType] = settings;
				}

				InstallCustomConfig(newTool, settings);
			}

			newTool.Restart();
			if (OSTailoredCode.IsUnixHost && newTool is RamSearch)
			{
				// the mono winforms implementation is buggy, skip to the return statement and call Show in MainForm instead
			}
			else
			{
				newTool.Show();
			}
			return (T)newTool;
		}

		public void AutoLoad()
		{
			var genericSettings = _config.CommonToolSettings
				.Where(kvp => kvp.Value.AutoLoad)
				.Select(kvp => kvp.Key);

			var customSettings = _config.CustomToolSettings
				.Where(list => list.Value.Any(kvp => kvp.Value is ToolDialogSettings settings && settings.AutoLoad))
				.Select(kvp => kvp.Key);

			var typeNames = genericSettings.Concat(customSettings);

			foreach (var typename in typeNames)
			{
				// this type resolution might not be sufficient.  more investigation is needed
				Type t = Type.GetType(typename);
				if (t == null)
				{
					Console.WriteLine("BENIGN: Couldn't find type {0}", typename);
				}
				else
				{
					Load(t, false);
				}
			}
		}

		private void RefreshSettings(Form form, ToolStripItemCollection menu, ToolDialogSettings settings, int idx)
		{
			((ToolStripMenuItem)menu[idx + 0]).Checked = settings.SaveWindowPosition;
			((ToolStripMenuItem)menu[idx + 1]).Checked = settings.TopMost;
			((ToolStripMenuItem)menu[idx + 2]).Checked = settings.FloatingWindow;
			((ToolStripMenuItem)menu[idx + 3]).Checked = settings.AutoLoad;

			form.TopMost = settings.TopMost;

			// do we need to do this OnShown() as well?
			form.Owner = settings.FloatingWindow ? null : _owner;
		}

		private void AttachSettingHooks(IToolFormAutoConfig tool, ToolDialogSettings settings)
		{
			var form = (Form)tool;
			ToolStripItemCollection dest = null;
			var oldSize = form.Size; // this should be the right time to grab this size
			foreach (Control c in form.Controls)
			{
				if (c is MenuStrip ms)
				{
					foreach (ToolStripMenuItem submenu in ms.Items)
					{
						if (submenu.Text.Contains("Settings"))
						{
							dest = submenu.DropDownItems;
							dest.Add(new ToolStripSeparator());
							break;
						}
					}

					if (dest == null)
					{
						var submenu = new ToolStripMenuItem("&Settings");
						ms.Items.Add(submenu);
						dest = submenu.DropDownItems;
					}

					break;
				}
			}

			if (dest == null)
			{
				throw new InvalidOperationException($"{nameof(IToolFormAutoConfig)} must have menu to bind to!");
			}

			int idx = dest.Count;

			dest.Add("Save Window &Position");
			dest.Add("Stay on &Top");
			dest.Add("&Float from Parent");
			dest.Add("&Autoload with EmuHawk");
			dest.Add("Restore &Defaults");

			RefreshSettings(form, dest, settings, idx);

			if (settings.UseWindowPosition && IsOnScreen(settings.TopLeft))
			{
				form.StartPosition = FormStartPosition.Manual;
				form.Location = settings.WindowPosition;
			}

			if (settings.UseWindowSize)
			{
				if (form.FormBorderStyle == FormBorderStyle.Sizable || form.FormBorderStyle == FormBorderStyle.SizableToolWindow)
				{
					form.Size = settings.WindowSize;
				}
			}

			form.FormClosing += (o, e) =>
			{
				if (form.WindowState == FormWindowState.Normal)
				{
					settings.Wndx = form.Location.X;
					settings.Wndy = form.Location.Y;
					if (settings.Wndx < 0)
					{
						settings.Wndx = 0;
					}

					if (settings.Wndy < 0)
					{
						settings.Wndy = 0;
					}

					settings.Width = form.Right - form.Left; // why not form.Size.Width?
					settings.Height = form.Bottom - form.Top;
				}
			};

			dest[idx + 0].Click += (o, e) =>
			{
				bool val = !((ToolStripMenuItem)o).Checked;
				settings.SaveWindowPosition = val;
				((ToolStripMenuItem)o).Checked = val;
			};
			dest[idx + 1].Click += (o, e) =>
			{
				bool val = !((ToolStripMenuItem)o).Checked;
				settings.TopMost = val;
				((ToolStripMenuItem)o).Checked = val;
				form.TopMost = val;
			};
			dest[idx + 2].Click += (o, e) =>
			{
				bool val = !((ToolStripMenuItem)o).Checked;
				settings.FloatingWindow = val;
				((ToolStripMenuItem)o).Checked = val;
				form.Owner = val ? null : _owner;
			};
			dest[idx + 3].Click += (o, e) =>
			{
				bool val = !((ToolStripMenuItem)o).Checked;
				settings.AutoLoad = val;
				((ToolStripMenuItem)o).Checked = val;
			};
			dest[idx + 4].Click += (o, e) =>
			{
				settings.RestoreDefaults();
				RefreshSettings(form, dest, settings, idx);
				form.Size = oldSize;

				form.GetType()
					.GetMethodsWithAttrib(typeof(RestoreDefaultsAttribute))
					.FirstOrDefault()
					?.Invoke(form, new object[0]);
			};
		}

		private static bool HasCustomConfig(IToolForm tool)
		{
			return tool.GetType().GetPropertiesWithAttrib(typeof(ConfigPersistAttribute)).Any();
		}

		private static void InstallCustomConfig(IToolForm tool, Dictionary<string, object> data)
		{
			Type type = tool.GetType();
			var props = type.GetPropertiesWithAttrib(typeof(ConfigPersistAttribute)).ToList();
			if (props.Count == 0)
			{
				return;
			}

			foreach (var prop in props)
			{
				if (data.TryGetValue(prop.Name, out var val))
				{
					if (val is string str && prop.PropertyType != typeof(string))
					{
						// if a type has a TypeConverter, and that converter can convert to string,
						// that will be used in place of object markup by JSON.NET

						// but that doesn't work with $type metadata, and JSON.NET fails to fall
						// back on regular object serialization when needed.  so try to undo a TypeConverter
						// operation here
						var converter = TypeDescriptor.GetConverter(prop.PropertyType);
						val = converter.ConvertFromString(null, CultureInfo.InvariantCulture, str);
					}
					else if (!(val is bool) && prop.PropertyType.IsPrimitive)
					{
						// numeric constants are similarly hosed
						val = Convert.ChangeType(val, prop.PropertyType, CultureInfo.InvariantCulture);
					}

					prop.SetValue(tool, val, null);
				}
			}

			((Form)tool).FormClosing += (o, e) => SaveCustomConfig(tool, data, props);
		}

		private static void SaveCustomConfig(IToolForm tool, Dictionary<string, object> data, List<PropertyInfo> props)
		{
			data.Clear();
			foreach (var prop in props)
			{
				data.Add(prop.Name, prop.GetValue(tool, BindingFlags.GetProperty, Type.DefaultBinder, null, CultureInfo.InvariantCulture));
			}
		}

		/// <summary>
		/// Determines whether a given IToolForm is already loaded
		/// </summary>
		/// <typeparam name="T">Type of tool to check</typeparam>
		public bool IsLoaded<T>() where T : IToolForm
		{
			var existingTool = _tools.FirstOrDefault(t => t is T);
			if (existingTool != null)
			{
				return !existingTool.IsDisposed;
			}

			return false;
		}

		public bool IsOnScreen(Point topLeft)
		{
			return Screen.AllScreens.Any(
				screen => screen.WorkingArea.Contains(topLeft));
		}

		/// <summary>
		/// Returns true if an instance of T exists
		/// </summary>
		/// <typeparam name="T">Type of tool to check</typeparam>
		public bool Has<T>() where T : IToolForm
		{
			return _tools.Any(t => t is T && !t.IsDisposed);
		}

		/// <summary>
		/// Gets the instance of T, or creates and returns a new instance
		/// </summary>
		/// <typeparam name="T">Type of tool to get</typeparam>
		public IToolForm Get<T>() where T : class, IToolForm
		{
			return Load<T>(false);
		}

		public IEnumerable<Type> AvailableTools => Assembly
			.GetAssembly(typeof(IToolForm))
			.GetTypes()
			.Where(t => typeof(IToolForm).IsAssignableFrom(t))
			.Where(t => !t.IsInterface)
			.Where(IsAvailable);
		
		public void UpdateBefore()
		{
			var beforeList = _tools.Where(t => t.UpdateBefore);
			foreach (var tool in beforeList)
			{
				if (!tool.IsDisposed
					|| (tool is RamWatch && _config.DisplayRamWatch)) // RAM Watch hack, on screen display should run even if RAM Watch is closed
				{
					tool.UpdateValues();
				}
			}

			foreach (var tool in _tools)
			{
				if (!tool.IsDisposed)
				{
					tool.NewUpdate(ToolFormUpdateType.PreFrame);
				}
			}
		}

		public void UpdateAfter()
		{
			var afterList = _tools.Where(t => !t.UpdateBefore);
			foreach (var tool in afterList)
			{
				if (!tool.IsDisposed
					|| (tool is RamWatch && _config.DisplayRamWatch)) // RAM Watch hack, on screen display should run even if RAM Watch is closed
				{
					tool.UpdateValues();
				}
			}

			foreach (var tool in _tools)
			{
				if (!tool.IsDisposed)
				{
					tool.NewUpdate(ToolFormUpdateType.PostFrame);
				}
			}
		}

		/// <summary>
		/// Calls UpdateValues() on an instance of T, if it exists
		/// </summary>
		/// <typeparam name="T">Type of tool to update</typeparam>
		public void UpdateValues<T>() where T : IToolForm
		{
			var tool = _tools.FirstOrDefault(t => t is T);
			if (tool != null)
			{
				if (!tool.IsDisposed ||
					(tool is RamWatch && _config.DisplayRamWatch)) // RAM Watch hack, on screen display should run even if RAM Watch is closed
				{
					tool.UpdateValues();
				}
			}
		}

		public void Restart(IEmulator emulator)
		{
			_emulator = emulator;
			_apiProvider = ApiManager.Restart(_emulator.ServiceProvider);
			// If Cheat tool is loaded, restarting will restart the list too anyway
			if (!Has<Cheats>())
			{
				Global.CheatList.NewList(GenerateDefaultCheatFilename(), autosave: true);
			}

			var unavailable = new List<IToolForm>();

			foreach (var tool in _tools)
			{
				if (ServiceInjector.IsAvailable(_emulator.ServiceProvider, tool.GetType()))
				{
					ServiceInjector.UpdateServices(_emulator.ServiceProvider, tool);
					
					if ((tool.IsHandleCreated && !tool.IsDisposed) || tool is RamWatch) // Hack for RAM Watch - in display watches mode it wants to keep running even closed, it will handle disposed logic
					{
						if (tool is IExternalToolForm)
							ApiInjector.UpdateApis(_apiProvider, tool);
						tool.Restart();
					}
				}
				else
				{
					unavailable.Add(tool);
					ServiceInjector.ClearServices(tool); // the services of the old emulator core are no longer valid on the tool
				}
			}

			foreach (var tool in unavailable)
			{
				tool.Close();
				_tools.Remove(tool);
			}
		}

		/// <summary>
		/// Calls Restart() on an instance of T, if it exists
		/// </summary>
		/// <typeparam name="T">Type of tool to restart</typeparam>
		public void Restart<T>() where T : IToolForm
		{
			var tool = _tools.FirstOrDefault(t => t is T);
			tool?.Restart();
		}

		/// <summary>
		/// Runs AskSave on every tool dialog, false is returned if any tool returns false
		/// </summary>
		public bool AskSave()
		{
			if (_config.SuppressAskSave) // User has elected to not be nagged
			{
				return true;
			}

			return _tools
				.Select(tool => tool.AskSaveChanges())
				.All(result => result);
		}

		/// <summary>
		/// Calls AskSave() on an instance of T, if it exists, else returns true
		/// The caller should interpret false as cancel and will back out of the action that invokes this call
		/// </summary>
		/// <typeparam name="T">Type of tool</typeparam>
		public bool AskSave<T>() where T : IToolForm
		{
			if (_config.SuppressAskSave) // User has elected to not be nagged
			{
				return true;
			}

			var tool = _tools.FirstOrDefault(t => t is T);
			return tool != null && tool.AskSaveChanges();
		}

		/// <summary>
		/// If T exists, this call will close the tool, and remove it from memory
		/// </summary>
		/// <typeparam name="T">Type of tool to close</typeparam>
		public void Close<T>() where T : IToolForm
		{
			var tool = _tools.FirstOrDefault(t => t is T);
			if (tool != null)
			{
				tool.Close();
				_tools.Remove(tool);
			}
		}

		public void Close(Type toolType)
		{
			var tool = _tools.FirstOrDefault(toolType.IsInstanceOfType);

			if (tool != null)
			{
				tool.Close();
				_tools.Remove(tool);
			}
		}

		public void Close()
		{
			_tools.ForEach(t => t.Close());
			_tools.Clear();
		}

		/// <summary>
		/// Create a new instance of an IToolForm and return it
		/// </summary>
		/// <typeparam name="T">Type of tool you want to create</typeparam>
		/// <param name="dllPath">Path .dll for an external tool</param>
		/// <returns>New instance of an IToolForm</returns>
		private IToolForm CreateInstance<T>(string dllPath)
			where T : IToolForm
		{
			return CreateInstance(typeof(T), dllPath);
		}

		/// <summary>
		/// Create a new instance of an IToolForm and return it
		/// </summary>
		/// <param name="toolType">Type of tool you want to create</param>
		/// <param name="dllPath">Path dll for an external tool</param>
		/// <returns>New instance of an IToolForm</returns>
		private IToolForm CreateInstance(Type toolType, string dllPath)
		{
			IToolForm tool;

			// Specific case for custom tools
			// TODO: Use AppDomain in order to be able to unload the assembly
			// Hard stuff as we need a proxy object that inherit from MarshalByRefObject.
			if (toolType == typeof(IExternalToolForm))
			{
				if (MessageBox.Show(
					"Are you sure want to load this external tool?\r\nAccept ONLY if you trust the source and if you know what you're doing. In any other case, choose no.",
					"Confirm loading", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
				{
					try
					{
						tool = Activator.CreateInstanceFrom(dllPath, "BizHawk.Client.EmuHawk.CustomMainForm").Unwrap() as IExternalToolForm;
						if (tool == null)
						{
							MessageBox.Show($"It seems that the object CustomMainForm does not implement {nameof(IExternalToolForm)}. Please review the code.", "No, no, no. Wrong Way !", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
							return null;
						}
					}
					catch (MissingMethodException)
					{
						MessageBox.Show("It seems that the object CustomMainForm does not have a public default constructor. Please review the code.", "No, no, no. Wrong Way !", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
						return null;
					}
					catch (TypeLoadException)
					{
						MessageBox.Show("It seems that the object CustomMainForm does not exists. Please review the code.", "No, no, no. Wrong Way !", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
						return null;
					}
				}
				else
				{
					return null;
				}
			}
			else
			{
				tool = (IToolForm)Activator.CreateInstance(toolType);
			}

			// Add to our list of tools
			_tools.Add(tool);
			return tool;
		}

		public void UpdateToolsBefore(bool fromLua = false)
		{
			if (Has<LuaConsole>())
			{
				if (!fromLua)
				{
					LuaConsole.LuaImp.StartLuaDrawing();
				}
			}

			UpdateBefore();
		}

		public void UpdateToolsAfter(bool fromLua = false)
		{
			if (!fromLua && Has<LuaConsole>())
			{
				LuaConsole.ResumeScripts(true);
			}

			UpdateAfter();
			GlobalWin.API.NewFrame();

			if (Has<LuaConsole>())
			{
				if (!fromLua)
				{
					LuaConsole.LuaImp.EndLuaDrawing();
				}
			}
		}

		public void FastUpdateBefore()
		{
			var beforeList = _tools.Where(t => t.UpdateBefore);
			foreach (var tool in beforeList)
			{
				if (!tool.IsDisposed
					|| (tool is RamWatch && _config.DisplayRamWatch)) // RAM Watch hack, on screen display should run even if RAM Watch is closed
				{
					tool.FastUpdate();
				}
			}
		}

		public void FastUpdateAfter(bool fromLua = false)
		{
			if (!fromLua && _config.RunLuaDuringTurbo && Has<LuaConsole>())
			{
				LuaConsole.ResumeScripts(true);
			}

			GlobalWin.API.NewFrame();

			var afterList = _tools.Where(t => !t.UpdateBefore);
			foreach (var tool in afterList)
			{
				if (!tool.IsDisposed
					|| (tool is RamWatch && _config.DisplayRamWatch)) // RAM Watch hack, on screen display should run even if RAM Watch is closed
				{
					tool.FastUpdate();
				}
			}

			if (_config.RunLuaDuringTurbo && Has<LuaConsole>())
			{
				LuaConsole.LuaImp.EndLuaDrawing();
			}
		}

		private static readonly Lazy<List<string>> LazyAsmTypes = new Lazy<List<string>>(() =>
			Assembly.GetAssembly(typeof(ToolManager)) // Confining the search to only EmuHawk, for now at least, we may want to broaden for ApiHawk one day
				.GetTypes()
				.Select(t => t.AssemblyQualifiedName)
				.ToList());

		public bool IsAvailable(Type tool)
		{
			if (!ServiceInjector.IsAvailable(_emulator.ServiceProvider, tool)
				|| !LazyAsmTypes.Value.Contains(tool.AssemblyQualifiedName)) // not a tool
			{
				return false;
			}

			ToolAttribute attr = tool.GetCustomAttributes(false).OfType<ToolAttribute>().SingleOrDefault();
			if (attr == null)
			{
				return true; // no ToolAttribute on given type -> assumed all supported
			}

			var displayName = _emulator.DisplayName();
			var systemId = _emulator.SystemId;
			return !attr.UnsupportedCores.Contains(displayName) // not unsupported
				&& (!attr.SupportedSystems.Any() || attr.SupportedSystems.Contains(systemId)); // supported (no supported list -> assumed all supported)
		}

		public bool IsAvailable<T>() => IsAvailable(typeof(T));

		// Note: Referencing these properties creates an instance of the tool and persists it.  They should be referenced by type if this is not desired
		#region Tools

		private T GetTool<T>() where T : class, IToolForm, new()
		{
			T tool = _tools.OfType<T>().FirstOrDefault();
			if (tool != null)
			{
				if (!tool.IsDisposed)
				{
					return tool;
				}
				_tools.Remove(tool);
			}
			tool = new T();
			_tools.Add(tool);
			return tool;
		}

		public RamWatch RamWatch => GetTool<RamWatch>();

		public RamSearch RamSearch => GetTool<RamSearch>();

		public Cheats Cheats => GetTool<Cheats>();

		public HexEditor HexEditor => GetTool<HexEditor>();

		public VirtualpadTool VirtualPad => GetTool<VirtualpadTool>();

		public SNESGraphicsDebugger SNESGraphicsDebugger => GetTool<SNESGraphicsDebugger>();

		public LuaConsole LuaConsole => GetTool<LuaConsole>();

		public TAStudio TAStudio => GetTool<TAStudio>();

		#endregion

		#region Specialized Tool Loading Logic

		public void LoadRamWatch(bool loadDialog)
		{
			if (IsLoaded<RamWatch>())
			{
				return;
			}

			Load<RamWatch>();

			if (IsAvailable<RamWatch>()) // Just because we attempted to load it, doesn't mean it was, the current core may not have the correct dependencies
			{
				if (_config.RecentWatches.AutoLoad && !_config.RecentWatches.Empty)
				{
					RamWatch.LoadFileFromRecent(_config.RecentWatches.MostRecent);
				}

				if (!loadDialog)
				{
					Get<RamWatch>().Close();
				}
			}
		}

		public void LoadGameGenieEc()
		{
			if (IsAvailable<GameShark>())
			{
				Load<GameShark>();
			}
		}

		#endregion

		public string GenerateDefaultCheatFilename()
		{
			var pathEntry = _config.PathEntries[Global.Game.System, "Cheats"]
							?? _config.PathEntries[Global.Game.System, "Base"];

			var path = PathManager.MakeAbsolutePath(pathEntry.Path, Global.Game.System);

			var f = new FileInfo(path);
			if (f.Directory != null && f.Directory.Exists == false)
			{
				f.Directory.Create();
			}

			return Path.Combine(path, $"{PathManager.FilesystemSafeName(Global.Game)}.cht");
		}

		public void UpdateCheatRelatedTools(object sender, CheatCollection.CheatListEventArgs e)
		{
			if (!_emulator.HasMemoryDomains())
			{
				return;
			}

			UpdateValues<RamWatch>();
			UpdateValues<RamSearch>();
			UpdateValues<HexEditor>();

			if (Has<Cheats>())
			{
				Cheats.UpdateDialog();
			}

			_owner.UpdateCheatStatus();
		}
	}
}
