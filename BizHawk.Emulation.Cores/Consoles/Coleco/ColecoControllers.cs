﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using BizHawk.Common;
using BizHawk.Emulation.Common;

namespace BizHawk.Emulation.Cores.ColecoVision
{
	/// <summary>
	/// Represents a controller plugged into a controller port on the Colecovision
	/// </summary>
	public interface IPort
	{
		byte Read(IController c, bool leftMode, bool updateWheel, float wheelAngle);

		float UpdateWheel(IController c);

		ControllerDefinition Definition { get; }

		void SyncState(Serializer ser);

		int PortNum { get; }
	}

	[DisplayName("Unplugged Controller")]
	public class UnpluggedController : IPort
	{
		public UnpluggedController(int portNum)
		{
			PortNum = portNum;
			Definition = new ControllerDefinition
			{
				BoolButtons = new List<string>()
			};
		}

		public byte Read(IController c, bool left_mode, bool updateWheel, float wheelAngle)
		{
			return 0x7F; // needs checking
		}

		public ControllerDefinition Definition { get; }

		public void SyncState(Serializer ser)
		{
			// Do nothing
		}

		public int PortNum { get; }

		public float UpdateWheel(IController c)
		{
			return 0;
		}
	}

	[DisplayName("ColecoVision Basic Controller")]
	public class StandardController : IPort
	{
		public StandardController(int portNum)
		{
			PortNum = portNum;
			Definition = new ControllerDefinition
			{
				BoolButtons = BaseDefinition
				.Select(b => "P" + PortNum + " " + b)
				.ToList()
			};
		}

		public int PortNum { get; }

		public byte Read(IController c, bool leftMode, bool updateWheel, float wheelAngle)
		{
			if (leftMode)
			{
				byte retval = 0x7F;
				if (c.IsPressed(Definition.BoolButtons[0])) retval &= 0xFE;
				if (c.IsPressed(Definition.BoolButtons[1])) retval &= 0xFD;
				if (c.IsPressed(Definition.BoolButtons[2])) retval &= 0xFB;
				if (c.IsPressed(Definition.BoolButtons[3])) retval &= 0xF7;
				if (c.IsPressed(Definition.BoolButtons[4])) retval &= 0x3F;
				return retval;
			}
			else
			{
				byte retval = 0xF;
				//                                   0x00;
				if (c.IsPressed(Definition.BoolButtons[14])) retval = 0x01;
				if (c.IsPressed(Definition.BoolButtons[10])) retval = 0x02;
				if (c.IsPressed(Definition.BoolButtons[11])) retval = 0x03;
				//                                             0x04;
				if (c.IsPressed(Definition.BoolButtons[13])) retval = 0x05;
				if (c.IsPressed(Definition.BoolButtons[16])) retval = 0x06;
				if (c.IsPressed(Definition.BoolButtons[8])) retval = 0x07;
				//                                             0x08;
				if (c.IsPressed(Definition.BoolButtons[17])) retval = 0x09;
				if (c.IsPressed(Definition.BoolButtons[6])) retval = 0x0A;
				if (c.IsPressed(Definition.BoolButtons[15])) retval = 0x0B;
				if (c.IsPressed(Definition.BoolButtons[9])) retval = 0x0C;
				if (c.IsPressed(Definition.BoolButtons[7])) retval = 0x0D;
				if (c.IsPressed(Definition.BoolButtons[12])) retval = 0x0E;

				if (c.IsPressed(Definition.BoolButtons[5]) == false) retval |= 0x40;
				retval |= 0x30; // always set these bits
				return retval;
			}
		}

		public ControllerDefinition Definition { get; }


		public void SyncState(Serializer ser)
		{
			// Nothing todo, I think
		}

		private static readonly string[] BaseDefinition =
		{
			"Up", "Right", "Down", "Left", "L", "R",
			"Key 0", "Key 1", "Key 2", "Key 3", "Key 4", "Key 5",
			"Key 6", "Key 7", "Key 8", "Key 9", "Pound", "Star"
		};

		public float UpdateWheel(IController c)
		{
			return 0;
		}
	}

	[DisplayName("Turbo Controller")]
	public class ColecoTurboController : IPort
	{
		public ColecoTurboController(int portNum)
		{
			PortNum = portNum;
			Definition = new ControllerDefinition
			{
				BoolButtons = BaseBoolDefinition
				.Select(b => "P" + PortNum + " " + b)
				.ToList(),
				FloatControls = { "P" + PortNum + " Disc X", "P" + PortNum + " Disc Y" },
				FloatRanges = { new[] { -127.0f, 0, 127.0f }, new[] { -127.0f, 0, 127.0f } }
			};
		}

		public int PortNum { get; }

		public ControllerDefinition Definition { get; }

		public byte Read(IController c, bool leftMode, bool updateWheel, float wheelAngle)
		{
			if (leftMode)
			{

				byte retval = 0x4F;
				
				if (c.IsPressed(Definition.BoolButtons[0])) retval &= 0x3F;
				
				float x = c.GetFloat(Definition.FloatControls[0]);
				float y = c.GetFloat(Definition.FloatControls[1]);

				float angle;
				
				if (updateWheel)
				{
					angle = wheelAngle;
				} 
				else
				{
					angle = CalcDirection(x, y);
				}
				
				byte temp2 = 0;

				int temp1 = (int)Math.Floor(angle / 1.25);
				temp1 = temp1 % 4;

				if (temp1 == 0)
				{
					temp2 = 0x10;
				}

				if (temp1 == 1)
				{
					temp2 = 0x30;
				}
				if (temp1 == 2)
				{
					temp2 = 0x20;
				}

				if (temp1 == 3)
				{
					temp2 = 0x00;
				}


				retval |= temp2;
				
				return retval;
			}
			else
			{
				byte retval = 0x7F;

				return retval;
			}
		}

		public void SyncState(Serializer ser)
		{
			// Nothing todo, I think
		}

		private static readonly string[] BaseBoolDefinition =
		{
			"Pedal"
		};

		// x and y are both assumed to be in [-127, 127]
		// x increases from left to right
		// y increases from top to bottom
		private static float CalcDirection(float x, float y)
		{
			y = -y; // vflip to match the arrangement of FloatControllerButtons

			// the wheel is arranged in a grey coded configuration of sensitivity ~2.5 degrees
			// for each signal
			// so overall the value returned changes every 1.25 degrees

			float angle = (float)(Math.Atan2(y, x) * 180.0/Math.PI);

			if (angle < 0)
			{
				angle = 360 + angle;
			}

			return angle;
		}

		public float UpdateWheel(IController c)
		{
			float x = c.GetFloat(Definition.FloatControls[0]);
			float y = c.GetFloat(Definition.FloatControls[1]);
			return CalcDirection(x, y);		
		}
	}

	[DisplayName("Super Action Controller")]
	public class ColecoSuperActionController : IPort
	{
		public ColecoSuperActionController(int portNum)
		{
			PortNum = portNum;
			Definition = new ControllerDefinition
			{
				BoolButtons = BaseBoolDefinition
				.Select(b => "P" + PortNum + " " + b)
				.ToList(),
				FloatControls = { "P" + PortNum + " Disc X", "P" + PortNum + " Disc Y" },
				FloatRanges = { new[] { -127.0f, 0, 127.0f }, new[] { -127.0f, 0, 127.0f } }
			};
		}

		public int PortNum { get; private set; }

		public ControllerDefinition Definition { get; private set; }

		public byte Read(IController c, bool left_mode, bool updateWheel, float wheelAngle)
		{
			if (left_mode)
			{
				byte retval = 0x4F;
				if (c.IsPressed(Definition.BoolButtons[0])) retval &= 0xFE;
				if (c.IsPressed(Definition.BoolButtons[1])) retval &= 0xFD;
				if (c.IsPressed(Definition.BoolButtons[2])) retval &= 0xFB;
				if (c.IsPressed(Definition.BoolButtons[3])) retval &= 0xF7;
				if (c.IsPressed(Definition.BoolButtons[4])) retval &= 0x3F;

				float x = c.GetFloat(Definition.FloatControls[0]);
				float y = c.GetFloat(Definition.FloatControls[1]);

				float angle;

				if (updateWheel)
				{
					angle = wheelAngle;
				}
				else
				{
					angle = CalcDirection(x, y);
				}

				byte temp2 = 0;

				int temp1 = (int)Math.Floor(angle / 1.25);
				temp1 = temp1 % 4;

				if (temp1 == 0)
				{
					temp2 = 0x10;
				}

				if (temp1 == 1)
				{
					temp2 = 0x30;
				}
				if (temp1 == 2)
				{
					temp2 = 0x20;
				}

				if (temp1 == 3)
				{
					temp2 = 0x00;
				}

				retval |= temp2;

				return retval;
			}
			else
			{
				byte retval = 0xF;
				//                                   0x00;
				if (c.IsPressed(Definition.BoolButtons[14])) retval = 0x01;
				if (c.IsPressed(Definition.BoolButtons[10])) retval = 0x02;
				if (c.IsPressed(Definition.BoolButtons[11])) retval = 0x03;
				//                                             0x04;
				if (c.IsPressed(Definition.BoolButtons[13])) retval = 0x05;
				if (c.IsPressed(Definition.BoolButtons[16])) retval = 0x06;
				if (c.IsPressed(Definition.BoolButtons[8])) retval = 0x07;
				//                                             0x08;
				if (c.IsPressed(Definition.BoolButtons[17])) retval = 0x09;
				if (c.IsPressed(Definition.BoolButtons[6])) retval = 0x0A;
				if (c.IsPressed(Definition.BoolButtons[15])) retval = 0x0B;
				if (c.IsPressed(Definition.BoolButtons[9])) retval = 0x0C;
				if (c.IsPressed(Definition.BoolButtons[7])) retval = 0x0D;
				if (c.IsPressed(Definition.BoolButtons[12])) retval = 0x0E;

				// extra buttons for SAC
				if (c.IsPressed(Definition.BoolButtons[18])) retval = 0x04;
				if (c.IsPressed(Definition.BoolButtons[19])) retval = 0x08;

				if (c.IsPressed(Definition.BoolButtons[5]) == false) retval |= 0x40;
				retval |= 0x30; // always set these bits
				return retval;
			}
		}

		public void SyncState(Serializer ser)
		{
			// nothing to do
		}

		private static readonly string[] BaseBoolDefinition =
		{
			"Up", "Right", "Down", "Left", "Yellow", "Red",
			"Key 0", "Key 1", "Key 2", "Key 3", "Key 4", "Key 5",
			"Key 6", "Key 7", "Key 8", "Key 9", "Pound", "Star",
			"Purple", "Blue"
		};

		// x and y are both assumed to be in [-127, 127]
		// x increases from left to right
		// y increases from top to bottom
		private static float CalcDirection(float x, float y)
		{
			y = -y; // vflip to match the arrangement of FloatControllerButtons

			// the wheel is arranged in a grey coded configuration of sensitivity ~2.5 degrees
			// for each signal
			// so overall the value returned changes every 1.25 degrees

			float angle = (float)(Math.Atan2(y, x) * 180.0 / Math.PI);

			if (angle < 0)
			{
				angle = 360 + angle;
			}

			return angle;
		}

		public float UpdateWheel(IController c)
		{
			float x = c.GetFloat(Definition.FloatControls[0]);
			float y = c.GetFloat(Definition.FloatControls[1]);
			return CalcDirection(x, y);
		}
	}
}
