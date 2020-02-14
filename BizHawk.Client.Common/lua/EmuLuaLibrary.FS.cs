using NLua;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BizHawk.Client.Common.lua
{
	public sealed class FSEmuLuaLibrary : LuaLibraryBase
	{
		public FSEmuLuaLibrary(Lua lua)
	: base(lua) { }

		public FSEmuLuaLibrary(Lua lua, Action<string> logOutputCallback)
			: base(lua, logOutputCallback) { }

		public override string Name { get { return "fs"; } }

		[LuaMethod(
			"getfiles",
			"Loads a lua table of files in a given directory. Can be given a search string as well."
		)]
		public LuaTable GetFiles(string path = ".", string search = null) => (string.IsNullOrWhiteSpace(search) ? Directory.GetFiles(path) : Directory.GetFiles(path, search)).ToLuaTable(Lua);
	}
}
