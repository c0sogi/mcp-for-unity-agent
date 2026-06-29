using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace McpForUnityAgent;

internal sealed class TrayAppContext : ApplicationContext
{
	private readonly NotifyIcon notifyIcon;

	private readonly Icon appIcon;

	private readonly Timer timer;

	private readonly ToolStripMenuItem statusItem;

	private readonly ToolStripMenuItem startItem;

	private readonly ToolStripMenuItem stopItem;

	private readonly ToolStripMenuItem restartItem;

	private Process serverProcess;

	private string logPath;

	private readonly object logLock = new object();

	private ConsoleForm consoleForm;

	private bool restartPending;

	private DateTime restartAt;

	private bool manualStopRequested;

	private Process suppressExitRestartFor;

	private bool exiting;

	private bool ServerRunning => serverProcess != null && !serverProcess.HasExited;

	public TrayAppContext()
	{
		AppPaths.EnsureDirs();
		appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
		ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
		statusItem = new ToolStripMenuItem("Status: stopped");
		statusItem.Enabled = false;
		contextMenuStrip.Items.Add(statusItem);
		contextMenuStrip.Items.Add(new ToolStripSeparator());
		startItem = new ToolStripMenuItem("Start", null, delegate
		{
			StartServer(showErrors: true);
		});
		stopItem = new ToolStripMenuItem("Stop", null, delegate
		{
			StopServer(notify: true, manual: true);
		});
		restartItem = new ToolStripMenuItem("Restart", null, delegate
		{
			RestartServer();
		});
		contextMenuStrip.Items.Add(startItem);
		contextMenuStrip.Items.Add(stopItem);
		contextMenuStrip.Items.Add(restartItem);
		contextMenuStrip.Items.Add(new ToolStripSeparator());
		contextMenuStrip.Items.Add(new ToolStripMenuItem("Console", null, delegate
		{
			ShowConsole();
		}));
		contextMenuStrip.Items.Add(new ToolStripMenuItem("Port Killer", null, delegate
		{
			ShowPorts();
		}));
		contextMenuStrip.Items.Add(new ToolStripMenuItem("Config", null, delegate
		{
			ShowConfig();
		}));
		contextMenuStrip.Items.Add(new ToolStripSeparator());
		contextMenuStrip.Items.Add(new ToolStripMenuItem("Exit", null, delegate
		{
			ExitApp();
		}));
		AppWindow.ApplyTheme(contextMenuStrip);
		notifyIcon = new NotifyIcon();
		notifyIcon.Icon = appIcon;
		notifyIcon.Text = AppVersion.DisplayName;
		notifyIcon.ContextMenuStrip = contextMenuStrip;
		notifyIcon.Visible = true;
		notifyIcon.DoubleClick += delegate
		{
			ShowConsole();
		};
		timer = new Timer();
		timer.Interval = 1000;
		timer.Tick += delegate
		{
			OnTimerTick();
		};
		timer.Start();
		RefreshMenuState();
		ConfigureUnityMcpSession(AppConfig.Load());
		StartServer(showErrors: false);
	}

	private void OnTimerTick()
	{
		AppConfig appConfig = AppConfig.Load();
		if (restartPending && !appConfig.AutoRestart)
		{
			restartPending = false;
		}
		if (restartPending && !ServerRunning && DateTime.Now >= restartAt)
		{
			restartPending = false;
			StartServer(showErrors: false);
		}
		RefreshMenuState();
	}

	private void ConfigureUnityMcpSession(AppConfig appConfig)
	{
		string message;
		bool flag = UnityMcpEditorPrefs.ConfigureForAgent(appConfig, out message);
		if (!string.IsNullOrEmpty(message))
		{
			AppendLog(flag ? "INFO" : "ERR", message);
		}
	}

	private void RefreshMenuState()
	{
		bool serverRunning = ServerRunning;
		if (serverRunning)
		{
			statusItem.Text = "Status: running";
			notifyIcon.Text = AppVersion.DisplayName + ": running";
		}
		else if (restartPending)
		{
			int num = Math.Max(1, (int)Math.Ceiling((restartAt - DateTime.Now).TotalSeconds));
			statusItem.Text = "Status: restarting in " + num + "s";
			notifyIcon.Text = AppVersion.DisplayName + ": restarting";
		}
		else
		{
			statusItem.Text = "Status: stopped";
			notifyIcon.Text = AppVersion.DisplayName + ": stopped";
		}
		startItem.Enabled = !serverRunning;
		stopItem.Enabled = serverRunning || restartPending;
		restartItem.Enabled = true;
	}

	private bool StartServer(bool showErrors)
	{
		if (ServerRunning)
		{
			RefreshMenuState();
			return true;
		}
		manualStopRequested = false;
		restartPending = false;
		AppConfig appConfig = AppConfig.Load();
		if (string.IsNullOrWhiteSpace(appConfig.CommandPath) || !File.Exists(appConfig.CommandPath))
		{
			string text = "Command executable was not found:\r\n" + (appConfig.CommandPath ?? "");
			AppendLog("ERR", text);
			if (showErrors)
			{
				MessageBox.Show(text, "McpForUnity Agent", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				ShowConfig();
			}
			ScheduleRestart("Start failed.");
			RefreshMenuState();
			return false;
		}
		CleanupStartPort(appConfig);
		logPath = AppPaths.NewServerLogPath();
		AppendLog("INFO", "Starting: " + appConfig.CommandPath + " " + appConfig.CommandArgs);
		ProcessStartInfo processStartInfo = new ProcessStartInfo();
		processStartInfo.FileName = appConfig.CommandPath;
		processStartInfo.Arguments = appConfig.CommandArgs ?? string.Empty;
		processStartInfo.WorkingDirectory = Path.GetDirectoryName(appConfig.CommandPath);
		processStartInfo.UseShellExecute = false;
		processStartInfo.CreateNoWindow = true;
		processStartInfo.RedirectStandardOutput = true;
		processStartInfo.RedirectStandardError = true;
		processStartInfo.StandardOutputEncoding = Encoding.UTF8;
		processStartInfo.StandardErrorEncoding = Encoding.UTF8;
		SetChildEnvironment(processStartInfo);
		Process process = new Process();
		process.StartInfo = processStartInfo;
		process.EnableRaisingEvents = true;
		process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
		{
			if (e.Data != null)
			{
				AppendLog("OUT", e.Data);
			}
		};
		process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
		{
			if (e.Data != null)
			{
				AppendLog("ERR", e.Data);
			}
		};
		process.Exited += delegate
		{
			try
			{
				AppendLog("INFO", "Process exited with code " + process.ExitCode);
			}
			catch
			{
				AppendLog("INFO", "Process exited.");
			}
			if (object.ReferenceEquals(serverProcess, process))
			{
				serverProcess = null;
			}
			bool flag = object.ReferenceEquals(suppressExitRestartFor, process);
			if (flag)
			{
				suppressExitRestartFor = null;
			}
			if (!flag && !manualStopRequested)
			{
				ScheduleRestart("mcp-for-unity stopped.");
			}
		};
		try
		{
			process.Start();
			serverProcess = process;
			process.BeginOutputReadLine();
			process.BeginErrorReadLine();
			AppendLog("INFO", "Started with PID " + process.Id);
			notifyIcon.ShowBalloonTip(1500, "McpForUnity Agent", "mcp-for-unity started.", ToolTipIcon.Info);
			RefreshMenuState();
			return true;
		}
		catch (Exception ex)
		{
			AppendLog("ERR", ex.ToString());
			if (showErrors)
			{
				MessageBox.Show(ex.Message, "McpForUnity Agent", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
			ScheduleRestart("Start failed.");
		}
		RefreshMenuState();
		return false;
	}

	private void StopServer(bool notify, bool manual)
	{
		if (manual)
		{
			manualStopRequested = true;
			restartPending = false;
		}
		if (!ServerRunning)
		{
			serverProcess = null;
			RefreshMenuState();
			return;
		}
		int id = serverProcess.Id;
		suppressExitRestartFor = serverProcess;
		AppendLog("INFO", "Stopping process tree for PID " + id);
		try
		{
			ProcessStartInfo processStartInfo = new ProcessStartInfo();
			processStartInfo.FileName = "taskkill.exe";
			processStartInfo.Arguments = "/PID " + id + " /T /F";
			processStartInfo.CreateNoWindow = true;
			processStartInfo.UseShellExecute = false;
			Process.Start(processStartInfo)?.WaitForExit(5000);
		}
		catch (Exception ex)
		{
			AppendLog("ERR", ex.ToString());
		}
		serverProcess = null;
		if (notify)
		{
			notifyIcon.ShowBalloonTip(1500, "McpForUnity Agent", "mcp-for-unity stopped.", ToolTipIcon.Info);
		}
		RefreshMenuState();
	}

	private void RestartServer()
	{
		manualStopRequested = false;
		restartPending = false;
		StopServer(notify: false, manual: false);
		StartServer(showErrors: true);
	}

	private void ScheduleRestart(string reason)
	{
		if (exiting || manualStopRequested)
		{
			return;
		}
		AppConfig appConfig = AppConfig.Load();
		if (!appConfig.AutoRestart)
		{
			return;
		}
		int num = appConfig.RestartDelaySeconds;
		if (num < 1)
		{
			num = 1;
		}
		restartAt = DateTime.Now.AddSeconds(num);
		restartPending = true;
		AppendLog("INFO", reason + " Restarting in " + num + " second(s).");
	}

	private void CleanupStartPort(AppConfig appConfig)
	{
		if (appConfig == null || !appConfig.KillPortOnAutoRestart)
		{
			return;
		}
		int port = appConfig.AutoRestartKillPort;
		if (port < 1 || port > 65535)
		{
			return;
		}
		List<PortProcessInfo> listeners;
		try
		{
			listeners = PortTools.FindByPort(port);
		}
		catch (Exception ex)
		{
			AppendLog("ERR", "Port cleanup before start failed for port " + port + ": " + ex.Message);
			return;
		}
		Dictionary<int, string> targets = new Dictionary<int, string>();
		int currentPid = Process.GetCurrentProcess().Id;
		foreach (PortProcessInfo listener in listeners)
		{
			if (listener.ProcessId <= 4 || listener.ProcessId == currentPid)
			{
				continue;
			}
			if (string.Equals(listener.Protocol, "TCP", StringComparison.OrdinalIgnoreCase) && !string.Equals(listener.State, "LISTENING", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			if (!targets.ContainsKey(listener.ProcessId))
			{
				targets.Add(listener.ProcessId, listener.ProcessName);
			}
		}
		if (targets.Count == 0)
		{
			AppendLog("INFO", "Port cleanup before start: no process is listening on port " + port + ".");
			return;
		}
		foreach (KeyValuePair<int, string> target in targets)
		{
			AppendLog("INFO", "Port cleanup before start: killing PID " + target.Key + " (" + target.Value + ") on port " + port + ".");
			Installer.KillProcessTree(target.Key);
		}
	}

	private void AppendLog(string level, string message)
	{
		message = OutputText.Clean(message);
		if (string.IsNullOrEmpty(logPath))
		{
			logPath = AppPaths.NewServerLogPath();
		}
		string text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + level + "] " + message + Environment.NewLine;
		lock (logLock)
		{
			try
			{
				File.AppendAllText(logPath, text, Encoding.UTF8);
			}
			catch (Exception ex)
			{
				AppPaths.WriteTrayError(ex.ToString());
			}
		}
		if (consoleForm != null && !consoleForm.IsDisposed)
		{
			consoleForm.Append(text);
		}
	}

	private void ShowConsole()
	{
		if (consoleForm == null || consoleForm.IsDisposed)
		{
			consoleForm = new ConsoleForm();
		}
		consoleForm.LoadLog(logPath);
		consoleForm.Show();
		consoleForm.WindowState = FormWindowState.Normal;
		consoleForm.Activate();
	}

	private void ShowPorts()
	{
		using PortForm portForm = new PortForm(PortTools.SuggestedPortFromConfig());
		portForm.ShowDialog();
	}

	private void ShowConfig()
	{
		AppConfig config = AppConfig.Load();
		using ConfigForm configForm = new ConfigForm(config);
		configForm.ConfigChanged += delegate(AppConfig updatedConfig)
		{
			ApplyConfig(updatedConfig);
		};
		configForm.ShowDialog();
	}

	private void ApplyConfig(AppConfig appConfig)
	{
		if (appConfig == null)
		{
			return;
		}
		appConfig.Save();
		ConfigureUnityMcpSession(appConfig);
		RefreshMenuState();
	}

	private void ExitApp()
	{
		exiting = true;
		StopServer(notify: false, manual: true);
		timer.Stop();
		notifyIcon.Visible = false;
		notifyIcon.Dispose();
		if (consoleForm != null && !consoleForm.IsDisposed)
		{
			consoleForm.Close();
		}
		appIcon.Dispose();
		ExitThread();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			exiting = true;
			StopServer(notify: false, manual: true);
			timer.Dispose();
			notifyIcon.Dispose();
			appIcon.Dispose();
		}
		base.Dispose(disposing);
	}

	private static void SetChildEnvironment(ProcessStartInfo psi)
	{
		SetEnv(psi, "PYTHONUTF8", "1");
		SetEnv(psi, "PYTHONIOENCODING", "utf-8");
		SetEnv(psi, "PYTHONUNBUFFERED", "1");
		SetEnv(psi, "NO_COLOR", "1");
		SetEnv(psi, "PY_COLORS", "0");
	}

	private static void SetEnv(ProcessStartInfo psi, string name, string value)
	{
		if (psi.EnvironmentVariables.ContainsKey(name))
		{
			psi.EnvironmentVariables[name] = value;
		}
		else
		{
			psi.EnvironmentVariables.Add(name, value);
		}
	}
}

