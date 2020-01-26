﻿using System;
using System.ComponentModel;

using BizHawk.Emulation.Common;
using BizHawk.Emulation.Common.IEmulatorExtensions;

using NLua;

// ReSharper disable UnusedMember.Global
namespace BizHawk.Client.Common
{
	[Description("Main memory library reads and writes from the Main memory domain (the default memory domain set by any given core)")]
	public sealed class MainMemoryLuaLibrary : DelegatingLuaLibrary
	{
		[RequiredService]
		private IEmulator Emulator { get; set; }

		[OptionalService]
		private IMemoryDomains MemoryDomainCore { get; set; }

		public MainMemoryLuaLibrary(Lua lua)
			: base(lua) { }

		public MainMemoryLuaLibrary(Lua lua, Action<string> logOutputCallback)
			: base(lua, logOutputCallback) { }

		public override string Name => "mainmemory";

		private MemoryDomain Domain
		{
			get
			{
				if (MemoryDomainCore != null)
				{
					return MemoryDomainCore.MainMemory;
				}

				var error = $"Error: {Emulator.Attributes().CoreName} does not implement memory domains";
				Log(error);
				throw new NotImplementedException(error);
			}
		}

		#region Unique Library Methods

		[LuaMethodExample("local stmaiget = mainmemory.getname( );")]
		[LuaMethod("getname", "returns the name of the domain defined as main memory for the given core")]
		public string GetName()
		{
			return Domain.Name;
		}

		[LuaMethodExample("local uimaiget = mainmemory.getcurrentmemorydomainsize( );")]
		[LuaMethod("getcurrentmemorydomainsize", "Returns the number of bytes of the domain defined as main memory")]
		public uint GetSize()
		{
			return (uint)Domain.Size;
		}

		#endregion

		#region Common Special and Legacy Methods

		[LuaMethodExample("local uimairea = mainmemory.readbyte( 0x100 );")]
		[LuaMethod("readbyte", "gets the value from the given address as an unsigned byte")]
		public uint ReadByte(int addr) => APIs.Mem.ReadByte(addr, Domain.Name);

		[LuaMethodExample("mainmemory.writebyte( 0x100, 1000 );")]
		[LuaMethod("writebyte", "Writes the given value to the given address as an unsigned byte")]
		public void WriteByte(int addr, uint value) => APIs.Mem.WriteByte(addr, value, Domain.Name);

		[LuaMethodExample("local nlmairea = mainmemory.readbyterange( 0x100, 64 );")]
		[LuaMethod("readbyterange", "Reads the address range that starts from address, and is length long. Returns the result into a table of key value pairs (where the address is the key).")]
		public LuaTable ReadByteRange(int addr, int length)
		{
			return APIs.Mem
				.ReadByteRange(addr, length, Domain.Name)
				.ToLuaTable(Lua);
		}

		/// <remarks>TODO C# version requires a contiguous address range</remarks>
		[LuaMethodExample("")]
		[LuaMethod("writebyterange", "Writes the given values to the given addresses as unsigned bytes")]
		public void WriteByteRange(LuaTable memoryblock)
		{
#if true
			foreach (var addr in memoryblock.Keys) APIs.Mem.WriteByte(LuaInt(addr), (uint) memoryblock[addr], Domain.Name);
#else
			var d = Domain;
			if (d.CanPoke())
			{
				foreach (var address in memoryblock.Keys)
				{
					var addr = LuaInt(address);
					if (addr < d.Size)
					{
						d.PokeByte(addr, (byte)LuaInt(memoryblock[address]));
					}
					else
					{
						Log($"Warning: Attempted write {addr} outside memory domain size of {d.Size} in writebyterange()");
					}
				}
			}
			else
			{
				Log($"Error: the domain {d.Name} is not writable");
			}
#endif
		}

		[LuaMethodExample("local simairea = mainmemory.readfloat(0x100, false);")]
		[LuaMethod("readfloat", "Reads the given address as a 32-bit float value from the main memory domain with th e given endian")]
		public float ReadFloat(int addr, bool bigendian)
		{
			APIs.Mem.SetBigEndian(bigendian);
			return APIs.Mem.ReadFloat(addr, Domain.Name);
		}

		[LuaMethodExample("mainmemory.writefloat( 0x100, 10.0, false );")]
		[LuaMethod("writefloat", "Writes the given 32-bit float value to the given address and endian")]
		public void WriteFloat(int addr, double value, bool bigendian)
		{
			APIs.Mem.SetBigEndian(bigendian);
			APIs.Mem.WriteFloat(addr, value, Domain.Name);
		}

		#endregion

		#region 1 Byte

		[LuaMethodExample("local inmairea = mainmemory.read_s8( 0x100 );")]
		[LuaMethod("read_s8", "read signed byte")]
		public int ReadS8(int addr) => APIs.Mem.ReadS8(addr, Domain.Name);

		[LuaMethodExample("mainmemory.write_s8( 0x100, 1000 );")]
		[LuaMethod("write_s8", "write signed byte")]
		public void WriteS8(int addr, uint value) => APIs.Mem.WriteS8(addr, unchecked((int) value), Domain.Name);

		[LuaMethodExample("local uimairea = mainmemory.read_u8( 0x100 );")]
		[LuaMethod("read_u8", "read unsigned byte")]
		public uint ReadU8(int addr) => APIs.Mem.ReadU8(addr, Domain.Name);

		[LuaMethodExample("mainmemory.write_u8( 0x100, 1000 );")]
		[LuaMethod("write_u8", "write unsigned byte")]
		public void WriteU8(int addr, uint value) => APIs.Mem.WriteU8(addr, value, Domain.Name);

		#endregion

		#region 2 Byte

		[LuaMethodExample("local inmairea = mainmemory.read_s16_le( 0x100 );")]
		[LuaMethod("read_s16_le", "read signed 2 byte value, little endian")]
		public int ReadS16Little(int addr)
		{
			APIs.Mem.SetBigEndian(false);
			return APIs.Mem.ReadS16(addr, Domain.Name);
		}

		[LuaMethodExample("mainmemory.write_s16_le( 0x100, -1000 );")]
		[LuaMethod("write_s16_le", "write signed 2 byte value, little endian")]
		public void WriteS16Little(int addr, int value)
		{
			APIs.Mem.SetBigEndian(false);
			APIs.Mem.WriteS16(addr, value, Domain.Name);
		}

		[LuaMethodExample("local inmairea = mainmemory.read_s16_be( 0x100 );")]
		[LuaMethod("read_s16_be", "read signed 2 byte value, big endian")]
		public int ReadS16Big(int addr)
		{
			APIs.Mem.SetBigEndian();
			return APIs.Mem.ReadS16(addr, Domain.Name);
		}

		[LuaMethodExample("mainmemory.write_s16_be( 0x100, -1000 );")]
		[LuaMethod("write_s16_be", "write signed 2 byte value, big endian")]
		public void WriteS16Big(int addr, int value)
		{
			APIs.Mem.SetBigEndian();
			APIs.Mem.WriteS16(addr, value, Domain.Name);
		}

		[LuaMethodExample("local uimairea = mainmemory.read_u16_le( 0x100 );")]
		[LuaMethod("read_u16_le", "read unsigned 2 byte value, little endian")]
		public uint ReadU16Little(int addr)
		{
			APIs.Mem.SetBigEndian(false);
			return APIs.Mem.ReadU16(addr, Domain.Name);
		}

		[LuaMethodExample("mainmemory.write_u16_le( 0x100, 1000 );")]
		[LuaMethod("write_u16_le", "write unsigned 2 byte value, little endian")]
		public void WriteU16Little(int addr, uint value)
		{
			APIs.Mem.SetBigEndian(false);
			APIs.Mem.WriteU16(addr, value, Domain.Name);
		}

		[LuaMethodExample("local uimairea = mainmemory.read_u16_be( 0x100 );")]
		[LuaMethod("read_u16_be", "read unsigned 2 byte value, big endian")]
		public uint ReadU16Big(int addr)
		{
			APIs.Mem.SetBigEndian();
			return APIs.Mem.ReadU16(addr, Domain.Name);
		}

		[LuaMethodExample("mainmemory.write_u16_be( 0x100, 1000 );")]
		[LuaMethod("write_u16_be", "write unsigned 2 byte value, big endian")]
		public void WriteU16Big(int addr, uint value)
		{
			APIs.Mem.SetBigEndian();
			APIs.Mem.WriteU16(addr, value, Domain.Name);
		}

		#endregion

		#region 3 Byte

		[LuaMethodExample("local inmairea = mainmemory.read_s24_le( 0x100 );")]
		[LuaMethod("read_s24_le", "read signed 24 bit value, little endian")]
		public int ReadS24Little(int addr)
		{
			APIs.Mem.SetBigEndian(false);
			return APIs.Mem.ReadS24(addr, Domain.Name);
		}

		[LuaMethodExample("mainmemory.write_s24_le( 0x100, -1000 );")]
		[LuaMethod("write_s24_le", "write signed 24 bit value, little endian")]
		public void WriteS24Little(int addr, int value)
		{
			APIs.Mem.SetBigEndian(false);
			APIs.Mem.WriteS24(addr, value, Domain.Name);
		}

		[LuaMethodExample("local inmairea = mainmemory.read_s24_be( 0x100 );")]
		[LuaMethod("read_s24_be", "read signed 24 bit value, big endian")]
		public int ReadS24Big(int addr)
		{
			APIs.Mem.SetBigEndian();
			return APIs.Mem.ReadS24(addr, Domain.Name);
		}

		[LuaMethodExample("mainmemory.write_s24_be( 0x100, -1000 );")]
		[LuaMethod("write_s24_be", "write signed 24 bit value, big endian")]
		public void WriteS24Big(int addr, int value)
		{
			APIs.Mem.SetBigEndian();
			APIs.Mem.WriteS24(addr, value, Domain.Name);
		}

		[LuaMethodExample("local uimairea = mainmemory.read_u24_le( 0x100 );")]
		[LuaMethod("read_u24_le", "read unsigned 24 bit value, little endian")]
		public uint ReadU24Little(int addr)
		{
			APIs.Mem.SetBigEndian(false);
			return APIs.Mem.ReadU24(addr, Domain.Name);
		}

		[LuaMethodExample("mainmemory.write_u24_le( 0x100, 1000 );")]
		[LuaMethod("write_u24_le", "write unsigned 24 bit value, little endian")]
		public void WriteU24Little(int addr, uint value)
		{
			APIs.Mem.SetBigEndian(false);
			APIs.Mem.WriteU24(addr, value, Domain.Name);
		}

		[LuaMethodExample("local uimairea = mainmemory.read_u24_be( 0x100 );")]
		[LuaMethod("read_u24_be", "read unsigned 24 bit value, big endian")]
		public uint ReadU24Big(int addr)
		{
			APIs.Mem.SetBigEndian();
			return APIs.Mem.ReadU24(addr, Domain.Name);
		}

		[LuaMethodExample("mainmemory.write_u24_be( 0x100, 1000 );")]
		[LuaMethod("write_u24_be", "write unsigned 24 bit value, big endian")]
		public void WriteU24Big(int addr, uint value)
		{
			APIs.Mem.SetBigEndian();
			APIs.Mem.WriteU24(addr, value, Domain.Name);
		}

		#endregion

		#region 4 Byte

		[LuaMethodExample("local inmairea = mainmemory.read_s32_le( 0x100 );")]
		[LuaMethod("read_s32_le", "read signed 4 byte value, little endian")]
		public int ReadS32Little(int addr)
		{
			APIs.Mem.SetBigEndian(false);
			return APIs.Mem.ReadS32(addr, Domain.Name);
		}

		[LuaMethodExample("mainmemory.write_s32_le( 0x100, -1000 );")]
		[LuaMethod("write_s32_le", "write signed 4 byte value, little endian")]
		public void WriteS32Little(int addr, int value)
		{
			APIs.Mem.SetBigEndian(false);
			APIs.Mem.WriteS32(addr, value, Domain.Name);
		}

		[LuaMethodExample("local inmairea = mainmemory.read_s32_be( 0x100 );")]
		[LuaMethod("read_s32_be", "read signed 4 byte value, big endian")]
		public int ReadS32Big(int addr)
		{
			APIs.Mem.SetBigEndian();
			return APIs.Mem.ReadS32(addr, Domain.Name);
		}

		[LuaMethodExample("mainmemory.write_s32_be( 0x100, -1000 );")]
		[LuaMethod("write_s32_be", "write signed 4 byte value, big endian")]
		public void WriteS32Big(int addr, int value)
		{
			APIs.Mem.SetBigEndian();
			APIs.Mem.WriteS32(addr, value, Domain.Name);
		}

		[LuaMethodExample("local uimairea = mainmemory.read_u32_le( 0x100 );")]
		[LuaMethod("read_u32_le", "read unsigned 4 byte value, little endian")]
		public uint ReadU32Little(int addr)
		{
			APIs.Mem.SetBigEndian(false);
			return APIs.Mem.ReadU32(addr, Domain.Name);
		}

		[LuaMethodExample("mainmemory.write_u32_le( 0x100, 1000 );")]
		[LuaMethod("write_u32_le", "write unsigned 4 byte value, little endian")]
		public void WriteU32Little(int addr, uint value)
		{
			APIs.Mem.SetBigEndian(false);
			APIs.Mem.WriteU32(addr, value, Domain.Name);
		}

		[LuaMethodExample("local uimairea = mainmemory.read_u32_be( 0x100 );")]
		[LuaMethod("read_u32_be", "read unsigned 4 byte value, big endian")]
		public uint ReadU32Big(int addr)
		{
			APIs.Mem.SetBigEndian();
			return APIs.Mem.ReadU32(addr, Domain.Name);
		}

		[LuaMethodExample("mainmemory.write_u32_be( 0x100, 1000 );")]
		[LuaMethod("write_u32_be", "write unsigned 4 byte value, big endian")]
		public void WriteU32Big(int addr, uint value)
		{
			APIs.Mem.SetBigEndian();
			APIs.Mem.WriteU32(addr, value, Domain.Name);
		}

		#endregion
	}
}
