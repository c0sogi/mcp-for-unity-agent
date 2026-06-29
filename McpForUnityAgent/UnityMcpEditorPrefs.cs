using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.Win32;

namespace McpForUnityAgent;

internal static class UnityMcpEditorPrefs
{
	private const string UnityEditorPrefsRegistryPath = "Software\\Unity Technologies\\Unity Editor 5.x";

	public static bool ConfigureForAgent(AppConfig config, out string message)
	{
		message = string.Empty;
		if (config == null || !config.AutoConnectUnitySession)
		{
			message = "Unity MCP session auto-connect is disabled.";
			return true;
		}
		try
		{
			string httpUrl = FindHttpUrl(config.CommandArgs) ?? "http://127.0.0.1:8080";
			using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(UnityEditorPrefsRegistryPath);
			if (registryKey == null)
			{
				message = "Unity EditorPrefs registry key could not be opened.";
				return false;
			}
			SetBool(registryKey, "MCPForUnity.UseHttpTransport", value: true);
			SetString(registryKey, "MCPForUnity.HttpTransportScope", "local");
			SetString(registryKey, "MCPForUnity.HttpUrl", httpUrl);
			SetBool(registryKey, "MCPForUnity.AutoStartOnLoad", value: true);
			SetBool(registryKey, "MCPForUnity.ProjectScopedTools.LocalHttp", ContainsArg(config.CommandArgs, "--project-scoped-tools"));
			message = "Configured Unity MCP EditorPrefs for HTTP session auto-connect at " + httpUrl + ".";
			return true;
		}
		catch (Exception ex)
		{
			message = "Failed to configure Unity MCP EditorPrefs: " + ex.Message;
			return false;
		}
	}

	private static void SetBool(RegistryKey registryKey, string key, bool value)
	{
		registryKey.SetValue(ToRegistryValueName(key), value ? 1 : 0, RegistryValueKind.DWord);
	}

	private static void SetString(RegistryKey registryKey, string key, string value)
	{
		byte[] valueBytes = Encoding.UTF8.GetBytes((value ?? string.Empty) + "\0");
		registryKey.SetValue(ToRegistryValueName(key), valueBytes, RegistryValueKind.Binary);
	}

	private static string ToRegistryValueName(string key)
	{
		return key + "_h" + Djb2Xor(key).ToString(CultureInfo.InvariantCulture);
	}

	private static uint Djb2Xor(string value)
	{
		uint hash = 5381u;
		foreach (char c in value)
		{
			hash = ((hash << 5) + hash) ^ c;
		}
		return hash;
	}

	private static bool ContainsArg(string commandArgs, string expected)
	{
		foreach (string arg in SplitArgs(commandArgs))
		{
			if (string.Equals(arg, expected, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	private static string FindHttpUrl(string commandArgs)
	{
		List<string> args = SplitArgs(commandArgs);
		for (int i = 0; i < args.Count; i++)
		{
			string arg = args[i];
			if (string.Equals(arg, "--http-url", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
			{
				return NormalizeBaseUrl(args[i + 1]);
			}
			if (arg.StartsWith("--http-url=", StringComparison.OrdinalIgnoreCase))
			{
				return NormalizeBaseUrl(arg.Substring("--http-url=".Length));
			}
		}
		return null;
	}

	private static string NormalizeBaseUrl(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}
		string text = value.Trim().TrimEnd('/');
		if (text.EndsWith("/mcp", StringComparison.OrdinalIgnoreCase))
		{
			text = text.Substring(0, text.Length - 4).TrimEnd('/');
		}
		return text;
	}

	private static List<string> SplitArgs(string commandArgs)
	{
		List<string> args = new List<string>();
		if (string.IsNullOrWhiteSpace(commandArgs))
		{
			return args;
		}
		StringBuilder current = new StringBuilder();
		bool inQuotes = false;
		for (int i = 0; i < commandArgs.Length; i++)
		{
			char c = commandArgs[i];
			if (c == '"')
			{
				inQuotes = !inQuotes;
				continue;
			}
			if (char.IsWhiteSpace(c) && !inQuotes)
			{
				AddCurrent(args, current);
			}
			else
			{
				current.Append(c);
			}
		}
		AddCurrent(args, current);
		return args;
	}

	private static void AddCurrent(List<string> args, StringBuilder current)
	{
		if (current.Length == 0)
		{
			return;
		}
		args.Add(current.ToString());
		current.Length = 0;
	}
}

