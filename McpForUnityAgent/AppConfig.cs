using System;
using System.IO;
using System.Text;

namespace McpForUnityAgent;

internal sealed class AppConfig
{
	public string CommandPath;

	public string CommandArgs;

	public bool AutoRestart;

	public int RestartDelaySeconds;

	public bool KillPortOnAutoRestart;

	public int AutoRestartKillPort;

	public bool AutoConnectUnitySession;

	public static AppConfig Defaults()
	{
		AppConfig appConfig = new AppConfig();
		appConfig.CommandPath = AppPaths.FindDefaultUvxPath();
		appConfig.CommandArgs = "--from \"mcpforunityserver==9.7.3\" mcp-for-unity --transport http --http-url http://127.0.0.1:8080 --project-scoped-tools";
		appConfig.AutoRestart = true;
		appConfig.RestartDelaySeconds = 5;
		appConfig.KillPortOnAutoRestart = false;
		appConfig.AutoRestartKillPort = 8080;
		appConfig.AutoConnectUnitySession = true;
		return appConfig;
	}

	public static AppConfig Load()
	{
		AppConfig appConfig = Defaults();
		string path = AppPaths.ConfigPath;
		if (!File.Exists(path) && File.Exists(AppPaths.LegacyConfigPath))
		{
			path = AppPaths.LegacyConfigPath;
		}
		if (!File.Exists(path))
		{
			return appConfig;
		}
		string[] array = File.ReadAllLines(path, Encoding.UTF8);
		foreach (string text in array)
		{
			if (string.IsNullOrWhiteSpace(text) || text.TrimStart().StartsWith("#", StringComparison.Ordinal))
			{
				continue;
			}
			int num = text.IndexOf('=');
			if (num >= 0)
			{
				string a = text.Substring(0, num).Trim();
				string text2 = text.Substring(num + 1);
				if (string.Equals(a, "CommandPath", StringComparison.OrdinalIgnoreCase))
				{
					appConfig.CommandPath = text2.Trim().Trim('"');
				}
				else if (string.Equals(a, "CommandArgs", StringComparison.OrdinalIgnoreCase))
				{
					appConfig.CommandArgs = text2;
				}
				else if (string.Equals(a, "AutoRestart", StringComparison.OrdinalIgnoreCase))
				{
					bool result;
					if (bool.TryParse(text2.Trim(), out result))
					{
						appConfig.AutoRestart = result;
					}
				}
				else if (string.Equals(a, "RestartDelaySeconds", StringComparison.OrdinalIgnoreCase))
				{
					int result2;
					if (int.TryParse(text2.Trim(), out result2) && result2 >= 1 && result2 <= 3600)
					{
						appConfig.RestartDelaySeconds = result2;
					}
				}
				else if (string.Equals(a, "KillPortOnAutoRestart", StringComparison.OrdinalIgnoreCase))
				{
					bool result3;
					if (bool.TryParse(text2.Trim(), out result3))
					{
						appConfig.KillPortOnAutoRestart = result3;
					}
				}
				else if (string.Equals(a, "AutoRestartKillPort", StringComparison.OrdinalIgnoreCase))
				{
					int result4;
					if (int.TryParse(text2.Trim(), out result4) && result4 >= 1 && result4 <= 65535)
					{
						appConfig.AutoRestartKillPort = result4;
					}
				}
				else if (string.Equals(a, "AutoConnectUnitySession", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "StartWithUnity", StringComparison.OrdinalIgnoreCase))
				{
					bool result5;
					if (bool.TryParse(text2.Trim(), out result5))
					{
						appConfig.AutoConnectUnitySession = result5;
					}
				}
			}
		}
		return appConfig;
	}

	public void Save()
	{
		AppPaths.EnsureDirs();
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("# McpForUnity Agent config");
		stringBuilder.AppendLine("CommandPath=" + (CommandPath ?? string.Empty));
		stringBuilder.AppendLine("CommandArgs=" + (CommandArgs ?? string.Empty));
		stringBuilder.AppendLine("AutoRestart=" + AutoRestart);
		stringBuilder.AppendLine("RestartDelaySeconds=" + RestartDelaySeconds);
		stringBuilder.AppendLine("KillPortOnAutoRestart=" + KillPortOnAutoRestart);
		stringBuilder.AppendLine("AutoRestartKillPort=" + AutoRestartKillPort);
		stringBuilder.AppendLine("AutoConnectUnitySession=" + AutoConnectUnitySession);
		File.WriteAllText(AppPaths.ConfigPath, stringBuilder.ToString(), Encoding.UTF8);
	}
}

