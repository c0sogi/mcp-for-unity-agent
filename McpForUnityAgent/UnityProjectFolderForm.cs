using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace McpForUnityAgent;

internal sealed class UnityProjectFolderForm : Form
{
	private readonly TextBox pathBox;

	public string ProjectPath => (pathBox.Text ?? string.Empty).Trim().Trim('"');

	public UnityProjectFolderForm(string initialPath)
	{
		Text = "Install Unity Plugin";
		AppWindow.ApplyIcon(this);
		base.Width = 720;
		base.Height = 190;
		MinimumSize = new Size(560, 170);
		base.StartPosition = FormStartPosition.CenterScreen;
		ShowIcon = true;
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(12),
			RowCount = 4,
			ColumnCount = 1,
			RowStyles =
			{
				new RowStyle(SizeType.Absolute, 28f),
				new RowStyle(SizeType.Absolute, 34f),
				new RowStyle(SizeType.Percent, 100f),
				new RowStyle(SizeType.Absolute, 42f)
			}
		};
		base.Controls.Add(tableLayoutPanel);
		tableLayoutPanel.Controls.Add(new Label
		{
			Text = "Unity project folder",
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft
		}, 0, 0);
		TableLayoutPanel pathRow = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			RowCount = 1,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.Absolute, 110f)
			}
		};
		tableLayoutPanel.Controls.Add(pathRow, 0, 1);
		pathBox = new TextBox
		{
			Dock = DockStyle.Fill,
			Text = initialPath ?? string.Empty,
			AutoCompleteMode = AutoCompleteMode.SuggestAppend,
			AutoCompleteSource = AutoCompleteSource.FileSystemDirectories
		};
		pathRow.Controls.Add(pathBox, 0, 0);
		Button browseButton = new Button
		{
			Text = "Browse...",
			Dock = DockStyle.Fill
		};
		browseButton.Click += delegate
		{
			BrowseForFolder();
		};
		pathRow.Controls.Add(browseButton, 1, 0);
		tableLayoutPanel.Controls.Add(new Label
		{
			Text = "Paste the Unity project root path, such as B:\\Projects\\MyUnityProject.",
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft
		}, 0, 2);
		FlowLayoutPanel buttons = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.RightToLeft
		};
		tableLayoutPanel.Controls.Add(buttons, 0, 3);
		Button installButton = new Button
		{
			Text = "Install",
			Width = 100
		};
		installButton.Click += delegate
		{
			if (string.IsNullOrWhiteSpace(ProjectPath))
			{
				MessageBox.Show(this, "Project folder is empty.", AppPaths.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}
			base.DialogResult = DialogResult.OK;
			Close();
		};
		buttons.Controls.Add(installButton);
		Button cancelButton = new Button
		{
			Text = "Cancel",
			Width = 100
		};
		cancelButton.Click += delegate
		{
			base.DialogResult = DialogResult.Cancel;
			Close();
		};
		buttons.Controls.Add(cancelButton);
		AcceptButton = installButton;
		CancelButton = cancelButton;
	}

	protected override void OnShown(EventArgs e)
	{
		base.OnShown(e);
		pathBox.Focus();
		pathBox.SelectAll();
	}

	private void BrowseForFolder()
	{
		using FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
		folderBrowserDialog.Description = "Select a Unity project folder";
		folderBrowserDialog.ShowNewFolderButton = false;
		if (Directory.Exists(ProjectPath))
		{
			folderBrowserDialog.SelectedPath = ProjectPath;
		}
		if (folderBrowserDialog.ShowDialog(this) == DialogResult.OK)
		{
			pathBox.Text = folderBrowserDialog.SelectedPath;
		}
	}
}
