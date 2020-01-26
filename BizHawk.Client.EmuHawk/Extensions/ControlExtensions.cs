﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

using BizHawk.Common;
using BizHawk.Common.ReflectionExtensions;

namespace BizHawk.Client.EmuHawk.WinFormExtensions
{
	public static class ControlExtensions
	{
		/// <exception cref="ArgumentException"><typeparamref name="T"/> does not inherit <see cref="Enum"/></exception>
		public static void PopulateFromEnum<T>(this ComboBox box, object enumVal)
			where T : struct, IConvertible
		{
			if (!typeof(T).IsEnum)
			{
				throw new ArgumentException("T must be an enumerated type");
			}

			box.Items.Clear();
			box.Items.AddRange(
				typeof(T).GetEnumDescriptions()
				.ToArray());
			box.SelectedItem = enumVal.GetDescription();
		}

		// extension method to make Control.Invoke easier to use
		public static object Invoke(this Control control, Action action)
		{
			return control.Invoke(action);
		}

		// extension method to make Control.BeginInvoke easier to use
		public static IAsyncResult BeginInvoke(this Control control, Action action)
		{
			return control.BeginInvoke(action);
		}

		public static ToolStripMenuItem ToColumnsMenu(this InputRoll inputRoll, Action changeCallback)
		{
			var menu = new ToolStripMenuItem
			{
				Name = "GeneratedColumnsSubMenu",
				Text = "Columns"
			};

			var columns = inputRoll.AllColumns;

			foreach (var column in columns)
			{
				var menuItem = new ToolStripMenuItem
				{
					Name = column.Name,
					Text = $"{column.Text} ({column.Name})",
					Checked = column.Visible,
					CheckOnClick = true,
					Tag = column.Name
				};

				menuItem.CheckedChanged += (o, ev) =>
				{
					var sender = (ToolStripMenuItem)o;
					columns.Find(c => c.Name == (string)sender.Tag).Visible = sender.Checked;
					columns.ColumnsChanged();
					changeCallback();
					inputRoll.Refresh();
				};

				menu.DropDownItems.Add(menuItem);
			}

			return menu;
		}

		public static Point ChildPointToScreen(this Control control, Control child)
		{
			return control.PointToScreen(new Point(child.Location.X, child.Location.Y));
		}

		public static Color Add(this Color color, int val)
		{
			var col = color.ToArgb();
			col += val;
			return Color.FromArgb(col);
		}

		public static T Clone<T>(this T controlToClone)
			where T : Control
		{
			PropertyInfo[] controlProperties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

			Type t = controlToClone.GetType();
			T instance = Activator.CreateInstance(t) as T;

			t.GetProperty("AutoSize")?.SetValue(instance, false, null);

			for (int i = 0; i < 3; i++)
			{
				foreach (var propInfo in controlProperties)
				{
					if (!propInfo.CanWrite)
					{
						continue;
					}

					if (propInfo.Name != "AutoSize" && propInfo.Name != "WindowTarget")
					{
						propInfo.SetValue(instance, propInfo.GetValue(controlToClone, null), null);
					}
				}
			}

			if (instance is RetainedViewportPanel panel)
			{
				var cloneBmp = (controlToClone as RetainedViewportPanel).GetBitmap().Clone() as Bitmap;
				panel.SetBitmap(cloneBmp);
			}

			return instance;
		}

		#region Enumerable to Enumerable<T>

		/// <summary>
		/// Converts the outdated IEnumerable Controls property to an <see cref="IEnumerable{T}"/> like .NET should have done a long time ago
		/// </summary>
		public static IEnumerable<Control> Controls(this Control control)
		{
			return control.Controls
				.OfType<Control>();
		}

		public static IEnumerable<TabPage> TabPages(this TabControl tabControl)
		{
			return tabControl.TabPages.Cast<TabPage>();
		}

		#endregion
	}

	public static class FormExtensions
	{
		/// <summary>
		/// Handles EmuHawk specific issues before showing a modal dialog
		/// </summary>
		public static DialogResult ShowHawkDialog(this Form form, IWin32Window owner = null, Point position = default(Point))
		{
			GlobalWin.Sound.StopSound();
			if (position != default(Point))
			{
				form.StartPosition = FormStartPosition.Manual;
				form.Location = position;
			}
			var result = (owner == null ? form.ShowDialog(new Form { TopMost = true }) : form.ShowDialog(owner));
			GlobalWin.Sound.StartSound();
			return result;
		}

		/// <summary>
		/// Handles EmuHawk specific issues before showing a modal dialog
		/// </summary>
		public static DialogResult ShowHawkDialog(this CommonDialog form)
		{
			GlobalWin.Sound.StopSound();
			using var tempForm = new Form { TopMost = true };
			var result = form.ShowDialog(tempForm);
			GlobalWin.Sound.StartSound();
			return result;
		}
	}

	public static class ListViewExtensions
	{
		/// <summary>
		/// Dumps the contents of the ListView into a tab separated list of lines
		/// </summary>
		public static string CopyItemsAsText(this ListView listViewControl)
		{
			var indexes = listViewControl.SelectedIndices;
			if (indexes.Count <= 0)
			{
				return "";
			}

			var sb = new StringBuilder();

			// walk over each selected item and subitem within it to generate a string from it
			foreach (int index in indexes)
			{
				foreach (ListViewItem.ListViewSubItem item in listViewControl.Items[index].SubItems)
				{
					if (!string.IsNullOrWhiteSpace(item.Text))
					{
						sb.Append(item.Text).Append('\t');
					}
				}

				// remove the last tab
				sb.Remove(sb.Length - 1, 1);

				sb.Append("\r\n");
			}

			// remove last newline
			sb.Length -= 2;


			return sb.ToString();
		}

		/// <exception cref="Win32Exception">unmanaged call failed</exception>
		public static void SetSortIcon(this ListView listViewControl, int columnIndex, SortOrder order)
		{
			const int LVM_GETHEADER = 4127;
			const int HDM_GETITEM = 4619;
			const int HDM_SETITEM = 4620;
			var columnHeader = Win32Imports.SendMessage(listViewControl.Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
			for (int columnNumber = 0, l = listViewControl.Columns.Count; columnNumber < l; columnNumber++)
			{
				var columnPtr = new IntPtr(columnNumber);
				var item = new Win32Imports.HDITEM { mask = Win32Imports.HDITEM.Mask.Format };
				if (Win32Imports.SendMessage(columnHeader, HDM_GETITEM, columnPtr, ref item) == IntPtr.Zero) throw new Win32Exception();
				if (columnNumber != columnIndex || order == SortOrder.None)
				{
					item.fmt &= ~Win32Imports.HDITEM.Format.SortDown & ~Win32Imports.HDITEM.Format.SortUp;
				}
				else if (order == SortOrder.Ascending)
				{
					item.fmt &= ~Win32Imports.HDITEM.Format.SortDown;
					item.fmt |= Win32Imports.HDITEM.Format.SortUp;
				}
				else if (order == SortOrder.Descending)
				{
					item.fmt &= ~Win32Imports.HDITEM.Format.SortUp;
					item.fmt |= Win32Imports.HDITEM.Format.SortDown;
				}
				if (Win32Imports.SendMessage(columnHeader, HDM_SETITEM, columnPtr, ref item) == IntPtr.Zero) throw new Win32Exception();
			}
		}

		public static bool IsOk(this DialogResult dialogResult)
		{
			return dialogResult == DialogResult.OK;
		}
	}
}
