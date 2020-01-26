﻿using System;
using System.IO;
using Newtonsoft.Json;

using BizHawk.Emulation.Common;

namespace BizHawk.Emulation.Cores.Atari.Lynx
{
	public partial class Lynx : IStatable
	{
		public bool BinarySaveStatesPreferred => true;

		public void SaveStateText(TextWriter writer)
		{
			var s = new TextState<TextStateData>();
			s.Prepare();
			var ff = s.GetFunctionPointersSave();
			LibLynx.TxtStateSave(Core, ref ff);
			s.ExtraData.IsLagFrame = IsLagFrame;
			s.ExtraData.LagCount = LagCount;
			s.ExtraData.Frame = Frame;

			_ser.Serialize(writer, s);

			////Console.WriteLine(BizHawk.Common.BufferExtensions.BufferExtensions.HashSHA1(SaveStateBinary()));
		}

		public void LoadStateText(TextReader reader)
		{
			var s = (TextState<TextStateData>)_ser.Deserialize(reader, typeof(TextState<TextStateData>));
			s.Prepare();
			var ff = s.GetFunctionPointersLoad();
			LibLynx.TxtStateLoad(Core, ref ff);
			IsLagFrame = s.ExtraData.IsLagFrame;
			LagCount = s.ExtraData.LagCount;
			Frame = s.ExtraData.Frame;
		}

		public void SaveStateBinary(BinaryWriter writer)
		{
			if (!LibLynx.BinStateSave(Core, _saveBuff, _saveBuff.Length))
			{
				throw new InvalidOperationException($"Core's {nameof(LibLynx.BinStateSave)}() returned false!");
			}

			writer.Write(_saveBuff.Length);
			writer.Write(_saveBuff);

			// other variables
			writer.Write(IsLagFrame);
			writer.Write(LagCount);
			writer.Write(Frame);
		}

		public void LoadStateBinary(BinaryReader reader)
		{
			int length = reader.ReadInt32();
			if (length != _saveBuff.Length)
			{
				throw new InvalidOperationException("Save buffer size mismatch!");
			}

			reader.Read(_saveBuff, 0, length);
			if (!LibLynx.BinStateLoad(Core, _saveBuff, _saveBuff.Length))
			{
				throw new InvalidOperationException($"Core's {nameof(LibLynx.BinStateLoad)}() returned false!");
			}

			// other variables
			IsLagFrame = reader.ReadBoolean();
			LagCount = reader.ReadInt32();
			Frame = reader.ReadInt32();
		}

		public byte[] SaveStateBinary()
		{
			using var ms = new MemoryStream(_saveBuff2, true);
			using var bw = new BinaryWriter(ms);
			SaveStateBinary(bw);
			bw.Flush();
			if (ms.Position != _saveBuff2.Length)
			{
				throw new InvalidOperationException();
			}

			ms.Close();
			return _saveBuff2;
		}

		private readonly JsonSerializer _ser = new JsonSerializer { Formatting = Formatting.Indented };
		private readonly byte[] _saveBuff;
		private readonly byte[] _saveBuff2;

		private class TextStateData
		{
			public int Frame;
			public int LagCount;
			public bool IsLagFrame;
		}
	}
}
