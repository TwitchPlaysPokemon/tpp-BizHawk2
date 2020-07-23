using System;
using NLua;
using BizHawk.Client.Common.Services;
using System.Collections.Generic;

namespace BizHawk.Client.Common
{
	public sealed class HTTPLuaLibrary : LuaLibraryBase
	{

		public HTTPLuaLibrary(Lua lua)
			: base(lua) { }

		public HTTPLuaLibrary(Lua lua, Action<string> logOutputCallback)
			: base(lua, logOutputCallback) { }

		public override string Name { get { return "http"; } }

		[LuaMethod(
			"postasync",
			"asynchronously sends given data to the given url via HTTP POST and will call a callback(response)"
		)]
		public void PostAsync(string url, string data, LuaFunction callback = null)
		{
			HTTPClient.PostAsync(url, data, (r, e) => callback?.Call(r ?? e?.Message ?? null)).ConfigureAwait(false);
		}

		[LuaMethod(
		"post",
		"synchronously sends given data to the given url via HTTP POST and returns the response"
		)]
		public string Post(string url, string data, out int status)
		{
			try
			{
				status = 200;
				return HTTPClient.PostSync(url, data);
			}
			catch (HTTPClient.HttpException e)
			{
				status = e.StatusCode;
				return e.Message;
			}
		}

		[LuaMethod(
			"getasync",
			"asynchronously gets data from the given url via HTTP GET and will call a callback(response)"
		)]
		public void GetAsync(string url, LuaFunction callback = null)
		{
			HTTPClient.GetAsync(url, (r, e) => callback?.Call(r ?? e?.Message ?? null)).ConfigureAwait(false);
		}

		[LuaMethod(
			"get",
			"synchronously gets data from the given url via HTTP GET"
		)]
		public string Get(string url, out int status)
		{
			try
			{
				status = 200;
				return HTTPClient.GetSync(url);
			}
			catch (HTTPClient.HttpException e)
			{
				status = e.StatusCode;
				return e.Message;
			}
		}

		[LuaMethod(
			"requestasync",
			"calls getasync or postasync based on parameters"
		)]
		public void RequestAsync(string url, string data = null, LuaFunction callback = null)
		{
			if (data == null)
				GetAsync(url, callback);
			else
				PostAsync(url, data, callback);
		}

		[LuaMethod(
			"request",
			"calls get or post based on parameters"
		)]
		public object Request(out int status, string url, string data = null)
		{
			if (data == null)
				return Get(url, out status);
			else
				return Post(url, data, out status);
		}

		[LuaMethod(
			"settimeout",
			"changes the timeout (in milliseconds) that the synchronous calls use"
		)]
		public void SetTimeout(int timeout)
		{
			HTTPClient.SyncTimeout = timeout;
		}


		[LuaMethod("listen", "Listens on the specified port and calls the given lua function whenever data arrives. Returned data is sent back to the caller")]
		public string Listen(int port, LuaFunction luaf)
		{
			try
			{
				return HTTPServer.Listen(port, (qs, body) =>
				{
					try
					{
						var queryString = Lua.NewTable();
						foreach (var key in qs.Keys)
						{
							queryString[key] = qs[key];
						}
						object[] result;
						lock (Locks.LuaLock)
						{
							result = luaf.Call(queryString, body);
						}
						if (result == null || result.Length < 1)
						{
							return null;
						}
						return result[0].ToString();
					}
					catch (Exception ex)
					{
						var err = $"Error running listener function attached to port {port}\nError message: {ex.Message}";
						LogOutputCallback(err);
						return err;
					}
				});
			}
			catch (Exception e)
			{
				var err = $"Could not start listening on port {port}\n Exception: {e.Message}";
				LogOutputCallback(err);
				return err;
			}
		}

	}
}
