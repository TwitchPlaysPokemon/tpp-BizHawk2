﻿using System;
using System.Collections.Generic;
using System.Linq;

using BizHawk.Emulation.Common;

namespace BizHawk.Emulation.Cores.Nintendo.GBHawk
{
	public partial class GBHawk
	{
		private IMemoryDomains MemoryDomains;

		public void SetupMemoryDomains()
		{
			var domains = new List<MemoryDomain>
			{
				new MemoryDomainDelegate(
					"Main RAM",
					RAM.Length,
					MemoryDomain.Endian.Little,
					addr => RAM[addr],
					(addr, value) => RAM[addr] = value,
					1),
				new MemoryDomainDelegate(
					"Zero Page RAM",
					ZP_RAM.Length,
					MemoryDomain.Endian.Little,
					addr => ZP_RAM[addr],
					(addr, value) => ZP_RAM[addr] = value,
					1),
				new MemoryDomainDelegate(
					"System Bus",
					0X10000,
					MemoryDomain.Endian.Little,
					addr => PeekSystemBus(addr),
					(addr, value) => PokeSystemBus(addr, value),
					1),
				new MemoryDomainDelegate(
					"ROM",
					_rom.Length,
					MemoryDomain.Endian.Little,
					addr => _rom[addr],
					(addr, value) => _rom[addr] = value,
					1),
				new MemoryDomainDelegate(
					"VRAM",
					VRAM.Length,
					MemoryDomain.Endian.Little,
					addr => VRAM[addr],
					(addr, value) => VRAM[addr] = value,
					1)
			};

			if (cart_RAM != null)
			{
				var CartRam = new MemoryDomainByteArray("Cart RAM", MemoryDomain.Endian.Little, cart_RAM, true, 1);
				domains.Add(CartRam);
			}

			MemoryDomains = new MemoryDomainList(domains);
			(ServiceProvider as BasicServiceProvider).Register<IMemoryDomains>(MemoryDomains);
		}

		private byte PeekSystemBus(long addr)
		{
			ushort addr2 = (ushort)(addr & 0xFFFF);
			return PeekMemory(addr2);
		}

		private void PokeSystemBus(long addr, byte value)
		{
			ushort addr2 = (ushort)(addr & 0xFFFF);
			WriteMemory(addr2, value);
		}
	}
}
