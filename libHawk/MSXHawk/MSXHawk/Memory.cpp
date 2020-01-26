#include <iostream>
#include <cstdint>
#include <iomanip>
#include <string>

#include "Memory.h"
#include "Z80A.h"
#include "TMS9918A.h"
#include "AY_3_8910.h"

using namespace std;

namespace MSXHawk
{
	uint8_t MemoryManager::HardwareRead(uint32_t port)
	{
		port &= 0xFF;

		if (port == 0x98) // VDP
		{
			return vdp_pntr->ReadData();
		}
		else if (port == 0x99) // VDP
		{
			return vdp_pntr->ReadVdpStatus();
		}
		else if (port == 0xA2)
		{
			return psg_pntr->ReadReg();
		}
		else if (port == 0xA8)
		{
			return PortA8;
		}
		else if (port == 0xA9)
		{
			return 0xFF;
		}
		
		return 0xFF;
	}
	
	void MemoryManager::HardwareWrite(uint32_t port, uint8_t value)
	{
		port &= 0xFF;

		if (port == 0x98) // VDP
		{
			vdp_pntr->WriteVdpData(value);
		}				
		else if(port == 0x99) // VDP
		{
			vdp_pntr->WriteVdpControl(value);
		}
		else if (port == 0xA0)
		{
			psg_pntr->port_sel = (value & 0xF);
		}
		else if (port == 0xA1)
		{
			psg_pntr->WriteReg(value);

			// update controller port data if port F is written to
			if (psg_pntr->port_sel == 0xF) 
			{
				if ((psg_pntr->Register[0xF] & 0x40) > 0)
				{
					psg_pntr->Register[0xE] = controller_byte_2;
				}
				else
				{
					psg_pntr->Register[0xE] = controller_byte_1;
				}
			}
		}
		else if (port == 0xA8)
		{
			PortA8 = value;
			remap();
		}
	}
	
	void MemoryManager::remap()
	{
		if ((PortA8 & 3) == 0) 
		{
			for (uint32_t i = 0; i < 16; i++)
			{
				cpu_pntr->MemoryMap[i] = &bios_rom[(0x400 * i)];
				cpu_pntr->MemoryMapMask[i] = 0;
			}
		}
		else if ((PortA8 & 3) == 1)
		{
			for (uint32_t i = 0; i < 16; i++)
			{
				cpu_pntr->MemoryMap[i] = &rom_1[(0x400 * i)];
				cpu_pntr->MemoryMapMask[i] = 0;
			}
		}
		else if ((PortA8 & 3) == 2)
		{
			for (uint32_t i = 0; i < 16; i++)
			{
				cpu_pntr->MemoryMap[i] = &rom_2[(0x400 * i)];
				cpu_pntr->MemoryMapMask[i] = 0;
			}
		}
		else if ((PortA8 & 3) == 3)
		{
			for (uint32_t i = 0; i < 16; i++)
			{
				cpu_pntr->MemoryMap[i] = &ram[(0x400 * i)];
				cpu_pntr->MemoryMapMask[i] = 0xFF;
			}
		}

		if (((PortA8 >> 2) & 3) == 0)
		{
			for (uint32_t i = 0; i < 16; i++)
			{
				cpu_pntr->MemoryMap[i + 16] = &basic_rom[(0x400 * i)];
				cpu_pntr->MemoryMapMask[i + 16] = 0;
			}
		}
		else if (((PortA8 >> 2) & 3) == 1)
		{
			for (uint32_t i = 0; i < 16; i++)
			{
				cpu_pntr->MemoryMap[i + 16] = &rom_1[0x4000 + (0x400 * i)];
				cpu_pntr->MemoryMapMask[i + 16] = 0;
			}
		}
		else if (((PortA8 >> 2) & 3) == 2)
		{
			for (uint32_t i = 0; i < 16; i++)
			{
				cpu_pntr->MemoryMap[i + 16] = &rom_2[0x4000 + (0x400 * i)];
				cpu_pntr->MemoryMapMask[i + 16] = 0;
			}
		}
		else if (((PortA8 >> 2) & 3) == 3)
		{
			for (uint32_t i = 0; i < 16; i++)
			{
				cpu_pntr->MemoryMap[i + 16] = &ram[0x4000 + (0x400 * i)];
				cpu_pntr->MemoryMapMask[i + 16] = 0xFF;
			}
		}

		if (((PortA8 >> 4) & 3) == 0)
		{
			for (uint32_t i = 0; i < 16; i++)
			{
				cpu_pntr->MemoryMap[i + 32] = &unmapped[0];
				cpu_pntr->MemoryMapMask[i + 32] = 0;
			}
		}
		else if (((PortA8 >> 4) & 3) == 1)
		{
			for (uint32_t i = 0; i < 16; i++)
			{
				cpu_pntr->MemoryMap[i + 32] = &rom_1[0x8000 + (0x400 * i)];
				cpu_pntr->MemoryMapMask[i + 32] = 0;
			}
		}
		else if (((PortA8 >> 4) & 3) == 2)
		{
			for (uint32_t i = 0; i < 16; i++)
			{
				cpu_pntr->MemoryMap[i + 32] = &rom_2[0x8000 + (0x400 * i)];
				cpu_pntr->MemoryMapMask[i + 32] = 0;
			}
		}
		else if (((PortA8 >> 4) & 3) == 3)
		{
			for (uint32_t i = 0; i < 16; i++)
			{
				cpu_pntr->MemoryMap[i + 32] = &ram[0x8000 + (0x400 * i)];
				cpu_pntr->MemoryMapMask[i + 32] = 0xFF;
			}
		}

		if (((PortA8 >> 6) & 3) == 0)
		{
			for (uint32_t i = 0; i < 16; i++)
			{
				cpu_pntr->MemoryMap[i + 48] = &unmapped[0];
				cpu_pntr->MemoryMapMask[i + 48] = 0;
			}
		}
		else if (((PortA8 >> 6) & 3) == 1)
		{
			for (uint32_t i = 0; i < 16; i++)
			{
				cpu_pntr->MemoryMap[i + 48] = &rom_1[0xC000 + (0x400 * i)];
				cpu_pntr->MemoryMapMask[i + 48] = 0;
			}
		}
		else if (((PortA8 >> 6) & 3) == 2)
		{
			for (uint32_t i = 0; i < 16; i++)
			{
				cpu_pntr->MemoryMap[i + 48] = &rom_2[0xC000 + (0x400 * i)];
				cpu_pntr->MemoryMapMask[i + 48] = 0;
			}
		}
		else if (((PortA8 >> 6) & 3) == 3)
		{
			for (uint32_t i = 0; i < 16; i++)
			{
				cpu_pntr->MemoryMap[i + 48] = &ram[0xC000 + (0x400 * i)];
				cpu_pntr->MemoryMapMask[i + 48] = 0xFF;
			}
		}
	}
}