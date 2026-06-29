using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace McpForUnityAgent;

internal static class PortTools
{
	private static readonly Regex WhitespaceRegex = new Regex("\\s+", RegexOptions.Compiled);

	public static List<PortProcessInfo> FindByPort(int port)
	{
		List<PortProcessInfo> list = new List<PortProcessInfo>();
		ProcessStartInfo processStartInfo = new ProcessStartInfo();
		processStartInfo.FileName = "netstat.exe";
		processStartInfo.Arguments = "-ano";
		processStartInfo.UseShellExecute = false;
		processStartInfo.CreateNoWindow = true;
		processStartInfo.RedirectStandardOutput = true;
		processStartInfo.RedirectStandardError = true;
		using (Process process = Process.Start(processStartInfo))
		{
			if (process == null)
			{
				return list;
			}
			string text = process.StandardOutput.ReadToEnd();
			process.WaitForExit(5000);
			string[] array = text.Split(new string[2] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
			string[] array2 = array;
			foreach (string text2 in array2)
			{
				string text3 = text2.Trim();
				if (!text3.StartsWith("TCP", StringComparison.OrdinalIgnoreCase) && !text3.StartsWith("UDP", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				string[] array3 = WhitespaceRegex.Split(text3);
				if (array3.Length < 4)
				{
					continue;
				}
				string text4 = array3[0];
				string text5 = array3[1];
				if (!AddressUsesPort(text5, port))
				{
					continue;
				}
				string state = "";
				string s;
				if (text4.Equals("TCP", StringComparison.OrdinalIgnoreCase))
				{
					if (array3.Length < 5)
					{
						continue;
					}
					state = array3[3];
					s = array3[4];
				}
				else
				{
					s = array3[array3.Length - 1];
				}
				if (int.TryParse(s, out var result))
				{
					list.Add(new PortProcessInfo
					{
						Protocol = text4.ToUpperInvariant(),
						LocalAddress = text5,
						State = state,
						ProcessId = result,
						ProcessName = GetProcessName(result)
					});
				}
			}
		}
		return list;
	}

	public static int SuggestedPortFromConfig()
	{
		AppConfig appConfig = AppConfig.Load();
		string input = appConfig.CommandArgs ?? string.Empty;
		Match match = Regex.Match(input, "--http-url\\s+(?:\"([^\"]+)\"|(\\S+))", RegexOptions.IgnoreCase);
		string text = null;
		if (match.Success)
		{
			text = (match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
		}
		if (!string.IsNullOrWhiteSpace(text) && Uri.TryCreate(text, UriKind.Absolute, out var result) && result.Port > 0)
		{
			return result.Port;
		}
		Match match2 = Regex.Match(input, ":(\\d{2,5})(?:/|\\s|$)");
		if (match2.Success && int.TryParse(match2.Groups[1].Value, out var result2))
		{
			return result2;
		}
		return 8080;
	}

	private static bool AddressUsesPort(string address, int port)
	{
		int num = address.LastIndexOf(':');
		if (num < 0 || num + 1 >= address.Length)
		{
			return false;
		}
		int result;
		return int.TryParse(address.Substring(num + 1), out result) && result == port;
	}

	private static string GetProcessName(int pid)
	{
		try
		{
			using Process process = Process.GetProcessById(pid);
			return process.ProcessName;
		}
		catch
		{
			return "(unknown)";
		}
	}
}

