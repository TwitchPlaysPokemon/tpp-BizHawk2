using System;
using System.Collections.Generic;
using System.Linq;
using BizHawk.Emulation.Common;
using System.Net;
using System.Text;

namespace BizHawk.Client.Common.Api.Public
{
	public class PublicApi
	{
		private HttpListener Listener;

		public PublicApi(IEmulatorServiceProvider serviceProvider)
		{
			apiProviders = ReflectiveEnumerator.GetEnumerableOfType<ApiProvider>();
			Documentation = new ApiCommand("Help", (args, domain) => string.Join("\n", Commands.Select(c => BuildDocString(c.Value))), new List<ApiParameter>(), "Lists available commands (This message)");
			Commands["Help"] = Documentation;
			foreach(var provider in apiProviders)
			{
				foreach(var command in provider.Commands)
				{
					Commands.Add(command.Name, command);
				}
			}
			Update(serviceProvider);
		}

		private ApiCommand Documentation;

		private string BuildDocString(ApiCommand command)
		{
			var docString = new StringBuilder(command.Name).Append(":\t");

			void DocParam(ApiParameter parameter)
			{
				if (parameter != null)
				{
					docString.Append("/").Append(parameter.Optional ? '[' : '<').Append(parameter.Name);
					if (!string.IsNullOrWhiteSpace(parameter.Type))
						docString.Append(':').Append(parameter.Type);
					docString.Append(parameter.Optional ? ']' : '>');
				}
			}

			if (command.Parameters != null)
			{
				docString.Append("(Usage: \"");
				DocParam(command.Parameters.FirstOrDefault(p => p.IsPrepend));
				docString.Append('/').Append(command.Name);
				foreach (var parameter in command.Parameters.Where(p => !p.IsPrepend))
					DocParam(parameter);
				docString.Append("\")\t");
			}

			docString.Append(command.Description ?? "No description provided");

			return docString.ToString();
		}

		public void StartHttp(int port)
		{
			if (!HttpListener.IsSupported)
			{
				throw new Exception("HTTPListener is not supported on this system.");
			}
			Listener = new HttpListener();
			var listenTo = $"http://localhost:{port}/";
			if (!Listener.Prefixes.Contains(listenTo))
			{
				Listener.Prefixes.Add(listenTo);
			}
			if (!Listener.IsListening)
			{
				Listener.Start();
				Listener.BeginGetContext(HttpListenHandler, Listener);
			}
		}

		private IEnumerable<ApiProvider> apiProviders;

		public Dictionary<string, ApiCommand> Commands = new Dictionary<string, ApiCommand>(StringComparer.OrdinalIgnoreCase);

		public void Update(IEmulatorServiceProvider newServiceProvider)
		{
			foreach (var provider in apiProviders)
			{
				ServiceInjector.UpdateServices(newServiceProvider, provider);
				provider.Update();
			}
		}

		private void HttpListenHandler(IAsyncResult result)
		{
			var context = Listener.EndGetContext(result);

			string body = null;
			if (context.Request.HasEntityBody)
			{
				using (System.IO.Stream bodyStream = context.Request.InputStream)
				{
					using (System.IO.StreamReader reader = new System.IO.StreamReader(bodyStream, context.Request.ContentEncoding))
					{
						body = reader.ReadToEnd();
					}
				}
			}
			
			var response = "ok";
			string command = "Unknown Command";
			try
			{
				var urlParams = new List<string>(context.Request.RawUrl.Split(new char[] { '/' })).Select(us => Uri.UnescapeDataString(us)).ToList();
				urlParams.Add(body);
				urlParams = urlParams.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();

				if (!urlParams.Any())
				{
					response = Documentation.Function(null, null);
				}
				else
				{
					string domain = null;
					if (urlParams.Count > 1 && Commands.ContainsKey(urlParams[1]))
					{
						domain = urlParams[0];
						urlParams.RemoveAt(0);
					}
					command = urlParams[0];
					urlParams.RemoveAt(0);

					if (!Commands.ContainsKey(command))
					{
						throw new ApiError($"Invalid Command");
					}
					
					command = Commands[command]?.Name; //normalize name for display during errors
					response = Commands[command].Function(urlParams, domain) ?? response;
				}
			}
			catch (ApiError e)
			{
				response = $"{command}: {e.Message}";
				context.Response.StatusCode = 400;
			}
			catch (Exception e)
			{
				response = e.Message;
				context.Response.StatusCode = 500;
			}
			byte[] buffer = System.Text.Encoding.UTF8.GetBytes(response);
			// Get a response stream and write the response to it.
			context.Response.ContentLength64 = buffer.Length;
			context.Response.ContentType = "text/plain";
			using (System.IO.Stream output = context.Response.OutputStream)
			{
				output.Write(buffer, 0, buffer.Length);
				output.Close();
			}
			Listener.BeginGetContext(HttpListenHandler, Listener);
		}
	}
}
