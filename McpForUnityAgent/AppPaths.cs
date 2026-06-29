using System;
using System.IO;
using System.Text;

namespace McpForUnityAgent;

internal static class AppPaths
{
	public const string AppName = "McpForUnity Agent";

	public const string ExeName = "McpForUnityAgent.exe";

	public const string DefaultArgs = "--from \"mcpforunityserver==9.7.3\" mcp-for-unity --transport http --http-url http://127.0.0.1:8080 --project-scoped-tools";

	public static readonly string InstallDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "McpForUnityAgent");

	public static readonly string InstalledExePath = Path.Combine(InstallDir, "McpForUnityAgent.exe");

	public static readonly string ConfigPath = Path.Combine(InstallDir, "config.ini");

	public static readonly string VersionPath = Path.Combine(InstallDir, "VERSION.txt");

	public static readonly string SourceDir = Path.Combine(InstallDir, "src");

	public static readonly string LogsDir = Path.Combine(InstallDir, "logs");

	public static readonly string StartupShortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "McpForUnity Agent.lnk");

	public static readonly string DesktopShortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "McpForUnity Agent.lnk");

	public static readonly string LegacyInstallDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "McpForUnityTray");

	public static readonly string LegacyConfigPath = Path.Combine(LegacyInstallDir, "config.ini");

	public static string[] LegacyShortcutPaths
	{
		get
		{
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
			return new string[4]
			{
				Path.Combine(folderPath, "MCP for Unity Tray.lnk"),
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "MCP for Unity Tray.lnk"),
				Path.Combine(folderPath, "MCP for Unity Logs.lnk"),
				Path.Combine(folderPath, "MCP for Unity Live Console.lnk")
			};
		}
	}

	public static void EnsureDirs()
	{
		Directory.CreateDirectory(InstallDir);
		Directory.CreateDirectory(LogsDir);
	}

	public static string FindDefaultUvxPath()
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		string text = Path.Combine(folderPath, ".local\\bin\\uvx.exe");
		if (File.Exists(text))
		{
			return text;
		}
		string text2 = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
		string[] array = text2.Split(Path.PathSeparator);
		string[] array2 = array;
		foreach (string text3 in array2)
		{
			try
			{
				string text4 = Path.Combine(text3.Trim(), "uvx.exe");
				if (File.Exists(text4))
				{
					return text4;
				}
			}
			catch
			{
			}
		}
		return text;
	}

	public static string NewServerLogPath()
	{
		EnsureDirs();
		return Path.Combine(LogsDir, "mcp-for-unity-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
	}

	public static void WriteTrayError(string text)
	{
		try
		{
			EnsureDirs();
			string path = Path.Combine(LogsDir, "tray-error.log");
			File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [TRAY] " + text + Environment.NewLine, Encoding.UTF8);
		}
		catch
		{
		}
	}
}

