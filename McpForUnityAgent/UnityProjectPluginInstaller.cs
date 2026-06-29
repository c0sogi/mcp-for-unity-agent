using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace McpForUnityAgent;

internal static class UnityProjectPluginInstaller
{
	private const string PackageName = "com.coplaydev.unity-mcp";

	private const string PackageSource = "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main";

	public static void InstallInteractive(IWin32Window owner)
	{
		using UnityProjectFolderForm unityProjectFolderForm = new UnityProjectFolderForm(GetInitialProjectPath());
		if (unityProjectFolderForm.ShowDialog(owner) != DialogResult.OK)
		{
			return;
		}
		try
		{
			string message;
			Install(unityProjectFolderForm.ProjectPath, out message);
			MessageBox.Show(owner, message, AppPaths.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
		}
		catch (Exception ex)
		{
			MessageBox.Show(owner, ex.Message, AppPaths.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	public static void Install(string projectDir, out string message)
	{
		if (string.IsNullOrWhiteSpace(projectDir) || !Directory.Exists(projectDir))
		{
			throw new InvalidOperationException("Unity project folder does not exist.");
		}
		string assetsDir = Path.Combine(projectDir, "Assets");
		if (!Directory.Exists(assetsDir))
		{
			throw new InvalidOperationException("The selected folder does not look like a Unity project. Expected an Assets folder.");
		}
		string packagesDir = Path.Combine(projectDir, "Packages");
		Directory.CreateDirectory(packagesDir);
		string manifestPath = Path.Combine(packagesDir, "manifest.json");
		string manifest = File.Exists(manifestPath) ? File.ReadAllText(manifestPath, Encoding.UTF8) : "{\r\n  \"dependencies\": {\r\n  }\r\n}\r\n";
		string updated = AddOrUpdateDependency(manifest, PackageName, PackageSource);
		if (string.Equals(manifest, updated, StringComparison.Ordinal))
		{
			message = "MCP for Unity plugin is already installed in this project.";
			return;
		}
		if (File.Exists(manifestPath))
		{
			string backupPath = manifestPath + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
			File.Copy(manifestPath, backupPath, overwrite: false);
		}
		File.WriteAllText(manifestPath, updated, Encoding.UTF8);
		message = "MCP for Unity plugin was added to Packages/manifest.json. Unity will import it when the project is opened or refreshed.";
	}

	private static string AddOrUpdateDependency(string manifest, string packageName, string packageSource)
	{
		string escapedName = Regex.Escape(packageName);
		Regex existingRegex = new Regex("(\"" + escapedName + "\"\\s*:\\s*\")([^\"]*)(\")", RegexOptions.Multiline);
		if (existingRegex.IsMatch(manifest))
		{
			return existingRegex.Replace(manifest, "$1" + EscapeJson(packageSource) + "$3", 1);
		}
		Regex dependenciesRegex = new Regex("\"dependencies\"\\s*:\\s*\\{", RegexOptions.Multiline);
		Match match = dependenciesRegex.Match(manifest);
		if (!match.Success)
		{
			string trimmed = manifest.Trim();
			if (trimmed == "{}")
			{
				return "{\r\n  \"dependencies\": {\r\n    \"" + packageName + "\": \"" + EscapeJson(packageSource) + "\"\r\n  }\r\n}\r\n";
			}
			throw new InvalidOperationException("Packages/manifest.json does not contain a dependencies object.");
		}
		int insertAt = match.Index + match.Length;
		bool hasExistingDependency = HasDependencyEntry(manifest, insertAt);
		string insertion = "\r\n    \"" + packageName + "\": \"" + EscapeJson(packageSource) + "\"" + (hasExistingDependency ? "," : "");
		return manifest.Insert(insertAt, insertion);
	}

	private static bool HasDependencyEntry(string manifest, int dependenciesStart)
	{
		int depth = 1;
		for (int i = dependenciesStart; i < manifest.Length; i++)
		{
			char c = manifest[i];
			if (c == '{')
			{
				depth++;
			}
			else if (c == '}')
			{
				depth--;
				if (depth == 0)
				{
					string inner = manifest.Substring(dependenciesStart, i - dependenciesStart);
					return inner.IndexOf(':') >= 0;
				}
			}
		}
		return false;
	}

	private static string EscapeJson(string value)
	{
		return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
	}

	private static string GetInitialProjectPath()
	{
		string currentDirectory = Environment.CurrentDirectory;
		if (LooksLikeUnityProject(currentDirectory))
		{
			return currentDirectory;
		}
		return string.Empty;
	}

	private static bool LooksLikeUnityProject(string path)
	{
		return !string.IsNullOrWhiteSpace(path) && Directory.Exists(Path.Combine(path, "Assets"));
	}
}

