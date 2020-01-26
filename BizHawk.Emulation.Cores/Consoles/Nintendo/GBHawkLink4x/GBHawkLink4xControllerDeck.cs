﻿using System;
using System.Collections.Generic;
using System.Linq;

using BizHawk.Common;
using BizHawk.Common.ReflectionExtensions;
using BizHawk.Emulation.Common;

namespace BizHawk.Emulation.Cores.Nintendo.GBHawkLink4x
{
	public class GBHawkLink4xControllerDeck
	{
		public GBHawkLink4xControllerDeck(string controller1Name, string controller2Name, string controller3Name, string controller4Name)
		{
			if (!ValidControllerTypes.ContainsKey(controller1Name))
			{
				throw new InvalidOperationException("Invalid controller type: " + controller1Name);
			}

			if (!ValidControllerTypes.ContainsKey(controller2Name))
			{
				throw new InvalidOperationException("Invalid controller type: " + controller2Name);
			}

			if (!ValidControllerTypes.ContainsKey(controller3Name))
			{
				throw new InvalidOperationException("Invalid controller type: " + controller3Name);
			}

			if (!ValidControllerTypes.ContainsKey(controller4Name))
			{
				throw new InvalidOperationException("Invalid controller type: " + controller4Name);
			}

			Port1 = (IPort)Activator.CreateInstance(ValidControllerTypes[controller1Name], 1);
			Port2 = (IPort)Activator.CreateInstance(ValidControllerTypes[controller2Name], 2);
			Port3 = (IPort)Activator.CreateInstance(ValidControllerTypes[controller3Name], 3);
			Port4 = (IPort)Activator.CreateInstance(ValidControllerTypes[controller3Name], 4);

			Definition = new ControllerDefinition
			{
				Name = Port1.Definition.Name,
				BoolButtons = Port1.Definition.BoolButtons
					.Concat(Port2.Definition.BoolButtons)
					.Concat(Port3.Definition.BoolButtons)
					.Concat(Port4.Definition.BoolButtons)
					.Concat(new[] { "Toggle Cable UD" } )
					.Concat(new[] { "Toggle Cable LR" } )
					.Concat(new[] { "Toggle Cable X" } )
					.Concat(new[] { "Toggle Cable 4x" })
					.ToList()
			};
		}

		public byte ReadPort1(IController c)
		{
			return Port1.Read(c);
		}

		public byte ReadPort2(IController c)
		{
			return Port2.Read(c);
		}

		public byte ReadPort3(IController c)
		{
			return Port3.Read(c);
		}

		public byte ReadPort4(IController c)
		{
			return Port4.Read(c);
		}

		public ControllerDefinition Definition { get; }

		public void SyncState(Serializer ser)
		{
			ser.BeginSection(nameof(Port1));
			Port1.SyncState(ser);
			ser.EndSection();

			ser.BeginSection(nameof(Port2));
			Port2.SyncState(ser);
			ser.EndSection();

			ser.BeginSection(nameof(Port3));
			Port3.SyncState(ser);
			ser.EndSection();

			ser.BeginSection(nameof(Port4));
			Port4.SyncState(ser);
			ser.EndSection();
		}

		private readonly IPort Port1;
		private readonly IPort Port2;
		private readonly IPort Port3;
		private readonly IPort Port4;

		private static Dictionary<string, Type> _controllerTypes;

		public static Dictionary<string, Type> ValidControllerTypes
		{
			get
			{
				if (_controllerTypes == null)
				{
					_controllerTypes = typeof(GBHawkLink4xControllerDeck).Assembly
						.GetTypes()
						.Where(t => typeof(IPort).IsAssignableFrom(t))
						.Where(t => !t.IsAbstract && !t.IsInterface)
						.ToDictionary(tkey => tkey.DisplayName());
				}

				return _controllerTypes;
			}
		}

		public static string DefaultControllerName => typeof(StandardControls).DisplayName();
	}
}
