using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BizHawk.Client.Common.Api.Public
{
	public abstract class ApiProvider
	{
		public abstract IEnumerable<ApiCommand> Commands { get; }

		private class ApiMissingError : ApiError
		{
			public ApiMissingError(string message = null) : base(message) { }
		}

		/// <summary>
		/// Override in case the Api Provider needs to do extra data gathering after injected dependencies get updated
		/// </summary>
		public virtual void Update() { }

		/// <summary>
		/// Override in case the Api Provider needs to do work right before the emulator runs a frame
		/// </summary>
		public virtual void OnFrame(int frameCount) { }

		protected T ParseRequired<T>(IEnumerable<string> args, int index, Func<string, T> process, string name, string invalidError = null)
		{
			if (args.Count() <= index)
			{
				throw new ApiMissingError($"Parameter {name} is missing");
			}
			try
			{
				return process(args.ElementAt(index));
			}
			catch (ApiError)
			{
				throw;
			}
			catch
			{
				throw new ApiError(invalidError ?? $"Provided {name} \"{args.ElementAt(index)}\" is invalid");
			}
		}

		protected T? ParseOptional<T>(IEnumerable<string> args, int index, Func<string, T> process, string name, string invalidError = null) where T: struct
		{
			try
			{
				return ParseRequired(args, index, process, name, invalidError);
			}
			catch (ApiMissingError)
			{
				return null;
			}
		}
	}
}
