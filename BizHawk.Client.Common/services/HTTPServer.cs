using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace BizHawk.Client.Common.Services
{
	public static class HTTPServer
	{

		public static string Listen(int port, Func<Dictionary<string, string>, string, string> callback)
		{
			lock (Handlers)
			{
				Handlers[port] = callback;
				return InitListener(port);
			}
		}
		private static Dictionary<int, Func<Dictionary<string, string>, string, string>> Handlers = new Dictionary<int, Func<Dictionary<string, string>, string, string>>();

		private static HttpListener Listener = null;

		private static void ListenHandler(IAsyncResult result)
		{
			var context = Listener.EndGetContext(result);
			
			var body = "";
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
			lock (Handlers)
			{
				if (Handlers.ContainsKey(context.Request.Url.Port))
					try
					{
						var qs = new Dictionary<string, string>();
						foreach (var key in context.Request.QueryString.AllKeys)
						{
							qs[key] = context.Request.QueryString[key];
						}
						response = Handlers[context.Request.Url.Port](qs, body) ?? response;
					}
					catch { }
			}
			byte[] buffer = System.Text.Encoding.UTF8.GetBytes(response);
			// Get a response stream and write the response to it.
			context.Response.ContentLength64 = buffer.Length;
			using (System.IO.Stream output = context.Response.OutputStream)
			{
				output.Write(buffer, 0, buffer.Length);
				output.Close();
			}
			Listener.BeginGetContext(ListenHandler, Listener);
		}

		private static string InitListener(int port)
		{
			if (!HttpListener.IsSupported)
			{
				throw new Exception("HTTPListener is not supported on this system.");
			}
			Listener = Listener ?? new HttpListener();
			var listenTo = $"http://localhost:{port}/";
			if (!Listener.Prefixes.Contains(listenTo))
			{
				Listener.Prefixes.Add(listenTo);
			}
			if (!Listener.IsListening)
			{
				Listener.Start();
				Listener.BeginGetContext(ListenHandler, Listener);
				return $"Started listening for connections to {listenTo}";
			}
			return $"Registered listening function for {listenTo}";
		}

	}
}
