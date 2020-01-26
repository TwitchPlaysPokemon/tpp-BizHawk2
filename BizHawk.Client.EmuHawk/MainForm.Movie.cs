﻿using System;
using System.IO;
using System.Windows.Forms;

using BizHawk.Client.Common;
using BizHawk.Emulation.Common;
using BizHawk.Emulation.Common.IEmulatorExtensions;

namespace BizHawk.Client.EmuHawk
{
	partial class MainForm
	{
		public bool StartNewMovie(IMovie movie, bool record)
		{
			// SuuperW: Check changes. adelikat: this could break bk2 movies
			// TODO: Clean up the saving process
			if (movie.IsActive() && (movie.Changes || !(movie is TasMovie)))
			{
				movie.Save();
			}

			try
			{
				MovieSession.QueueNewMovie(movie, record, Emulator);
			}
			catch (MoviePlatformMismatchException ex)
			{
				using var ownerForm = new Form { TopMost = true };
				MessageBox.Show(ownerForm, ex.Message, "Movie/Platform Mismatch", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}

			RebootCore();

			if (MovieSession.PreviousNES_InQuickNES.HasValue)
			{
				Config.NesInQuickNes = MovieSession.PreviousNES_InQuickNES.Value;
				MovieSession.PreviousNES_InQuickNES = null;
			}

			if (MovieSession.PreviousSNES_InSnes9x.HasValue)
			{
				Config.SnesInSnes9x = MovieSession.PreviousSNES_InSnes9x.Value;
				MovieSession.PreviousSNES_InSnes9x = null;
			}

			if (MovieSession.PreviousGBA_UsemGBA.HasValue)
			{
				Config.GbaUsemGba = MovieSession.PreviousGBA_UsemGBA.Value;
				MovieSession.PreviousGBA_UsemGBA = null;
			}

			Config.RecentMovies.Add(movie.Filename);

			if (Emulator.HasSavestates() && movie.StartsFromSavestate)
			{
				if (movie.TextSavestate != null)
				{
					Emulator.AsStatable().LoadStateText(new StringReader(movie.TextSavestate));
				}
				else
				{
					Emulator.AsStatable().LoadStateBinary(new BinaryReader(new MemoryStream(movie.BinarySavestate, false)));
				}

				if (movie.SavestateFramebuffer != null && Emulator.HasVideoProvider())
				{
					var b1 = movie.SavestateFramebuffer;
					var b2 = Emulator.AsVideoProvider().GetVideoBuffer();
					int len = Math.Min(b1.Length, b2.Length);
					for (int i = 0; i < len; i++)
					{
						b2[i] = b1[i];
					}
				}

				Emulator.ResetCounters();
			}
			else if (Emulator.HasSaveRam() && movie.StartsFromSaveRam)
			{
				Emulator.AsSaveRam().StoreSaveRam(movie.SaveRam);
			}

			MovieSession.RunQueuedMovie(record);

			SetMainformMovieInfo();

			Tools.Restart<VirtualpadTool>();


			if (MovieSession.Movie.Hash != Game.Hash)
			{
				AddOnScreenMessage("Warning: Movie hash does not match the ROM");
			}

			return !(Emulator is NullEmulator);
		}

		public void SetMainformMovieInfo()
		{
			if (MovieSession.Movie.IsPlaying())
			{
				PlayRecordStatusButton.Image = Properties.Resources.Play;
				PlayRecordStatusButton.ToolTipText = "Movie is in playback mode";
				PlayRecordStatusButton.Visible = true;
			}
			else if (MovieSession.Movie.IsRecording())
			{
				PlayRecordStatusButton.Image = Properties.Resources.RecordHS;
				PlayRecordStatusButton.ToolTipText = "Movie is in record mode";
				PlayRecordStatusButton.Visible = true;
			}
			else if (!MovieSession.Movie.IsActive())
			{
				PlayRecordStatusButton.Image = Properties.Resources.Blank;
				PlayRecordStatusButton.ToolTipText = "No movie is active";
				PlayRecordStatusButton.Visible = false;
			}

			SetWindowText();
			UpdateStatusSlots();
		}

		public void RestartMovie()
		{
			if (IsSlave && Master.WantsToControlRestartMovie)
			{
				Master.RestartMovie();
			}
			else
			{
				if (MovieSession.Movie.IsActive())
				{
					StartNewMovie(MovieSession.Movie, false);
					AddOnScreenMessage("Replaying movie file in read-only mode");
				}
			}
		}
	}
}
