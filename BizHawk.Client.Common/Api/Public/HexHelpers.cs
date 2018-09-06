using BizHawk.Common.BufferExtensions;
using System.Collections.Generic;

namespace BizHawk.Client.Common.Api.Public
{
	public static class HexHelpers
	{
		static HexHelpers()
		{
			HexLookup = new Dictionary<string, byte>();
			for (int i = 0; i < 256; i++)
			{
				HexLookup[i.ToString("X2")] = (byte)i;
			}
		}

		private static Dictionary<string, byte> HexLookup = null;

		public static byte[] HexStringToBytes(string hex)
		{
			if (hex == null)
				return null;
			var bytes = new List<byte>();
			for (int i = 0; i < hex.Length; i += 2)
			{
				bytes.Add(HexLookup[hex.Substring(i, 2)]);
			}
			return bytes.ToArray();
		}

		public static string BytesToHexString(byte[] bytes) => bytes == null ? null : bytes.BytesToHexString();
	}
}
