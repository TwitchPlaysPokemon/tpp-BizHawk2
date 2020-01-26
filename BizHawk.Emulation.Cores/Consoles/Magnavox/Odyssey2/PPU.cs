﻿using System;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.InteropServices;
using BizHawk.Common;
using BizHawk.Common.NumberExtensions;
using BizHawk.Emulation.Common;


namespace BizHawk.Emulation.Cores.Consoles.O2Hawk
{
	public class PPU : ISoundProvider
	{
		public O2Hawk Core { get; set; }

		public const int HBL_CNT = 45;

		public byte[] Sprites = new byte[16];
		public byte[] Sprite_Shapes = new byte[32];
		public byte[] Foreground = new byte[48];
		public byte[] Quad_Chars = new byte[64];
		public byte[] Grid_H = new byte[18];
		public byte[] Grid_V = new byte[10];

		public byte VDC_ctrl, VDC_status, VDC_collision, VDC_col_ret, VDC_color;
		public byte Pixel_Stat;
		public int bg_brightness, grid_brightness;
		public byte A4_latch, A5_latch;

		public int grid_fill;
		public byte grid_fill_col;
		public int LY;
		public int cycle;
		public bool VBL;
		public bool HBL;

		public byte ReadReg(int addr)
		{
			byte ret = 0;

			if (addr < 0x10)
			{
				ret = Sprites[addr];
			}
			else if (addr < 0x40)
			{
				ret = Foreground[addr - 0x10];
			}
			else if (addr < 0x80)
			{
				ret = Quad_Chars[addr - 0x40];
			}
			else if (addr < 0xA0)
			{
				ret = Sprite_Shapes[addr - 0x80];
			}
			else if (addr == 0xA0)
			{
				ret = VDC_ctrl;
				Core.cpu.IRQPending = false;
			}
			else if (addr == 0xA1)
			{
				ret = VDC_status;
			}
			else if (addr == 0xA2)
			{
				ret = VDC_col_ret;
				//Console.WriteLine("col: " + ret + " " + Core.cpu.TotalExecutedCycles);
			}
			else if (addr == 0xA3)
			{
				ret = VDC_color;
			}
			else if(addr == 0xA4)
			{
				if (VDC_ctrl.Bit(1)) { ret = A4_latch; }
				else { ret = (byte)LY; }
			}
			else if (addr == 0xA5)
			{
				if (VDC_ctrl.Bit(1)) { ret = A5_latch; }
				else { ret = (byte)(cycle - HBL_CNT); }
			}
			else if (addr <= 0xAA)
			{
				ret = AudioReadReg(addr);
			}
			else if ((addr >= 0xC0) && (addr <= 0xC8))
			{
				ret = Grid_H[addr - 0xC0];
			}
			else if ((addr >= 0xD0) && (addr <= 0xD8))
			{
				ret = Grid_H[addr - 0xD0 + 9];
			}
			else if ((addr >= 0xE0) && (addr <= 0xE9))
			{
				ret = Grid_V[addr - 0xE0];
			}

			return ret;
		}

		public void WriteReg(int addr, byte value)
		{
			if (addr < 0x10)
			{
				if (!VDC_ctrl.Bit(5)) { Sprites[addr] = value; }
                //Console.WriteLine("spr: " + addr + " " + value + " " + Core.cpu.TotalExecutedCycles);
			}
			else if (addr < 0x40)
			{
				// chars position is not effected by last bit
				if ((addr % 4) == 0) { value &= 0xFE; }
				if (!VDC_ctrl.Bit(5)) { Foreground[addr - 0x10] = value; }
				//Console.WriteLine("char: " + addr + " " + value + " " + Core.cpu.TotalExecutedCycles);
			}
			else if (addr < 0x80)
			{
				// chars position is not effected by last bit
				if ((addr % 4) == 0) { value &= 0xFE; }
				if (!VDC_ctrl.Bit(5)) { Quad_Chars[addr - 0x40] = value; }

				//Console.WriteLine("quad: " + (addr - 0x40) + " " + value + " " + Core.cpu.TotalExecutedCycles);
			}
			else if (addr < 0xA0)
			{
				Sprite_Shapes[addr - 0x80] = value;
			}
			else if (addr == 0xA0)
			{
				VDC_ctrl = value;
				//Console.WriteLine("VDC_ctrl: " + value + " " + Core.cpu.TotalExecutedCycles);
				if (VDC_ctrl.Bit(1)) 
				{ 
					VDC_status &= 0xFD;
					A4_latch = (byte)LY;
					A5_latch = (byte)(cycle - HBL_CNT);
				}
				else { VDC_status |= 0x2; }
			}
			else if (addr == 0xA1)
			{
				// not writable
				// VDC_status = value;
			}
			else if (addr == 0xA2)
			{
				VDC_collision = value;
				//Console.WriteLine("VDC_collide: " + value + " " + Core.cpu.TotalExecutedCycles);
			}
			else if (addr == 0xA3)
			{
				VDC_color = value;
				//Console.WriteLine("VDC_color: " + value + " " + Core.cpu.TotalExecutedCycles);
				bg_brightness = VDC_color.Bit(7) ? 8 : 0;
				grid_brightness = VDC_color.Bit(6) ? 8 : 0;
			}
			else if (addr == 0xA4)
			{
				// writing has no effect
			}
			else if (addr == 0xA5)
			{
				// writing has no effect
			}
			else if (addr <= 0xAA)
			{
				AudioWriteReg(addr, value);
			}
			else if ((addr >= 0xC0) && (addr <= 0xC8))
			{
				if (!VDC_ctrl.Bit(3)) { Grid_H[addr - 0xC0] = value; }
			}
			else if ((addr >= 0xD0) && (addr <= 0xD8))
			{
				if (!VDC_ctrl.Bit(3)) { Grid_H[addr - 0xD0 + 9] = value; }
			}
			else if ((addr >= 0xE0) && (addr <= 0xE9))
			{
				if (!VDC_ctrl.Bit(3)) { Grid_V[addr - 0xE0] = value; }
			}
		}

		public void tick()
		{
			Pixel_Stat = 0;
			// drawing cycles
			if (cycle >= HBL_CNT)
			{
				// draw a pixel
				if (LY < 240)
				{
					if (cycle == HBL_CNT)
					{
						HBL = false;
						VDC_status |= 0x01;
						// Send T1 pulses
						Core.cpu.T1 = false;
					}

					// background
					Core._vidbuffer[LY * 186 + (cycle - HBL_CNT)] = (int)Color_Palette_BG[((VDC_color >> 3) & 0x7) + bg_brightness];
					
					if (grid_fill > 0)
					{
						Core._vidbuffer[LY * 186 + (cycle - HBL_CNT)] = (int)Color_Palette_BG[(VDC_color & 0x7) + grid_brightness];
						Pixel_Stat |= grid_fill_col;
						grid_fill--;
					}
					
					if ((((cycle - HBL_CNT) % 16) == 8) && ((LY - 24) >= 0))
					{
						int k = (int)Math.Floor((cycle - HBL_CNT) / 16.0);
						int j = (int)Math.Floor((LY - 24) / 24.0);
						if ((k < 10) && (j < 8))
						{
							if (Grid_V[k].Bit(j))
							{
								Core._vidbuffer[LY * 186 + (cycle - HBL_CNT)] = (int)Color_Palette_BG[(VDC_color & 0x7) + grid_brightness];
								Pixel_Stat |= 0x10;
								if (VDC_ctrl.Bit(7)) { grid_fill = 15; }
								else { grid_fill = 1; }
								grid_fill_col = 0x10;
							}
						}
					}

					if (((LY % 24) < 3) && ((cycle - HBL_CNT - 8) >= 0) && ((LY - 24) >= 0))
					{
						int k = (int)Math.Floor((cycle - HBL_CNT - 8) / 16.0);
						int j = (int)Math.Floor((LY - 24) / 24.0);
						//Console.WriteLine(k + " " + j);
						if ((k < 9) && (j < 9))
						{
							if (j == 8)
							{
								if (Grid_H[k + 9].Bit(0))
								{
									Core._vidbuffer[LY * 186 + (cycle - HBL_CNT)] = (int)Color_Palette_BG[(VDC_color & 0x7) + grid_brightness];
									Pixel_Stat |= 0x20;

									if (((cycle - HBL_CNT - 8) % 16) == 15) { grid_fill = 2; grid_fill_col = 0x20; }
								}
							}
							else
							{
								if (Grid_H[k].Bit(j))
								{
									Core._vidbuffer[LY * 186 + (cycle - HBL_CNT)] = (int)Color_Palette_BG[(VDC_color & 0x7) + grid_brightness];
									Pixel_Stat |= 0x20;
									if (((cycle - HBL_CNT - 8) % 16) == 15) { grid_fill = 2; grid_fill_col = 0x20; }
								}
							}
						}
					}
					
					// grid
					if (VDC_ctrl.Bit(6) && ((LY - 24) >= 0) && ((LY % 24) < 3))
					{

						if ((((cycle - HBL_CNT) % 16) == 8) || (((cycle - HBL_CNT) % 16) == 9))
						{
							int k = (int)Math.Floor((cycle - HBL_CNT) / 16.0);
							int j = (int)Math.Floor((LY - 24) / 24.0);
							if ((k < 10) && (j < 9))
							{
								Core._vidbuffer[LY * 186 + (cycle - HBL_CNT)] = (int)Color_Palette_BG[(VDC_color & 0x7) + grid_brightness];
								Pixel_Stat |= 0x20;
							}
						}
					}
					
					// single characters
					for (int i = 0; i < 12; i++)
					{
						if ((LY >= Foreground[i * 4]) && (LY < (Foreground[i * 4] + 8 * 2)))
						{
							if (((cycle - HBL_CNT) >= Foreground[i * 4 + 1]) && ((cycle - HBL_CNT) < (Foreground[i * 4 + 1] + 8)))
							{
								// sprite is in drawing region, pick a pixel
								int offset_y = (LY - Foreground[i * 4]) >> 1;
								int offset_x = 7 - ((cycle - HBL_CNT) - Foreground[i * 4 + 1]);
								int char_sel = Foreground[i * 4 + 2];

								int char_pick = (char_sel - (((~(Foreground[i * 4] >> 1)) + 1) & 0xFF));
								
								if (char_pick < 0)
								{
									char_pick &= 0xFF;
									char_pick |= (Foreground[i * 4 + 3] & 1) << 8;
								}
								else
								{
									char_pick &= 0xFF;
									char_pick |= (~(Foreground[i * 4 + 3] & 1)) << 8;
									char_pick &= 0x1FF;
								}
								
								// don't display past the end of a character
								int pixel_pick = 0;

								if (((char_pick + 1) & 7) + offset_y < 8)
								{
									pixel_pick = (Internal_Graphics[(char_pick + offset_y) % 0x200] >> offset_x) & 1;
								}

								if (pixel_pick == 1)
								{
									if (Core._settings.Show_Chars)
									{
										Core._vidbuffer[LY * 186 + (cycle - HBL_CNT)] = (int) Color_Palette_SPR[(Foreground[i * 4 + 3] >> 1) & 0x7];
									}

									Pixel_Stat |= 0x80;
								}
							}
						}
					}
                    
					// quads
					for (int i = 3; i >= 0; i--)
					{
						if ((LY >= Quad_Chars[i * 16]) && (LY < (Quad_Chars[i * 16] + 8 * 2)))
						{
							if (((cycle - HBL_CNT) >= Quad_Chars[i * 16 + 1]) && ((cycle - HBL_CNT) < (Quad_Chars[i * 16 + 1] + 64)))
							{
								// sprite is in drawing region, pick a pixel
								int offset_y = (LY - Quad_Chars[i * 16]) >> 1;
								int offset_x = 63 - ((cycle - HBL_CNT) - Quad_Chars[i * 16 + 1]);
								int quad_num = 3;
								while (offset_x > 15)
								{
									offset_x -= 16;
									quad_num--;
								}

								if (offset_x > 7)
								{
									offset_x -= 8;
									
									int char_sel = Quad_Chars[i * 16 + 4 * quad_num + 2];

									int char_pick = (char_sel - (((~(Quad_Chars[i * 16] >> 1)) + 1) & 0xFF));
									
									if (char_pick < 0)
									{
										char_pick &= 0xFF;
										char_pick |= (Quad_Chars[i * 16 + 4 * quad_num + 3] & 1) << 8;
									}
									else
									{
										char_pick &= 0xFF;
										char_pick |= (~(Quad_Chars[i * 16 + 4 * quad_num + 3] & 1)) << 8;
										char_pick &= 0x1FF;
									}

									// don't display past the end of a character
									int pixel_pick = 0;

									if (((char_pick + 1) & 7) + offset_y < 8)
									{
										pixel_pick = (Internal_Graphics[(char_pick + offset_y) % 0x200] >> offset_x) & 1;
									}

									if (pixel_pick == 1)
									{
										if (Core._settings.Show_Quads)
										{
											Core._vidbuffer[LY * 186 + (cycle - HBL_CNT)] = (int) Color_Palette_SPR[(Quad_Chars[i * 16 + 4 * quad_num + 3] >> 1) & 0x7];
										}

										Pixel_Stat |= 0x80;
									}
								}
							}
						}
					}

					// sprites
					for (int i = 3; i >= 0; i--)
					{
						int double_size = Sprites[i * 4 + 2].Bit(2) ? 4 : 2;

						if ((LY >= Sprites[i * 4]) && (LY < (Sprites[i * 4] + 8 * double_size)))
						{
							if (((cycle - HBL_CNT) >= Sprites[i * 4 + 1]) && ((cycle - HBL_CNT) < (Sprites[i * 4 + 1] + 8 * (double_size / 2))))
							{
								// character is in drawing region, pick a pixel
								int offset_y = (LY - Sprites[i * 4]) >> (double_size / 2);
								int offset_x = ((cycle - HBL_CNT) - Sprites[i * 4 + 1]) >> (double_size / 2 - 1);

								int pixel_pick = (Sprite_Shapes[i * 8 + offset_y] >> offset_x) & 1;

								if (pixel_pick == 1)
								{
									if (Core._settings.Show_Sprites)
									{
										Core._vidbuffer[LY * 186 + (cycle - HBL_CNT)] = (int)Color_Palette_SPR[(Sprites[i * 4 + 2] >> 3) & 0x7];
									}

									Pixel_Stat |= (byte)(1 << i);
								}
							}
						}
					}

					// calculate collision
					int col_bit = -1;
					for (int i = 7; i >= 0; i--)
					{
						if (VDC_collision.Bit(i))
						{
							col_bit = i;
						}
					}

					if (col_bit >= 0)
					{
						for (int i = 0; i < 8; i++)
						{
							if (Pixel_Stat.Bit(i) & Pixel_Stat.Bit(col_bit) && (i != col_bit))
							{
								VDC_col_ret |= (byte)(1 << i);
							}
						}
					}
				}
			}

			cycle++;

			// end of scanline
			if (cycle == 228)
			{
				cycle = 0;

				LY++;

				if (LY == 240)
				{
					VBL = true;
					Core.in_vblank = true;
					VDC_status |= 0x08;
					Core.cpu.IRQPending = true;
					Core.cpu.T1 = true;
				}
				if (LY == 262)
				{
					LY = 0;
					VBL = false;
					Core.in_vblank = false;
					VDC_status &= 0xF7;
					VDC_col_ret = 0;
				}
				if (LY < 240)
				{
					HBL = true;
					VDC_status &= 0xFE;
					// send T1 pulses
					Core.cpu.T1 = true;
				}
			}
		}

		// might be needed, not sure yet
		public void latch_delay()
		{

		}

		public void process_sprite()
		{

		}

		public void Reset()
		{
			Sprites = new byte[16];
			Sprite_Shapes = new byte[32];
			Foreground = new byte[48];
			Quad_Chars = new byte[64];
			Grid_H = new byte[18];
			Grid_V = new byte[10];

			AudioReset();
		}

		public static readonly byte[] Internal_Graphics = { 0x7C, 0xC6, 0xC6, 0xC6, 0xC6, 0xC6, 0x7C, 00, // 0				0x00
															0x18, 0x38, 0x18, 0x18, 0x18, 0x18, 0x3C, 00, // 1				0x01
															0x3C, 0x66, 0x0C, 0x18, 0x30, 0x60, 0x7E, 00, // 2				0x02
															0x3C, 0x66, 0x06, 0x1C, 0x06, 0x66, 0x3C, 00, // 3				0x03
															0xCC, 0xCC, 0xCC, 0xFE, 0x0C, 0x0C, 0x0C, 00, // 4				0x04
															0x7E, 0x60, 0x60, 0x3C, 0x06, 0x66, 0x3C, 00, // 5				0x05
															0x3C, 0x66, 0x60, 0x7C, 0x66, 0x66, 0x3C, 00, // 6				0x06
															0xFE, 0x06, 0x0C, 0x18, 0x30, 0x60, 0xC0, 00, // 7				0x07
															0x3C, 0x66, 0x66, 0x3C, 0x66, 0x66, 0x3C, 00, // 8				0x08
															0x3C, 0x66, 0x66, 0x3E, 0x02, 0x66, 0x3C, 00, // 9				0x09
															0x00, 0x18, 0x18, 0x00, 0x18, 0x18, 0x00, 00, // :				0x0A
															0x18, 0x7E, 0x58, 0x7E, 0x1A, 0x7E, 0x18, 00, // $				0x0B
															0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 00, //  				0x0C
															0x3C, 0x66, 0x0C, 0x18, 0x18, 0x00, 0x18, 00, // ?				0x0D
															0x60, 0x60, 0x60, 0x60, 0x60, 0x60, 0x7E, 00, // L				0x0E
															0x7C, 0x66, 0x66, 0x7C, 0x60, 0x60, 0x60, 00, // P				0x0F
															0x00, 0x18, 0x18, 0x7E, 0x18, 0x18, 0x00, 00, // +				0x10
															0xC6, 0xC6, 0xC6, 0xD6, 0xFE, 0xEE, 0xC6, 00, // W				0x11
															0x7E, 0x60, 0x60, 0x7C, 0x60, 0x60, 0x7E, 00, // E				0x12
															0xFC, 0xC6, 0xC6, 0xFC, 0xD8, 0xCC, 0xC6, 00, // R				0x13
															0x7E, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 00, // T				0x14
															0xC6, 0xC6, 0xC6, 0xC6, 0xC6, 0xC6, 0x7C, 00, // U				0x15
															0x3C, 0x18, 0x18, 0x18, 0x18, 0x18, 0x3C, 00, // I				0x16
															0x7C, 0xC6, 0xC6, 0xC6, 0xC6, 0xC6, 0x7C, 00, // O				0x17
															0x7C, 0xC6, 0xC6, 0xC6, 0xD6, 0xCC, 0x76, 00, // Q				0x18
															0x3C, 0x66, 0x60, 0x3C, 0x06, 0x66, 0x3C, 00, // S				0x19
															0x7C, 0x66, 0x66, 0x66, 0x66, 0x66, 0x7C, 00, // D				0x1A
															0xFE, 0xC0, 0xC0, 0xF8, 0xC0, 0xC0, 0xC0, 00, // F				0x1B
															0x7C, 0xC6, 0xC0, 0xC0, 0xCE, 0xC6, 0x7E, 00, // G				0x1C
															0xC6, 0xC6, 0xC6, 0xFE, 0xC6, 0xC6, 0xC6, 00, // H				0x1D
															0x06, 0x06, 0x06, 0x06, 0x06, 0xC6, 0x7C, 00, // J				0x1E
															0xC6, 0xCC, 0xD8, 0xF0, 0xD8, 0xCC, 0xC6, 00, // K				0x1F
															0x38, 0x6C, 0xC6, 0xC6, 0xFE, 0xC6, 0xC6, 00, // A				0x20
															0x7E, 0x06, 0x0C, 0x18, 0x30, 0x60, 0x7E, 00, // Z				0x21
															0xC6, 0xC6, 0x6C, 0x38, 0x6C, 0xC6, 0xC6, 00, // X				0x22
															0x7C, 0xC6, 0xC0, 0xC0, 0xC0, 0xC6, 0x7C, 00, // C				0x23
															0xC6, 0xC6, 0xC6, 0xC6, 0xC6, 0x6C, 0x38, 00, // V				0x24
															0x7C, 0x66, 0x66, 0x7C, 0x66, 0x66, 0x7C, 00, // B				0x25
															0xC6, 0xEE, 0xFE, 0xD6, 0xC6, 0xC6, 0xC6, 00, // M				0x26
															0x00, 0x00, 0x00, 0x00, 0x00, 0x38, 0x38, 00, // .				0x27
															0x00, 0x00, 0x00, 0x7E, 0x00, 0x00, 0x00, 00, // -				0x28
															0x00, 0x66, 0x3C, 0x18, 0x3C, 0x66, 0x00, 00, // x				0x29
															0x00, 0x18, 0x00, 0x7E, 0x00, 0x18, 0x00, 00, // (div)			0x2A
															0x00, 0x00, 0x7E, 0x00, 0x7E, 0x00, 0x00, 00, // =				0x2B
															0x66, 0x66, 0x66, 0x3C, 0x18, 0x18, 0x18, 00, // Y				0x2C
															0xC6, 0xE6, 0xF6, 0xFE, 0xDE, 0xCE, 0xC6, 00, // N				0x2D
															0x03, 0x06, 0x0C, 0x18, 0x30, 0x60, 0xC0, 00, // /				0x2E
															0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 00, // (box)			0x2F
															0xCE, 0xDB, 0xDB, 0xDB, 0xDB, 0xDB, 0xCE, 00, // 10				0x30
															0x00, 0x00, 0x3C, 0x7E, 0x7E, 0x7E, 0x3C, 00, // (ball)			0x31
															0x38, 0x38, 0x30, 0x3C, 0x30, 0x30, 0x38, 00, // (person R)		0x32
															0x38, 0x38, 0x30, 0x3C, 0x30, 0x68, 0x4C, 00, // (runner R)		0x33
															0x38, 0x38, 0x18, 0x78, 0x18, 0x2C, 0x64, 00, // (runner L)		0x34
															0x38, 0x38, 0x18, 0x78, 0x18, 0x18, 0x38, 00, // (person L)		0x35
															0x00, 0x18, 0x0C, 0xFE, 0x0C, 0x18, 0x00, 00, // (arrow R)		0x36
															0x18, 0x3C, 0x7E, 0xFF, 0xFF, 0x18, 0x18, 00, // (tree)			0x37
															0x01, 0x03, 0x07, 0x0F, 0x1F, 0x3F, 0x7F, 00, // (ramp R)		0x38
															0x80, 0xC0, 0xE0, 0xF0, 0xF8, 0xFC, 0xFE, 00, // (ramp L)		0x39
															0x38, 0x38, 0x12, 0xFE, 0xB8, 0x28, 0x6C, 00, // (person F)		0x3A
															0xC0, 0x60, 0x30, 0x18, 0x0C, 0x06, 0x03, 00, // \				0x3B
															0x00, 0x00, 0x18, 0x10, 0x10, 0xF7, 0x7C, 00, // (boat 1)		0x3C
															0x00, 0x03, 0x63, 0xFF, 0xFF, 0x18, 0x08, 00, // (plane)		0x3D
															0x00, 0x00, 0x00, 0x01, 0x38, 0xFF, 0x7E, 00, // (boat 2)		0x3E
															0x00, 0x00, 0x00, 0x54, 0x54, 0xFF, 0x7E, 00  // (boat 3 unk)	0x3F
															};

		public static readonly uint[] Color_Palette_SPR =
		{
			0xFF676767, // grey
			0xFFC75151, // light red
			0xFF56C469, // light green
			0xFFC6B869, // light yellow
			0xFF5C80F6, // light blue
			0xFFDC84D4, // light violet
			0xFFCECECE, // light grey
			0xFFFFFFFF, // white
		};

		public static readonly uint[] Color_Palette_BG =
		{
			0xFF000000, // black
			0xFF1A37BE, // blue
			0xFF006D07, // green
			0xFF2AAABE, // blue-green
			0xFF790000, // red
			0xFF94309F, // violet
			0xFF77670B, // yellow
			0xFF676767, // grey
			0xFF000000, // black
			0xFF5C80F6, // light blue
			0xFF56C469, // light green
			0xFF77E6EB, // light blue-green
			0xFFC75151, // light red
			0xFFDC84D4, // light violet
			0xFFC6B869, // light yellow
			0xFFCECECE, // light grey
		};


		public void SyncState(Serializer ser)
		{
			ser.Sync(nameof(Sprites), ref Sprites, false);
			ser.Sync(nameof(Sprite_Shapes), ref Sprite_Shapes, false);
			ser.Sync(nameof(Foreground), ref Foreground, false);
			ser.Sync(nameof(Quad_Chars), ref Quad_Chars, false);
			ser.Sync(nameof(Grid_H), ref Grid_H, false);
			ser.Sync(nameof(Grid_V), ref Grid_V, false);
			ser.Sync(nameof(A4_latch), ref A4_latch);
			ser.Sync(nameof(A5_latch), ref A5_latch);

			ser.Sync(nameof(VDC_ctrl), ref VDC_ctrl);
			ser.Sync(nameof(VDC_status), ref VDC_status);
			ser.Sync(nameof(VDC_collision), ref VDC_collision);
			ser.Sync(nameof(VDC_col_ret), ref VDC_col_ret);
			ser.Sync(nameof(VDC_color), ref VDC_color);
			ser.Sync(nameof(Pixel_Stat), ref Pixel_Stat);
			ser.Sync(nameof(bg_brightness), ref bg_brightness);
			ser.Sync(nameof(grid_brightness), ref grid_brightness);

			ser.Sync(nameof(grid_fill), ref grid_fill);
			ser.Sync(nameof(grid_fill_col), ref grid_fill_col);
			ser.Sync(nameof(LY), ref LY);
			ser.Sync(nameof(cycle), ref cycle);
			ser.Sync(nameof(VBL), ref VBL);
			ser.Sync(nameof(HBL), ref HBL);

			AudioSyncState(ser);
		}

		private BlipBuffer _blip_C = new BlipBuffer(15000);

		public byte sample;

		public byte shift_0, shift_1, shift_2, aud_ctrl;
		public byte shift_reg_0, shift_reg_1, shift_reg_2;

		public uint master_audio_clock;

		public int tick_cnt, output_bit;

		public int latched_sample_C;

		public byte AudioReadReg(int addr)
		{
			byte ret = 0;

			switch (addr)
			{
				case 0xA7: ret = shift_reg_0; break;
				case 0xA8: ret = shift_reg_1; break;
				case 0xA9: ret = shift_reg_2; break;
				case 0xAA: ret = aud_ctrl; break;
			}
			//Console.WriteLine("aud read: " + (addr - 0xA7) + " " + ret + " " + Core.cpu.TotalExecutedCycles);
			return ret;
		}

		public void AudioWriteReg(int addr, byte value)
		{
			switch (addr)
			{
				case 0xA7: shift_0 = shift_reg_0 = value; break;
				case 0xA8: shift_1 = shift_reg_1 = value; break;
				case 0xA9: shift_2 = shift_reg_2 = value; break;
				case 0xAA: aud_ctrl = value; break;
			}

			//Console.WriteLine("aud write: " + (addr - 0xA7) + " " + value + " " + Core.cpu.TotalExecutedCycles);
		}

		public void Audio_tick()
		{
			int C_final = 0;

			if (aud_ctrl.Bit(7))
			{
				tick_cnt++;
				if (tick_cnt > (aud_ctrl.Bit(5) ? 455 : 1820))
				{
					tick_cnt = 0;

					output_bit = shift_2 & 1;

					shift_2 = (byte)((shift_2 >> 1) | ((shift_1 & 1) << 7));
					shift_1 = (byte)((shift_1 >> 1) | ((shift_0 & 1) << 7));

					if (aud_ctrl.Bit(6))
					{
						shift_0 = (byte)((shift_0 >> 1) | (output_bit << 7));
					}
					else
					{
						shift_0 = (byte)(shift_0 >> 1);
					}

					if (aud_ctrl.Bit(4))
					{
						if (shift_2.Bit(7) == output_bit.Bit(0))
						{
							shift_2 &= 0x7F;
						}
						else
						{
							shift_2 = (byte)(shift_2 | 0x80);
						}
						
					}
				}

				C_final = output_bit;
				C_final *= ((aud_ctrl & 0xF) + 1) * 3200;
			}

			if (C_final != latched_sample_C)
			{
				_blip_C.AddDelta(master_audio_clock, C_final - latched_sample_C);
				latched_sample_C = C_final;
			}

			master_audio_clock++;
		}

		public void AudioReset()
		{
			master_audio_clock = 0;

			sample = 0;

			_blip_C.SetRates(1792000, 44100);
		}

		public void AudioSyncState(Serializer ser)
		{
			ser.Sync(nameof(master_audio_clock), ref master_audio_clock);

			ser.Sync(nameof(sample), ref sample);
			ser.Sync(nameof(latched_sample_C), ref latched_sample_C);

			ser.Sync(nameof(aud_ctrl), ref aud_ctrl);
			ser.Sync(nameof(shift_0), ref shift_0);
			ser.Sync(nameof(shift_1), ref shift_1);
			ser.Sync(nameof(shift_2), ref shift_2);
			ser.Sync(nameof(shift_reg_0), ref shift_reg_0);
			ser.Sync(nameof(shift_reg_1), ref shift_reg_1);
			ser.Sync(nameof(shift_reg_2), ref shift_reg_2);
			ser.Sync(nameof(tick_cnt), ref tick_cnt);
			ser.Sync(nameof(output_bit), ref output_bit);
		}

		#region audio

		public bool CanProvideAsync => false;

		public void SetSyncMode(SyncSoundMode mode)
		{
			if (mode != SyncSoundMode.Sync)
			{
				throw new InvalidOperationException("Only Sync mode is supported_");
			}
		}

		public SyncSoundMode SyncMode => SyncSoundMode.Sync;

		public void GetSamplesSync(out short[] samples, out int nsamp)
		{
			_blip_C.EndFrame(master_audio_clock);

			nsamp = _blip_C.SamplesAvailable();

			samples = new short[nsamp * 2];

			if (nsamp != 0)
			{
				_blip_C.ReadSamples(samples, nsamp, true);
			}

			master_audio_clock = 0;
		}

		public void GetSamplesAsync(short[] samples)
		{
			throw new NotSupportedException("Async is not available");
		}

		public void DiscardSamples()
		{
			_blip_C.Clear();
			master_audio_clock = 0;
		}

		private void GetSamples(short[] samples)
		{

		}

		public void DisposeSound()
		{
			_blip_C.Clear();
			_blip_C.Dispose();
			_blip_C = null;
		}

		#endregion
	}
}
