﻿using System;
using System.Collections.Generic;
using System.Linq;
using BizHawk.Emulation.Common;
using BizHawk.Emulation.Cores.Consoles.Sega.Saturn;

namespace BizHawk.Client.Common.movie.import
{
	// https://code.google.com/archive/p/yabause-rr/wikis/YMVfileformat.wiki
	// ReSharper disable once UnusedMember.Global
	[ImporterFor("Yabause", ".ymv")]
	internal class YmvImport : MovieImporter
	{
		protected override void RunImport()
		{
			Result.Movie.HeaderEntries[HeaderKeys.PLATFORM] = "SAT";
			var ss = new Saturnus.SyncSettings
			{
				Port1 = SaturnusControllerDeck.Device.Gamepad,
				Port2 = SaturnusControllerDeck.Device.None
			};

			using var sr = SourceFile.OpenText();
			string line;
			while ((line = sr.ReadLine()) != null)
			{
				if (string.IsNullOrWhiteSpace(line))
				{
					continue;
				}

				if (line[0] == '|')
				{
					ImportTextFrame(line);
				}
				else if (line.ToLower().StartsWith("emuversion"))
				{
					Result.Movie.Comments.Add($"{EmulationOrigin} Yabause version {ParseHeader(line, "emuVersion")}");
				}
				else if (line.ToLower().StartsWith("version"))
				{
					string version = ParseHeader(line, "version");
					Result.Movie.Comments.Add($"{MovieOrigin} .ymv version {version}");
				}
				else if (line.ToLower().StartsWith("cdGameName"))
				{
					Result.Movie.HeaderEntries[HeaderKeys.GAMENAME] = ParseHeader(line, "romFilename");
				}
				else if (line.ToLower().StartsWith("rerecordcount"))
				{
					int rerecordCount;

					// Try to parse the re-record count as an integer, defaulting to 0 if it fails.
					try
					{
						rerecordCount = int.Parse(ParseHeader(line, "rerecordCount"));
					}
					catch
					{
						rerecordCount = 0;
					}

					Result.Movie.Rerecords = (ulong)rerecordCount;
				}
				else if (line.ToLower().StartsWith("startsfromsavestate"))
				{
					// If this movie starts from a savestate, we can't support it.
					if (ParseHeader(line, "StartsFromSavestate") == "1")
					{
						Result.Errors.Add("Movies that begin with a savestate are not supported.");
					}
				}
				else if (line.ToLower().StartsWith("ispal"))
				{
					bool pal = ParseHeader(line, "isPal") == "1";
					Result.Movie.HeaderEntries[HeaderKeys.PAL] = pal.ToString();
				}
				else
				{
					// Everything not explicitly defined is treated as a comment.
					Result.Movie.Comments.Add(line);
				}
			}

			Result.Movie.SyncSettingsJson = ConfigService.SaveWithType(ss);
		}

		private void ImportTextFrame(string line)
		{
			// Yabause only supported 1 controller
			var controllers = new SimpleController
			{
				Definition = new ControllerDefinition
				{
					Name = "Saturn Controller",
					BoolButtons = new List<string>
					{
						"Reset", "Power", "Previous Disk", "Next Disk", "P1 Left", "P1 Right", "P1 Up", "P1 Down", "P1 Start", "P1 A", "P1 B", "P1 C", "P1 X", "P1 Y", "P1 Z", "P1 L", "P1 R"
					}
				}
			};

			// Split up the sections of the frame.
			string[] sections = line.Split(new [] { "|" }, StringSplitOptions.RemoveEmptyEntries);
			if (sections.Length != 2)
			{
				Result.Errors.Add("Unsupported input configuration");
				return;
			}

			if (sections[0][0] == '1')
			{
				controllers["Reset"] = true;
			}

			var buttonNames = controllers.Definition.ControlsOrdered.Skip(1).First().ToList();
			
			// Only count lines with that have the right number of buttons and are for valid players.
			if (sections[1].Length == buttonNames.Count)
			{
				for (int button = 0; button < buttonNames.Count; button++)
				{
					// Consider the button pressed so long as its spot is not occupied by a ".".
					controllers[buttonNames[button]] = sections[1][button] != '.';
				}
			}

			// Convert the data for the controllers to a mnemonic and add it as a frame.
			Result.Movie.AppendFrame(controllers);
		}

	}
}
