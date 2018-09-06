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
		private IMemoryDomains MemoryDomainCore { get; set; }
		#endregion

		public MemoryApi()
		{
			constructedCommands.Add(new ApiCommand("ReadByteRange", WrapMemoryCall(ReadRegion)));
			constructedCommands.Add(new ApiCommand("WriteByteRange", WrapMemoryCall(WriteRegion)));
			constructedCommands.Add(new ApiCommand("ReadByte", WrapMemoryCall((a, d) => (int)ReadUnsignedByte(a, d))));
			constructedCommands.Add(new ApiCommand("WriteByte", WrapMemoryCall((a, v, d) => WriteUnsignedByte(a, (uint)v, d))));
			for (var b = 1; b<= 4; b++) //8, 16, 24, and 32-bit commands
			{
				constructedCommands.Add(new ApiCommand($"ReadS{b * 8}BE", WrapMemoryCall((a, d) => ReadSignedBig(a, b, d), b)));
				constructedCommands.Add(new ApiCommand($"ReadS{b * 8}LE", WrapMemoryCall((a, d) => ReadSignedLittle(a, b, d), b)));
				constructedCommands.Add(new ApiCommand($"ReadU{b * 8}BE", WrapMemoryCall((a, d) => (int)ReadUnsignedBig(a, b, d), b)));
				constructedCommands.Add(new ApiCommand($"ReadU{b * 8}LE", WrapMemoryCall((a, d) => (int)ReadUnsignedLittle(a, b, d), b)));
				constructedCommands.Add(new ApiCommand($"WriteS{b * 8}BE", WrapMemoryCall((a,v, d) => WriteSignedBig(a,v, b, d))));
				constructedCommands.Add(new ApiCommand($"WriteS{b * 8}LE", WrapMemoryCall((a,v, d) => WriteSignedLittle(a, v,b, d))));
				constructedCommands.Add(new ApiCommand($"WriteU{b * 8}BE", WrapMemoryCall((a, v, d) => WriteUnsignedBig(a, (uint)v, b, d))));
				constructedCommands.Add(new ApiCommand($"WriteU{b * 8}LE", WrapMemoryCall((a, v, d) => WriteUnsignedLittle(a, (uint)v, b, d))));
			}
		}

		private List<ApiCommand> constructedCommands = new List<ApiCommand>();

		public override IEnumerable<ApiCommand> Commands => constructedCommands;

		private int GetAddr(IEnumerable<string> args) => ParseRequired(args, 0, hex => int.Parse(hex, System.Globalization.NumberStyles.HexNumber), "Address");
		private int GetValue(IEnumerable<string> args) => ParseRequired(args, 1, hex => int.Parse(hex, System.Globalization.NumberStyles.HexNumber), "Value");
		private int GetLength(IEnumerable<string> args) => ParseRequired(args, 1, hex => int.Parse(hex, System.Globalization.NumberStyles.HexNumber), "Length");
		private byte[] GetData(IEnumerable<string> args) => ParseRequired(args, 1, HexStringToBytes, "Data");

		// Reads
		private Func<IEnumerable<string>, string, string> WrapMemoryCall(Func<int, string, int> innerCall, int bytesOut = 1) => (IEnumerable<string> args, string domain) => innerCall(GetAddr(args), domain).ToString($"X{bytesOut * 2}");
		// ReadRange
		private Func<IEnumerable<string>, string, string> WrapMemoryCall(Func<int, int, string, byte[]> innerCall) => (IEnumerable<string> args, string domain) => BytesToHexString(innerCall(GetAddr(args), GetLength(args), domain));
		// Writes
		private Func<IEnumerable<string>, string, string> WrapMemoryCall(Action<int, int, string> innerCall) => (IEnumerable<string> args, string domain) =>
		{
			innerCall(GetAddr(args), GetValue(args), domain);
			return null;
		};
		// WriteRange
		private Func<IEnumerable<string>, string, string> WrapMemoryCall(Action<int, byte[], string> innerCall) => (IEnumerable<string> args, string domain) =>
		{
			innerCall(GetAddr(args), GetData(args), domain);
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
