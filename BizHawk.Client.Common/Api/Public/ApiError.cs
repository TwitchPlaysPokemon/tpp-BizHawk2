using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BizHawk.Client.Common.Api.Public
{
	public class ApiError : Exception
	{
		public ApiError(string message = null) : base(message) { }
	}
}
