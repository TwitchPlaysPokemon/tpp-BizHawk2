﻿using BizHawk.Common;
using System;

namespace BizHawk.Emulation.Cores.Nintendo.NES
{
	// Mapper for Aladdin Super Game
	public sealed class Mapper114 : MMC3Board_Base
	{
		private ByteBuffer EXPREGS = new ByteBuffer(2);

		private int prg_mask_16;

		private byte[] sec = { 0, 3, 1, 5, 6, 7, 2, 4 };

		public override bool Configure(NES.EDetectionOrigin origin)
		{
			switch (Cart.board_type)
			{
				case "MAPPER114":
					break;
				default:
					return false;
			}

			BaseSetup();
			SetMirrorType(EMirrorType.Horizontal);
			mmc3.MMC3Type = MMC3.EMMC3Type.MMC3A;
			prg_mask_16 = Cart.prg_size / 16 - 1;
			return true;
		}

		public override void Dispose()
		{
			EXPREGS.Dispose();
			base.Dispose();
		}

		public override void SyncState(Serializer ser)
		{
			base.SyncState(ser);
			ser.Sync("expregs", ref EXPREGS);
		}

		public override void WriteEXP(int addr, byte value)
		{
			if ((addr & 0x7) == 0 && addr >= 0x1000)
			{
				EXPREGS[0] = value;
			}
		}

		public override void WriteWRAM(int addr, byte value)
		{
			if ((addr & 0x7) == 0)
			{
				EXPREGS[0] = value;
			}
		}

		public override void WritePRG(int addr, byte value)
		{
			switch (addr & 0x6000)
			{
				case 0x0000: //$8000
					base.SetMirrorType((value & 1) == 1 ? EMirrorType.Horizontal : EMirrorType.Vertical);
					break;
				case 0x2000: //$A000
					value = (byte)((value & 0xC0) | sec[value & 0x07]);
					EXPREGS[1] = 1;
					base.WritePRG(0, value);
					break;
				case 0x4000: //$C000
					if(EXPREGS[1] == 1)
					{
						EXPREGS[1] = 0;
						base.WritePRG(1, value);
					}
					break;
				case 0x6000: //$E000 
					if (value > 0)
					{
						base.WritePRG(0x6001, value);
						base.WritePRG(0x4000, value);
						base.WritePRG(0x4001, value);
					}
					else
					{
						base.WritePRG(0x6000, value);
					}
					break;
			}
		}

		public override byte ReadPRG(int addr)
		{
			if ((EXPREGS[0] & 0x80) > 0)
			{
				var bank = EXPREGS[0] & 0x1F & prg_mask_16;
				return ROM[(bank << 14) + (addr & 0x3FFF)];
			}
			else
			{
				return base.ReadPRG(addr);
			}
		}
	}
}
