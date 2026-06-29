using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace McpForUnityAgent;

internal sealed class ClientConfigurationControl : UserControl
{
	private readonly ListView clientList;

	private readonly TextBox snippetBox;

	private readonly Label statusLabel;

	public ClientConfigurationControl()
	{
		Dock = DockStyle.Fill;
		TableLayoutPanel root = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 4,
			RowStyles =
			{
				new RowStyle(SizeType.Absolute, 42f),
				new RowStyle(SizeType.Percent, 60f),
				new RowStyle(SizeType.Percent, 40f),
				new RowStyle(SizeType.Absolute, 28f)
			}
		};
		Controls.Add(root);
		FlowLayoutPanel buttons = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false
		};
		root.Controls.Add(buttons, 0, 0);
		Button configureAllButton = new Button
		{
			Text = "Configure All Detected Clients",
			Width = 210
		};
		configureAllButton.Click += delegate
		{
			ConfigureAll();
		};
		buttons.Controls.Add(configureAllButton);
		Button configureSelectedButton = new Button
		{
			Text = "Configure Selected",
			Width = 145
		};
		configureSelectedButton.Click += delegate
		{
			ConfigureSelected();
		};
		buttons.Controls.Add(configureSelectedButton);
		Button openButton = new Button
		{
			Text = "Open Config",
			Width = 110
		};
		openButton.Click += delegate
		{
			OpenSelectedConfig();
		};
		buttons.Controls.Add(openButton);
		Button refreshButton = new Button
		{
			Text = "Refresh",
			Width = 90
		};
		refreshButton.Click += delegate
		{
			RefreshClients();
		};
		buttons.Controls.Add(refreshButton);
		clientList = new ListView
		{
			Dock = DockStyle.Fill,
			View = View.Details,
			FullRowSelect = true,
			MultiSelect = false,
			GridLines = true
		};
		clientList.Columns.Add("Client", 180);
		clientList.Columns.Add("Status", 150);
		clientList.Columns.Add("Config Path", 420);
		clientList.SelectedIndexChanged += delegate
		{
			UpdateSnippet();
		};
		clientList.DoubleClick += delegate
		{
			ConfigureSelected();
		};
		root.Controls.Add(clientList, 0, 1);
		snippetBox = new TextBox
		{
			Dock = DockStyle.Fill,
			Multiline = true,
			ReadOnly = true,
			ScrollBars = ScrollBars.Both,
			WordWrap = false
		};
		root.Controls.Add(snippetBox, 0, 2);
		statusLabel = new Label
		{
			Dock = DockStyle.Fill,
			TextAlign = System.Drawing.ContentAlignment.MiddleLeft
		};
		root.Controls.Add(statusLabel, 0, 3);
		Load += delegate
		{
			RefreshClients();
		};
	}

	private void RefreshClients()
	{
		clientList.Items.Clear();
		List<ClientConfiguration.ClientConfigResult> clients = ClientConfiguration.GetClients();
		foreach (ClientConfiguration.ClientConfigResult client in clients)
		{
			ListViewItem item = new ListViewItem(client.Name);
			item.SubItems.Add(client.Status ?? "");
			item.SubItems.Add(client.ConfigPath ?? "");
			item.Tag = client;
			if (string.Equals(client.Status, "Configured", StringComparison.OrdinalIgnoreCase))
			{
				item.ForeColor = AppWindow.SuccessColor;
			}
			else if (!client.SupportsHttp || (client.Status ?? "").StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
			{
				item.ForeColor = AppWindow.ErrorColor;
			}
			clientList.Items.Add(item);
		}
		if (clientList.Items.Count > 0 && clientList.SelectedItems.Count == 0)
		{
			clientList.Items[0].Selected = true;
		}
		statusLabel.Text = "Detected clients are clients whose config directory exists. Unsupported clients are skipped by bulk configure.";
		UpdateSnippet();
	}

	private void ConfigureAll()
	{
		ClientConfiguration.ClientConfigSummary summary = ClientConfiguration.ConfigureAllDetected();
		StringBuilder message = new StringBuilder();
		message.AppendLine(summary.Success + " configured, " + summary.Failed + " failed, " + summary.Skipped + " skipped.");
		message.AppendLine();
		foreach (string line in summary.Messages)
		{
			message.AppendLine(line);
		}
		MessageBox.Show(this, message.ToString(), "Configure Detected Clients", MessageBoxButtons.OK, summary.Failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
		RefreshClients();
	}

	private void ConfigureSelected()
	{
		ClientConfiguration.ClientConfigResult selected = GetSelectedClient();
		if (selected == null)
		{
			MessageBox.Show(this, "Select a client first.", AppPaths.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}
		try
		{
			ClientConfiguration.ConfigureByName(selected.Name);
			RefreshClients();
			statusLabel.Text = selected.Name + " configured.";
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, ex.Message, AppPaths.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private void OpenSelectedConfig()
	{
		ClientConfiguration.ClientConfigResult selected = GetSelectedClient();
		if (selected == null || string.IsNullOrWhiteSpace(selected.ConfigPath))
		{
			return;
		}
		string path = selected.ConfigPath;
		string target = File.Exists(path) ? path : Path.GetDirectoryName(path);
		if (string.IsNullOrWhiteSpace(target) || !Directory.Exists(target) && !File.Exists(target))
		{
			MessageBox.Show(this, "Config path does not exist yet.", AppPaths.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}
		Process.Start("explorer.exe", "\"" + target + "\"");
	}

	private void UpdateSnippet()
	{
		ClientConfiguration.ClientConfigResult selected = GetSelectedClient();
		if (selected == null)
		{
			snippetBox.Text = "";
			return;
		}
		try
		{
			snippetBox.Text = ClientConfiguration.GetManualSnippet(selected.Name);
		}
		catch (Exception ex)
		{
			snippetBox.Text = ex.Message;
		}
	}

	private ClientConfiguration.ClientConfigResult GetSelectedClient()
	{
		if (clientList.SelectedItems.Count == 0)
		{
			return null;
		}
		return clientList.SelectedItems[0].Tag as ClientConfiguration.ClientConfigResult;
	}
}
