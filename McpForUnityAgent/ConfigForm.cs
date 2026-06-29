using System;
using System.Collections.Generic;
using System.Diagnostics;
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

	private readonly Timer autoSaveTimer;

	private bool autoSaveReady;

	private string lastNotifiedFingerprint;

	public AppConfig Result { get; private set; }

	public event Action<AppConfig> ConfigChanged;

	public ConfigForm(AppConfig config)
	{
		Text = "McpForUnity Agent Config";
		AppWindow.ApplyIcon(this);
		base.Width = 820;
		base.Height = 540;
		MinimumSize = new Size(720, 460);
		base.StartPosition = FormStartPosition.CenterScreen;
		TableLayoutPanel root = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(12),
			RowCount = 2,
			ColumnCount = 1,
			RowStyles =
			{
				new RowStyle(SizeType.Percent, 100f),
				new RowStyle(SizeType.Absolute, 42f)
			}
		};
		base.Controls.Add(root);
		TableLayoutPanel tabs = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			RowCount = 2,
			ColumnCount = 1,
			RowStyles =
			{
				new RowStyle(SizeType.Absolute, 42f),
				new RowStyle(SizeType.Percent, 100f)
			}
		};
		root.Controls.Add(tabs, 0, 0);
		FlowLayoutPanel tabHeader = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false,
			Margin = new Padding(0, 0, 0, 6)
		};
		tabs.Controls.Add(tabHeader, 0, 0);
		Panel tabContent = new Panel
		{
			Dock = DockStyle.Fill,
			Margin = new Padding(0)
		};
		tabs.Controls.Add(tabContent, 0, 1);
		List<Button> tabButtons = new List<Button>();
		TableLayoutPanel agentLayout = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(8),
			RowCount = 8,
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
				new RowStyle(SizeType.Absolute, 100f),
				new RowStyle(SizeType.Absolute, 32f),
				new RowStyle(SizeType.Absolute, 32f),
				new RowStyle(SizeType.Absolute, 32f),
				new RowStyle(SizeType.Absolute, 42f),
				new RowStyle(SizeType.Absolute, 74f),
				new RowStyle(SizeType.Percent, 100f)
			}
		};
		Label executableLabel = new Label
		{
			Text = "Executable",
			TextAlign = ContentAlignment.MiddleLeft,
			Dock = DockStyle.Fill
		};
		agentLayout.Controls.Add(executableLabel, 0, 0);
		commandPathBox = new TextBox
		{
			Dock = DockStyle.Fill,
			Text = config.CommandPath ?? string.Empty
		};
		agentLayout.Controls.Add(commandPathBox, 1, 0);
		Button browseButton = new Button
		{
			Text = "Browse",
			Dock = DockStyle.Fill
		};
		browseButton.Click += delegate { BrowseExecutable(); };
		agentLayout.Controls.Add(browseButton, 2, 0);
		Label argumentsLabel = new Label
		{
			Text = "Arguments",
			TextAlign = ContentAlignment.MiddleLeft,
			Dock = DockStyle.Fill
		};
		agentLayout.Controls.Add(argumentsLabel, 0, 1);
		commandArgsBox = new TextBox
		{
			Dock = DockStyle.Fill,
			Multiline = true,
			ScrollBars = ScrollBars.Vertical,
			Text = config.CommandArgs ?? string.Empty
		};
		agentLayout.Controls.Add(commandArgsBox, 1, 1);
		agentLayout.SetColumnSpan(commandArgsBox, 2);
		Label retryLabel = new Label
		{
			Text = "Retry",
			TextAlign = ContentAlignment.MiddleLeft,
			Dock = DockStyle.Fill
		};
		agentLayout.Controls.Add(retryLabel, 0, 2);
		FlowLayoutPanel retryPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false
		};
		agentLayout.Controls.Add(retryPanel, 1, 2);
		agentLayout.SetColumnSpan(retryPanel, 2);
		autoRestartBox = new CheckBox
		{
			Text = "Auto restart when stopped",
			Checked = config.AutoRestart,
			AutoSize = true,
			Margin = new Padding(0, 7, 12, 0)
		};
		retryPanel.Controls.Add(autoRestartBox);
		restartDelayBox = new NumericUpDown
		{
			Minimum = 1m,
			Maximum = 3600m,
			Value = Math.Min(Math.Max(config.RestartDelaySeconds, 1), 3600),
			Width = 90,
			Margin = new Padding(0, 4, 0, 0)
		};
		retryPanel.Controls.Add(restartDelayBox);
		retryPanel.Controls.Add(new Label
		{
			Text = "seconds",
			AutoSize = true,
			TextAlign = ContentAlignment.MiddleLeft,
			Margin = new Padding(8, 7, 0, 0)
		});
		Label portCleanupLabel = new Label
		{
			Text = "Port cleanup",
			TextAlign = ContentAlignment.MiddleLeft,
			Dock = DockStyle.Fill
		};
		agentLayout.Controls.Add(portCleanupLabel, 0, 3);
		FlowLayoutPanel portCleanupPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false
		};
		agentLayout.Controls.Add(portCleanupPanel, 1, 3);
		agentLayout.SetColumnSpan(portCleanupPanel, 2);
		killPortOnRestartBox = new CheckBox
		{
			Text = "Kill port before start/restart",
			Checked = config.KillPortOnAutoRestart,
			AutoSize = true,
			Margin = new Padding(0, 7, 12, 0)
		};
		portCleanupPanel.Controls.Add(killPortOnRestartBox);
		killPortBox = new NumericUpDown
		{
			Minimum = 1m,
			Maximum = 65535m,
			Value = Math.Min(Math.Max(config.AutoRestartKillPort, 1), 65535),
			Width = 90,
			Margin = new Padding(0, 4, 0, 0)
		};
		portCleanupPanel.Controls.Add(killPortBox);
		portCleanupPanel.Controls.Add(new Label
		{
			Text = "port",
			AutoSize = true,
			TextAlign = ContentAlignment.MiddleLeft,
			Margin = new Padding(8, 7, 0, 0)
		});
		Label autoConnectLabel = new Label
		{
			Text = "Auto-connect",
			TextAlign = ContentAlignment.MiddleLeft,
			Dock = DockStyle.Fill
		};
		agentLayout.Controls.Add(autoConnectLabel, 0, 4);
		startWithUnityBox = new CheckBox
		{
			Text = "Unity MCP session",
			Checked = config.AutoConnectUnitySession,
			AutoSize = true,
			Margin = new Padding(0, 7, 0, 0)
		};
		agentLayout.Controls.Add(startWithUnityBox, 1, 4);
		agentLayout.SetColumnSpan(startWithUnityBox, 2);
		Label toolsLabel = new Label
		{
			Text = "Tools",
			TextAlign = ContentAlignment.MiddleLeft,
			Dock = DockStyle.Fill
		};
		agentLayout.Controls.Add(toolsLabel, 0, 5);
		Control toolsPanel = BuildAgentToolsPanel();
		agentLayout.Controls.Add(toolsPanel, 1, 5);
		agentLayout.SetColumnSpan(toolsPanel, 2);
		Label aboutLabel = new Label
		{
			Text = "About",
			TextAlign = ContentAlignment.MiddleLeft,
			Dock = DockStyle.Fill
		};
		agentLayout.Controls.Add(aboutLabel, 0, 6);
		Control aboutPanel = BuildAgentAboutPanel();
		agentLayout.Controls.Add(aboutPanel, 1, 6);
		agentLayout.SetColumnSpan(aboutPanel, 2);
		AddConfigTab(tabHeader, tabContent, tabButtons, "Server", agentLayout, 96);
		AddConfigTab(tabHeader, tabContent, tabButtons, "Client", new ClientConfigurationControl(), 96);
		SelectConfigTab(tabButtons, tabContent, agentLayout);
		base.Shown += delegate { SelectConfigTab(tabButtons, tabContent, agentLayout); };
		FlowLayoutPanel buttons = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.RightToLeft
		};
		root.Controls.Add(buttons, 0, 1);
		Button closeButton = new Button { Text = "Close", Width = 90 };
		closeButton.Click += delegate { CloseWithAutoSave(); };
		buttons.Controls.Add(closeButton);
		autoSaveTimer = new Timer
		{
			Interval = 350
		};
		autoSaveTimer.Tick += delegate
		{
			autoSaveTimer.Stop();
			FlushAutoSave();
		};
		base.FormClosing += delegate
		{
			autoSaveTimer.Stop();
			FlushAutoSave();
		};
		base.FormClosed += delegate { autoSaveTimer.Dispose(); };
		RegisterAutoSaveHandlers();
		Result = BuildResult();
		lastNotifiedFingerprint = GetConfigFingerprint(Result);
		autoSaveReady = true;
	}

	private static void AddConfigTab(FlowLayoutPanel tabHeader, Panel tabContent, List<Button> tabButtons, string text, Control content, int width)
	{
		content.Dock = DockStyle.Fill;
		content.Visible = false;
		tabContent.Controls.Add(content);
		Button button = new Button
		{
			Text = text,
			Width = width,
			Height = 34,
			Margin = new Padding(0, 0, 4, 0),
			Tag = content
		};
		button.Click += delegate { SelectConfigTab(tabButtons, tabContent, content); };
		tabButtons.Add(button);
		tabHeader.Controls.Add(button);
	}

	private static void SelectConfigTab(List<Button> tabButtons, Panel tabContent, Control selectedContent)
	{
		foreach (Control content in tabContent.Controls)
		{
			content.Visible = ReferenceEquals(content, selectedContent);
		}
		selectedContent.BringToFront();
		foreach (Button button in tabButtons)
		{
			bool selected = ReferenceEquals(button.Tag, selectedContent);
			button.FlatStyle = FlatStyle.Flat;
			button.UseVisualStyleBackColor = false;
			button.BackColor = selected ? AppWindow.PanelBackColor : Color.FromArgb(27, 29, 33);
			button.ForeColor = selected ? AppWindow.TextColor : AppWindow.MutedTextColor;
			button.FlatAppearance.BorderColor = selected ? AppWindow.AccentColor : AppWindow.BorderColor;
			button.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 55, 63);
			button.FlatAppearance.MouseDownBackColor = Color.FromArgb(60, 66, 76);
		}
	}

	private Control BuildAgentToolsPanel()
	{
		TableLayoutPanel panel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 4,
			RowCount = 1,
			Margin = new Padding(0),
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 25f),
				new ColumnStyle(SizeType.Percent, 25f),
				new ColumnStyle(SizeType.Percent, 25f),
				new ColumnStyle(SizeType.Percent, 25f)
			}
		};
		Button dependenciesButton = new Button { Text = "Dependencies...", Dock = DockStyle.Fill, Margin = new Padding(0, 3, 4, 3) };
		dependenciesButton.Click += delegate { DependencySetup.SetupInteractive(this); };
		panel.Controls.Add(dependenciesButton, 0, 0);
		Button unityPluginButton = new Button { Text = "Install Unity Plugin...", Dock = DockStyle.Fill, Margin = new Padding(0, 3, 4, 3) };
		unityPluginButton.Click += delegate { UnityProjectPluginInstaller.InstallInteractive(this); };
		panel.Controls.Add(unityPluginButton, 1, 0);
		Button logsButton = new Button { Text = "Open Logs Folder", Dock = DockStyle.Fill, Margin = new Padding(0, 3, 4, 3) };
		logsButton.Click += delegate
		{
			AppPaths.EnsureDirs();
			Process.Start("explorer.exe", "\"" + AppPaths.LogsDir + "\"");
		};
		panel.Controls.Add(logsButton, 2, 0);
		Button installDirButton = new Button { Text = "Open Install Folder", Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3) };
		installDirButton.Click += delegate
		{
			AppPaths.EnsureDirs();
			Process.Start("explorer.exe", "\"" + AppPaths.InstallDir + "\"");
		};
		panel.Controls.Add(installDirButton, 3, 0);
		return panel;
	}

	private Control BuildAgentAboutPanel()
	{
		TableLayoutPanel panel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			Margin = new Padding(0),
			ColumnCount = 1,
			RowCount = 2,
			RowStyles =
			{
				new RowStyle(SizeType.Absolute, 28f),
				new RowStyle(SizeType.Absolute, 34f)
			}
		};
		panel.Controls.Add(new Label
		{
			Text = AppVersion.DisplayName,
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft,
			Font = new Font(Font.FontFamily, 10f, FontStyle.Bold)
		}, 0, 0);
		panel.Controls.Add(new TextBox
		{
			Text = AppPaths.InstallDir,
			ReadOnly = true,
			Dock = DockStyle.Fill
		}, 0, 1);
		return panel;
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

	private void RegisterAutoSaveHandlers()
	{
		commandPathBox.TextChanged += delegate { QueueAutoSave(); };
		commandArgsBox.TextChanged += delegate { QueueAutoSave(); };
		autoRestartBox.CheckedChanged += delegate { QueueAutoSave(); };
		restartDelayBox.ValueChanged += delegate { QueueAutoSave(); };
		killPortOnRestartBox.CheckedChanged += delegate { QueueAutoSave(); };
		killPortBox.ValueChanged += delegate { QueueAutoSave(); };
		startWithUnityBox.CheckedChanged += delegate { QueueAutoSave(); };
	}

	private void QueueAutoSave()
	{
		if (!autoSaveReady)
		{
			return;
		}
		Result = BuildResult();
		autoSaveTimer.Stop();
		autoSaveTimer.Start();
	}

	private void FlushAutoSave()
	{
		if (!autoSaveReady)
		{
			return;
		}
		Result = BuildResult();
		string fingerprint = GetConfigFingerprint(Result);
		if (string.Equals(fingerprint, lastNotifiedFingerprint, StringComparison.Ordinal))
		{
			return;
		}
		lastNotifiedFingerprint = fingerprint;
		Action<AppConfig> handler = ConfigChanged;
		if (handler != null)
		{
			handler(Result);
		}
	}

	private void CloseWithAutoSave()
	{
		autoSaveTimer.Stop();
		FlushAutoSave();
		base.DialogResult = DialogResult.OK;
		Close();
	}

	private AppConfig BuildResult()
	{
		return new AppConfig
		{
			CommandPath = commandPathBox.Text.Trim().Trim('"'),
			CommandArgs = commandArgsBox.Text.Trim(),
			AutoRestart = autoRestartBox.Checked,
			RestartDelaySeconds = (int)restartDelayBox.Value,
			KillPortOnAutoRestart = killPortOnRestartBox.Checked,
			AutoRestartKillPort = (int)killPortBox.Value,
			AutoConnectUnitySession = startWithUnityBox.Checked
		};
	}

	private static string GetConfigFingerprint(AppConfig config)
	{
		if (config == null)
		{
			return string.Empty;
		}
		return (config.CommandPath ?? string.Empty) + "\n"
			+ (config.CommandArgs ?? string.Empty) + "\n"
			+ config.AutoRestart + "\n"
			+ config.RestartDelaySeconds + "\n"
			+ config.KillPortOnAutoRestart + "\n"
			+ config.AutoRestartKillPort + "\n"
			+ config.AutoConnectUnitySession;
	}
}
