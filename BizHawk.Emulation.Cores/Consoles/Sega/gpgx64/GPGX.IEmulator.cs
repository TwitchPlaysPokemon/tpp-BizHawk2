﻿using System;
using BizHawk.Emulation.Common;

namespace BizHawk.Emulation.Cores.Consoles.Sega.gpgx
{
	public partial class GPGX : IEmulator, ISoundProvider
	{
		public IEmulatorServiceProvider ServiceProvider { get; }

		public ControllerDefinition ControllerDefinition { get; private set; }

		public bool FrameAdvance(IController controller, bool render, bool renderSound = true)
		{
			if (controller.IsPressed("Reset"))
				Core.gpgx_reset(false);
			if (controller.IsPressed("Power"))
				Core.gpgx_reset(true);
			if (_cds != null)
			{
				var prev = controller.IsPressed("Previous Disk");
				var next = controller.IsPressed("Next Disk");
				int newDisk = _discIndex;
				if (prev && !_prevDiskPressed)
					newDisk--;
				if (next && !_nextDiskPressed)
					newDisk++;

				_prevDiskPressed = prev;
				_nextDiskPressed = next;

				if (newDisk < -1)
					newDisk = -1;
				if (newDisk >= _cds.Length)
					newDisk = _cds.Length - 1;

				if (newDisk != _discIndex)
				{
					_discIndex = newDisk;
					Core.gpgx_swap_disc(_discIndex == -1 ? null : GetCDDataStruct(_cds[_discIndex]));
					Console.WriteLine("IMMA CHANGING MAH DISKS");
				}
			}

			// this shouldn't be needed, as nothing has changed
			// if (!Core.gpgx_get_control(input, inputsize))
			//	throw new Exception("gpgx_get_control() failed!");

			ControlConverter.ScreenWidth = _vwidth;
			ControlConverter.ScreenHeight = _vheight;
			ControlConverter.Convert(controller, input);

			if (!Core.gpgx_put_control(input, inputsize))
				throw new Exception($"{nameof(Core.gpgx_put_control)}() failed!");

			IsLagFrame = true;
			Frame++;
			_driveLight = false;

			Core.gpgx_advance();

			if (render)
				UpdateVideo();

			if (renderSound)
				update_audio();

			if (IsLagFrame)
				LagCount++;

			if (_cds != null)
				DriveLightOn = _driveLight;

			return true;
		}

		public int Frame { get; private set; }

		public string SystemId => "GEN";

		public bool DeterministicEmulation => true;

		public void ResetCounters()
		{
			Frame = 0;
			IsLagFrame = false;
			LagCount = 0;
		}

		public CoreComm CoreComm { get; }

		public void Dispose()
		{
			if (!_disposed)
			{
				_elf?.Dispose();
				if (_cds != null)
					foreach (var cd in _cds)
						cd.Dispose();
				_disposed = true;
			}
		}
	}
}
