using BizHawk.Emulation.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BizHawk.Client.Common
{
	public class AddressResolver
	{

		[OptionalService]
		private IMemoryDomains MemoryDomains { get; set; }

		public AddressResolver() { }
		public AddressResolver(IEmulatorServiceProvider serviceProvider) => Update(serviceProvider);

		public void Update(IEmulatorServiceProvider serviceProvider) => ServiceInjector.UpdateServices(serviceProvider, this);

		private MemoryDomain SystemBus => MemoryDomains?.HasSystemBus ?? false ? MemoryDomains?.SystemBus : MemoryDomains?.MainMemory;

		public uint ResolveAddress(string addr, string domainName) => ResolveAddress(addr, MemoryDomains?.FirstOrDefault(d => d.Name == domainName));
		public uint ResolveAddress(string addr, MemoryDomain domain) => uint.Parse(ParseAddressSubstring(addr, domain), System.Globalization.NumberStyles.HexNumber);

		private static readonly string operatorsRegex = "([*+-][^*+-]+)";

		private string ParseAddressSubstring(string subString, MemoryDomain domain)
		{
			var parenIndex = new Stack<int>();
			for (int i = 0; i < subString.Length; i++)
			{
				if (subString[i] == '(')
				{
					parenIndex.Push(i);
				}
				else if (subString[i] == ')')
				{
					var startIndex = parenIndex.Pop();
					if (!parenIndex.Any())
					{
						var length = i - startIndex;
						var replacement = ParseAddressSubstring(subString.Substring(startIndex + 1, length - 1), domain);
						subString = subString.Remove(startIndex, length + 1).Insert(startIndex, replacement);
						i -= length - replacement.Length;
					}
				}
			}
			uint address = 0;
			var operations = Regex.Split(subString, operatorsRegex).Where(o => !string.IsNullOrWhiteSpace(o)).ToArray();
			for(var i = 0; i < operations.Length; i++)
			{
				switch(operations[i][0])
				{
					case '*':
						if (i == 0)
							address = DereferencePointer(ParseStringAddress(operations[i].Substring(1)), domain);
						else
							address *= ParseStringAddress(operations[i].Substring(1));
						break;
					case '+':
						address += ParseStringAddress(operations[i].Substring(1));
						break;
					case '-':
						address -= ParseStringAddress(operations[i].Substring(1));
						break;
					default:
						address = ParseStringAddress(operations[i]);
						break;
				}
			}
			return address.ToString("X");
		}

		private uint DereferencePointer(uint ptrAddr, MemoryDomain domain) => domain.PeekUint(ptrAddr, domain.EndianType == MemoryDomain.Endian.Big) % (uint)domain.Size;

		private uint ParseStringAddress(string addr)
		{
			//TODO: Symbols
			return uint.Parse(addr, System.Globalization.NumberStyles.HexNumber);
		}
	}

}
