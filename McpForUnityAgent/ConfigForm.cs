using System;
using System.Drawing;
using System.Windows.Forms;

namespace McpForUnityAgent;

internal sealed class ConfigForm : Form
{
	private readonly TextBox commandPathBox;

	private readonly TextBox commandArgsBox;

	private readonly CheckBox autoRestartBox;

	private readonly NumericUpDown restartDelayBox;

	private readonly CheckBox killPortOnRestartBox;

	private readonly NumericUpDown killPortBox;

	private readonly CheckBox startWithUnityBox;

	public AppConfig Result { get; private set; }

	public ConfigForm(AppConfig config)
	{
		Text = "McpForUnity Agent Config";
		AppWindow.ApplyIcon(this);
		base.Width = 720;
		base.Height = 390;
		MinimumSize = new Size(620, 360);
		base.StartPosition = FormStartPosition.CenterScreen;
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(12),
			RowCount = 7,
			ColumnCount = 3,
			ColumnStyles = 
			{
				new ColumnStyle(SizeType.Absolute, 110f),
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.Absolute, 90f)
			},
			RowStyles = 
			{
				new RowStyle(SizeType.Absolute, 32f),
				new RowStyle(SizeType.Absolute, 90f),
				new RowStyle(SizeType.Absolute, 32f),
				new RowStyle(SizeType.Absolute, 32f),
				new RowStyle(SizeType.Absolute, 32f),
				new RowStyle(SizeType.Percent, 100f),
				new RowStyle(SizeType.Absolute, 42f)
			}
		};
		base.Controls.Add(tableLayoutPanel);
		Label control = new Label
		{
			Text = "Executable",
			TextAlign = ContentAlignment.MiddleLeft,
			Dock = DockStyle.Fill
		};
		tableLayoutPanel.Controls.Add(control, 0, 0);
		commandPathBox = new TextBox();
		commandPathBox.Dock = DockStyle.Fill;
		commandPathBox.Text = config.CommandPath ?? string.Empty;
		tableLayoutPanel.Controls.Add(commandPathBox, 1, 0);
		Button button = new Button();
		button.Text = "Browse";
		button.Dock = DockStyle.Fill;
		button.Click += delegate
		{
			BrowseExecutable();
		};
		tableLayoutPanel.Controls.Add(button, 2, 0);
		Label control2 = new Label
		{
			Text = "Arguments",
			TextAlign = ContentAlignment.MiddleLeft,
			Dock = DockStyle.Fill
		};
		tableLayoutPanel.Controls.Add(control2, 0, 1);
		commandArgsBox = new TextBox();
		commandArgsBox.Dock = DockStyle.Fill;
		commandArgsBox.Multiline = true;
		commandArgsBox.ScrollBars = ScrollBars.Vertical;
		commandArgsBox.Text = config.CommandArgs ?? string.Empty;
		tableLayoutPanel.Controls.Add(commandArgsBox, 1, 1);
		tableLayoutPanel.SetColumnSpan(commandArgsBox, 2);
		Label control3 = new Label
		{
			Text = "Retry",
			TextAlign = ContentAlignment.MiddleLeft,
			Dock = DockStyle.Fill
		};
		tableLayoutPanel.Controls.Add(control3, 0, 2);
		FlowLayoutPanel retryPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false
		};
		tableLayoutPanel.Controls.Add(retryPanel, 1, 2);
		tableLayoutPanel.SetColumnSpan(retryPanel, 2);
		autoRestartBox = new CheckBox();
		autoRestartBox.Text = "Auto restart when stopped";
		autoRestartBox.Checked = config.AutoRestart;
		autoRestartBox.AutoSize = true;
		autoRestartBox.Padding = new Padding(0, 5, 12, 0);
		retryPanel.Controls.Add(autoRestartBox);
		restartDelayBox = new NumericUpDown();
		restartDelayBox.Minimum = 1m;
		restartDelayBox.Maximum = 3600m;
		restartDelayBox.Value = Math.Min(Math.Max(config.RestartDelaySeconds, 1), 3600);
		restartDelayBox.Width = 90;
		retryPanel.Controls.Add(restartDelayBox);
		Label secondsLabel = new Label
		{
			Text = "seconds",
			AutoSize = true,
			TextAlign = ContentAlignment.MiddleLeft,
			Padding = new Padding(6, 5, 0, 0)
		};
		retryPanel.Controls.Add(secondsLabel);
		Label portCleanupLabel = new Label
		{
			Text = "Port cleanup",
			TextAlign = ContentAlignment.MiddleLeft,
			Dock = DockStyle.Fill
		};
		tableLayoutPanel.Controls.Add(portCleanupLabel, 0, 3);
		FlowLayoutPanel portCleanupPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false
		};
		tableLayoutPanel.Controls.Add(portCleanupPanel, 1, 3);
		tableLayoutPanel.SetColumnSpan(portCleanupPanel, 2);
		killPortOnRestartBox = new CheckBox();
		killPortOnRestartBox.Text = "Kill port before auto restart";
		killPortOnRestartBox.Checked = config.KillPortOnAutoRestart;
		killPortOnRestartBox.AutoSize = true;
		killPortOnRestartBox.Padding = new Padding(0, 5, 12, 0);
		portCleanupPanel.Controls.Add(killPortOnRestartBox);
		killPortBox = new NumericUpDown();
		killPortBox.Minimum = 1m;
		killPortBox.Maximum = 65535m;
		killPortBox.Value = Math.Min(Math.Max(config.AutoRestartKillPort, 1), 65535);
		killPortBox.Width = 90;
		portCleanupPanel.Controls.Add(killPortBox);
		Label portLabel = new Label
		{
			Text = "port",
			AutoSize = true,
			TextAlign = ContentAlignment.MiddleLeft,
			Padding = new Padding(6, 5, 0, 0)
		};
		portCleanupPanel.Controls.Add(portLabel);
		Label autoConnectLabel = new Label
		{
			Text = "Auto-connect",
			TextAlign = ContentAlignment.MiddleLeft,
			Dock = DockStyle.Fill
		};
		tableLayoutPanel.Controls.Add(autoConnectLabel, 0, 4);
		startWithUnityBox = new CheckBox();
		startWithUnityBox.Text = "Unity MCP session";
		startWithUnityBox.Checked = config.AutoConnectUnitySession;
		startWithUnityBox.Dock = DockStyle.Fill;
		tableLayoutPanel.Controls.Add(startWithUnityBox, 1, 4);
		tableLayoutPanel.SetColumnSpan(startWithUnityBox, 2);
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.RightToLeft
		};
		tableLayoutPanel.Controls.Add(flowLayoutPanel, 0, 6);
		tableLayoutPanel.SetColumnSpan(flowLayoutPanel, 3);
		Button button2 = new Button();
		button2.Text = "Save";
		button2.Width = 90;
		button2.Click += delegate
		{
			SaveAndClose();
		};
		flowLayoutPanel.Controls.Add(button2);
		Button button3 = new Button();
		button3.Text = "Cancel";
		button3.Width = 90;
		button3.Click += delegate
		{
			base.DialogResult = DialogResult.Cancel;
			Close();
		};
		flowLayoutPanel.Controls.Add(button3);
		Button dependencyButton = new Button();
		dependencyButton.Text = "Dependencies...";
		dependencyButton.Width = 120;
		dependencyButton.Click += delegate
		{
			DependencySetup.SetupInteractive(this);
		};
		flowLayoutPanel.Controls.Add(dependencyButton);
	}

	private void BrowseExecutable()
	{
		using OpenFileDialog openFileDialog = new OpenFileDialog();
		openFileDialog.Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*";
		openFileDialog.FileName = commandPathBox.Text;
		if (openFileDialog.ShowDialog() == DialogResult.OK)
		{
			commandPathBox.Text = openFileDialog.FileName;
		}
	}

	private void SaveAndClose()
	{
		Result = new AppConfig
		{
			CommandPath = commandPathBox.Text.Trim().Trim('"'),
			CommandArgs = commandArgsBox.Text.Trim(),
			AutoRestart = autoRestartBox.Checked,
			RestartDelaySeconds = (int)restartDelayBox.Value,
			KillPortOnAutoRestart = killPortOnRestartBox.Checked,
			AutoRestartKillPort = (int)killPortBox.Value,
			AutoConnectUnitySession = startWithUnityBox.Checked
		};
		base.DialogResult = DialogResult.OK;
		Close();
	}
}

