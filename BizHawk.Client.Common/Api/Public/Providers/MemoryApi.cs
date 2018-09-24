using System;
using System.Collections.Generic;
using System.Linq;
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
		private IDisassemblable DisassemblableCore { get; set; }

		[OptionalService]
		private IMemoryDomains MemoryDomainCore { get; set; }
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

			constructedCommands.Add(new ApiCommand("ReadByteRange", WrapMemoryCall(ReadRegion), DocParams.ReadRange, "Reads Length bytes of memory starting at Address"));
			constructedCommands.Add(new ApiCommand("WriteByteRange", WrapMemoryCall(WriteRegion), DocParams.WriteRange, "Writes Data to memory starting at Address"));

			constructedCommands.Add(new ApiCommand("HashByteRange", WrapMemoryCall(HashRegion), DocParams.ReadRange, "Calculates the SHA256 hash of Length bytes of memory starting at Address"));

			constructedCommands.Add(new ApiCommand("Disassemble", WrapMemoryCall((a, d) => Disassemble((uint)a, d)), DocParams.ReadParams, "Generates a disassembly of the instruction at Address"));
		}

		private static class DocParams
		{
			public static ApiParameter Address = new ApiParameter("Address");
			public static ApiParameter Value = new ApiParameter("Value");
			public static ApiParameter Length = new ApiParameter("Length");
			public static ApiParameter Domain = new ApiParameter("Domain", "string", true, true);
			public static ApiParameter Data = new ApiParameter("Data", "bytes");

			public static List<ApiParameter> ReadParams = new List<ApiParameter>() { Domain, Address };
			public static List<ApiParameter> WriteParams = new List<ApiParameter>() { Domain, Address, Value };
			public static List<ApiParameter> ReadRange = new List<ApiParameter>() { Domain, Address, Length };
			public static List<ApiParameter> WriteRange = new List<ApiParameter>() { Domain, Address, Data };
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

		private int GetAddr(IEnumerable<string> args, string domain) => ParseRequired(args, 0, hex => (int)ResolveAddress(hex, domain), "Address");
		private int GetValue(IEnumerable<string> args) => ParseRequired(args, 1, hex => int.Parse(hex, System.Globalization.NumberStyles.HexNumber), "Value");
		private int GetLength(IEnumerable<string> args) => ParseRequired(args, 1, hex => int.Parse(hex, System.Globalization.NumberStyles.HexNumber), "Length");
		private byte[] GetData(IEnumerable<string> args) => ParseRequired(args, 1, HexStringToBytes, "Data");

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


		private MemoryDomain _currentMemoryDomain;

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
	}
}
