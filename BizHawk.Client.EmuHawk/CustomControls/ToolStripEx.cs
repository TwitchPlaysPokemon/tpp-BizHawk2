﻿//credit: http://blogs.msdn.com/b/rickbrew/archive/2006/01/09/511003.aspx

using System;
using System.Windows.Forms;

/// <summary>
/// This class adds on to the functionality provided in System.Windows.Forms.ToolStrip.
/// </summary>
public class ToolStripEx : ToolStrip
{
	/// <summary>
	/// Gets or sets whether the ToolStripEx honors item clicks when its containing form does
	/// not have input focus.
	/// </summary>
	public bool ClickThrough { get; set; } = true;

	protected override void WndProc(ref Message m)
	{
		base.WndProc(ref m);
		if (ClickThrough
			&& m.Msg == NativeConstants.WM_MOUSEACTIVATE
			&& m.Result == (IntPtr)NativeConstants.MA_ACTIVATEANDEAT)
		{
			m.Result = (IntPtr)NativeConstants.MA_ACTIVATE;
		}
	}
}

/// <summary>
/// This class adds on to the functionality provided in System.Windows.Forms.MenuStrip.
/// </summary>
public class MenuStripEx : MenuStrip
{
	/// <summary>
	/// Gets or sets whether the ToolStripEx honors item clicks when its containing form does
	/// not have input focus.
	/// </summary>
	public bool ClickThrough { get; set; } = true;

	protected override void WndProc(ref Message m)
	{
		base.WndProc(ref m);
		if (ClickThrough
			&& m.Msg == NativeConstants.WM_MOUSEACTIVATE
			&& m.Result == (IntPtr)NativeConstants.MA_ACTIVATEANDEAT)
		{
			m.Result = (IntPtr)NativeConstants.MA_ACTIVATE;
		}
	}
}

/// <summary>
/// This class adds on to the functionality provided in System.Windows.Forms.MenuStrip.
/// </summary>
public class StatusStripEx : StatusStrip
{
	/// <summary>
	/// Gets or sets whether the ToolStripEx honors item clicks when its containing form does
	/// not have input focus.
	/// </summary>
	public bool ClickThrough { get; set; } = true;

	protected override void WndProc(ref Message m)
	{
		base.WndProc(ref m);
		if (ClickThrough
			&& m.Msg == NativeConstants.WM_MOUSEACTIVATE
			&& m.Result == (IntPtr)NativeConstants.MA_ACTIVATEANDEAT)
		{
			m.Result = (IntPtr)NativeConstants.MA_ACTIVATE;
		}
	}
}

internal sealed class NativeConstants
{
	private NativeConstants(){}
	internal const uint WM_MOUSEACTIVATE = 0x21;
	internal const uint MA_ACTIVATE = 1;
	internal const uint MA_ACTIVATEANDEAT = 2;
	internal const uint MA_NOACTIVATE = 3;
	internal const uint MA_NOACTIVATEANDEAT = 4;
}