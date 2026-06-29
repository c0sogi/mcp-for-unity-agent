using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace McpForUnityAgent;

internal static class DependencySetup
{
	private const string UvInstallerUrl = "https://astral.sh/uv/install.ps1";

	private const string UvReleasesApiUrl = "https://api.github.com/repos/astral-sh/uv/releases/latest";

	private const string GitForWindowsReleasesApiUrl = "https://api.github.com/repos/git-for-windows/git/releases/latest";

	public static string GetStatusText()
	{
		StringBuilder stringBuilder = new StringBuilder();
		DependencySnapshot dependencySnapshot = CheckStatus(probeInstallers: false);
		stringBuilder.AppendLine("Dependency status:");
		stringBuilder.AppendLine("uv: " + FormatInstalledStatus(dependencySnapshot.UvInstalled, dependencySnapshot.UvInstalledDetail));
		stringBuilder.AppendLine("git: " + FormatInstalledStatus(dependencySnapshot.GitInstalled, dependencySnapshot.GitInstalledDetail));
		return stringBuilder.ToString();
	}

	public static int RunDiagnostics(TextWriter writer, bool simulateMissing)
	{
		writer.WriteLine(GetStatusText());
		string validationText;
		bool flag = TryGetInstallerValidationText(simulateMissing, includeInstallCommands: true, out validationText);
		writer.WriteLine(validationText);
		return flag ? 0 : 1;
	}

	public static void SetupInteractive(IWin32Window owner)
	{
		using DependencySetupForm dependencySetupForm = new DependencySetupForm();
		dependencySetupForm.ShowDialog(owner);
	}

	public static DependencySnapshot CheckStatus(bool probeInstallers)
	{
		DependencySnapshot dependencySnapshot = new DependencySnapshot();
		dependencySnapshot.UvInstalled = IsUvAvailable();
		dependencySnapshot.GitInstalled = IsCommandAvailable("git.exe");
		dependencySnapshot.UvInstalledDetail = dependencySnapshot.UvInstalled ? GetUvInstalledVersion().FirstOutputLine : "";
		dependencySnapshot.GitInstalledDetail = dependencySnapshot.GitInstalled ? GetInstalledVersion("git.exe").FirstOutputLine : "";
		if (probeInstallers)
		{
			ProcessResult processResult = ProbeUvInstaller();
			dependencySnapshot.UvInstallerAvailable = processResult.ExitCode == 0;
			dependencySnapshot.UvInstallerDetail = processResult.FirstOutputLine;
			ProcessResult processResult2 = ProbeGitInstaller();
			dependencySnapshot.GitInstallerAvailable = processResult2.ExitCode == 0;
			dependencySnapshot.GitInstallerDetail = processResult2.FirstOutputLine;
		}
		return dependencySnapshot;
	}

	public static void StartUvInstaller()
	{
		StartPowerShell("uv installer", GetUvInstallOrUpdateScript());
	}

	public static void StartGitInstaller()
	{
		StartPowerShell("Git for Windows installer", GetGitInstallScript());
	}

	public static void StartMissingInstallers(DependencySnapshot snapshot)
	{
		if (snapshot == null)
		{
			snapshot = CheckStatus(probeInstallers: false);
		}
		if (!snapshot.UvInstalled && snapshot.UvInstallerAvailable)
		{
			StartUvInstaller();
		}
		if (!snapshot.GitInstalled && snapshot.GitInstallerAvailable)
		{
			StartGitInstaller();
		}
	}

	public static void StartAvailableInstallers(DependencySnapshot snapshot)
	{
		if (snapshot == null)
		{
			snapshot = CheckStatus(probeInstallers: true);
		}
		if (snapshot.UvInstallerAvailable)
		{
			StartUvInstaller();
		}
		if (snapshot.GitInstallerAvailable)
		{
			StartGitInstaller();
		}
	}

	private static void StartPowerShell(string title, string script)
	{
		ProcessStartInfo processStartInfo = new ProcessStartInfo();
		processStartInfo.FileName = "powershell.exe";
		processStartInfo.Arguments = "-NoExit -ExecutionPolicy Bypass -Command " + QuotePowerShellArgument("$host.UI.RawUI.WindowTitle = '" + EscapePowerShellString(title) + "'; " + script);
		processStartInfo.UseShellExecute = true;
		Process.Start(processStartInfo);
	}

	private static bool TryGetInstallerValidationText(bool simulateMissing, bool includeInstallCommands, out string text)
	{
		StringBuilder stringBuilder = new StringBuilder();
		bool result = true;
		stringBuilder.AppendLine("official installer validation:");
		result &= AppendOfficialInstallerStatus(stringBuilder, "uv", "Astral official installer", ProbeUvInstaller());
		result &= AppendOfficialInstallerStatus(stringBuilder, "git", "Git for Windows release installer", ProbeGitInstaller());
		if (includeInstallCommands)
		{
			stringBuilder.AppendLine();
			stringBuilder.AppendLine(simulateMissing ? "Installer actions if dependencies are missing:" : "Installer actions for currently missing dependencies:");
			bool flag = false;
			if (simulateMissing || !IsUvAvailable())
			{
				stringBuilder.AppendLine("uv: " + GetUvInstallCommandText());
				flag = true;
			}
			if (simulateMissing || !IsCommandAvailable("git.exe"))
			{
				stringBuilder.AppendLine("git: download latest Git-*-64-bit.exe from " + GitForWindowsReleasesApiUrl + " and launch it");
				flag = true;
			}
			if (!flag)
			{
				stringBuilder.AppendLine("(none)");
			}
		}
		text = stringBuilder.ToString();
		return result;
	}

	private static bool AppendOfficialInstallerStatus(StringBuilder stringBuilder, string label, string sourceName, ProcessResult processResult)
	{
		bool flag = processResult.ExitCode == 0;
		string detail = string.IsNullOrWhiteSpace(processResult.FirstOutputLine) ? "" : " (" + processResult.FirstOutputLine + ")";
		stringBuilder.AppendLine(label + " " + sourceName + ": " + (flag ? "found" : "not found") + detail);
		return flag;
	}

	private static string FormatInstalledStatus(bool installed, string detail)
	{
		if (!installed)
		{
			return "missing";
		}
		if (string.IsNullOrWhiteSpace(detail))
		{
			return "installed";
		}
		return "installed (" + detail + ")";
	}

	private static ProcessResult ProbeUvInstaller()
	{
		return RunPowerShellProbe("$ProgressPreference='SilentlyContinue'; " +
			"$installer = Invoke-WebRequest -UseBasicParsing -Uri '" + UvInstallerUrl + "'; " +
			"if ($installer.StatusCode -lt 200 -or $installer.StatusCode -ge 400) { throw 'uv installer script unavailable.' }; " +
			"$release = Invoke-RestMethod -Uri '" + UvReleasesApiUrl + "'; " +
			"$tag = [string]$release.tag_name; " +
			"if ([string]::IsNullOrWhiteSpace($tag)) { throw 'uv release version not found.' }; " +
			"if ($tag.StartsWith('v')) { $tag = $tag.Substring(1) }; " +
			"'uv ' + $tag");
	}

	private static ProcessResult ProbeGitInstaller()
	{
		return RunPowerShellProbe("$ProgressPreference='SilentlyContinue'; $release = Invoke-RestMethod -Uri '" + GitForWindowsReleasesApiUrl + "'; $asset = $release.assets | Where-Object { $_.name -match '^Git-.*-64-bit\\.exe$' } | Select-Object -First 1; if (-not $asset) { throw 'Git installer asset not found.' } $asset.name");
	}

	private static ProcessResult RunPowerShellProbe(string script)
	{
		return RunProcess("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command " + QuotePowerShellArgument(script), 30000);
	}

	private static ProcessResult GetUvInstalledVersion()
	{
		string text = FindCommandPath("uv.exe");
		if (string.IsNullOrWhiteSpace(text))
		{
			text = FindCommandPath("uvx.exe");
		}
		if (!string.IsNullOrWhiteSpace(text))
		{
			return GetInstalledVersion(text);
		}
		string text2 = AppPaths.FindDefaultUvxPath();
		if (File.Exists(text2))
		{
			return GetInstalledVersion(text2);
		}
		return new ProcessResult(-1, "");
	}

	private static ProcessResult GetInstalledVersion(string exeNameOrPath)
	{
		if (string.IsNullOrWhiteSpace(exeNameOrPath))
		{
			return new ProcessResult(-1, "");
		}
		string fileName = exeNameOrPath;
		if (!Path.IsPathRooted(fileName))
		{
			fileName = FindCommandPath(exeNameOrPath);
		}
		if (string.IsNullOrWhiteSpace(fileName))
		{
			return new ProcessResult(-1, "");
		}
		return RunProcess(fileName, "--version", 5000);
	}

	private static string GetUvInstallCommandText()
	{
		return "powershell -ExecutionPolicy ByPass -c \"if (Get-Command uv -ErrorAction SilentlyContinue) { try { uv self update } catch { irm " + UvInstallerUrl + " | iex } } else { irm " + UvInstallerUrl + " | iex }\"";
	}

	private static string GetUvInstallOrUpdateScript()
	{
		return "$ErrorActionPreference='Stop'; " +
			"$uv = Get-Command uv -ErrorAction SilentlyContinue; " +
			"if ($uv) { " +
			"Write-Host 'Updating uv with uv self update...'; " +
			"try { uv self update } catch { Write-Warning $_.Exception.Message; Write-Host 'Falling back to the official uv installer...'; irm '" + UvInstallerUrl + "' | iex } " +
			"} else { " +
			"Write-Host 'Installing uv with the official uv installer...'; " +
			"irm '" + UvInstallerUrl + "' | iex " +
			"}; " +
			"Write-Host ''; " +
			"Write-Host 'uv install/update finished. Restart this app or reopen your terminal if PATH changed.'; " +
			"uv --version; uvx --version";
	}

	private static string GetGitInstallScript()
	{
		return "$ErrorActionPreference='Stop'; " +
			"$ProgressPreference='SilentlyContinue'; " +
			"$release = Invoke-RestMethod -Uri '" + GitForWindowsReleasesApiUrl + "'; " +
			"$asset = $release.assets | Where-Object { $_.name -match '^Git-.*-64-bit\\.exe$' } | Select-Object -First 1; " +
			"if (-not $asset) { throw 'Git installer asset not found.' }; " +
			"$path = Join-Path $env:TEMP $asset.name; " +
			"Invoke-WebRequest -UseBasicParsing -Uri $asset.browser_download_url -OutFile $path; " +
			"Start-Process -FilePath $path -Wait; " +
			"Write-Host ''; " +
			"Write-Host 'Git for Windows installer finished. Restart this app or reopen your terminal if PATH changed.'; " +
			"git --version";
	}

	private static string QuotePowerShellArgument(string script)
	{
		return "\"" + (script ?? string.Empty).Replace("\"", "\\\"") + "\"";
	}

	private static string EscapePowerShellString(string value)
	{
		return (value ?? string.Empty).Replace("'", "''");
	}

	private static ProcessResult RunProcess(string fileName, string arguments, int timeoutMilliseconds)
	{
		try
		{
			ProcessStartInfo processStartInfo = new ProcessStartInfo();
			processStartInfo.FileName = fileName;
			processStartInfo.Arguments = arguments;
			processStartInfo.UseShellExecute = false;
			processStartInfo.CreateNoWindow = true;
			processStartInfo.RedirectStandardOutput = true;
			processStartInfo.RedirectStandardError = true;
			Process process = new Process();
			process.StartInfo = processStartInfo;
			process.Start();
			string output = process.StandardOutput.ReadToEnd();
			string error = process.StandardError.ReadToEnd();
			if (!process.WaitForExit(timeoutMilliseconds))
			{
				try
				{
					process.Kill();
				}
				catch
				{
				}
				return new ProcessResult(-1, "timeout");
			}
			return new ProcessResult(process.ExitCode, FirstLine(output, error));
		}
		catch (Exception ex)
		{
			return new ProcessResult(-1, ex.Message);
		}
	}

	private static string FirstLine(string output, string error)
	{
		string text = string.IsNullOrWhiteSpace(output) ? error : output;
		if (string.IsNullOrWhiteSpace(text))
		{
			return "no output";
		}
		using StringReader stringReader = new StringReader(text);
		return (stringReader.ReadLine() ?? "no output").Trim();
	}

	private static bool IsUvAvailable()
	{
		return IsCommandAvailable("uvx.exe") || IsCommandAvailable("uv.exe") || File.Exists(AppPaths.FindDefaultUvxPath());
	}

	private static bool IsCommandAvailable(string exeName)
	{
		return !string.IsNullOrWhiteSpace(FindCommandPath(exeName));
	}

	private static string FindCommandPath(string exeName)
	{
		string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
		foreach (string directory in path.Split(Path.PathSeparator))
		{
			try
			{
				string text = Path.Combine(directory.Trim(), exeName);
				if (!string.IsNullOrWhiteSpace(directory) && File.Exists(text))
				{
					return text;
				}
			}
			catch
			{
			}
		}
		return string.Empty;
	}

	private sealed class ProcessResult
	{
		public readonly int ExitCode;

		public readonly string FirstOutputLine;

		public ProcessResult(int exitCode, string firstOutputLine)
		{
			ExitCode = exitCode;
			FirstOutputLine = firstOutputLine ?? string.Empty;
		}
	}

	public sealed class DependencySnapshot
	{
		public bool UvInstalled;

		public bool GitInstalled;

		public bool UvInstallerAvailable;

		public bool GitInstallerAvailable;

		public string UvInstalledDetail;

		public string GitInstalledDetail;

		public string UvInstallerDetail;

		public string GitInstallerDetail;

		public bool HasMissing => !UvInstalled || !GitInstalled;

		public bool CanInstallMissing => (!UvInstalled && UvInstallerAvailable) || (!GitInstalled && GitInstallerAvailable);

		public bool CanInstallAny => UvInstallerAvailable || GitInstallerAvailable;
	}
}

