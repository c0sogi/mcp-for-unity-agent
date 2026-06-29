using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace McpForUnityAgent;

internal sealed class ConsoleForm : Form
{
	private const int MaxVisibleChars = 524288;

	private const int TrimmedVisibleChars = 393216;

	private static readonly Regex LogLineRegex = new Regex("^(\\d{4}-\\d{2}-\\d{2} \\d{2}:\\d{2}:\\d{2}) \\[(OUT|ERR|INFO)\\] ?(.*)$", RegexOptions.Compiled);

	private readonly WebBrowser output;

	private readonly StringBuilder rawText = new StringBuilder();

	private string loadedPath;

	private bool documentReady;

	private string pendingHtml = string.Empty;

	public ConsoleForm()
	{
		Text = "MCP for Unity Console";
		AppWindow.ApplyIcon(this);
		base.Width = 920;
		base.Height = 560;
		base.StartPosition = FormStartPosition.CenterScreen;
		ConfigureBrowserEmulation();
		output = new WebBrowser();
		output.Dock = DockStyle.Fill;
		output.AllowWebBrowserDrop = false;
		output.IsWebBrowserContextMenuEnabled = false;
		output.ScriptErrorsSuppressed = true;
		output.WebBrowserShortcutsEnabled = true;
		output.DocumentCompleted += Output_DocumentCompleted;
		output.ContextMenuStrip = BuildContextMenu();
		base.Controls.Add(output);
	}

	public void LoadLog(string path)
	{
		if (string.IsNullOrEmpty(path) || string.Equals(path, loadedPath, StringComparison.OrdinalIgnoreCase))
		{
			if (rawText.Length == 0)
			{
				SetConsoleText("No output yet. Start or restart mcp-for-unity from the tray menu." + Environment.NewLine);
			}
			ScrollToBottom();
			return;
		}
		loadedPath = path;
		try
		{
			if (File.Exists(path))
			{
				SetConsoleText(OutputText.Clean(TailText(path, 262144)));
			}
			else
			{
				SetConsoleText("No output yet. Start or restart mcp-for-unity from the tray menu." + Environment.NewLine);
			}
		}
		catch (Exception ex)
		{
			SetConsoleText(ex.Message + Environment.NewLine);
		}
	}

	public void Append(string text)
	{
		if (base.IsDisposed)
		{
			return;
		}
		if (base.InvokeRequired)
		{
			BeginInvoke(new Action<string>(Append), text);
			return;
		}
		string cleanText = OutputText.Clean(text);
		if (string.IsNullOrEmpty(cleanText))
		{
			return;
		}
		rawText.Append(cleanText);
		if (rawText.Length > MaxVisibleChars)
		{
			rawText.Remove(0, rawText.Length - TrimmedVisibleChars);
			RenderDocument(rawText.ToString());
			return;
		}
		AppendHtml(BuildLinesHtml(cleanText));
	}

	protected override void OnShown(EventArgs e)
	{
		base.OnShown(e);
		BeginInvoke(new Action(delegate
		{
			ScrollToBottom();
			output.Focus();
		}));
	}

	protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
	{
		if (keyData == (Keys.Control | Keys.A))
		{
			ExecCommand("SelectAll");
			return true;
		}
		if (keyData == (Keys.Control | Keys.C) || keyData == (Keys.Control | Keys.Insert))
		{
			ExecCommand("Copy");
			return true;
		}
		return base.ProcessCmdKey(ref msg, keyData);
	}

	private ContextMenuStrip BuildContextMenu()
	{
		ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
		ToolStripMenuItem copyItem = new ToolStripMenuItem("Copy", null, delegate
		{
			ExecCommand("Copy");
		});
		ToolStripMenuItem selectAllItem = new ToolStripMenuItem("Select All", null, delegate
		{
			ExecCommand("SelectAll");
		});
		contextMenuStrip.Items.Add(copyItem);
		contextMenuStrip.Items.Add(selectAllItem);
		contextMenuStrip.Opening += delegate(object sender, CancelEventArgs e)
		{
			copyItem.Enabled = output.Document != null;
			selectAllItem.Enabled = rawText.Length > 0;
		};
		AppWindow.ApplyTheme(contextMenuStrip);
		return contextMenuStrip;
	}

	private void Output_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
	{
		documentReady = true;
		if (!string.IsNullOrEmpty(pendingHtml))
		{
			string html = pendingHtml;
			pendingHtml = string.Empty;
			AppendHtml(html);
		}
		ScrollToBottom();
	}

	private void SetConsoleText(string text)
	{
		rawText.Length = 0;
		rawText.Append(text ?? string.Empty);
		RenderDocument(rawText.ToString());
	}

	private void RenderDocument(string text)
	{
		documentReady = false;
		pendingHtml = string.Empty;
		output.DocumentText = BuildDocumentHtml(BuildLinesHtml(text));
	}

	private void AppendHtml(string html)
	{
		if (string.IsNullOrEmpty(html))
		{
			return;
		}
		if (!documentReady || output.Document == null)
		{
			pendingHtml += html;
			return;
		}
		try
		{
			output.Document.InvokeScript("appendLog", new object[1] { html });
		}
		catch
		{
			pendingHtml += html;
		}
	}

	private static string BuildDocumentHtml(string bodyHtml)
	{
		return "<!doctype html><html><head><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" />" +
			"<meta charset=\"utf-8\" />" +
			"<style>" +
			"html,body{margin:0;padding:0;background:#121212;color:#e6e6e6;}" +
			"body{font:13px/1.38 'JetBrains Mono','D2Coding','Cascadia Mono','Cascadia Code','Consolas','Courier New','Segoe UI Emoji','Segoe UI Symbol',monospace;}" +
			"#log{box-sizing:border-box;min-height:100vh;padding:6px 10px 12px 0;}" +
			".line{display:flex;align-items:flex-start;white-space:pre-wrap;word-break:break-word;word-wrap:break-word;}" +
			".time{flex:0 0 154px;color:#6ebeff;padding-right:8px;box-sizing:border-box;user-select:text;}" +
			".msg{flex:1;min-width:0;color:#e6e6e6;white-space:pre-wrap;word-break:break-word;}" +
			".info{color:#cfcfcf;}" +
			"</style>" +
			"<script>function appendLog(h){var e=document.getElementById('log');e.insertAdjacentHTML('beforeend',h);window.scrollTo(0,document.body.scrollHeight);}</script>" +
			"</head><body><div id=\"log\">" + bodyHtml + "</div></body></html>";
	}

	private static void ConfigureBrowserEmulation()
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Internet Explorer\\Main\\FeatureControl\\FEATURE_BROWSER_EMULATION");
			if (registryKey != null)
			{
				registryKey.SetValue(AppPaths.ExeName, 11001, RegistryValueKind.DWord);
			}
		}
		catch
		{
		}
	}

	private static string BuildLinesHtml(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder();
		string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
		string[] lines = normalized.Split('\n');
		for (int i = 0; i < lines.Length; i++)
		{
			AppendLineHtml(stringBuilder, lines[i]);
		}
		return stringBuilder.ToString();
	}

	private static void AppendLineHtml(StringBuilder stringBuilder, string line)
	{
		Match match = LogLineRegex.Match(line);
		if (!match.Success)
		{
			if (line.Length == 0)
			{
				return;
			}
			stringBuilder.Append("<div class=\"line\"><span class=\"time\"></span><span class=\"msg\">");
			stringBuilder.Append(HtmlEncode(line));
			stringBuilder.Append("</span></div>");
			return;
		}
		string message = match.Groups[3].Value;
		if (message.Length == 0 && !string.Equals(match.Groups[2].Value, "INFO", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		stringBuilder.Append("<div class=\"line\"><span class=\"time\">");
		stringBuilder.Append(HtmlEncode(match.Groups[1].Value));
		stringBuilder.Append("</span><span class=\"msg\">");
		if (string.Equals(match.Groups[2].Value, "INFO", StringComparison.OrdinalIgnoreCase))
		{
			stringBuilder.Append("<span class=\"info\">[INFO] </span>");
		}
		stringBuilder.Append(HtmlEncode(message));
		stringBuilder.Append("</span></div>");
	}

	private static string HtmlEncode(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder(text.Length + 16);
		foreach (char c in text)
		{
			switch (c)
			{
			case '&':
				stringBuilder.Append("&amp;");
				break;
			case '<':
				stringBuilder.Append("&lt;");
				break;
			case '>':
				stringBuilder.Append("&gt;");
				break;
			case '"':
				stringBuilder.Append("&quot;");
				break;
			default:
				stringBuilder.Append(c);
				break;
			}
		}
		return stringBuilder.ToString();
	}

	private void ExecCommand(string command)
	{
		try
		{
			if (output.Document != null)
			{
				output.Document.ExecCommand(command, false, null);
			}
		}
		catch
		{
		}
	}

	private void ScrollToBottom()
	{
		try
		{
			if (output.Document != null)
			{
				output.Document.Window.ScrollTo(0, output.Document.Body.ScrollRectangle.Height);
			}
		}
		catch
		{
		}
	}

	private static string TailText(string path, int maxBytes)
	{
		FileInfo fileInfo = new FileInfo(path);
		using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		long offset = Math.Max(0L, fileInfo.Length - maxBytes);
		fileStream.Seek(offset, SeekOrigin.Begin);
		using StreamReader streamReader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
		return streamReader.ReadToEnd();
	}
}

