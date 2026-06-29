using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace McpForUnityAgent;

internal sealed class InstallForm : Form
{
	private readonly ConfigFormContent content;

	private readonly CheckBox startNowBox;

	public InstallForm()
	{
		Text = "Install " + AppVersion.DisplayName;
		AppWindow.ApplyIcon(this);
		base.Width = 760;
		base.Height = 500;
		MinimumSize = new Size(680, 420);
		base.StartPosition = FormStartPosition.CenterScreen;
		AppConfig config = (File.Exists(AppPaths.ConfigPath) ? AppConfig.Load() : AppConfig.Defaults());
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(12),
			RowCount = 5,
			ColumnCount = 1,
			RowStyles = 
			{
				new RowStyle(SizeType.Absolute, 44f),
				new RowStyle(SizeType.Percent, 100f),
				new RowStyle(SizeType.Absolute, 34f),
				new RowStyle(SizeType.Absolute, 40f),
				new RowStyle(SizeType.Absolute, 44f)
			}
		};
		base.Controls.Add(tableLayoutPanel);
		Label control = new Label
		{
			Text = "Install one agent app, one desktop shortcut, and one startup entry.",
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft
		};
		tableLayoutPanel.Controls.Add(control, 0, 0);
		content = new ConfigFormContent(config);
		tableLayoutPanel.Controls.Add(content, 0, 1);
		startNowBox = new CheckBox();
		startNowBox.Text = "Start after install";
		startNowBox.Checked = true;
		startNowBox.Dock = DockStyle.Fill;
		tableLayoutPanel.Controls.Add(startNowBox, 0, 2);
		FlowLayoutPanel dependencyPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.LeftToRight
		};
		tableLayoutPanel.Controls.Add(dependencyPanel, 0, 3);
		Button setupButton = new Button();
		setupButton.Text = "Setup Dependencies";
		setupButton.Width = 150;
		setupButton.Click += delegate
		{
			DependencySetup.SetupInteractive(this);
		};
		dependencyPanel.Controls.Add(setupButton);
		Label dependencyLabel = new Label
		{
			Text = "Checks/installs uv and Git.",
			AutoSize = true,
			TextAlign = ContentAlignment.MiddleLeft,
			Padding = new Padding(0, 7, 0, 0)
		};
		dependencyPanel.Controls.Add(dependencyLabel);
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.RightToLeft
		};
		tableLayoutPanel.Controls.Add(flowLayoutPanel, 0, 4);
		Button button = new Button();
		button.Text = "Install";
		button.Width = 100;
		button.Click += delegate
		{
			DoInstall();
		};
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

	private void DoInstall()
	{
		AppConfig config = content.GetConfig();
		if (string.IsNullOrWhiteSpace(config.CommandPath))
		{
			MessageBox.Show("Executable path is empty.", "McpForUnity Agent", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			return;
		}
		if (!File.Exists(config.CommandPath))
		{
			DialogResult dialogResult = MessageBox.Show("Executable was not found:\r\n" + config.CommandPath + "\r\n\r\nInstall anyway?", "McpForUnity Agent", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
			if (dialogResult != DialogResult.Yes)
			{
				return;
			}
		}
		try
		{
			Installer.Install(config, startNowBox.Checked);
			MessageBox.Show("Installed.\r\n\r\nUse the McpForUnity Agent icon on the desktop or in the system tray.", "McpForUnity Agent", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			Close();
		}
		catch (Exception ex)
		{
			AppPaths.WriteTrayError(ex.ToString());
			MessageBox.Show(ex.Message, "McpForUnity Agent", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}
}

