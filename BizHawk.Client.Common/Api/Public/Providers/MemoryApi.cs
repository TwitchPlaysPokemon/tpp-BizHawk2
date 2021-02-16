using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using BizHawk.Emulation.Common;
using BizHawk.Emulation.Common.IEmulatorExtensions;
using static BizHawk.Client.Common.Api.Public.HexHelpers;

namespace BizHawk.Client.Common.Api.Public
{
	public class MemoryApi : ApiProvider
	{

		#region Injected Dependencies
		[RequiredService]
		private IEmulator Emulator { get; set; }

		[OptionalService]
		private IDebuggable DebuggableCore { get; set; }

		[OptionalService]
		private IDisassemblable DisassemblableCore { get; set; }

		[OptionalService]
		private IMemoryDomains MemoryDomainCore { get; set; }

		[OptionalService]
		private IMemoryDomains Domains { get; set; }
		#endregion

		private AddressResolver resolver = new AddressResolver();

		public MemoryApi()
		{
			constructedCommands.Add(new ApiCommand("ReadByte", WrapMemoryCall((a, d) => (int)ReadUnsignedByte(a, d)), DocParams.ReadParams, "Reads the byte at Address"));
			constructedCommands.Add(new ApiCommand("WriteByte", WrapMemoryCall((a, v, d) => WriteUnsignedByte(a, (uint)v, d)), DocParams.WriteParams, "Writes the byte Value at Address"));

			void ByteSizeCommands(int bytes)
			{
				constructedCommands.Add(new ApiCommand($"ReadS{bytes * 8}BE", WrapMemoryCall((a, d) => ReadSignedBig(a, bytes, d), bytes), DocParams.ReadParams, $"Reads the signed {bytes * 8}-bit big-endian value at Address"));
				constructedCommands.Add(new ApiCommand($"ReadS{bytes * 8}LE", WrapMemoryCall((a, d) => ReadSignedLittle(a, bytes, d), bytes), DocParams.ReadParams, $"Reads the signed {bytes * 8}-bit little-endian value at Address"));
				constructedCommands.Add(new ApiCommand($"ReadU{bytes * 8}BE", WrapMemoryCall((a, d) => (int)ReadUnsignedBig(a, bytes, d), bytes), DocParams.ReadParams, $"Reads the unsigned {bytes * 8}-bit big-endian value at Address"));
				constructedCommands.Add(new ApiCommand($"ReadU{bytes * 8}LE", WrapMemoryCall((a, d) => (int)ReadUnsignedLittle(a, bytes, d), bytes), DocParams.ReadParams, $"Reads the unsigned {bytes * 8}-bit little-endian value at Address"));
				constructedCommands.Add(new ApiCommand($"WriteS{bytes * 8}BE", WrapMemoryCall((a, v, d) => WriteSignedBig(a, v, bytes, d)), DocParams.WriteParams, $"Writes Value as a signed {bytes * 8}-bit big-endian integer at Address"));
				constructedCommands.Add(new ApiCommand($"WriteS{bytes * 8}LE", WrapMemoryCall((a, v, d) => WriteSignedLittle(a, v, bytes, d)), DocParams.WriteParams, $"Writes Value as a signed, {bytes * 8}-bit little-endian integer at Address"));
				constructedCommands.Add(new ApiCommand($"WriteU{bytes * 8}BE", WrapMemoryCall((a, v, d) => WriteUnsignedBig(a, (uint)v, bytes, d)), DocParams.WriteParams, $"Writes Value as an unsigned, {bytes * 8}-bit big-endian integer at Address"));
				constructedCommands.Add(new ApiCommand($"WriteU{bytes * 8}LE", WrapMemoryCall((a, v, d) => WriteUnsignedLittle(a, (uint)v, bytes, d)), DocParams.WriteParams, $"Writes Value as an unsigned {bytes * 8}-bit little-endian integer at Address"));
			}
			for (var b = 2; b <= 4; ByteSizeCommands(b++)) ; //16, 24, and 32-bit commands

			constructedCommands.Add(new ApiCommand("ReadByteRange", WrapMultiMemoryCall(ReadRegion), DocParams.ReadRange, "Reads Length bytes of memory starting at Address"));
			constructedCommands.Add(new ApiCommand("WriteByteRange", WrapMemoryCall(WriteRegion), DocParams.WriteRange, "Writes Data to memory starting at Address"));

			constructedCommands.Add(new ApiCommand("CheckFlag", WrapMemoryCall((a, f, d) => CheckFlag(a, f, d)), DocParams.FlagsParams, "Checks the value of Flag in flags starting at Address"));
			constructedCommands.Add(new ApiCommand("SetFlag", WrapMemoryCall((a, f, d) => SetFlag(a, f, d)), DocParams.FlagsParams, "Sets the Flag in flags starting at Address"));
			constructedCommands.Add(new ApiCommand("ClearFlag", WrapMemoryCall((a, f, d) => ClearFlag(a, f, d)), DocParams.FlagsParams, "Clears the Flag in flags starting at Address"));

			constructedCommands.Add(new ApiCommand("HashByteRange", WrapMemoryCall(HashRegion), DocParams.ReadRange, "Calculates the SHA256 hash of Length bytes of memory starting at Address"));

			constructedCommands.Add(new ApiCommand("Disassemble", WrapMemoryCall((a, d) => Disassemble((uint)a, d)), DocParams.ReadParams, "Generates a disassembly of the instruction at Address"));

			constructedCommands.Add(new ApiCommand("MemoryDomains", (a, d) => string.Join("\n", DomainList?.Select(m => m.Name)), new List<ApiParameter>(), "Lists available memory domains for the current core."));
			constructedCommands.Add(new ApiCommand("SetDefaultMemoryDomain", (a, d) => _UseMemoryDomain(a.FirstOrDefault() ?? d), new List<ApiParameter>() { new ApiParameter("Domain", "string") }, "Sets which memory domain gets used when no domain is provided"));

			constructedCommands.Add(new ApiCommand("OnMemoryRead", WrapMemoryEvent(MemoryCallbackType.Read, SetMemoryEvent), DocParams.EventParams, "Blocks emulation and connects to the provided CallbackUrl when Address (Length bytes) is read from. System Bus only. Returns the Name of the callback in case you wish to remove it later."));
			constructedCommands.Add(new ApiCommand("OnMemoryWrite", WrapMemoryEvent(MemoryCallbackType.Write, SetMemoryEvent), DocParams.EventParams, "Blocks emulation and connects to the provided CallbackUrl when Address (Length bytes) is written to. System Bus only. Returns the Name of the callback in case you wish to remove it later."));
			constructedCommands.Add(new ApiCommand("OnMemoryExecute", WrapMemoryEvent(MemoryCallbackType.Execute, SetMemoryEvent), DocParams.EventParams, "Blocks emulation and connects to the provided CallbackUrl when Address (Length bytes) is executed. System Bus only. Returns the Name of the callback in case you wish to remove it later."));

			constructedCommands.Add(new ApiCommand("OnMemoryReadIfValue", WrapConditionalMemoryEvent(MemoryCallbackType.Read, SetMemoryEvent), DocParams.ConditionalEventParams, "Blocks emulation and connects to the provided CallbackUrl when Address1 (Length bytes) is read from, as long as Address2 is Value. System Bus only. Returns the Name of the callback in case you wish to remove it later."));
			constructedCommands.Add(new ApiCommand("OnMemoryWriteIfValue", WrapConditionalMemoryEvent(MemoryCallbackType.Write, SetMemoryEvent), DocParams.ConditionalEventParams, "Blocks emulation and connects to the provided CallbackUrl when Address1 (Length bytes) is written to, as long as Address2 is Value. System Bus only. Returns the Name of the callback in case you wish to remove it later."));
			constructedCommands.Add(new ApiCommand("OnMemoryExecuteIfValue", WrapConditionalMemoryEvent(MemoryCallbackType.Execute, SetMemoryEvent), DocParams.ConditionalEventParams, "Blocks emulation and connects to the provided CallbackUrl when Address1 (Length bytes) is executed, as long as Address2 is Value. System Bus only. Returns the Name of the callback in case you wish to remove it later."));

			constructedCommands.Add(new ApiCommand("RemoveMemoryCallback", WrapVoidDomain(RemoveMemoryEvent), new List<ApiParameter> { DocParams.EventName }, "Removes the memory callback, looking it up by its given Name."));
		}

		private static class DocParams
		{
			public static ApiParameter Address = new ApiParameter("Address", "address");
			public static ApiParameter Value = new ApiParameter("Value");
			public static ApiParameter Length = new ApiParameter("Length");
			public static ApiParameter Flag = new ApiParameter("Flag");
			public static ApiParameter Domain = new ApiParameter("Domain", "string", true, true);
			public static ApiParameter Data = new ApiParameter("Data", "bytes");
			public static ApiParameter Callback = new ApiParameter("CallbackUrl", "url");
			public static ApiParameter EventName = new ApiParameter("Name", "string", true, true);

			public static List<ApiParameter> ReadParams = new List<ApiParameter>() { Domain, Address };
			public static List<ApiParameter> WriteParams = new List<ApiParameter>() { Domain, Address, Value };
			public static List<ApiParameter> FlagsParams = new List<ApiParameter>() { Domain, Address, Flag };
			public static List<ApiParameter> ReadRange = new List<ApiParameter>() { Domain, Address, Length };
			public static List<ApiParameter> WriteRange = new List<ApiParameter>() { Domain, Address, Data };
			public static List<ApiParameter> EventParams = new List<ApiParameter>() { EventName, Address, Length, Callback };
			public static List<ApiParameter> ConditionalEventParams = new List<ApiParameter>() { EventName, Address, Length, Address, Value, Callback };
		}

		private List<ApiCommand> constructedCommands = new List<ApiCommand>();

		public override IEnumerable<ApiCommand> Commands => constructedCommands;

		private uint ResolveAddress(string hex, string domainName)
		{
			try
			{
				return resolver.ResolveAddress(hex, Domain(domainName));
			}
			catch (Exception e)
			{
				if (e is ApiError)
					throw;
				throw new ApiError($"Could not parse \"{hex}\" to a memory address");
			}
		}

		private int GetAddr(IEnumerable<string> args, string domain, int offset = 0) => ParseRequired(args, offset, hex => (int)ResolveAddress(hex?.ToUpper(), domain), "Address");
		private int GetValue(IEnumerable<string> args, int offset = 1) => ParseRequired(args, offset, hex => int.Parse(hex?.ToUpper(), System.Globalization.NumberStyles.HexNumber), "Value");
		private int GetLength(IEnumerable<string> args, int offset = 1) => ParseRequired(args, offset, hex => int.Parse(hex?.ToUpper(), System.Globalization.NumberStyles.HexNumber), "Length");
		private byte[] GetData(IEnumerable<string> args, int offset = 1) => ParseRequired(args, offset, HexStringToBytes, "Data");

		// Reads
		private Func<IEnumerable<string>, string, string> WrapMemoryCall(Func<int, string, int> innerCall, int bytesOut = 1) => (IEnumerable<string> args, string domain) => innerCall(GetAddr(args, domain), domain).ToString($"X{bytesOut * 2}");
		// ReadRange
		private Func<IEnumerable<string>, string, string> WrapMemoryCall(Func<int, int, string, byte[]> innerCall) => (IEnumerable<string> args, string domain) => BytesToHexString(innerCall(GetAddr(args, domain), GetLength(args), domain));
		// Writes
		private Func<IEnumerable<string>, string, string> WrapMemoryCall(Action<int, int, string> innerCall) => (IEnumerable<string> args, string domain) =>
		{
			innerCall(GetAddr(args, domain), GetValue(args), domain);
			return null;
		};
		// WriteRange
		private Func<IEnumerable<string>, string, string> WrapMemoryCall(Action<int, byte[], string> innerCall) => (IEnumerable<string> args, string domain) =>
		{
			innerCall(GetAddr(args, domain), GetData(args), domain);
			return null;
		};
		// Disassemble
		private Func<IEnumerable<string>, string, string> WrapMemoryCall(Func<int, string, string> innerCall) => (IEnumerable<string> args, string domain) => innerCall(GetAddr(args, domain), domain);
		// MemoryEvents
		private Func<IEnumerable<string>, string, string> WrapMemoryEvent(MemoryCallbackType type, Func<MemoryCallbackType, string, uint, uint, string, uint?, uint, string, string> innerCall)
			=> (IEnumerable<string> args, string domain)
			=> innerCall(type, string.Join("/", args.Skip(2)), (uint)GetAddr(args, CurrentDomain.Name), (uint)GetLength(args), domain, null, 0, null);
		private Func<IEnumerable<string>, string, string> WrapConditionalMemoryEvent(MemoryCallbackType type, Func<MemoryCallbackType, string, uint, uint, string, uint?, uint, string, string> innerCall)
			=> (IEnumerable<string> args, string domain)
			=> innerCall(type, string.Join("/", args.Skip(4)), (uint)GetAddr(args, CurrentDomain.Name), (uint)GetLength(args), domain, (uint)GetAddr(args, CurrentDomain.Name, 2), (uint)GetValue(args, 3), null);
		// ReadRangeMultiple
		private Func<IEnumerable<string>, string, string> WrapMultiMemoryCall(Func<int, int, string, byte[]> innerCall) => (IEnumerable<string> args, string domain) => string.Join("",
			//Enumerable.Zip(args, args.Skip(1), (addr, len) => new string[] { addr, len })
			args.Select((val, i) => new { val, i }).GroupBy(v => v.i / 2).Select(g => g.Select(v => v.val))
			.Select(a => BytesToHexString(innerCall(GetAddr(a, domain), GetLength(a), domain))));
		// CheckFlag
		private Func<IEnumerable<string>, string, string> WrapMemoryCall(Func<int, int, string, int> innerCall, int bytesOut = 1) => (IEnumerable<string> args, string domain) => innerCall(GetAddr(args, domain), GetValue(args), domain).ToString($"X{bytesOut * 2}");

		private static Func<IEnumerable<string>, string, string> WrapVoidDomain(Action<string> innerCall) => (IEnumerable<string> args, string domain) =>
		{
			innerCall(domain ?? string.Join("/", args));
			return null;
		};

		private void _UseMemoryDomain(string domain) => _currentMemoryDomain = Domain(domain);


		private byte[] ReadRegion(int addr, int count, string domain = null)
		{
			var d = Domain(domain, addr, count);
			byte[] data = new byte[count];
			for (int i = 0; i < count; i++)
			{
				data[i] = d.PeekByte(addr + i);
			}
			return data;
		}

		private void WriteRegion(int addr, byte[] data, string domain = null)
		{
			var d = Domain(domain, addr, data.Length);
			if (!d.CanPoke())
			{
				throw new ApiError($"The domain {d.Name} is not writable");
			}
			for (int i = 0; i < data.Length; i++)
			{
				d.PokeByte(addr + i, data[i]);
			}
		}

		private byte[] HashRegion(int addr, int count, string domain = null)
		{
			using (var hasher = System.Security.Cryptography.SHA256.Create())
			{
				return hasher.ComputeHash(ReadRegion(addr, count, domain));
			}
		}

		private string Disassemble(uint address, string domainName = "")
		{
			if (DisassemblableCore == null)
			{
				throw new ApiError($"{Emulator.Attributes().CoreName} does not yet implement disassembly");
			}

			var d = DisassemblableCore.Disassemble(Domain(domainName), address, out int byteLength);
			return $"{d}\t({byteLength} bytes)";
		}

		private string SetMemoryEvent(MemoryCallbackType type, string callback, uint address, uint bytes, string name, uint? checkAddress = null, uint checkValue = 0, string domain = "System Bus")
		{
			domain = NormalizeDomain(domain);
			try
			{
				if (DebuggableCore?.MemoryCallbacksAvailable() == true
					&& (type != MemoryCallbackType.Execute || DebuggableCore.MemoryCallbacks.ExecuteCallbacksAvailable))
				{
					name = name ?? callback;
					
					uint mask = 0;
					for (var i = 0; i < bytes; i++)
						mask |= (uint)(0xFF << (i * 8));

					if (!HasDomain(domain))
					{
						throw new ApiError($"{Emulator.Attributes().CoreName} does not support memory callbacks on the domain {domain}");
					}
					HttpClient client = new HttpClient();
					DebuggableCore.MemoryCallbacks.Add(new MemoryCallback(
						domain,
						type,
						name,
						(uint address, uint value, uint flags) =>
						{
							if (checkAddress != null && ReadUnsignedByte((int)checkAddress, domain) == checkValue)
							{
								try
								{
									client.GetAsync(callback).Result.ToString();
								}
								catch { }
							}
						},
						address,
						mask
					));
					return name;
				}
				// fall through
			}
			catch (NotImplementedException) { }
			if (type == MemoryCallbackType.Execute)
				throw new ApiError($"{Emulator.Attributes().CoreName} does not support memory execute callbacks.");
			throw new ApiError($"{Emulator.Attributes().CoreName} does not support memory callbacks.");
		}

		private int FlagByte(int flagsAddr, int flag) => flagsAddr + (flag / 8);
		private uint FlagMask(int flag) => (uint)(1 << flag % 8);
		private uint ReadFlagByte(int flagsAddr, int flag, string domain = null) => ReadUnsignedByte(FlagByte(flagsAddr, flag), domain);

		private int CheckFlag(int flagsAddr, int flag, string domain = null) => ((ReadFlagByte(flagsAddr, flag, domain) & FlagMask(flag)) > 0) ? 1 : 0;
		private void SetFlag(int flagsAddr, int flag, string domain = null) => WriteUnsignedByte(FlagByte(flagsAddr, flag), ReadFlagByte(flagsAddr, flag, domain) | FlagMask(flag), domain);
		private void ClearFlag(int flagsAddr, int flag, string domain = null) => WriteUnsignedByte(FlagByte(flagsAddr, flag), ReadFlagByte(flagsAddr, flag, domain) & ~FlagMask(flag), domain);

		private void RemoveMemoryEvent(string name) => DebuggableCore.MemoryCallbacks.Remove(DebuggableCore.MemoryCallbacks.FirstOrDefault(c => c.Name == name).Callback);

		private MemoryDomain _currentMemoryDomain;

		public override void Update()
		{
			_currentMemoryDomain = null; //release reference to old memory
			base.Update();
		}

		private MemoryDomain CurrentDomain => _currentMemoryDomain ?? (_currentMemoryDomain = DomainList.HasSystemBus ? DomainList.SystemBus : DomainList.MainMemory);
		private IMemoryDomains DomainList => MemoryDomainCore ?? throw new ApiError($"{Emulator.Attributes().CoreName} does not implement memory domains");
		private string FixDomainNameCase(string domain) => DomainList.Where(d => d.Name.ToLower() == domain?.ToLower()).FirstOrDefault()?.Name;
		public MemoryDomain VerifyMemoryDomain(string domain) => DomainList[FixDomainNameCase(domain)] ?? throw new ApiError($"{Emulator.Attributes().CoreName} does not have memory domain \"{domain}\"");

		private MemoryDomain Domain(string domainName = null, int addr = 0, int bytes = 1)
		{
			var domain = string.IsNullOrEmpty(domainName) ? CurrentDomain : VerifyMemoryDomain(domainName);
			if (addr >= domain.Size)
			{
				throw new ApiError($"Requested address {addr.ToString("X")} is outside of memory domain {domain.Name}'s range of {domain.Size.ToString("X")}");
			}
			if (addr < 0)
			{
				throw new ApiError($"Requested address {addr.ToString("X")} is negative.");
			}
			if (bytes < 1)
			{
				throw new ApiError($"Requested bytes {bytes.ToString("X")} is less than 1.");
			}
			if (addr + bytes > domain.Size)
			{
				throw new ApiError($"Requested address {(addr).ToString("X")} + {(bytes).ToString("X")} bytes is outside of memory domain {domain.Name}'s range of {domain.Size.ToString("X")}");
			}
			return domain;
		}

		private uint ReadUnsignedByte(int addr, string domain = null) => Domain(domain, addr).PeekByte(addr);

		private void WriteUnsignedByte(int addr, uint v, string domain = null)
		{
			var d = Domain(domain, addr);
			if (d.CanPoke())
			{
				d.PokeByte(addr, (byte)v);
			}
			else
			{
				throw new ApiError($"The domain {d.Name} is not writable");
			}
		}

		private static int ToSigned(uint u, int size)
		{
			var s = (int)u;
			s <<= 8 * (4 - size);
			s >>= 8 * (4 - size);
			return s;
		}

		private uint ReadUnsignedLittle(int addr, int size, string domain = null)
		{
			uint v = 0;
			for (var i = 0; i < size; ++i)
			{
				v |= ReadUnsignedByte(addr + i, domain) << (8 * i);
			}

			return v;
		}

		private uint ReadUnsignedBig(int addr, int size, string domain = null)
		{
			uint v = 0;
			for (var i = 0; i < size; ++i)
			{
				v |= ReadUnsignedByte(addr + i, domain) << (8 * (size - 1 - i));
			}

			return v;
		}

		private void WriteUnsignedLittle(int addr, uint v, int size, string domain = null)
		{
			for (var i = 0; i < size; ++i)
			{
				WriteUnsignedByte(addr + i, (v >> (8 * i)) & 0xFF, domain);
			}
		}

		private void WriteUnsignedBig(int addr, uint v, int size, string domain = null)
		{
			for (var i = 0; i < size; ++i)
			{
				WriteUnsignedByte(addr + i, (v >> (8 * (size - 1 - i))) & 0xFF, domain);
			}
		}

		private int ReadSignedLittle(int addr, int size, string domain = null) => ToSigned(ReadUnsignedLittle(addr, size, domain), size);

		private int ReadSignedBig(int addr, int size, string domain = null) => ToSigned(ReadUnsignedBig(addr, size, domain), size);

		private void WriteSignedLittle(int addr, int v, int size, string domain = null) => WriteUnsignedLittle(addr, (uint)v, size, domain);

		private void WriteSignedBig(int addr, int v, int size, string domain = null) => WriteUnsignedBig(addr, (uint)v, size, domain);

		private string NormalizeDomain(string scope)
		{
			if (string.IsNullOrWhiteSpace(scope))
			{
				if (Domains != null && Domains.HasSystemBus)
				{
					scope = Domains.SystemBus.Name;
				}
				else
				{
					scope = DebuggableCore.MemoryCallbacks.AvailableScopes.First();
				}
			}

			return scope;
		}
		private bool HasDomain(string scope)
		{
			return string.IsNullOrWhiteSpace(scope) || DebuggableCore.MemoryCallbacks.AvailableScopes.Contains(scope);
		}
	}
}
