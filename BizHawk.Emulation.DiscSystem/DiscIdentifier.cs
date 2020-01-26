﻿using System;
using System.Collections.Generic;
using System.Linq;

//disc type identification logic

namespace BizHawk.Emulation.DiscSystem
{
	public enum DiscType
	{
		/// <summary>
		/// Disc contains audio in track 1. This may be a PCFX or PCECD game, but if not it is assumed AudioDisc
		/// </summary>
		AudioDisc,

		/// <summary>
		/// Nothing is known about this data disc type
		/// </summary>
		UnknownFormat,

		/// <summary>
		/// This is definitely a CDFS disc, but we can't identify anything more about it
		/// </summary>
		UnknownCDFS,

		/// <summary>
		/// Sony PSX
		/// </summary>
		SonyPSX,

		/// <summary>
		/// Sony PSP
		/// </summary>
		SonyPSP,

		/// <summary>
		/// Sega Saturn
		/// </summary>
		SegaSaturn,

		/// <summary>
		/// PC Engine CD
		/// </summary>
		TurboCD,

		/// <summary>
		/// MegaDrive addon
		/// </summary>
		MegaCD,

		/// <summary>
		/// By NEC.
		/// </summary>
		PCFX,

		/// <summary>
		/// By Panasonic
		/// </summary>
		Panasonic3DO,

		/// <summary>
		/// Philips
		/// </summary>
		CDi,

		/// <summary>
		/// Nintendo Gamecube
		/// </summary>
		GameCube,

		/// <summary>
		/// Nintendo Wii
		/// </summary>
		Wii,

		/// <summary>
		/// SNK NeoGeo
		/// </summary>
		NeoGeoCD,

		/// <summary>
		/// Bandai Playdia
		/// </summary>
		Playdia,

		/// <summary>
		/// Either CDTV or CD32 (I havent found a reliable way of distinguishing between them yet -asni)
		/// </summary>
		Amiga,

		/// <summary>
		/// Sega Dreamcast
		/// </summary>
		Dreamcast
	}

	public class DiscIdentifier
	{
		public DiscIdentifier(Disc disc)
		{
			_disc = disc;
			_dsr = new DiscSectorReader(disc);

			//the first check for mode 0 should be sufficient for blocking attempts to read audio sectors
			//but github #928 had a data track with an audio sector
			//so let's be careful here.. we're just trying to ID things, not be robust
			_dsr.Policy.ThrowExceptions2048 = false;
		}

		private readonly Disc _disc;
		private readonly DiscSectorReader _dsr;
		private readonly Dictionary<int, byte[]> _sectorCache = new Dictionary<int, byte[]>();

		/// <summary>
		/// Attempts to determine the type of the disc.
		/// In the future, we might return a struct or a class with more detailed information
		/// </summary>
		public DiscType DetectDiscType()
		{
			// PCFX & TurboCD sometimes (if not alltimes) have audio on track 1 - run these before the AudioDisc detection (asni)
			if (DetectPCFX())
				return DiscType.PCFX;

			if (DetectTurboCD())
				return DiscType.TurboCD;            

			//check track 1's data type. if it's an audio track, further data-track testing is useless
			//furthermore, it's probably senseless (no binary data there to read)
			if (!_disc.TOC.TOCItems[1].IsData)
				return DiscType.AudioDisc;

			// if (_dsr.ReadLBA_Mode(_disc.TOC.TOCItems[1].LBA) == 0)
				// return DiscType.AudioDisc;

			// sega doesnt put anything identifying in the cdfs volume info. but its consistent about putting its own header here in sector 0
			//asni - this isn't strictly true - SystemIdentifier in volume descriptor has been observed on occasion (see below)
			if (DetectSegaSaturn())
				return DiscType.SegaSaturn;

			// not fully tested yet
			if (DetectMegaCD())
				return DiscType.MegaCD;

			// not fully tested yet
			if (DetectPSX())
				return DiscType.SonyPSX;

			if (Detect3DO())
				return DiscType.Panasonic3DO;

			if (DetectCDi())
				return DiscType.CDi;

			if (DetectGameCube())
				return DiscType.GameCube;

			if (DetectWii())
				return DiscType.Wii;

			var discView = EDiscStreamView.DiscStreamView_Mode1_2048;
			if (_disc.TOC.Session1Format == SessionFormat.Type20_CDXA)
				discView = EDiscStreamView.DiscStreamView_Mode2_Form1_2048;

			var iso = new ISOFile();
			bool isIso = iso.Parse(new DiscStream(_disc, discView, 0));

			if (!isIso)
			{
				// its much quicker to detect dreamcast from ISO data. Only do this if ISO is not detected
				if (DetectDreamcast())
					return DiscType.Dreamcast;
			}

			//*** asni - 20171011 - Suggestion: move this to the beginning of the DetectDiscType() method before any longer running lookups?
			//its a cheap win for a lot of systems, but ONLY if the iso.Parse() method is quick running (might have to time it)
			if (isIso)
			{
				var appId = System.Text.Encoding.ASCII.GetString(iso.VolumeDescriptors[0].ApplicationIdentifier).TrimEnd('\0', ' ');
				var sysId = System.Text.Encoding.ASCII.GetString(iso.VolumeDescriptors[0].SystemIdentifier).TrimEnd('\0', ' ');

				//for example: PSX magical drop F (JP SLPS_02337) doesn't have the correct iso PVD fields
				//but, some PSX games (junky rips) don't have the 'licensed by string' so we'll hope they get caught here
				if (appId == "PLAYSTATION")
					return DiscType.SonyPSX;

				if (appId == "PSP GAME")
					return DiscType.SonyPSP;
				// in case the appId is not set correctly...
				if (iso.Root.Children.Where(a => a.Key == "PSP_GAME").FirstOrDefault().Value as ISODirectoryNode != null)
					return DiscType.SonyPSP;

				if (sysId == "SEGA SEGASATURN")
					return DiscType.SegaSaturn;

				if (sysId.Contains("SEGAKATANA"))
					return DiscType.Dreamcast;

				if (sysId == "MEGA_CD")
					return DiscType.MegaCD;

				if (sysId == "ASAHI-CDV")
					return DiscType.Playdia;

				if (sysId == "CDTV" || sysId == "AMIGA")
					return DiscType.Amiga;
				foreach (var f in iso.Root.Children)
					if (f.Key.ToLower().Contains("cd32"))
						return DiscType.Amiga;

				// NeoGeoCD Check
				var absTxt = iso.Root.Children.Where(a => a.Key.Contains("ABS.TXT")).ToList();
				if (absTxt.Count > 0)
				{
					if (SectorContains("abstracted by snk", Convert.ToInt32(absTxt.First().Value.Offset)))
						return DiscType.NeoGeoCD;
				}
					

				return DiscType.UnknownCDFS;
			}                

			return DiscType.UnknownFormat;
		}

		/// <summary>
		/// This is reasonable approach to ID saturn.
		/// </summary>
		bool DetectSegaSaturn()
		{
			return StringAt("SEGA SEGASATURN", 0);
		}

		/// <summary>
		/// probably wrong
		/// </summary>
		bool DetectMegaCD()
		{
			return StringAt("SEGADISCSYSTEM", 0) || StringAt("SEGADISCSYSTEM", 16);
		}

		bool DetectPSX()
		{
			if (!StringAt("          Licensed  by          ", 0, 4)) return false;
			return (StringAt("Sony Computer Entertainment Euro", 32, 4)
				|| StringAt("Sony Computer Entertainment Inc.", 32, 4)
				|| StringAt("Sony Computer Entertainment Amer", 32, 4)
				|| StringAt("Sony Computer Entertainment of A", 32, 4)
				);
		}

		bool DetectPCFX()
		{
			var toc = _disc.TOC;
			for (int t = toc.FirstRecordedTrackNumber;
				t <= toc.LastRecordedTrackNumber;
				t++)
			{
				var track = _disc.TOC.TOCItems[t];
				//asni - this search is less specific - turns out there are discs where 'Hu:' is not present
				if (track.IsData && SectorContains("pc-fx", track.LBA))
					return true;
			}
			return false;
		}

		//asni 20171011 - this ONLY works if a valid cuefile/ccd is passed into DiscIdentifier.
		//if an .iso is presented, the internally manufactured cue data does not work - possibly something to do with
		//track 01 being Audio. Not tested, but presumably PCFX has the same issue
		bool DetectTurboCD()
		{
			var toc = _disc.TOC;
			for (int t = toc.FirstRecordedTrackNumber;
				t <= toc.LastRecordedTrackNumber;
				t++)
			{
				var track = _disc.TOC.TOCItems[t];
				//asni - pcfx games also contain the 'PC Engine' string
				if ((track.IsData && SectorContains("pc engine", track.LBA + 1) && !SectorContains("pc-fx", track.LBA + 1)))
					return true;
			}
			return false;
		}

		bool Detect3DO()
		{
			var toc = _disc.TOC;
			for (int t = toc.FirstRecordedTrackNumber;
				t <= toc.LastRecordedTrackNumber;
				t++)
			{
				var track = _disc.TOC.TOCItems[t];
				if (track.IsData && SectorContains("iamaduckiamaduck", track.LBA))
					return true;
			}
			return false;
		}

		//asni - slightly longer running than the others due to its brute-force nature. Should run later in the method
		bool DetectDreamcast()
		{
			for (int i = 0; i < 1000; i++)
			{
				if (SectorContains("segakatana", i))
					return true;
			}

			return false;
		}

		bool DetectCDi()
		{
			return StringAt("CD-RTOS", 8, 16);
		}

		bool DetectGameCube()
		{
			var data = ReadSectorCached(0);
			if (data == null) return false;
			byte[] magic = data.Skip(28).Take(4).ToArray();
			string hexString = "";
			foreach (var b in magic)            
				hexString += b.ToString("X2");

			return hexString == "C2339F3D";
		}

		bool DetectWii()
		{
			var data = ReadSectorCached(0);
			if (data == null) return false;
			byte[] magic = data.Skip(24).Take(4).ToArray();
			string hexString = "";
			foreach (var b in magic)
				hexString += b.ToString("X2");

			return hexString == "5D1C9EA3";
		}

		private byte[] ReadSectorCached(int lba)
		{
			//read it if we dont have it cached
			//we wont be caching very much here, it's no big deal
			//identification is not something we want to take a long time
			byte[] data;
			if (!_sectorCache.TryGetValue(lba, out data))
			{
				data = new byte[2048];
				int read = _dsr.ReadLBA_2048(lba, data, 0);
				if (read != 2048)
					return null;
				_sectorCache[lba] = data;
			}
			return data;
		}

		private bool StringAt(string s, int n, int lba = 0)
		{
			var data = ReadSectorCached(lba);
			if (data == null) return false;
			byte[] cmp = System.Text.Encoding.ASCII.GetBytes(s);
			byte[] cmp2 = new byte[cmp.Length];
			Buffer.BlockCopy(data, n, cmp2, 0, cmp.Length);
			return System.Linq.Enumerable.SequenceEqual(cmp, cmp2);
		}

		private bool SectorContains(string s, int lba = 0)
		{
			var data = ReadSectorCached(lba);
			if (data == null) return false;
			return System.Text.Encoding.ASCII.GetString(data).ToLower().Contains(s.ToLower());
		}
	}
}