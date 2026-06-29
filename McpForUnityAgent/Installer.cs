using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Reflection;
using System.Windows.Forms;

namespace McpForUnityAgent;

internal static class Installer
{
	public static void Install(AppConfig config, bool startNow)
	{
		AppPaths.EnsureDirs();
		StopInstalledTray();
		StopLegacyPowerShellTray();
		RemoveLegacyShortcuts();
		RemoveLegacyInstalledFiles();
		string executablePath = Application.ExecutablePath;
		if (!string.Equals(Path.GetFullPath(executablePath), Path.GetFullPath(AppPaths.InstalledExePath), StringComparison.OrdinalIgnoreCase))
		{
			File.Copy(executablePath, AppPaths.InstalledExePath, overwrite: true);
		}
		config.Save();
		WriteVersionFile();
		CopySourceFolderIfPresent(Path.Combine(Path.GetDirectoryName(executablePath) ?? string.Empty, "src"), AppPaths.SourceDir);
		CreateShortcut(AppPaths.StartupShortcutPath, AppPaths.InstalledExePath, "", AppPaths.InstallDir, "Starts McpForUnity Agent at login.");
		CreateShortcut(AppPaths.DesktopShortcutPath, AppPaths.InstalledExePath, "", AppPaths.InstallDir, "Opens McpForUnity Agent.");
		if (startNow)
		{
			ProcessStartInfo processStartInfo = new ProcessStartInfo();
			processStartInfo.FileName = AppPaths.InstalledExePath;
			processStartInfo.WorkingDirectory = AppPaths.InstallDir;
			processStartInfo.UseShellExecute = true;
			Process.Start(processStartInfo);
		}
	}

	public static void Uninstall(bool removeFiles)
	{
		StopInstalledTray();
		StopLegacyPowerShellTray();
		DeleteFile(AppPaths.StartupShortcutPath);
		DeleteFile(AppPaths.DesktopShortcutPath);
		RemoveLegacyShortcuts();
		if (removeFiles && Directory.Exists(AppPaths.InstallDir))
		{
			Directory.Delete(AppPaths.InstallDir, recursive: true);
		}
	}

	private static void RemoveLegacyShortcuts()
	{
		string[] legacyShortcutPaths = AppPaths.LegacyShortcutPaths;
		foreach (string path in legacyShortcutPaths)
		{
			DeleteFile(path);
		}
	}

	private static void RemoveLegacyInstalledFiles()
	{
		DeleteFile(Path.Combine(AppPaths.InstallDir, "tray.ps1"));
		DeleteFile(Path.Combine(AppPaths.InstallDir, "open-live-console.ps1"));
		DeleteFile(Path.Combine(AppPaths.InstallDir, "mcpforunity-tray.ico"));
		DeleteFile(Path.Combine(AppPaths.LegacyInstallDir, "McpForUnityTray.exe"));
		DeleteFile(Path.Combine(AppPaths.LegacyInstallDir, "config.ini"));
		DeleteFile(Path.Combine(AppPaths.LegacyInstallDir, "tray.ps1"));
		DeleteFile(Path.Combine(AppPaths.LegacyInstallDir, "open-live-console.ps1"));
		DeleteFile(Path.Combine(AppPaths.LegacyInstallDir, "mcpforunity-tray.ico"));
	}

	private static void WriteVersionFile()
	{
		File.WriteAllText(AppPaths.VersionPath, AppVersion.DisplayName + Environment.NewLine + "Built from local source package." + Environment.NewLine, System.Text.Encoding.UTF8);
	}

	private static void CopySourceFolderIfPresent(string sourceDir, string targetDir)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
			{
				return;
			}
			if (string.Equals(Path.GetFullPath(sourceDir), Path.GetFullPath(targetDir), StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
			if (Directory.Exists(targetDir))
			{
				Directory.Delete(targetDir, recursive: true);
			}
			CopyDirectory(sourceDir, targetDir);
		}
		catch (Exception ex)
		{
			AppPaths.WriteTrayError("Source copy failed: " + ex);
		}
	}

	private static void CopyDirectory(string sourceDir, string targetDir)
	{
		Directory.CreateDirectory(targetDir);
		foreach (string file in Directory.GetFiles(sourceDir))
		{
			File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), overwrite: true);
		}
		foreach (string directory in Directory.GetDirectories(sourceDir))
		{
			CopyDirectory(directory, Path.Combine(targetDir, Path.GetFileName(directory)));
		}
	}

	private static void DeleteFile(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}

	private static void CreateShortcut(string shortcutPath, string targetPath, string arguments, string workingDirectory, string description)
	{
		Type typeFromProgID = Type.GetTypeFromProgID("WScript.Shell");
		object target = Activator.CreateInstance(typeFromProgID);
		object obj = typeFromProgID.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, target, new object[1] { shortcutPath });
		Type type = obj.GetType();
		type.InvokeMember("TargetPath", BindingFlags.SetProperty, null, obj, new object[1] { targetPath });
		type.InvokeMember("Arguments", BindingFlags.SetProperty, null, obj, new object[1] { arguments });
		type.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, obj, new object[1] { workingDirectory });
		type.InvokeMember("Description", BindingFlags.SetProperty, null, obj, new object[1] { description });
		type.InvokeMember("IconLocation", BindingFlags.SetProperty, null, obj, new object[1] { targetPath + ",0" });
		type.InvokeMember("Save", BindingFlags.InvokeMethod, null, obj, null);
	}

	public static void StopInstalledTray()
	{
		string fullPath = Path.GetFullPath(AppPaths.InstalledExePath);
		string fullPath2 = Path.GetFullPath(Path.Combine(AppPaths.LegacyInstallDir, "McpForUnityTray.exe"));
		int id = Process.GetCurrentProcess().Id;
		List<int> list = new List<int>();
		try
		{
			using ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher("SELECT ProcessId, ExecutablePath FROM Win32_Process WHERE Name = 'McpForUnityAgent.exe' OR Name = 'McpForUnityTray.exe'");
			foreach (ManagementObject item in managementObjectSearcher.Get())
			{
				object obj = item["ProcessId"];
				object obj2 = item["ExecutablePath"];
				if (obj != null && obj2 != null)
				{
					int num = Convert.ToInt32(obj);
					string path = Convert.ToString(obj2);
					string fullPath3 = Path.GetFullPath(path);
					if (num != id && (string.Equals(fullPath3, fullPath, StringComparison.OrdinalIgnoreCase) || string.Equals(fullPath3, fullPath2, StringComparison.OrdinalIgnoreCase)))
					{
						list.Add(num);
					}
				}
			}
		}
		catch
		{
		}
		foreach (int item2 in list)
		{
			KillProcessTree(item2);
		}
	}

	public static void StopLegacyPowerShellTray()
	{
		int id = Process.GetCurrentProcess().Id;
		List<int> list = new List<int>();
		try
		{
			using ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'powershell.exe' OR Name = 'pwsh.exe'");
			foreach (ManagementObject item in managementObjectSearcher.Get())
			{
				object obj = item["ProcessId"];
				object obj2 = item["CommandLine"];
				if (obj != null && obj2 != null)
				{
					int num = Convert.ToInt32(obj);
					string text = Convert.ToString(obj2);
					if (num != id && text.IndexOf("McpForUnityTray", StringComparison.OrdinalIgnoreCase) >= 0 && text.IndexOf("tray.ps1", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						list.Add(num);
					}
				}
			}
		}
		catch
		{
		}
		foreach (int item2 in list)
		{
			KillProcessTree(item2);
		}
	}

	public static void KillProcessTree(int pid)
	{
		try
		{
			ProcessStartInfo processStartInfo = new ProcessStartInfo();
			processStartInfo.FileName = "taskkill.exe";
			processStartInfo.Arguments = "/PID " + pid + " /T /F";
			processStartInfo.CreateNoWindow = true;
			processStartInfo.UseShellExecute = false;
			Process.Start(processStartInfo)?.WaitForExit(5000);
		}
		catch
		{
		}
	}
}

