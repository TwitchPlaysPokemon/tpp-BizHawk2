using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BizHawk.Client.Common.Services
{
	public static class HTTPClient
	{
		public static async Task PostAsync(string url, string data, Action<string, Exception> callback = null)
		{
			try
			{
				using (var client = new WebClient())
				{
					string response = await client.UploadStringTaskAsync(new Uri(url), data);
					callback?.Invoke(response, null);
				}
			}
			catch (Exception e)
			{
				callback?.Invoke(null, e);
			}
		}

		public static string PostSync(string url, string data, bool returnException = false)
		{
			try
			{
				return syncClient.UploadString(new Uri(url), data);

			}
			catch (Exception e)
			{
				return returnException ? e.Message : null;
			}
		}

		public static async Task GetAsync(string url, Action<string, Exception> callback = null)
		{
			try
			{
				using (var client = new WebClient())
				{
					string response = await client.DownloadStringTaskAsync(url);
					callback?.Invoke(response, null);
				}
			}
			catch (Exception e)
			{
				callback?.Invoke(null, e);
			}
		}

		public static string GetSync(string url, bool returnException = false)
		{
			try
			{
				return syncClient.DownloadString(url);
			}
			catch (Exception e)
			{
				return returnException ? e.Message : null;
			}
		}

		public static int SyncTimeout
		{
			set
			{
				Timeout = value;
			}
		}

		private static int Timeout;
		private static ImpatientWebClient syncClient = new ImpatientWebClient();

		private class ImpatientWebClient : WebClient
		{
			protected override WebRequest GetWebRequest(Uri uri)
			{
				WebRequest request = base.GetWebRequest(uri);
				if (Timeout > 0)
				{
					request.Timeout = Timeout;
				}
				return request;
			}
		}
	}
}
