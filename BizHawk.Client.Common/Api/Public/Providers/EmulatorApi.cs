using System.Linq;
using System.Collections.Generic;
using BizHawk.Emulation.Common;
using BizHawk.Emulation.Common.IEmulatorExtensions;

namespace BizHawk.Client.Common.Api.Public.Providers
{
	class EmulatorApi : ApiProvider
	{

		#region Injected Dependencies
		[RequiredService]
		private IEmulator Emulator { get; set; }

		[OptionalService]
		private IDebuggable DebuggableCore { get; set; }


		[OptionalService]
		private IMemoryDomains MemoryDomains { get; set; }
		#endregion

		public override IEnumerable<ApiCommand> Commands => new List<ApiCommand>()
		{
			new ApiCommand("FrameCount", args=>Emulator.Frame.ToString(), new List<ApiParameter>(), "Returns the current frame count"),
			new ApiCommand("GetRegisters", args=>GetRegisters(), new List<ApiParameter>(), $"Returns the complete set of available flags and registers for the current core"),
			new ApiCommand("GetRegister", args=>GetRegister(args.FirstOrDefault() ?? throw new ApiError("Name was not provided")), new List<ApiParameter>(){ Name }, "Returns the value of a cpu register or flag specified by Name"),
			new ApiCommand("SetRegister", args=>SetRegister(args.FirstOrDefault() ?? throw new ApiError("Name was not provided"), args.ElementAtOrDefault(1) ?? throw new ApiError("Value was not provided")), new List<ApiParameter>(){ Name, Value }, "Sets the given register Name to the given Value"),
			new ApiCommand("GetROMName", args=>Global.Game?.Name ?? throw new ApiError("No ROM is loaded"), new List<ApiParameter>(), "Returns the name of the currently loaded ROM, if a ROM is loaded"),
			new ApiCommand("GetROMHash", args=>Global.Game?.Hash ?? throw new ApiError("No ROM is loaded"), new List<ApiParameter>(), "Returns the hash of the currently loaded ROM, if a ROM is loaded")
		};

		private static ApiParameter Name = new ApiParameter("Name", "string");
		private static ApiParameter Value = new ApiParameter("Value");

		private IDebuggable Debug => DebuggableCore ?? throw new ApiError($"{Emulator.Attributes().CoreName} does not expose its registers");

		private string GetRegisters() => string.Join("\n", Debug.GetCpuFlagsAndRegisters().Select(kvp => $"{kvp.Key}\t{kvp.Value.Value.ToString($"X{kvp.Value.BitSize / 4}")}"));

		private KeyValuePair<string, RegisterValue> GetRegisterByName(string registerName)
		{
			var registers = Debug.GetCpuFlagsAndRegisters();
			if (!registers.Any(kvp => kvp.Key.ToLower() == registerName.ToLower()))
			{
				throw new ApiError($"Register ${registerName} does not exist in {Emulator.Attributes().CoreName}");
			}
			return registers.First(kvp => kvp.Key.ToLower() == registerName.ToLower());
		}

		private string GetRegister(string registerName)
		{
			var register = GetRegisterByName(registerName).Value;
			return register.Value.ToString($"X{register.BitSize / 4}");
		}

		private string SetRegister(string registerName, string value)
		{
			var normalizedRegisterName = GetRegisterByName(registerName).Key;
			try
			{
				Debug.SetCpuRegister(normalizedRegisterName, int.Parse(value, System.Globalization.NumberStyles.HexNumber));
			}
			catch (System.NotImplementedException)
			{
				throw new ApiError($"{Emulator.Attributes().CoreName} does not support SetRegister");
			}
			catch
			{
				throw new ApiError($"Value {value} is invalid for register {normalizedRegisterName} or {normalizedRegisterName} cannot be written to");
			}
			return null;
		}
	}
}
