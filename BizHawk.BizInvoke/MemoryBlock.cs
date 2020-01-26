﻿using System;
using System.Runtime.InteropServices;
using System.IO;

namespace BizHawk.BizInvoke
{
	public sealed class MemoryBlock : MemoryBlockBase
	{
		/// <summary>
		/// handle returned by CreateFileMapping
		/// </summary>
		private IntPtr _handle;

		/// <inheritdoc cref="MemoryBlockBase(ulong,ulong)"/>
		/// <exception cref="InvalidOperationException">failed to create file mapping</exception>
		public MemoryBlock(ulong start, ulong size) : base(start, size)
		{
			_handle = Kernel32.CreateFileMapping(
				Kernel32.INVALID_HANDLE_VALUE,
				IntPtr.Zero,
				Kernel32.FileMapProtection.PageExecuteReadWrite | Kernel32.FileMapProtection.SectionCommit,
				(uint)(Size >> 32),
				(uint)Size,
				null
			);
			if (_handle == IntPtr.Zero) throw new InvalidOperationException($"{nameof(Kernel32.CreateFileMapping)}() returned NULL");
		}

		/// <exception cref="InvalidOperationException"><see cref="MemoryBlockBase.Active"/> is <see langword="true"/> or failed to map file view</exception>
		public override void Activate()
		{
			if (Active)
				throw new InvalidOperationException("Already active");
			if (Kernel32.MapViewOfFileEx(
					_handle,
					Kernel32.FileMapAccessType.Read | Kernel32.FileMapAccessType.Write | Kernel32.FileMapAccessType.Execute,
					0,
					0,
					Z.UU(Size),
					Z.US(AddressRange.Start)
				) != Z.US(AddressRange.Start))
			{
				throw new InvalidOperationException($"{nameof(Kernel32.MapViewOfFileEx)}() returned NULL");
			}
			ProtectAll();
			Active = true;
		}

		/// <exception cref="InvalidOperationException"><see cref="MemoryBlockBase.Active"/> is <see langword="false"/> or failed to unmap file view</exception>
		public override void Deactivate()
		{
			if (!Active)
				throw new InvalidOperationException("Not active");
			if (!Kernel32.UnmapViewOfFile(Z.US(AddressRange.Start)))
				throw new InvalidOperationException($"{nameof(Kernel32.UnmapViewOfFile)}() returned NULL");
			Active = false;
		}

		/// <exception cref="InvalidOperationException">snapshot already taken, <see cref="MemoryBlockBase.Active"/> is <see langword="false"/>, or failed to make memory read-only</exception>
		public override void SaveXorSnapshot()
		{
			if (_snapshot != null)
				throw new InvalidOperationException("Snapshot already taken");
			if (!Active)
				throw new InvalidOperationException("Not active");

			// temporarily switch the entire block to `R`: in case some areas are unreadable, we don't want
			// that to complicate things
			Kernel32.MemoryProtection old;
			if (!Kernel32.VirtualProtect(Z.UU(AddressRange.Start), Z.UU(Size), Kernel32.MemoryProtection.READONLY, out old))
				throw new InvalidOperationException($"{nameof(Kernel32.VirtualProtect)}() returned FALSE!");

			_snapshot = new byte[Size];
			var ds = new MemoryStream(_snapshot, true);
			var ss = GetStream(AddressRange.Start, Size, false);
			ss.CopyTo(ds);
			XorHash = WaterboxUtils.Hash(_snapshot);

			ProtectAll();
		}

		/// <exception cref="InvalidOperationException"><see cref="MemoryBlockBase.Active"/> is <see langword="false"/> or failed to make memory read-only</exception>
		public override byte[] FullHash()
		{
			if (!Active)
				throw new InvalidOperationException("Not active");
			// temporarily switch the entire block to `R`
			Kernel32.MemoryProtection old;
			if (!Kernel32.VirtualProtect(Z.UU(AddressRange.Start), Z.UU(Size), Kernel32.MemoryProtection.READONLY, out old))
				throw new InvalidOperationException($"{nameof(Kernel32.VirtualProtect)}() returned FALSE!");
			var ret = WaterboxUtils.Hash(GetStream(AddressRange.Start, Size, false));
			ProtectAll();
			return ret;
		}

		private static Kernel32.MemoryProtection GetKernelMemoryProtectionValue(Protection prot)
		{
			Kernel32.MemoryProtection p;
			switch (prot)
			{
				case Protection.None: p = Kernel32.MemoryProtection.NOACCESS; break;
				case Protection.R: p = Kernel32.MemoryProtection.READONLY; break;
				case Protection.RW: p = Kernel32.MemoryProtection.READWRITE; break;
				case Protection.RX: p = Kernel32.MemoryProtection.EXECUTE_READ; break;
				default: throw new ArgumentOutOfRangeException(nameof(prot));
			}
			return p;
		}

		protected override void ProtectAll()
		{
			int ps = 0;
			for (int i = 0; i < _pageData.Length; i++)
			{
				if (i == _pageData.Length - 1 || _pageData[i] != _pageData[i + 1])
				{
					var p = GetKernelMemoryProtectionValue(_pageData[i]);
					ulong zstart = GetStartAddr(ps);
					ulong zend = GetStartAddr(i + 1);
					Kernel32.MemoryProtection old;
					if (!Kernel32.VirtualProtect(Z.UU(zstart), Z.UU(zend - zstart), p, out old))
						throw new InvalidOperationException($"{nameof(Kernel32.VirtualProtect)}() returned FALSE!");
					ps = i + 1;
				}
			}
		}

		/// <exception cref="InvalidOperationException">failed to protect memory</exception>
		public override void Protect(ulong start, ulong length, Protection prot)
		{
			if (length == 0)
				return;
			int pstart = GetPage(start);
			int pend = GetPage(start + length - 1);

			var p = GetKernelMemoryProtectionValue(prot);
			for (int i = pstart; i <= pend; i++)
				_pageData[i] = prot; // also store the value for later use

			if (Active) // it's legal to Protect() if we're not active; the information is just saved for the next activation
			{
				var computedStart = WaterboxUtils.AlignDown(start);
				var computedEnd = WaterboxUtils.AlignUp(start + length);
				var computedLength = computedEnd - computedStart;

				Kernel32.MemoryProtection old;
				if (!Kernel32.VirtualProtect(Z.UU(computedStart),
					Z.UU(computedLength), p, out old))
					throw new InvalidOperationException($"{nameof(Kernel32.VirtualProtect)}() returned FALSE!");
			}
		}

		public override void Dispose(bool disposing)
		{
			if (_handle != IntPtr.Zero)
			{
				if (Active)
					Deactivate();
				Kernel32.CloseHandle(_handle);
				_handle = IntPtr.Zero;
			}
		}

		~MemoryBlock()
		{
			Dispose(false);
		}

		private static class Kernel32
		{
			[DllImport("kernel32.dll", SetLastError = true)]
			public static extern bool VirtualProtect(UIntPtr lpAddress, UIntPtr dwSize,
			   MemoryProtection flNewProtect, out MemoryProtection lpflOldProtect);

			[Flags]
			public enum MemoryProtection : uint
			{
				EXECUTE = 0x10,
				EXECUTE_READ = 0x20,
				EXECUTE_READWRITE = 0x40,
				EXECUTE_WRITECOPY = 0x80,
				NOACCESS = 0x01,
				READONLY = 0x02,
				READWRITE = 0x04,
				WRITECOPY = 0x08,
				GUARD_Modifierflag = 0x100,
				NOCACHE_Modifierflag = 0x200,
				WRITECOMBINE_Modifierflag = 0x400
			}

			[DllImport("kernel32.dll", SetLastError = true)]
			public static extern IntPtr CreateFileMapping(
				IntPtr hFile,
				IntPtr lpFileMappingAttributes,
				FileMapProtection flProtect,
				uint dwMaximumSizeHigh,
				uint dwMaximumSizeLow,
				string lpName);

			[Flags]
			public enum FileMapProtection : uint
			{
				PageReadonly = 0x02,
				PageReadWrite = 0x04,
				PageWriteCopy = 0x08,
				PageExecuteRead = 0x20,
				PageExecuteReadWrite = 0x40,
				SectionCommit = 0x8000000,
				SectionImage = 0x1000000,
				SectionNoCache = 0x10000000,
				SectionReserve = 0x4000000,
			}

			[DllImport("kernel32.dll", SetLastError = true)]
			public static extern bool CloseHandle(IntPtr hObject);

			[DllImport("kernel32.dll", SetLastError = true)]
			public static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

			[DllImport("kernel32.dll")]
			public static extern IntPtr MapViewOfFileEx(IntPtr hFileMappingObject,
			   FileMapAccessType dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow,
			   UIntPtr dwNumberOfBytesToMap, IntPtr lpBaseAddress);

			[Flags]
			public enum FileMapAccessType : uint
			{
				Copy = 0x01,
				Write = 0x02,
				Read = 0x04,
				AllAccess = 0x08,
				Execute = 0x20,
			}

			public static readonly IntPtr INVALID_HANDLE_VALUE = Z.US(0xffffffffffffffff);
		}
	}
}
