using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace McpForUnityAgent;

internal sealed class PortForm : Form
{
	private readonly TextBox portBox;

	private readonly ListView listView;

	private readonly Label statusLabel;

	public PortForm(int suggestedPort)
	{
		Text = "MCP for Unity Ports";
		AppWindow.ApplyIcon(this);
		base.Width = 760;
		base.Height = 430;
		MinimumSize = new Size(660, 360);
		base.StartPosition = FormStartPosition.CenterScreen;
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(12),
			RowCount = 3,
			ColumnCount = 1,
			RowStyles = 
			{
				new RowStyle(SizeType.Absolute, 36f),
				new RowStyle(SizeType.Percent, 100f),
				new RowStyle(SizeType.Absolute, 28f)
			}
		};
		base.Controls.Add(tableLayoutPanel);
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false
		};
		tableLayoutPanel.Controls.Add(flowLayoutPanel, 0, 0);
		Label value = new Label
		{
			Text = "Port",
			Width = 38,
			TextAlign = ContentAlignment.MiddleLeft
		};
		flowLayoutPanel.Controls.Add(value);
		portBox = new TextBox();
		portBox.Width = 90;
		portBox.Text = suggestedPort.ToString();
		flowLayoutPanel.Controls.Add(portBox);
		Button button = new Button
		{
			Text = "Refresh",
			Width = 90
		};
		EventHandler value2 = delegate
		{
			RefreshPorts();
		};
		button.Click += value2;
		flowLayoutPanel.Controls.Add(button);
		Button button2 = new Button();
		button2.Text = "Kill Selected";
		button2.Width = 110;
		button2.Click += delegate
		{
			KillSelected();
		};
		flowLayoutPanel.Controls.Add(button2);
		listView = new ListView();
		listView.Dock = DockStyle.Fill;
		listView.View = View.Details;
		listView.FullRowSelect = true;
		listView.MultiSelect = true;
		listView.GridLines = true;
		listView.Columns.Add("Proto", 70);
		listView.Columns.Add("Local Address", 230);
		listView.Columns.Add("State", 110);
		listView.Columns.Add("PID", 80);
		listView.Columns.Add("Process", 180);
		listView.DoubleClick += delegate
		{
			KillSelected();
		};
		tableLayoutPanel.Controls.Add(listView, 0, 1);
		statusLabel = new Label();
		statusLabel.Dock = DockStyle.Fill;
		statusLabel.TextAlign = ContentAlignment.MiddleLeft;
		tableLayoutPanel.Controls.Add(statusLabel, 0, 2);
		base.Shown += delegate
		{
			RefreshPorts();
		};
	}

	private bool TryGetPort(out int port)
	{
		if (!int.TryParse(portBox.Text.Trim(), out port) || port < 1 || port > 65535)
		{
			MessageBox.Show("Enter a port between 1 and 65535.", "McpForUnity Agent", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			portBox.Focus();
			return false;
		}
		return true;
	}

	private void RefreshPorts()
	{
		if (!TryGetPort(out var port))
		{
			return;
		}
		listView.Items.Clear();
		try
		{
			List<PortProcessInfo> list = PortTools.FindByPort(port);
			foreach (PortProcessInfo item in list)
			{
				ListViewItem listViewItem = new ListViewItem(item.Protocol);
				listViewItem.SubItems.Add(item.LocalAddress);
				listViewItem.SubItems.Add(item.State);
				listViewItem.SubItems.Add(item.ProcessId.ToString());
				listViewItem.SubItems.Add(item.ProcessName);
				listViewItem.Tag = item;
				listView.Items.Add(listViewItem);
			}
			statusLabel.Text = ((list.Count == 0) ? ("No process is using port " + port + ".") : (list.Count + " socket(s) found on port " + port + "."));
		}
		catch (Exception ex)
		{
			statusLabel.Text = "Failed to query port.";
			MessageBox.Show(ex.Message, "McpForUnity Agent", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private void KillSelected()
	{
		if (listView.SelectedItems.Count == 0)
		{
			MessageBox.Show("Select one or more rows first.", "McpForUnity Agent", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return;
		}
		Dictionary<int, string> dictionary = new Dictionary<int, string>();
		foreach (ListViewItem selectedItem in listView.SelectedItems)
		{
			if (selectedItem.Tag is PortProcessInfo portProcessInfo && !dictionary.ContainsKey(portProcessInfo.ProcessId))
			{
				dictionary.Add(portProcessInfo.ProcessId, portProcessInfo.ProcessName);
			}
		}
		StringBuilder stringBuilder = new StringBuilder();
		foreach (KeyValuePair<int, string> item in dictionary)
		{
			stringBuilder.AppendLine(item.Key + "  " + item.Value);
		}
		DialogResult dialogResult = MessageBox.Show("Kill these process tree(s)?\r\n\r\n" + stringBuilder.ToString(), "McpForUnity Agent", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
		if (dialogResult != DialogResult.Yes)
		{
			return;
		}
		foreach (int key in dictionary.Keys)
		{
			Installer.KillProcessTree(key);
		}
		Thread.Sleep(500);
		RefreshPorts();
	}
}

