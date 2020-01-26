﻿using System;
using System.IO;

using BizHawk.Common;
using BizHawk.Emulation.Common;

namespace BizHawk.Emulation.Cores.Components.M6502
{
	public sealed partial class MOS6502X<TLink> where TLink : IMOS6502XLink
	{
		private readonly TLink _link;

		public MOS6502X(TLink link)
		{
			_link = link;
			InitOpcodeHandlers();
			Reset();
		}

		public bool BCD_Enabled = true;
		public bool debug = false;
		public bool throw_unhandled;

		public void Reset()
		{
			A = 0;
			X = 0;
			Y = 0;
			P = 0x20; // 5th bit always set
			S = 0;
			PC = 0;
			TotalExecutedCycles = 0;
			mi = 0;
			opcode = VOP_RESET;
			iflag_pending = true;
			RDY = true;
		}

		public void NESSoftReset()
		{
			opcode = VOP_RESET;
			mi = 0;
			iflag_pending = true;
			FlagI = true;
		}

		public string TraceHeader
		{
			get { return "6502: PC, machine code, mnemonic, operands, registers (A, X, Y, P, SP), flags (NVTBDIZCR)"; }
		}

		public TraceInfo State(bool disassemble = true)
		{
			if (!disassemble)
			{
				return new TraceInfo {
					Disassembly = "",
					RegisterInfo = ""
				};
			}

			int length;
			string rawbytes = "";
			string disasm = Disassemble(PC, out length);

			for (int i = 0; i < length; i++)
			{
				rawbytes += $" {_link.PeekMemory((ushort)(PC + i)):X2}";
			}

			return new TraceInfo
			{
				Disassembly = $"{PC:X4}: {rawbytes,-9}  {disasm} ".PadRight(32),
				RegisterInfo = string.Join("  ",
					$"A:{A:X2}",
					$"X:{X:X2}",
					$"Y:{Y:X2}",
					$"SP:{S:X2}",
					$"P:{P:X2}",
					string.Concat(
						FlagN ? "N" : "n",
						FlagV ? "V" : "v",
						FlagT ? "T" : "t",
						FlagB ? "B" : "b",
						FlagD ? "D" : "d",
						FlagI ? "I" : "i",
						FlagZ ? "Z" : "z",
						FlagC ? "C" : "c"
//						!RDY ? "R" : "r"
						),
					$"Cy:{TotalExecutedCycles}",
					$"PPU-Cy:{ext_ppu_cycle}")
			};
		}

		public bool AtStart { get { return opcode == VOP_Fetch1 || Microcode[opcode][mi] >= Uop.End; } }

		public TraceInfo TraceState()
		{
			// only disassemble when we're at the beginning of an opcode
			return State(AtStart);
		}

		public const ushort NMIVector = 0xFFFA;
		public const ushort ResetVector = 0xFFFC;
		public const ushort BRKVector = 0xFFFE;
		public const ushort IRQVector = 0xFFFE;

		enum ExceptionType
		{
			BRK, NMI, IRQ
		}


		// ==== CPU State ====

		public byte A;
		public byte X;
		public byte Y;
		public byte P;
		public ushort PC;
		public byte S;

		public bool IRQ;
		public bool NMI;
		public bool RDY;

		// ppu cycle (used with SubNESHawk)
		public int ext_ppu_cycle = 0;

		public void SyncState(Serializer ser)
		{
			ser.BeginSection(nameof(MOS6502X));
			ser.Sync(nameof(A), ref A);
			ser.Sync(nameof(X), ref X);
			ser.Sync(nameof(Y), ref Y);
			ser.Sync(nameof(P), ref P);
			ser.Sync(nameof(PC), ref PC);
			ser.Sync(nameof(S), ref S);
			ser.Sync(nameof(NMI), ref NMI);
			ser.Sync(nameof(IRQ), ref IRQ);
			ser.Sync(nameof(RDY), ref RDY);
			ser.Sync(nameof(TotalExecutedCycles), ref TotalExecutedCycles);
			ser.Sync(nameof(opcode), ref opcode);
			ser.Sync(nameof(opcode2), ref opcode2);
			ser.Sync(nameof(opcode3), ref opcode3);
			ser.Sync(nameof(ea), ref ea);
			ser.Sync(nameof(alu_temp), ref alu_temp);
			ser.Sync(nameof(mi), ref mi);
			ser.Sync(nameof(iflag_pending), ref iflag_pending);
			ser.Sync(nameof(interrupt_pending), ref interrupt_pending);
			ser.Sync(nameof(branch_irq_hack), ref branch_irq_hack);
			ser.Sync(nameof(rdy_freeze), ref rdy_freeze);
			ser.Sync(nameof(ext_ppu_cycle), ref ext_ppu_cycle);
			ser.EndSection();
		}

		public void SaveStateBinary(BinaryWriter writer) { SyncState(Serializer.CreateBinaryWriter(writer)); }
		public void LoadStateBinary(BinaryReader reader) { SyncState(Serializer.CreateBinaryReader(reader)); }

		// ==== End State ====

		/// <summary>Carry Flag</summary>
		public bool FlagC
		{
			get => (P & 0x01) != 0;
			private set => P = (byte)((P & ~0x01) | (value ? 0x01 : 0x00));
		}

		/// <summary>Zero Flag</summary>
		public bool FlagZ
		{
			get => (P & 0x02) != 0;
			private set => P = (byte)((P & ~0x02) | (value ? 0x02 : 0x00));
		}

		/// <summary>Interrupt Disable Flag</summary>
		public bool FlagI
		{
			get => (P & 0x04) != 0;
			set => P = (byte)((P & ~0x04) | (value ? 0x04 : 0x00));
		}

		/// <summary>Decimal Mode Flag</summary>
		public bool FlagD
		{
			get => (P & 0x08) != 0;
			private set => P = (byte)((P & ~0x08) | (value ? 0x08 : 0x00));
		}

		/// <summary>Break Flag</summary>
		public bool FlagB
		{
			get => (P & 0x10) != 0;
			private set => P = (byte)((P & ~0x10) | (value ? 0x10 : 0x00));
		}

		/// <summary>T... Flag</summary>
		public bool FlagT
		{
			get => (P & 0x20) != 0;
			private set => P = (byte)((P & ~0x20) | (value ? 0x20 : 0x00));
		}

		/// <summary>Overflow Flag</summary>
		public bool FlagV
		{
			get => (P & 0x40) != 0;
			private set => P = (byte)((P & ~0x40) | (value ? 0x40 : 0x00));
		}

		/// <summary>Negative Flag</summary>
		public bool FlagN
		{
			get => (P & 0x80) != 0;
			private set => P = (byte)((P & ~0x80) | (value ? 0x80 : 0x00));
		}

		public long TotalExecutedCycles;

		public ushort ReadWord(ushort address)
		{
			byte l = _link.ReadMemory(address);
			byte h = _link.ReadMemory(++address);
			return (ushort)((h << 8) | l);
		}

		public ushort PeekWord(ushort address)
		{
			byte l = _link.PeekMemory(address);
			byte h = _link.PeekMemory(++address);
			return (ushort)((h << 8) | l);
		}

		private void WriteWord(ushort address, ushort value)
		{
			byte l = (byte)(value & 0xFF);
			byte h = (byte)(value >> 8);
			_link.WriteMemory(address, l);
			_link.WriteMemory(++address, h);
		}

		private ushort ReadWordPageWrap(ushort address)
		{
			ushort highAddress = (ushort)((address & 0xFF00) + ((address + 1) & 0xFF));
			return (ushort)(_link.ReadMemory(address) | (_link.ReadMemory(highAddress) << 8));
		}

		// SO pin
		public void SetOverflow()
		{
			FlagV = true;
		}

		private static readonly byte[] TableNZ = 
		{ 
			0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
			0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
			0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
			0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
			0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
			0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
			0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
			0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80
		};
	}
}
