using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace McpForUnityAgent;

internal sealed class DependencySetupForm : Form
{
	private readonly Label uvStatusLabel;

	private readonly Label uvStatusVersionLabel;

	private readonly Label uvSourceLabel;

	private readonly Label gitStatusLabel;

	private readonly Label gitStatusVersionLabel;

	private readonly Label gitSourceLabel;

	private readonly Label summaryLabel;

	private readonly Button uvButton;

	private readonly Button gitButton;

	private readonly Button installAllButton;

	private readonly Button refreshButton;

	private DependencySetup.DependencySnapshot snapshot;

	private bool checking;

	public DependencySetupForm()
	{
		Text = "Dependencies";
		AppWindow.ApplyIcon(this);
		base.Width = 740;
		base.Height = 260;
		MinimumSize = new Size(660, 240);
		base.StartPosition = FormStartPosition.CenterParent;
		TableLayoutPanel root = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(12),
			RowCount = 2,
			ColumnCount = 1,
			RowStyles =
			{
				new RowStyle(SizeType.Percent, 100f),
				new RowStyle(SizeType.Absolute, 46f)
			}
		};
		base.Controls.Add(root);
		TableLayoutPanel grid = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 4,
			RowCount = 3,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Absolute, 88f),
				new ColumnStyle(SizeType.Absolute, 190f),
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.Absolute, 110f)
			},
			RowStyles =
			{
				new RowStyle(SizeType.Absolute, 30f),
				new RowStyle(SizeType.Absolute, 54f),
				new RowStyle(SizeType.Absolute, 54f)
			}
		};
		root.Controls.Add(grid, 0, 0);
		AddHeader(grid, "Dependency", 0);
		AddHeader(grid, "Status", 1);
		AddHeader(grid, "Latest installer", 2);
		AddHeader(grid, "Action", 3);
		AddName(grid, "uv", 0, 1);
		AddStatus(grid, 1, out uvStatusLabel, out uvStatusVersionLabel);
		uvSourceLabel = AddValue(grid, "", 2, 1);
		uvButton = AddAction(grid, "Install", 3, 1);
		uvButton.Click += delegate
		{
			DependencySetup.StartUvInstaller();
			SetSummary("uv install/update window started.", AppWindow.AccentColor);
		};
		AddName(grid, "Git", 0, 2);
		AddStatus(grid, 2, out gitStatusLabel, out gitStatusVersionLabel);
		gitSourceLabel = AddValue(grid, "", 2, 2);
		gitButton = AddAction(grid, "Install", 3, 2);
		gitButton.Click += delegate
		{
			DependencySetup.StartGitInstaller();
			summaryLabel.Text = "Git installer window started.";
		};
		summaryLabel = new Label
		{
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft,
			Font = new Font(Font, FontStyle.Bold)
		};
		TableLayoutPanel bottom = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			RowCount = 1,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.Absolute, 430f)
			}
		};
		root.Controls.Add(bottom, 0, 1);
		bottom.Controls.Add(summaryLabel, 0, 0);
		FlowLayoutPanel buttons = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.RightToLeft
		};
		bottom.Controls.Add(buttons, 1, 0);
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
		installAllButton = new Button
		{
			Text = "Install Missing",
			Width = 120
		};
		installAllButton.Click += delegate
		{
			if (snapshot != null && snapshot.HasMissing)
			{
				DependencySetup.StartMissingInstallers(snapshot);
			}
			else
			{
				DependencySetup.StartAvailableInstallers(snapshot);
			}
			summaryLabel.Text = "Installer window(s) started.";
		};
		buttons.Controls.Add(installAllButton);
		refreshButton = new Button
		{
			Text = "Refresh",
			Width = 92
		};
		refreshButton.Click += delegate
		{
			RefreshStatus();
		};
		buttons.Controls.Add(refreshButton);
		UpdateView(DependencySetup.CheckStatus(probeInstallers: false), "Checking");
	}

	protected override void OnShown(EventArgs e)
	{
		base.OnShown(e);
		RefreshStatus();
	}

	private static void AddHeader(TableLayoutPanel grid, string text, int column)
	{
		grid.Controls.Add(new Label
		{
			Text = text,
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft,
			Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold)
		}, column, 0);
	}

	private static void AddName(TableLayoutPanel grid, string text, int column, int row)
	{
		grid.Controls.Add(new Label
		{
			Text = text,
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft
		}, column, row);
	}

	private static Label AddValue(TableLayoutPanel grid, string text, int column, int row)
	{
		Label label = new Label
		{
			Text = text,
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft,
			AutoEllipsis = true
		};
		grid.Controls.Add(label, column, row);
		return label;
	}

	private static void AddStatus(TableLayoutPanel grid, int row, out Label statusLabel, out Label versionLabel)
	{
		TableLayoutPanel panel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 2,
			Margin = new Padding(0)
		};
		panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
		panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		statusLabel = new Label
		{
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.BottomLeft,
			Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
			AutoEllipsis = true
		};
		versionLabel = new Label
		{
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.TopLeft,
			Font = new Font(SystemFonts.MessageBoxFont.FontFamily, Math.Max(7f, SystemFonts.MessageBoxFont.Size - 1f)),
			ForeColor = SystemColors.GrayText,
			AutoEllipsis = true
		};
		panel.Controls.Add(statusLabel, 0, 0);
		panel.Controls.Add(versionLabel, 0, 1);
		grid.Controls.Add(panel, 1, row);
	}

	private static Button AddAction(TableLayoutPanel grid, string text, int column, int row)
	{
		Button button = new Button
		{
			Text = text,
			Anchor = AnchorStyles.None,
			Width = 96,
			Height = 30,
			Margin = new Padding(0)
		};
		grid.Controls.Add(button, column, row);
		return button;
	}

	private void RefreshStatus()
	{
		if (checking)
		{
			return;
		}
		checking = true;
		SetBusyState();
		BackgroundWorker backgroundWorker = new BackgroundWorker();
		backgroundWorker.DoWork += delegate(object sender, DoWorkEventArgs e)
		{
			e.Result = DependencySetup.CheckStatus(probeInstallers: true);
		};
		backgroundWorker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
		{
			checking = false;
			if (e.Error != null)
			{
				SetSummary("Check failed", AppWindow.ErrorColor);
				EnableButtons();
				return;
			}
			UpdateView((DependencySetup.DependencySnapshot)e.Result, "Ready");
		};
		backgroundWorker.RunWorkerAsync();
	}

	private void SetBusyState()
	{
		refreshButton.Enabled = false;
		installAllButton.Enabled = false;
		uvButton.Enabled = false;
		gitButton.Enabled = false;
		SetSummary("Checking", AppWindow.AccentColor);
		SetStatus(uvStatusLabel, uvStatusVersionLabel, snapshot != null && snapshot.UvInstalled, snapshot != null ? snapshot.UvInstalledDetail : null);
		SetStatus(gitStatusLabel, gitStatusVersionLabel, snapshot != null && snapshot.GitInstalled, snapshot != null ? snapshot.GitInstalledDetail : null);
		uvSourceLabel.Text = "Checking...";
		gitSourceLabel.Text = "Checking...";
	}

	private void UpdateView(DependencySetup.DependencySnapshot newSnapshot, string message)
	{
		snapshot = newSnapshot;
		SetStatus(uvStatusLabel, uvStatusVersionLabel, snapshot.UvInstalled, snapshot.UvInstalledDetail);
		SetStatus(gitStatusLabel, gitStatusVersionLabel, snapshot.GitInstalled, snapshot.GitInstalledDetail);
		SetSource(uvSourceLabel, snapshot.UvInstallerAvailable, snapshot.UvInstallerDetail);
		SetSource(gitSourceLabel, snapshot.GitInstallerAvailable, snapshot.GitInstallerDetail);
		EnableButtons();
		SetSummary(message, string.Equals(message, "Ready", StringComparison.OrdinalIgnoreCase) ? AppWindow.SuccessColor : AppWindow.AccentColor);
	}

	private static void SetStatus(Label statusLabel, Label versionLabel, bool installed, string detail)
	{
		statusLabel.Text = installed ? "Installed" : "Missing";
		statusLabel.ForeColor = installed ? AppWindow.SuccessColor : AppWindow.ErrorColor;
		versionLabel.Text = installed && !string.IsNullOrWhiteSpace(detail) ? ShortVersion(detail) : string.Empty;
	}

	private void SetSummary(string text, Color color)
	{
		summaryLabel.Text = text;
		summaryLabel.ForeColor = color;
	}

	private static string ShortVersion(string detail)
	{
		string text = detail.Trim();
		int index = text.IndexOf(" (", StringComparison.Ordinal);
		if (index > 0)
		{
			return text.Substring(0, index);
		}
		return text;
	}

	private static void SetSource(Label label, bool available, string detail)
	{
		if (available)
		{
			label.Text = string.IsNullOrWhiteSpace(detail) ? "Available" : detail;
			label.ForeColor = AppWindow.SuccessColor;
		}
		else
		{
			label.Text = "Unavailable";
			label.ForeColor = AppWindow.ErrorColor;
		}
	}

	private void EnableButtons()
	{
		refreshButton.Enabled = !checking;
		uvButton.Enabled = !checking && snapshot != null && snapshot.UvInstallerAvailable;
		gitButton.Enabled = !checking && snapshot != null && snapshot.GitInstallerAvailable;
		installAllButton.Enabled = !checking && snapshot != null && (snapshot.HasMissing ? snapshot.CanInstallMissing : snapshot.CanInstallAny);
		uvButton.Text = snapshot != null && snapshot.UvInstalled ? "Reinstall" : "Install";
		gitButton.Text = snapshot != null && snapshot.GitInstalled ? "Reinstall" : "Install";
		installAllButton.Text = snapshot != null && snapshot.HasMissing ? "Install Missing" : "Reinstall All";
	}
}
