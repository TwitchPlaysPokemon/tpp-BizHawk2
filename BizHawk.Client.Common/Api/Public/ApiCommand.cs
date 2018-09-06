using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BizHawk.Client.Common.Api.Public
{
	public class ApiCommand
	{
		public string Name { get; private set; }
		public Func<IEnumerable<string>, string, string> Function { get; private set; }

		public ApiCommand (string name, Func<IEnumerable<string>, string, string> func)
		{
			Name = name;
			Function = func;
		}
	}
}
