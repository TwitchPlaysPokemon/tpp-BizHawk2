﻿using System;
using System.Collections.Generic;
using System.Linq;
using BizHawk.Common.BufferExtensions;
using BizHawk.Emulation.Common;

namespace BizHawk.Emulation.Cores.Consoles.ChannelF
{
	public partial class ChannelF
	{
		internal IMemoryDomains memoryDomains;
		private readonly Dictionary<string, MemoryDomainByteArray> _byteArrayDomains = new Dictionary<string, MemoryDomainByteArray>();
		private bool _memoryDomainsInit = false;

		private void SetupMemoryDomains()
		{
			var domains = new List<MemoryDomain>
			{
				new MemoryDomainDelegate("System Bus", 0x10000, MemoryDomain.Endian.Big,
					(addr) =>
					{
						if (addr < 0 || addr >= 65536)
						{
							throw new ArgumentOutOfRangeException();
						}
						return ReadBus((ushort)addr);
					},
					(addr, value) =>
					{
						if (addr < 0 || addr >= 65536)
						{
							throw new ArgumentOutOfRangeException();
						}

						WriteBus((ushort)addr, value);
					}, 1)
			};

			SyncAllByteArrayDomains();

			memoryDomains = new MemoryDomainList(_byteArrayDomains.Values.Concat(domains).ToList());
			(ServiceProvider as BasicServiceProvider)?.Register<IMemoryDomains>(memoryDomains);

			_memoryDomainsInit = true;
		}

		private void SyncAllByteArrayDomains()
		{
			SyncByteArrayDomain("BIOS1", BIOS01);
			SyncByteArrayDomain("BIOS2", BIOS02);
			SyncByteArrayDomain("ROM", Rom);
			SyncByteArrayDomain("VRAM", VRAM);
		}

		private void SyncByteArrayDomain(string name, byte[] data)
		{
			if (_memoryDomainsInit || _byteArrayDomains.ContainsKey(name))
			{
				var m = _byteArrayDomains[name];
				m.Data = data;
			}
			else
			{
				var m = new MemoryDomainByteArray(name, MemoryDomain.Endian.Big, data, false, 1);
				_byteArrayDomains.Add(name, m);
			}
		}
	}
}
