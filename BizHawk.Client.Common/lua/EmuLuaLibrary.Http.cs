using System;
using NLua;
using System.Net;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BizHawk.Client.Common
{
	public sealed class HTTPLuaLibrary : LuaLibraryBase
	{

		public HTTPLuaLibrary(Lua lua)
			: base(lua) { }

		public HTTPLuaLibrary(Lua lua, Action<string> logOutputCallback)
			: base(lua, logOutputCallback) { }

		public override string Name { get { return "http"; } }

		#region Client Methods

		[LuaMethod(
			"postasync",
			"asynchronously sends given data to the given url via HTTP POST and ignores the response"
		)]
		public void PostAsync(string url, string data)
		{
			try
			{
				using (var client = new WebClient())
				{
					client.UploadStringAsync(new Uri(url), data);
				}
			}
			catch (Exception e)
			{
				LogOutputCallback(e.Message);
			}
		}

		[LuaMethod(
		"post",
		"synchronously sends given data to the given url via HTTP POST and returns the response"
		)]
		public string Post(string url, string data, bool silentFailure = true)
		{
			try
			{
				return webClient.UploadString(new Uri(url), data);

			}
			catch (Exception e)
			{
				return silentFailure ? null : e.Message;
			}
		}

		[LuaMethod(
			"get",
			"synchronously gets data from the given url via HTTP GET"
		)]
		public string Get(string url, bool silentFailure = true)
		{
			try
			{
				return webClient.DownloadString(url);
			}
			catch (Exception e)
			{
				return silentFailure ? null : e.Message;
			}
		}

		[LuaMethod(
			"request",
			"calls get or post based on parameters"
		)]
		public string Request(string url, string data = null)
		{
			if (data == null)
				return Get(url);
			return Post(url, data);
		}

		[LuaMethod(
			"settimeout",
			"changes the timeout (in milliseconds) that the synchronous calls use"
		)]
		public void SetTimeout(int timeout)
		{
			Timeout = timeout;
		}

		private static int Timeout = 10;

		private static ImpatientWebClient webClient = new ImpatientWebClient();

		private class ImpatientWebClient : WebClient
		{
			protected override WebRequest GetWebRequest(Uri uri)
			{
				WebRequest request = base.GetWebRequest(uri);
				request.Timeout = Timeout;
				return request;
			}
		}
		#endregion

		#region Server Methods

		private Dictionary<int, LuaFunction> Handlers = new Dictionary<int, LuaFunction>();

		private HttpListener Listener = null;

		private void ListenHandler(IAsyncResult result)
		{
			var context = Listener.EndGetContext(result);
			var queryString = Lua.NewTable();
			foreach (var key in context.Request.QueryString.AllKeys)
			{
				queryString[key] = context.Request.QueryString[key];
			}
			var body = "";
			if (context.Request.HasEntityBody)
			{
				using (System.IO.Stream bodyStream = context.Request.InputStream) // here we have data
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
						var resp = Handlers[context.Request.Url.Port].Call(queryString, body);
						if (resp.Length > 0)
						{
							response = (resp?[0] ?? response).ToString();
						}
					}
					catch (Exception ex)
					{
						LogOutputCallback($"Error running listener function attached to port {context.Request.Url.Port}\nError message: {ex.Message}");
					}
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

		private void InitListener(int port)
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
				LogOutputCallback($"Started listening for connections to {listenTo}");
				Listener.BeginGetContext(ListenHandler, Listener);
			}

		}

		[LuaMethod("listen", "Listens on the specified port and calls the given lua function whenever data arrives. Returned data is sent back to the caller")]
		public void Listen(int port, LuaFunction luaf)
		{
			lock (Handlers)
			{
				Handlers[port] = luaf;
				try
				{
					InitListener(port);
				}
				catch (Exception e)
				{
					LogOutputCallback($"Could not start listening on port {port}\n Exception: {e.Message}");
				}
			}
		}

		#endregion
	}
}
