﻿namespace BizHawk.Client.EmuHawk
{
	partial class MarkerControl
	{
		/// <summary> 
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary> 
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Component Designer generated code

		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			this.MarkerContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.JumpToMarkerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.ScrollToMarkerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.EditMarkerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.AddMarkerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
			this.RemoveMarkerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.JumpToMarkerButton = new System.Windows.Forms.Button();
			this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
			this.EditMarkerButton = new System.Windows.Forms.Button();
			this.AddMarkerButton = new System.Windows.Forms.Button();
			this.RemoveMarkerButton = new System.Windows.Forms.Button();
			this.ScrollToMarkerButton = new System.Windows.Forms.Button();
			this.AddMarkerWithTextButton = new System.Windows.Forms.Button();
			this.MarkerView = new BizHawk.Client.EmuHawk.InputRoll();
			this.MarkersGroupBox = new System.Windows.Forms.GroupBox();
			this.MarkerContextMenu.SuspendLayout();
			this.MarkersGroupBox.SuspendLayout();
			this.SuspendLayout();
			// 
			// MarkerContextMenu
			// 
			this.MarkerContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.JumpToMarkerToolStripMenuItem,
            this.ScrollToMarkerToolStripMenuItem,
            this.EditMarkerToolStripMenuItem,
            this.AddMarkerToolStripMenuItem,
            this.toolStripSeparator1,
            this.RemoveMarkerToolStripMenuItem});
			this.MarkerContextMenu.Name = "MarkerContextMenu";
			this.MarkerContextMenu.Size = new System.Drawing.Size(119, 120);
			this.MarkerContextMenu.Opening += new System.ComponentModel.CancelEventHandler(this.MarkerContextMenu_Opening);
			// 
			// JumpToMarkerToolStripMenuItem
			// 
			this.JumpToMarkerToolStripMenuItem.Image = global::BizHawk.Client.EmuHawk.Properties.Resources.JumpTo;
			this.JumpToMarkerToolStripMenuItem.Name = "JumpToMarkerToolStripMenuItem";
			this.JumpToMarkerToolStripMenuItem.Size = new System.Drawing.Size(118, 22);
			this.JumpToMarkerToolStripMenuItem.Text = "Jump To";
			this.JumpToMarkerToolStripMenuItem.Click += new System.EventHandler(this.JumpToMarkerToolStripMenuItem_Click);
			// 
			// ScrollToMarkerToolStripMenuItem
			// 
			this.ScrollToMarkerToolStripMenuItem.Image = global::BizHawk.Client.EmuHawk.Properties.Resources.ScrollTo;
			this.ScrollToMarkerToolStripMenuItem.Name = "ScrollToMarkerToolStripMenuItem";
			this.ScrollToMarkerToolStripMenuItem.Size = new System.Drawing.Size(118, 22);
			this.ScrollToMarkerToolStripMenuItem.Text = "Scroll To";
			this.ScrollToMarkerToolStripMenuItem.Click += new System.EventHandler(this.ScrollToMarkerToolStripMenuItem_Click);
			// 
			// EditMarkerToolStripMenuItem
			// 
			this.EditMarkerToolStripMenuItem.Image = global::BizHawk.Client.EmuHawk.Properties.Resources.pencil;
			this.EditMarkerToolStripMenuItem.Name = "EditMarkerToolStripMenuItem";
			this.EditMarkerToolStripMenuItem.Size = new System.Drawing.Size(118, 22);
			this.EditMarkerToolStripMenuItem.Text = "Edit";
			this.EditMarkerToolStripMenuItem.Click += new System.EventHandler(this.EditMarkerToolStripMenuItem_Click);
			// 
			// AddMarkerToolStripMenuItem
			// 
			this.AddMarkerToolStripMenuItem.Image = global::BizHawk.Client.EmuHawk.Properties.Resources.add;
			this.AddMarkerToolStripMenuItem.Name = "AddMarkerToolStripMenuItem";
			this.AddMarkerToolStripMenuItem.Size = new System.Drawing.Size(118, 22);
			this.AddMarkerToolStripMenuItem.Text = "Add";
			this.AddMarkerToolStripMenuItem.Click += new System.EventHandler(this.AddMarkerToolStripMenuItem_Click);
			// 
			// toolStripSeparator1
			// 
			this.toolStripSeparator1.Name = "toolStripSeparator1";
			this.toolStripSeparator1.Size = new System.Drawing.Size(115, 6);
			// 
			// RemoveMarkerToolStripMenuItem
			// 
			this.RemoveMarkerToolStripMenuItem.Image = global::BizHawk.Client.EmuHawk.Properties.Resources.Delete;
			this.RemoveMarkerToolStripMenuItem.Name = "RemoveMarkerToolStripMenuItem";
			this.RemoveMarkerToolStripMenuItem.Size = new System.Drawing.Size(118, 22);
			this.RemoveMarkerToolStripMenuItem.Text = "Remove";
			this.RemoveMarkerToolStripMenuItem.Click += new System.EventHandler(this.RemoveMarkerToolStripMenuItem_Click);
			// 
			// JumpToMarkerButton
			// 
			this.JumpToMarkerButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.JumpToMarkerButton.Enabled = false;
			this.JumpToMarkerButton.Image = global::BizHawk.Client.EmuHawk.Properties.Resources.JumpTo;
			this.JumpToMarkerButton.Location = new System.Drawing.Point(66, 247);
			this.JumpToMarkerButton.Name = "JumpToMarkerButton";
			this.JumpToMarkerButton.Size = new System.Drawing.Size(24, 24);
			this.JumpToMarkerButton.TabIndex = 3;
			this.toolTip1.SetToolTip(this.JumpToMarkerButton, "Jump To Marker Frame");
			this.JumpToMarkerButton.UseVisualStyleBackColor = true;
			this.JumpToMarkerButton.Click += new System.EventHandler(this.JumpToMarkerToolStripMenuItem_Click);
			// 
			// EditMarkerButton
			// 
			this.EditMarkerButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.EditMarkerButton.Enabled = false;
			this.EditMarkerButton.Image = global::BizHawk.Client.EmuHawk.Properties.Resources.pencil;
			this.EditMarkerButton.Location = new System.Drawing.Point(126, 247);
			this.EditMarkerButton.Name = "EditMarkerButton";
			this.EditMarkerButton.Size = new System.Drawing.Size(24, 24);
			this.EditMarkerButton.TabIndex = 5;
			this.toolTip1.SetToolTip(this.EditMarkerButton, "Edit Marker Text");
			this.EditMarkerButton.UseVisualStyleBackColor = true;
			this.EditMarkerButton.Click += new System.EventHandler(this.EditMarkerToolStripMenuItem_Click);
			// 
			// AddMarkerButton
			// 
			this.AddMarkerButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.AddMarkerButton.Image = global::BizHawk.Client.EmuHawk.Properties.Resources.add;
			this.AddMarkerButton.Location = new System.Drawing.Point(6, 247);
			this.AddMarkerButton.Name = "AddMarkerButton";
			this.AddMarkerButton.Size = new System.Drawing.Size(24, 24);
			this.AddMarkerButton.TabIndex = 1;
			this.toolTip1.SetToolTip(this.AddMarkerButton, "Add Marker to Emulated Frame");
			this.AddMarkerButton.UseVisualStyleBackColor = true;
			this.AddMarkerButton.Click += new System.EventHandler(this.AddMarkerToolStripMenuItem_Click);
			// 
			// RemoveMarkerButton
			// 
			this.RemoveMarkerButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.RemoveMarkerButton.Enabled = false;
			this.RemoveMarkerButton.Image = global::BizHawk.Client.EmuHawk.Properties.Resources.Delete;
			this.RemoveMarkerButton.Location = new System.Drawing.Point(156, 247);
			this.RemoveMarkerButton.Name = "RemoveMarkerButton";
			this.RemoveMarkerButton.Size = new System.Drawing.Size(24, 24);
			this.RemoveMarkerButton.TabIndex = 6;
			this.toolTip1.SetToolTip(this.RemoveMarkerButton, "Remove Marker");
			this.RemoveMarkerButton.UseVisualStyleBackColor = true;
			this.RemoveMarkerButton.Click += new System.EventHandler(this.RemoveMarkerToolStripMenuItem_Click);
			// 
			// ScrollToMarkerButton
			// 
			this.ScrollToMarkerButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.ScrollToMarkerButton.Enabled = false;
			this.ScrollToMarkerButton.Image = global::BizHawk.Client.EmuHawk.Properties.Resources.ScrollTo;
			this.ScrollToMarkerButton.Location = new System.Drawing.Point(96, 247);
			this.ScrollToMarkerButton.Name = "ScrollToMarkerButton";
			this.ScrollToMarkerButton.Size = new System.Drawing.Size(24, 24);
			this.ScrollToMarkerButton.TabIndex = 4;
			this.toolTip1.SetToolTip(this.ScrollToMarkerButton, "Scroll View To Marker Frame");
			this.ScrollToMarkerButton.UseVisualStyleBackColor = true;
			this.ScrollToMarkerButton.Click += new System.EventHandler(this.ScrollToMarkerToolStripMenuItem_Click);
			// 
			// AddMarkerWithTextButton
			// 
			this.AddMarkerWithTextButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.AddMarkerWithTextButton.Image = global::BizHawk.Client.EmuHawk.Properties.Resources.AddEdit;
			this.AddMarkerWithTextButton.Location = new System.Drawing.Point(36, 247);
			this.AddMarkerWithTextButton.Name = "AddMarkerWithTextButton";
			this.AddMarkerWithTextButton.Size = new System.Drawing.Size(24, 24);
			this.AddMarkerWithTextButton.TabIndex = 2;
			this.toolTip1.SetToolTip(this.AddMarkerWithTextButton, "Add Marker with Text to Emulated Frame");
			this.AddMarkerWithTextButton.UseVisualStyleBackColor = true;
			this.AddMarkerWithTextButton.Click += new System.EventHandler(this.AddMarkerWithTextToolStripMenuItem_Click);
			// 
			// MarkerView
			// 
			this.MarkerView.CellWidthPadding = 3;
			this.MarkerView.GridLines = true;
			this.MarkerView.AllowColumnReorder = false;
			this.MarkerView.AllowColumnResize = false;
			this.MarkerView.AlwaysScroll = false;
			this.MarkerView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.MarkerView.CellHeightPadding = 0;
			this.MarkerView.ContextMenuStrip = this.MarkerContextMenu;
			this.MarkerView.Font = new System.Drawing.Font("Arial", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.MarkerView.FullRowSelect = true;
			this.MarkerView.HideWasLagFrames = false;
			this.MarkerView.HorizontalOrientation = false;
			this.MarkerView.LagFramesToHide = 0;
			this.MarkerView.LetKeysModifySelection = false;
			this.MarkerView.Location = new System.Drawing.Point(6, 19);
			this.MarkerView.MultiSelect = false;
			this.MarkerView.Name = "MarkerView";
			this.MarkerView.RowCount = 0;
			this.MarkerView.ScrollSpeed = 1;
			this.MarkerView.SeekingCutoffInterval = 0;
			this.MarkerView.Size = new System.Drawing.Size(186, 224);
			this.MarkerView.TabIndex = 0;
			this.MarkerView.TabStop = false;
			this.MarkerView.SelectedIndexChanged += new System.EventHandler(this.MarkerView_SelectedIndexChanged);
			this.MarkerView.MouseClick += new System.Windows.Forms.MouseEventHandler(this.MarkerView_MouseClick);
			this.MarkerView.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.MarkerView_MouseDoubleClick);
			// 
			// MarkersGroupBox
			// 
			this.MarkersGroupBox.Controls.Add(this.MarkerView);
			this.MarkersGroupBox.Controls.Add(this.AddMarkerButton);
			this.MarkersGroupBox.Controls.Add(this.AddMarkerWithTextButton);
			this.MarkersGroupBox.Controls.Add(this.RemoveMarkerButton);
			this.MarkersGroupBox.Controls.Add(this.ScrollToMarkerButton);
			this.MarkersGroupBox.Controls.Add(this.EditMarkerButton);
			this.MarkersGroupBox.Controls.Add(this.JumpToMarkerButton);
			this.MarkersGroupBox.Dock = System.Windows.Forms.DockStyle.Fill;
			this.MarkersGroupBox.Location = new System.Drawing.Point(0, 0);
			this.MarkersGroupBox.Name = "MarkersGroupBox";
			this.MarkersGroupBox.Size = new System.Drawing.Size(198, 278);
			this.MarkersGroupBox.TabIndex = 0;
			this.MarkersGroupBox.TabStop = false;
			this.MarkersGroupBox.Text = "Markers";
			// 
			// MarkerControl
			// 
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Inherit;
			this.Controls.Add(this.MarkersGroupBox);
			this.Name = "MarkerControl";
			this.Size = new System.Drawing.Size(198, 278);
			this.MarkerContextMenu.ResumeLayout(false);
			this.MarkersGroupBox.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion

		private InputRoll MarkerView;
		private System.Windows.Forms.Button AddMarkerButton;
		private System.Windows.Forms.Button RemoveMarkerButton;
		private System.Windows.Forms.Button JumpToMarkerButton;
		private System.Windows.Forms.Button EditMarkerButton;
		private System.Windows.Forms.Button ScrollToMarkerButton;
		private System.Windows.Forms.ToolTip toolTip1;
		private System.Windows.Forms.ContextMenuStrip MarkerContextMenu;
		private System.Windows.Forms.ToolStripMenuItem ScrollToMarkerToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem EditMarkerToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem AddMarkerToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem RemoveMarkerToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem JumpToMarkerToolStripMenuItem;
		private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
		private System.Windows.Forms.Button AddMarkerWithTextButton;
		private System.Windows.Forms.GroupBox MarkersGroupBox;
	}
}
