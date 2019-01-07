using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace BizHawk.Client.Common.Services
{
	public static class HTTPClient
	{
		public class HttpException : Exception
		{
			public int StatusCode { get; set; }
			public HttpException(string message, int statusCode, Exception innerException = null) : base(message, innerException)
			{
				StatusCode = statusCode;
			}
		}

		public static async Task PostAsync(string url, string data, Action<string, Exception> callback = null)
		{
			HttpResponseMessage response = null;
			using (var client = new HttpClient())
			{
				try
				{
					response = await client.PostAsync(url, new StringContent(data));
					response.EnsureSuccessStatusCode();
					callback?.Invoke(await response.Content.ReadAsStringAsync(), null);
				}
				catch (Exception e)
				{
					callback?.Invoke(null, new HttpException(e.Message, (int?)response?.StatusCode ?? 0, e));
				}
			}
		}

		public static string PostSync(string url, string data)
		{
			HttpResponseMessage response = null;
			using (var client = new HttpClient())
			{
				try
				{
					client.Timeout = new TimeSpan(0, 0, 0, 0, Timeout);
					response = client.PostAsync(url, new StringContent(data)).Result;
					response.EnsureSuccessStatusCode();
					return response.Content.ReadAsStringAsync().Result;

				}
				catch (Exception e)
				{
					throw new HttpException(e.Message, (int?)response?.StatusCode ?? 0, e);
				}
			}
		}

		public static async Task GetAsync(string url, Action<string, Exception> callback = null)
		{
			HttpResponseMessage response = null;
			using (var client = new HttpClient())
			{
				try
				{
					response = await client.GetAsync(url);
					response.EnsureSuccessStatusCode();
					callback?.Invoke(await response.Content.ReadAsStringAsync(), null);
				}
				catch (Exception e)
				{
					callback?.Invoke(null, new HttpException(e.Message, (int?)response?.StatusCode ?? 0, e));
				}
			}
		}

		public static string GetSync(string url, bool returnException = false)
		{
			HttpResponseMessage response = null;
			using (var client = new HttpClient())
			{
				try
				{
					client.Timeout = new TimeSpan(0, 0, 0, 0, Timeout);
					response = client.GetAsync(url).Result;
					response.EnsureSuccessStatusCode();
					return response.Content.ReadAsStringAsync().Result;

				}
				catch (Exception e)
				{
					throw new HttpException(e.Message, (int?)response?.StatusCode ?? 0, e);
				}
			}
		}

		public static int SyncTimeout
		{
			set
			{
				Timeout = value;
			}
		}

		private static int Timeout = 100;
	}
}
