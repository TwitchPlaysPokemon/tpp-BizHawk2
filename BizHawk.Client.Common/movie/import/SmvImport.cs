﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using BizHawk.Emulation.Common;
using BizHawk.Emulation.Cores.Nintendo.SNES;

namespace BizHawk.Client.Common.movie.import
{
	// ReSharper disable once UnusedMember.Global
	// http://tasvideos.org/EmulatorResources/Snes9x/SMV.html
	[ImporterFor("Snes9x", ".smv")]
	internal class SmvImport : MovieImporter
	{
		private LibsnesControllerDeck _deck;

		protected override void RunImport()
		{
			var bsnesName = ((CoreAttribute)Attribute.GetCustomAttribute(typeof(LibsnesCore), typeof(CoreAttribute))).CoreName;
			Result.Movie.HeaderEntries[HeaderKeys.CORE] = bsnesName;

			using var fs = SourceFile.Open(FileMode.Open, FileAccess.Read);
			using var r = new BinaryReader(fs);

			// 000 4-byte signature: 53 4D 56 1A "SMV\x1A"
			string signature = new string(r.ReadChars(4));
			if (signature != "SMV\x1A")
			{
				Result.Errors.Add("This is not a valid .SMV file.");
				return;
			}

			Result.Movie.HeaderEntries[HeaderKeys.PLATFORM] = "SNES";

			// 004 4-byte little-endian unsigned int: version number
			uint versionNumber = r.ReadUInt32();
			string version;
			switch (versionNumber)
			{
				case 1:
					version = "1.43";
					break;
				case 4:
					version = "1.51";
					break;
				case 5:
					version = "1.52";
					break;
				default:
					version = "Unknown";
					break;
			}

			Result.Movie.Comments.Add($"{EmulationOrigin} Snes9x version {version}");
			Result.Movie.Comments.Add($"{MovieOrigin} .SMV");

			/*
			 008 4-byte little-endian integer: movie "uid" - identifies the movie-savestate relationship, also used as the
			 recording time in Unix epoch format
			*/
			uint uid = r.ReadUInt32();

			// 00C 4-byte little-endian unsigned int: rerecord count
			Result.Movie.Rerecords = r.ReadUInt32();

			// 010 4-byte little-endian unsigned int: number of frames
			uint frameCount = r.ReadUInt32();

			// 014 1-byte flags "controller mask"
			byte controllerFlags = r.ReadByte();

			/*
			 * bit 0: controller 1 in use
			 * bit 1: controller 2 in use
			 * bit 2: controller 3 in use
			 * bit 3: controller 4 in use
			 * bit 4: controller 5 in use
			 * other: reserved, set to 0
			*/
			bool[] controllersUsed = new bool[5]; // Eww, this is a clunky way to do this
			for (int controller = 1; controller <= controllersUsed.Length; controller++)
			{
				controllersUsed[controller - 1] = ((controllerFlags >> (controller - 1)) & 0x1) != 0;
			}

			var controllerCount = controllersUsed.Count(c => c);

			var ss = new LibsnesCore.SnesSyncSettings
			{
				LeftPort = LibsnesControllerDeck.ControllerType.Gamepad,
				RightPort = LibsnesControllerDeck.ControllerType.Gamepad
			};

			if (controllerCount == 1)
			{
				ss.RightPort = LibsnesControllerDeck.ControllerType.Unplugged;
			}
			else if (controllerCount > 2)
			{
				// More than 2 controllers means a multi-tap on the first port
				// Snes9x only supported up to 5 controllers, so right port would never be multitap
				ss.LeftPort = LibsnesControllerDeck.ControllerType.Multitap;

				// Unless there are exactly 5, right port is unplugged, as the multitap will handle 4 controllers
				if (controllerCount < 5)
				{
					ss.RightPort = LibsnesControllerDeck.ControllerType.Unplugged;
				}
			}

			_deck = new LibsnesControllerDeck(ss);

			// 015 1-byte flags "movie options"
			byte movieFlags = r.ReadByte();
			/*
				 bit 0:
					 if "0", movie begins from an embedded "quicksave" snapshot
					 if "1", a SRAM is included instead of a quicksave; movie begins from reset
			*/
			if ((movieFlags & 0x1) == 0)
			{
				Result.Errors.Add("Movies that begin with a savestate are not supported.");
				return;
			}

			// bit 1: if "0", movie is NTSC (60 fps); if "1", movie is PAL (50 fps)
			bool pal = ((movieFlags >> 1) & 0x1) != 0;
			Result.Movie.HeaderEntries[HeaderKeys.PAL] = pal.ToString();

			// other: reserved, set to 0
			/*
			 016 1-byte flags "sync options":
				 bit 0: MOVIE_SYNC2_INIT_FASTROM
				 other: reserved, set to 0
			*/
			r.ReadByte();

			/*
			 017 1-byte flags "sync options":
				 bit 0: MOVIE_SYNC_DATA_EXISTS
					 if "1", all sync options flags are defined.
					 if "0", all sync options flags have no meaning.
				 bit 1: MOVIE_SYNC_WIP1TIMING
				 bit 2: MOVIE_SYNC_LEFTRIGHT
				 bit 3: MOVIE_SYNC_VOLUMEENVX
				 bit 4: MOVIE_SYNC_FAKEMUTE
				 bit 5: MOVIE_SYNC_SYNCSOUND
				 bit 6: MOVIE_SYNC_HASROMINFO
					 if "1", there is extra ROM info located right in between of the metadata and the savestate.
				 bit 7: set to 0.
			*/
			byte syncFlags = r.ReadByte();
			/*
			 Extra ROM info is always positioned right before the savestate. Its size is 30 bytes if MOVIE_SYNC_HASROMINFO
			 is used (and MOVIE_SYNC_DATA_EXISTS is set), 0 bytes otherwise.
			*/
			int extraRomInfo = (((syncFlags >> 6) & 0x1) != 0 && (syncFlags & 0x1) != 0) ? 30 : 0;

			// 018 4-byte little-endian unsigned int: offset to the savestate inside file
			uint savestateOffset = r.ReadUInt32();

			// 01C 4-byte little-endian unsigned int: offset to the controller data inside file
			uint firstFrameOffset = r.ReadUInt32();
			int[] controllerTypes = new int[2];

			// The (.SMV 1.51 and up) header has an additional 32 bytes at the end
			if (version != "1.43")
			{
				// 020 4-byte little-endian unsigned int: number of input samples, primarily for peripheral-using games
				r.ReadBytes(4);
				/*
				 024 2 1-byte unsigned ints: what type of controller is plugged into ports 1 and 2 respectively: 0=NONE,
				 1=JOYPAD, 2=MOUSE, 3=SUPERSCOPE, 4=JUSTIFIER, 5=MULTITAP
				*/
				controllerTypes[0] = r.ReadByte();
				controllerTypes[1] = r.ReadByte();

				// 026 4 1-byte signed ints: controller IDs of port 1, or -1 for unplugged
				r.ReadBytes(4);

				// 02A 4 1-byte signed ints: controller IDs of port 2, or -1 for unplugged
				r.ReadBytes(4);

				// 02E 18 bytes: reserved for future use
				r.ReadBytes(18);
			}

			/*
			 After the header comes "metadata", which is UTF16-coded movie title string (author info). The metadata begins
			 from position 32 (0x20 (0x40 for 1.51 and up)) and ends at <savestate_offset -
			 length_of_extra_rom_info_in_bytes>.
			*/
			byte[] metadata = r.ReadBytes((int)(savestateOffset - extraRomInfo - ((version != "1.43") ? 0x40 : 0x20)));
			string author = NullTerminated(Encoding.Unicode.GetString(metadata).Trim());
			if (!string.IsNullOrWhiteSpace(author))
			{
				Result.Movie.HeaderEntries[HeaderKeys.AUTHOR] = author;
			}

			if (extraRomInfo == 30)
			{
				// 000 3 bytes of zero padding: 00 00 00 003 4-byte integer: CRC32 of the ROM 007 23-byte ascii string
				r.ReadBytes(3);
				int crc32 = r.ReadInt32();
				Result.Movie.HeaderEntries["CRC32"] = crc32.ToString();

				// the game name copied from the ROM, truncated to 23 bytes (the game name in the ROM is 21 bytes)
				string gameName = NullTerminated(Encoding.UTF8.GetString(r.ReadBytes(23)));
				Result.Movie.HeaderEntries[HeaderKeys.GAMENAME] = gameName;
			}

			SimpleController controllers = new SimpleController
			{
				Definition = _deck.Definition
			};

			r.BaseStream.Position = firstFrameOffset;
			/*
			 01 00 (reserved)
			 02 00 (reserved)
			 04 00 (reserved)
			 08 00 (reserved)
			 10 00 R
			 20 00 L
			 40 00 X
			 80 00 A
			 00 01 Right
			 00 02 Left
			 00 04 Down
			 00 08 Up
			 00 10 Start
			 00 20 Select
			 00 40 Y
			 00 80 B
			*/
			string[] buttons =
			{
				"Right", "Left", "Down", "Up", "Start", "Select", "Y", "B", "R", "L", "X", "A"
			};

			for (int frame = 0; frame <= frameCount; frame++)
			{
				controllers["Reset"] = true;
				for (int player = 1; player <= controllersUsed.Length; player++)
				{
					if (!controllersUsed[player - 1])
					{
						continue;
					}

					/*
					 Each frame consists of 2 bytes per controller. So if there are 3 controllers, a frame is 6 bytes and
					 if there is only 1 controller, a frame is 2 bytes.
					*/
					byte controllerState1 = r.ReadByte();
					byte controllerState2 = r.ReadByte();

					/*
					 In the reset-recording patch, a frame that contains the value FF FF for every controller denotes a
					 reset. The reset is done through the S9xSoftReset routine.
					*/
					if (controllerState1 != 0xFF || controllerState2 != 0xFF)
					{
						controllers["Reset"] = false;
					}

					/*
					 While the meaning of controller data (for 1.51 and up) for a single standard SNES controller pad
					 remains the same, each frame of controller data can contain additional bytes if input for peripherals
					 is being recorded.
					*/
					if (version != "1.43" && player <= controllerTypes.Length)
					{
						var peripheral = "";
						switch (controllerTypes[player - 1])
						{
							case 0: // NONE
								continue;
							case 1: // JOYPAD
								break;
							case 2: // MOUSE
								peripheral = "Mouse";

								// 5*num_mouse_ports
								r.ReadBytes(5);
								break;
							case 3: // SUPERSCOPE
								peripheral = "Super Scope"; // 6*num_superscope_ports
								r.ReadBytes(6);
								break;
							case 4: // JUSTIFIER
								peripheral = "Justifier";

								// 11*num_justifier_ports
								r.ReadBytes(11);
								break;
							case 5: // MULTITAP
								peripheral = "Multitap";
								break;
						}

						if (peripheral != "" && !Result.Warnings.Any())
						{
							Result.Warnings.Add($"Unable to import {peripheral}. Not supported yet");
						}
					}

					ushort controllerState = (ushort)(((controllerState1 << 4) & 0x0F00) | controllerState2);
					for (int button = 0; button < buttons.Length; button++)
					{
						controllers[$"P{player} {buttons[button]}"] =
							((controllerState >> button) & 0x1) != 0;
					}
				}

				// The controller data contains <number_of_frames + 1> frames.
				if (frame == 0)
				{
					continue;
				}

				Result.Movie.AppendFrame(controllers);

				Result.Movie.SyncSettingsJson = ConfigService.SaveWithType(ss);
				Global.Config.SnesInSnes9x = false;
			}
		}
	}
}
