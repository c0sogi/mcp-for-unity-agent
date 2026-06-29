using System;
using System.Drawing;
using System.Windows.Forms;

namespace McpForUnityAgent;

internal sealed class UninstallForm : Form
{
	private readonly CheckBox removeFilesBox;

	public UninstallForm()
	{
		Text = "Uninstall McpForUnity Agent";
		AppWindow.ApplyIcon(this);
		base.Width = 520;
		base.Height = 190;
		base.StartPosition = FormStartPosition.CenterScreen;
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(12),
			RowCount = 3,
			ColumnCount = 1,
			RowStyles = 
			{
				new RowStyle(SizeType.Percent, 100f),
				new RowStyle(SizeType.Absolute, 34f),
				new RowStyle(SizeType.Absolute, 44f)
			}
		};
		base.Controls.Add(tableLayoutPanel);
		Label control = new Label
		{
			Text = "This removes the startup entry and desktop shortcut.\r\nThe running tray app will be stopped first.",
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft
		};
		tableLayoutPanel.Controls.Add(control, 0, 0);
		removeFilesBox = new CheckBox();
		removeFilesBox.Text = "Also remove installed files and logs";
		removeFilesBox.Dock = DockStyle.Fill;
		tableLayoutPanel.Controls.Add(removeFilesBox, 0, 1);
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.RightToLeft
		};
		tableLayoutPanel.Controls.Add(flowLayoutPanel, 0, 2);
		Button button = new Button
		{
			Text = "Uninstall",
			Width = 100
		};
		EventHandler value = delegate
		{
			DoUninstall();
		};
		button.Click += value;
		flowLayoutPanel.Controls.Add(button);
		Button button2 = new Button();
		button2.Text = "Cancel";
		button2.Width = 100;
		button2.Click += delegate
		{
			Close();
		};
		flowLayoutPanel.Controls.Add(button2);
	}

	private void DoUninstall()
	{
		try
		{
			Installer.Uninstall(removeFilesBox.Checked);
			MessageBox.Show("Uninstalled.", "McpForUnity Agent", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			Close();
		}
		catch (Exception ex)
		{
			AppPaths.WriteTrayError(ex.ToString());
			MessageBox.Show(ex.Message, "McpForUnity Agent", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}
}

