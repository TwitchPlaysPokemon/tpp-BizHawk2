using System;
using System.Collections.Generic;
using System.Linq;
using BizHawk.Emulation.Common;
using System.Net;
using System.Text;
using System.IO;
using System.Drawing;
using System.Runtime.ExceptionServices;

namespace BizHawk.Client.Common.Api.Public
{
	public class PublicApi
	{
		private HttpListener Listener;

		public Icon Favicon { get; set; }

		[RequiredService]
		private IEmulator Emulator { get; set; }

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

		public void StartHttp(int port, Icon favicon = null)
		{
			if (!HttpListener.IsSupported)
			{
				throw new Exception("HTTPListener is not supported on this system.");
			}
			Favicon = favicon ?? Favicon;
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

		public void NewFrame()
		{
			var frame = Emulator.Frame;
			foreach (var provider in apiProviders)
			{
				provider.OnFrame(frame);
			}
		}

		private IEnumerable<ApiProvider> apiProviders;

		public Dictionary<string, ApiCommand> Commands = new Dictionary<string, ApiCommand>(StringComparer.OrdinalIgnoreCase);

		public void Update(IEmulatorServiceProvider newServiceProvider)
		{
			ServiceInjector.UpdateServices(newServiceProvider, this);
			foreach (var provider in apiProviders)
			{
				ServiceInjector.UpdateServices(newServiceProvider, provider);
				provider.Update();
			}
		}

		[HandleProcessCorruptedStateExceptions]
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

			byte[] responseBuffer;

			if (context.Request.RawUrl == "/favicon.ico")
			{
				if (Favicon == null)
				{
					responseBuffer = new byte[0];
					context.Response.StatusCode = 404;
				}
				else
				{
					using (var stream = new MemoryStream())
					{
						Favicon.Save(stream);
						responseBuffer = stream.ToArray();
						context.Response.ContentType = "image/x-icon";
					}
				}
			}
			else
			{
				var response = "ok";
				string command = "Unknown Command";
				try
				{
					var urlParams = new List<string>(context.Request.RawUrl.Split(new char[] { '/' })).Select(us => Uri.UnescapeDataString(us)).ToList();
					if (!string.IsNullOrWhiteSpace(body))
					{
						urlParams.Add(body);
					}
					if (string.IsNullOrWhiteSpace(urlParams.FirstOrDefault()))
					{
						urlParams.RemoveAt(0);
					}
					if (string.IsNullOrWhiteSpace(urlParams.LastOrDefault()) && urlParams.Any())
					{
						urlParams.RemoveAt(urlParams.Count - 1);
					}
					//urlParams = urlParams.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();

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
				catch (Exception e)
				{
					response = $"{command}: {e.Message}";
					context.Response.StatusCode = e is ApiError ? 400 : 500;
				}
				responseBuffer = Encoding.UTF8.GetBytes(response);
				context.Response.ContentType = "text/plain";
			}
			// Get a response stream and write the response to it.
			context.Response.ContentLength64 = responseBuffer.Length;
			using (System.IO.Stream output = context.Response.OutputStream)
			{
				output.Write(responseBuffer, 0, responseBuffer.Length);
				output.Close();
			}
			Listener.BeginGetContext(HttpListenHandler, Listener);
		}
	}
}
