﻿using System;

using NLua;

using BizHawk.Client.Common;

namespace BizHawk.Client.EmuHawk
{
	public sealed class SavestateLuaLibrary : DelegatingLuaLibraryEmu
	{
		public SavestateLuaLibrary(Lua lua)
			: base(lua) { }

		public SavestateLuaLibrary(Lua lua, Action<string> logOutputCallback)
			: base(lua, logOutputCallback) { }

		public override string Name => "savestate";

		[LuaMethodExample("savestate.load( \"C:\\state.bin\" );")]
		[LuaMethod("load", "Loads a savestate with the given path. If EmuHawk is deferring quicksaves, to TAStudio for example, that form will do what it likes (and the path is ignored).")]
		public void Load(string path, bool suppressOSD = false) => APIs.SaveState.Load(path, suppressOSD);

		[LuaMethodExample("savestate.loadslot( 7 );")]
		[LuaMethod("loadslot", "Loads the savestate at the given slot number (must be an integer between 0 and 9). If EmuHawk is deferring quicksaves, to TAStudio for example, that form will do what it likes with the slot number.")]
		public void LoadSlot(int slotNum, bool suppressOSD = false) => APIs.SaveState.LoadSlot(slotNum, suppressOSD);

		[LuaMethodExample("savestate.save( \"C:\\state.bin\" );")]
		[LuaMethod("save", "Saves a state at the given path. If EmuHawk is deferring quicksaves, to TAStudio for example, that form will do what it likes (and the path is ignored).")]
		public void Save(string path, bool suppressOSD = false) => APIs.SaveState.Save(path, suppressOSD);

		[LuaMethodExample("savestate.saveslot( 7 );")]
		[LuaMethod("saveslot", "Saves a state at the given save slot (must be an integer between 0 and 9). If EmuHawk is deferring quicksaves, to TAStudio for example, that form will do what it likes with the slot number.")]
		public void SaveSlot(int slotNum, bool suppressOSD = false) => APIs.SaveState.SaveSlot(slotNum, suppressOSD);
	}
}
