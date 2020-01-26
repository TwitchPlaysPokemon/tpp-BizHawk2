using System;
using System.Runtime.InteropServices;

using static BizHawk.BizInvoke.MemoryBlockBase;

namespace BizHawk.BizInvoke
{
	public static class POSIXLibC
	{
		[DllImport("libc.so.6")]
		public static extern int close(int fd);

		[DllImport("libc.so.6")]
		public static extern int memfd_create(string name, uint flags);

		[DllImport("libc.so.6")]
		private static extern IntPtr mmap(IntPtr addr, UIntPtr length, int prot, int flags, int fd, IntPtr offset);

		public static IntPtr mmap(IntPtr addr, UIntPtr length, MemoryProtection prot, int flags, int fd, IntPtr offset) => mmap(addr, length, (int) prot, flags, fd, offset);

		[DllImport("libc.so.6")]
		private static extern int mprotect(IntPtr addr, UIntPtr len, int prot);

		public static int mprotect(IntPtr addr, UIntPtr len, MemoryProtection prot) => mprotect(addr, len, (int) prot);

		[DllImport("libc.so.6")]
		public static extern int munmap(IntPtr addr, UIntPtr length);

		/// <remarks>32-bit signed int</remarks>
		[Flags]
		public enum MemoryProtection { None = 0x0, Read = 0x1, Write = 0x2, Execute = 0x4 }

		public static MemoryProtection ToMemoryProtection(this Protection prot)
		{
			switch (prot)
			{
				case Protection.None: return MemoryProtection.None;
				case Protection.R: return MemoryProtection.Read;
				case Protection.RW: return MemoryProtection.Read | MemoryProtection.Write;
				case Protection.RX: return MemoryProtection.Read | MemoryProtection.Execute;
				default: throw new ArgumentOutOfRangeException(nameof(prot));
			}
		}
	}
}
