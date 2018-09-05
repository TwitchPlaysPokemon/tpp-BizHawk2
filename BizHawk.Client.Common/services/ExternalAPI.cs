using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BizHawk.Emulation.Common;
using BizHawk.Emulation.Common.IEmulatorExtensions;
using BizHawk.Common.BufferExtensions;
using System.Net;

namespace BizHawk.Client.Common.Services
{
	public class ExternalAPI
	{

		private HttpListener Listener;

		public ExternalAPI(int port, IEmulatorServiceProvider serviceProvider)
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
			if (HexLookup == null)
			{
				HexLookup = new Dictionary<string, byte>();
				for (int i = 0; i < 256; i++)
				{
					HexLookup[i.ToString("X2")] = (byte)i;
				}
			}
			Update(serviceProvider);
		}

		#region Hex Translation

		private static Dictionary<string, byte> HexLookup = null;

		private byte[] HexStringToBytes(string hex)
		{
			if (hex == null)
				return null;
			var bytes = new List<byte>();
			for (int i = 0; i < hex.Length; i += 2)
			{
				bytes.Add(HexLookup[hex.Substring(i, 2)]);
			}
			return bytes.ToArray();
		}

		private string BytesToHexString(byte[] bytes) => bytes == null ? null : bytes.BytesToHexString();

		#endregion

		#region API Management
		private class ApiError : Exception
		{
			public ApiError(string message = null) : base(message) { }
		}

		public void Update(IEmulatorServiceProvider newServiceProvider)
		{
			ServiceInjector.UpdateServices(newServiceProvider, this);
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
			try
			{
				var urlParams = new List<string>(context.Request.RawUrl.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries));
				string domain = null;
				Commands? command = null;
				int addr = 0;
				string data = null;
				try
				{
					var cmdStrings = Enum.GetNames(typeof(Commands)).Select(c => c.ToLower());
					if (cmdStrings.Contains(urlParams[1].ToLower()))
					{
						domain = Uri.UnescapeDataString(urlParams[0]);
						urlParams.RemoveAt(0);
					}
					if (!cmdStrings.Contains(urlParams[0].ToLower())) {
						throw new ApiError("Invalid command");
					}
					command = (Commands)Enum.Parse(typeof(Commands), Enum.GetNames(typeof(Commands)).Where(c => c.ToLower() == Uri.UnescapeDataString(urlParams[0].ToLower())).First());
					addr = int.Parse(Uri.UnescapeDataString(urlParams[1]), System.Globalization.NumberStyles.HexNumber);
					data = urlParams.Count > 2 ? Uri.UnescapeDataString(urlParams[2]) : body;
				}
				catch
				{
					throw new ApiError("Parameters given are invalid/missing");
				}
				response = ProcessCommands(command, addr, data, domain) ?? response;
			}
			catch (ApiError e)
			{
				response = e.Message;
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

		private enum Commands
		{
			ReadByte, WriteByte,
			ReadS16BE, WriteS16BE, ReadS16LE, WriteS16LE, ReadU16BE, WriteU16BE, ReadU16LE, WriteU16LE,
			ReadS24BE, WriteS24BE, ReadS24LE, WriteS24LE, ReadU24BE, WriteU24BE, ReadU24LE, WriteU24LE,
			ReadS32BE, WriteS32BE, ReadS32LE, WriteS32LE, ReadU32BE, WriteU32BE, ReadU32LE, WriteU32LE,
			ReadRange, WriteRange
		}

		private string ProcessCommands(Commands? command, int addr, string data, string domain = null)
		{

			int dataValue(string varName = "Value")
			{
				if (data == null)
					throw new ApiError($"{Enum.GetName(typeof(Commands), command)}: {varName} was not provided");
				return int.Parse(data, System.Globalization.NumberStyles.HexNumber);
			}
			byte[] dataBytes(string varName = "Data")=> HexStringToBytes(data) ?? throw new ApiError($"{Enum.GetName(typeof(Commands), command)}: {varName} was not provided");

			switch (command)
			{
				case Commands.ReadByte:
					return ReadUnsignedByte(addr, domain).ToString("X2");
				case Commands.ReadU16BE:
					return ReadUnsignedBig(addr, 2, domain).ToString("X4");
				case Commands.ReadU24BE:
					return ReadUnsignedBig(addr, 3, domain).ToString("X6");
				case Commands.ReadU32BE:
					return ReadUnsignedBig(addr, 4, domain).ToString("X8");
				case Commands.ReadU16LE:
					return ReadUnsignedLittle(addr, 2, domain).ToString("X4");
				case Commands.ReadU24LE:
					return ReadUnsignedLittle(addr, 3, domain).ToString("X6");
				case Commands.ReadU32LE:
					return ReadUnsignedLittle(addr, 4, domain).ToString("X8");
				case Commands.ReadS16BE:
					return ReadSignedBig(addr, 2, domain).ToString("X4");
				case Commands.ReadS24BE:
					return ReadSignedBig(addr, 3, domain).ToString("X6");
				case Commands.ReadS32BE:
					return ReadSignedBig(addr, 4, domain).ToString("X8");
				case Commands.ReadS16LE:
					return ReadSignedLittle(addr, 2, domain).ToString("X4");
				case Commands.ReadS24LE:
					return ReadSignedLittle(addr, 3, domain).ToString("X6");
				case Commands.ReadS32LE:
					return ReadSignedLittle(addr, 4, domain).ToString("X8");
				case Commands.WriteByte:
					WriteUnsignedByte(addr, (uint)dataValue(), domain);
					break;
				case Commands.WriteU16BE:
					WriteUnsignedBig(addr, (uint)dataValue(), 2, domain);
					break;
				case Commands.WriteU24BE:
					WriteUnsignedBig(addr, (uint)dataValue(), 3, domain);
					break;
				case Commands.WriteU32BE:
					WriteUnsignedBig(addr, (uint)dataValue(), 4, domain);
					break;
				case Commands.WriteU16LE:
					WriteUnsignedLittle(addr, (uint)dataValue(), 2, domain);
					break;
				case Commands.WriteU24LE:
					WriteUnsignedLittle(addr, (uint)dataValue(), 3, domain);
					break;
				case Commands.WriteU32LE:
					WriteUnsignedLittle(addr, (uint)dataValue(), 4, domain);
					break;
				case Commands.WriteS16BE:
					WriteSignedBig(addr, dataValue(), 2, domain);
					break;
				case Commands.WriteS24BE:
					WriteSignedBig(addr, dataValue(), 3, domain);
					break;
				case Commands.WriteS32BE:
					WriteSignedBig(addr, dataValue(), 4, domain);
					break;
				case Commands.WriteS16LE:
					WriteSignedLittle(addr, dataValue(), 2, domain);
					break;
				case Commands.WriteS24LE:
					WriteSignedLittle(addr, dataValue(), 3, domain);
					break;
				case Commands.WriteS32LE:
					WriteSignedLittle(addr, dataValue(), 4, domain);
					break;
				case Commands.ReadRange:
					return BytesToHexString(ReadRegion(addr, dataValue("Length"), domain));
				case Commands.WriteRange:
					WriteRegion(addr, dataBytes(), domain);
					break;
			}
			return null;
		}

		#endregion

		#region Top Level Methods
		private void _UseMemoryDomain(string domain) => _currentMemoryDomain = Domain(domain);

		private byte[] ReadRegion(int addr, int count, string domain = null)
		{
			var d = Domain(domain, addr, count);
			byte[] data = new byte[count];
			for (int i = 0; i < count; i++)
			{
				data[i] = d.PeekByte(addr + i);
			}
			return data;
		}

		private void WriteRegion(int addr, byte[] data, string domain = null)
		{
			var d = Domain(domain, addr, data.Length);
			if (!d.CanPoke())
			{
				throw new ApiError($"The domain {d.Name} is not writable");
			}
			for (int i = 0; i < data.Length; i++)
			{
				d.PokeByte(addr + i, data[i]);
			}
		}

		private byte[] HashRegion(int addr, int count, string domain = null)
		{
			using (var hasher = System.Security.Cryptography.SHA256.Create())
			{
				return hasher.ComputeHash(ReadRegion(addr, count, domain));
			}
		}

		#endregion

		#region Internal Methods
		[RequiredService]
		private IEmulator Emulator { get; set; }

		[OptionalService]
		private IMemoryDomains MemoryDomainCore { get; set; }

		private MemoryDomain _currentMemoryDomain;

		private MemoryDomain CurrentDomain => _currentMemoryDomain ?? (_currentMemoryDomain = DomainList.HasSystemBus ? DomainList.SystemBus : DomainList.MainMemory);
		private IMemoryDomains DomainList => MemoryDomainCore ?? throw new ApiError($"{Emulator.Attributes().CoreName} does not implement memory domains");
		private string FixDomainNameCase(string domain) => DomainList.Where(d => d.Name.ToLower() == domain?.ToLower()).FirstOrDefault()?.Name;
		public MemoryDomain VerifyMemoryDomain(string domain) => DomainList[FixDomainNameCase(domain)] ?? throw new ApiError($"{Emulator.Attributes().CoreName} does not have memory domain \"{domain}\"");

		private MemoryDomain Domain(string domainName = null, int addr = 0, int bytes = 1)
		{
			var domain = string.IsNullOrEmpty(domainName) ? CurrentDomain : VerifyMemoryDomain(domainName);
			if (addr >= domain.Size)
			{
				throw new ApiError($"Requested address {addr.ToString("X")} is outside of memory domain {domain.Name}'s range of {domain.Size.ToString("X")}");
			}
			if (addr < 0) {
				throw new ApiError($"Requested address {addr.ToString("X")} is negative.");
			}
			if (bytes < 1)
			{
				throw new ApiError($"Requested bytes {bytes.ToString("X")} is less than 1.");
			}
			if (addr + bytes > domain.Size)
			{
				throw new ApiError($"Requested address {(addr).ToString("X")} + {(bytes).ToString("X")} bytes is outside of memory domain {domain.Name}'s range of {domain.Size.ToString("X")}");
			}
			return domain;
		}

		private uint ReadUnsignedByte(int addr, string domain = null) => Domain(domain, addr).PeekByte(addr);

		private void WriteUnsignedByte(int addr, uint v, string domain = null)
		{
			var d = Domain(domain, addr);
			if (d.CanPoke())
			{
				d.PokeByte(addr, (byte)v);
			}
			else
			{
				throw new ApiError($"The domain {d.Name} is not writable");
			}
		}

		private static int ToSigned(uint u, int size)
		{
			var s = (int)u;
			s <<= 8 * (4 - size);
			s >>= 8 * (4 - size);
			return s;
		}

		private uint ReadUnsignedLittle(int addr, int size, string domain = null)
		{
			uint v = 0;
			for (var i = 0; i < size; ++i)
			{
				v |= ReadUnsignedByte(addr + i, domain) << (8 * i);
			}

			return v;
		}

		private uint ReadUnsignedBig(int addr, int size, string domain = null)
		{
			uint v = 0;
			for (var i = 0; i < size; ++i)
			{
				v |= ReadUnsignedByte(addr + i, domain) << (8 * (size - 1 - i));
			}

			return v;
		}

		private void WriteUnsignedLittle(int addr, uint v, int size, string domain = null)
		{
			for (var i = 0; i < size; ++i)
			{
				WriteUnsignedByte(addr + i, (v >> (8 * i)) & 0xFF, domain);
			}
		}

		private void WriteUnsignedBig(int addr, uint v, int size, string domain = null)
		{
			for (var i = 0; i < size; ++i)
			{
				WriteUnsignedByte(addr + i, (v >> (8 * (size - 1 - i))) & 0xFF, domain);
			}
		}

		private int ReadSignedLittle(int addr, int size, string domain = null) => ToSigned(ReadUnsignedLittle(addr, size, domain), size);

		private int ReadSignedBig(int addr, int size, string domain = null) => ToSigned(ReadUnsignedBig(addr, size, domain), size);

		private void WriteSignedLittle(int addr, int v, int size, string domain = null) => WriteUnsignedLittle(addr, (uint)v, size, domain);

		private void WriteSignedBig(int addr, int v, int size, string domain = null) => WriteUnsignedBig(addr, (uint)v, size, domain);

		#endregion
	}
}
