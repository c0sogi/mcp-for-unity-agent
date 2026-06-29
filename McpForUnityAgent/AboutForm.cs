using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace McpForUnityAgent;

internal sealed class AboutForm : Form
{
	public AboutForm()
	{
		Text = "About " + AppPaths.AppName;
		AppWindow.ApplyIcon(this);
		base.Width = 520;
		base.Height = 210;
		MinimumSize = new Size(460, 190);
		StartPosition = FormStartPosition.CenterScreen;
		TableLayoutPanel root = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(16),
			ColumnCount = 1,
			RowCount = 4,
			RowStyles =
			{
				new RowStyle(SizeType.Absolute, 34f),
				new RowStyle(SizeType.Absolute, 24f),
				new RowStyle(SizeType.Absolute, 36f),
				new RowStyle(SizeType.Percent, 100f)
			}
		};
		Controls.Add(root);
		Label titleLabel = new Label
		{
			Text = AppVersion.DisplayName,
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft,
			Font = new Font(Font.FontFamily, 12f, FontStyle.Bold)
		};
		root.Controls.Add(titleLabel, 0, 0);
		Label installDirLabel = new Label
		{
			Text = "Install dir",
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft
		};
		root.Controls.Add(installDirLabel, 0, 1);
		TextBox installDirBox = new TextBox
		{
			Dock = DockStyle.Fill,
			ReadOnly = true,
			Text = AppPaths.InstallDir
		};
		root.Controls.Add(installDirBox, 0, 2);
		FlowLayoutPanel buttons = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.RightToLeft
		};
		root.Controls.Add(buttons, 0, 3);
		Button closeButton = new Button
		{
			Text = "Close",
			Width = 92
		};
		closeButton.Click += delegate
		{
			Close();
		};
		buttons.Controls.Add(closeButton);
		Button openButton = new Button
		{
			Text = "Open Folder",
			Width = 110
		};
		openButton.Click += delegate
		{
			OpenInstallDir();
		};
		buttons.Controls.Add(openButton);
		AcceptButton = openButton;
		CancelButton = closeButton;
	}

	private static void OpenInstallDir()
	{
		AppPaths.EnsureDirs();
		Process.Start("explorer.exe", "\"" + AppPaths.InstallDir + "\"");
	}
}
