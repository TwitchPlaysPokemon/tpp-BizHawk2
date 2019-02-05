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
		public IEnumerable<ApiParameter> Parameters { get; private set; }
		public string Description { get; private set; }

		public ApiCommand(string name, Func<IEnumerable<string>, string, string> func, IEnumerable<ApiParameter> parameters = null, string description = null)
		{
			Name = name;
			Function = func;
			Parameters = parameters;
			Description = description;
		}
		public ApiCommand(string name, Func<IEnumerable<string>, string> func, IEnumerable<ApiParameter> parameters = null, string description = null) : this(name, (args, domain) => func(args), parameters, description) { }
		public ApiCommand(string name, Action<IEnumerable<string>, string> action, IEnumerable<ApiParameter> parameters = null, string description = null): this(name, (args, domain) =>
		{
			action(args, domain);
			return null;
		}, parameters, description) { }
		public ApiCommand(string name, Action<IEnumerable<string>> action, IEnumerable<ApiParameter> parameters = null, string description = null) : this(name, (args, domain) => action(args), parameters, description) { }
	}

	public class ApiParameter
	{
		public string Name { get; private set; }
		public string Type { get; private set; }
		public bool Optional { get; private set; }
		public bool IsPrepend { get; private set; }

		public ApiParameter(string name, string type = "int(hex)", bool optional = false, bool isPrepend = false)
		{
			Name = name;
			Type = type;
			Optional = optional;
			IsPrepend = isPrepend;
		}
	}
}
