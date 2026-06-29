using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace McpForUnityAgent;

internal static class ClientConfiguration
{
	private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

	private static readonly Regex HttpUrlRegex = new Regex("--http-url\\s+(?:\"([^\"]+)\"|(\\S+))", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	public static List<ClientConfigResult> GetClients()
	{
		List<ClientConfigResult> clients = new List<ClientConfigResult>();
		foreach (ClientDefinition definition in GetDefinitions())
		{
			clients.Add(GetStatus(definition));
		}
		return clients;
	}

	public static ClientConfigSummary ConfigureAllDetected()
	{
		ClientConfigSummary summary = new ClientConfigSummary();
		foreach (ClientDefinition definition in GetDefinitions())
		{
			ClientConfigResult status = GetStatus(definition);
			if (!status.Installed)
			{
				summary.Skipped++;
				summary.Messages.Add(definition.Name + ": not installed, skipped.");
				continue;
			}
			if (!definition.SupportsHttp)
			{
				summary.Skipped++;
				summary.Messages.Add(definition.Name + ": HTTP unsupported by this client, skipped.");
				continue;
			}
			try
			{
				Configure(definition, createDirectory: false);
				summary.Success++;
				summary.Messages.Add(definition.Name + ": configured.");
			}
			catch (Exception ex)
			{
				summary.Failed++;
				summary.Messages.Add(definition.Name + ": " + ex.Message);
			}
		}
		return summary;
	}

	public static ClientConfigResult ConfigureByName(string name)
	{
		ClientDefinition definition = FindDefinition(name);
		Configure(definition, createDirectory: true);
		return GetStatus(definition);
	}

	public static string GetManualSnippet(string name)
	{
		ClientDefinition definition = FindDefinition(name);
		if (!definition.SupportsHttp)
		{
			return "This client does not support the Agent-managed HTTP endpoint.";
		}
		if (definition.TomlLayout)
		{
			return BuildCodexTomlSnippet();
		}
		Dictionary<string, object> root = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, object> container = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
		root[definition.VsCodeLayout ? "servers" : "mcpServers"] = container;
		container["unityMCP"] = BuildUnityServer(definition);
		return SerializePretty(root);
	}

	private static ClientConfigResult GetStatus(ClientDefinition definition)
	{
		ClientConfigResult result = new ClientConfigResult();
		result.Name = definition.Name;
		result.ConfigPath = definition.ConfigPath;
		result.Installed = ParentDirectoryExists(definition.ConfigPath);
		result.SupportsHttp = definition.SupportsHttp;
		if (!definition.SupportsHttp)
		{
			result.Status = result.Installed ? "HTTP unsupported" : "Not installed";
			return result;
		}
		if (!result.Installed)
		{
			result.Status = "Not installed";
			return result;
		}
		if (!File.Exists(definition.ConfigPath))
		{
			result.Status = "Not configured";
			return result;
		}
		if (definition.TomlLayout)
		{
			return GetTomlStatus(definition, result);
		}
		try
		{
			Dictionary<string, object> root = ReadJsonObject(definition.ConfigPath);
			Dictionary<string, object> server = FindUnityServer(root, definition);
			if (server == null)
			{
				result.Status = "Missing unityMCP";
				return result;
			}
			string configuredUrl = GetString(server, definition.UrlProperty);
			if (string.IsNullOrWhiteSpace(configuredUrl))
			{
				configuredUrl = GetString(server, "url") ?? GetString(server, "serverUrl") ?? GetString(server, "httpUrl");
			}
			result.ConfiguredUrl = configuredUrl ?? string.Empty;
			result.Status = UrlsEqual(configuredUrl, GetMcpUrl()) ? "Configured" : "Different endpoint";
			return result;
		}
		catch (Exception ex)
		{
			result.Status = "Error: " + ex.Message;
			return result;
		}
	}

	private static void Configure(ClientDefinition definition, bool createDirectory)
	{
		if (!definition.SupportsHttp)
		{
			throw new InvalidOperationException("This client does not support the Agent-managed HTTP endpoint.");
		}
		string directory = Path.GetDirectoryName(definition.ConfigPath);
		if (string.IsNullOrWhiteSpace(directory))
		{
			throw new InvalidOperationException("Invalid config path.");
		}
		if (!Directory.Exists(directory))
		{
			if (!createDirectory)
			{
				throw new DirectoryNotFoundException("Client config directory was not found.");
			}
			Directory.CreateDirectory(directory);
		}
		if (definition.TomlLayout)
		{
			ConfigureToml(definition);
			return;
		}
		Dictionary<string, object> root = File.Exists(definition.ConfigPath)
			? ReadJsonObject(definition.ConfigPath)
			: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, object> container = EnsureObject(root, definition.VsCodeLayout ? "servers" : "mcpServers");
		container["unityMCP"] = BuildUnityServer(definition);
		BackupExistingFile(definition.ConfigPath);
		File.WriteAllText(definition.ConfigPath, SerializePretty(root), Encoding.UTF8);
	}

	private static Dictionary<string, object> BuildUnityServer(ClientDefinition definition)
	{
		Dictionary<string, object> server = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
		server[definition.UrlProperty] = GetMcpUrl();
		server["type"] = definition.StreamableHttpType ? "streamableHttp" : "http";
		if (definition.EnsureEnvObject)
		{
			server["env"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
		}
		foreach (KeyValuePair<string, object> field in definition.DefaultFields)
		{
			server[field.Key] = field.Value;
		}
		return server;
	}

	private static string BuildCodexTomlSnippet()
	{
		return "[features]" + Environment.NewLine
			+ "rmcp_client = true" + Environment.NewLine
			+ Environment.NewLine
			+ "[mcp_servers.unityMCP]" + Environment.NewLine
			+ "url = " + ToTomlString(GetMcpUrl()) + Environment.NewLine;
	}

	private static ClientConfigResult GetTomlStatus(ClientDefinition definition, ClientConfigResult result)
	{
		try
		{
			string configuredUrl = FindCodexUnityUrl(File.ReadAllText(definition.ConfigPath, Encoding.UTF8));
			if (string.IsNullOrWhiteSpace(configuredUrl))
			{
				result.Status = "Missing unityMCP";
				return result;
			}
			result.ConfiguredUrl = configuredUrl;
			result.Status = UrlsEqual(configuredUrl, GetMcpUrl()) ? "Configured" : "Different endpoint";
			return result;
		}
		catch (Exception ex)
		{
			result.Status = "Error: " + ex.Message;
			return result;
		}
	}

	private static void ConfigureToml(ClientDefinition definition)
	{
		string existingToml = File.Exists(definition.ConfigPath)
			? File.ReadAllText(definition.ConfigPath, Encoding.UTF8)
			: string.Empty;
		BackupExistingFile(definition.ConfigPath);
		File.WriteAllText(definition.ConfigPath, UpsertCodexToml(existingToml), Encoding.UTF8);
	}

	private static string UpsertCodexToml(string existingToml)
	{
		List<string> lines = new List<string>(SplitLines(existingToml));
		RemoveTomlTable(lines, "mcp_servers.unityMCP");
		RemoveTomlTable(lines, "mcpServers.unityMCP");
		EnsureTomlFeatureFlag(lines);
		TrimTrailingBlankLines(lines);
		if (lines.Count > 0)
		{
			lines.Add(string.Empty);
		}
		lines.Add("[mcp_servers.unityMCP]");
		lines.Add("url = " + ToTomlString(GetMcpUrl()));
		return string.Join(Environment.NewLine, lines.ToArray()) + Environment.NewLine;
	}

	private static string FindCodexUnityUrl(string toml)
	{
		bool inUnityTable = false;
		foreach (string line in SplitLines(toml))
		{
			string trimmed = line.Trim();
			if (IsTomlTableHeader(trimmed))
			{
				string tableName = GetTomlTableName(trimmed);
				inUnityTable = string.Equals(tableName, "mcp_servers.unityMCP", StringComparison.OrdinalIgnoreCase)
					|| string.Equals(tableName, "mcpServers.unityMCP", StringComparison.OrdinalIgnoreCase);
				continue;
			}
			if (!inUnityTable || !TomlKeyEquals(trimmed, "url"))
			{
				continue;
			}
			int equalsIndex = trimmed.IndexOf('=');
			return equalsIndex < 0 ? null : ParseTomlValue(trimmed.Substring(equalsIndex + 1));
		}
		return null;
	}

	private static void RemoveTomlTable(List<string> lines, string tableName)
	{
		for (int index = 0; index < lines.Count;)
		{
			string trimmed = lines[index].Trim();
			if (!IsTomlTableHeader(trimmed) || !string.Equals(GetTomlTableName(trimmed), tableName, StringComparison.OrdinalIgnoreCase))
			{
				index++;
				continue;
			}
			int end = index + 1;
			while (end < lines.Count && !IsTomlTableHeader(lines[end].Trim()))
			{
				end++;
			}
			lines.RemoveRange(index, end - index);
		}
	}

	private static void EnsureTomlFeatureFlag(List<string> lines)
	{
		int featuresStart = FindTomlTable(lines, "features");
		if (featuresStart < 0)
		{
			TrimTrailingBlankLines(lines);
			if (lines.Count > 0)
			{
				lines.Add(string.Empty);
			}
			lines.Add("[features]");
			lines.Add("rmcp_client = true");
			return;
		}
		int featuresEnd = FindTomlTableEnd(lines, featuresStart);
		for (int index = featuresStart + 1; index < featuresEnd; index++)
		{
			if (TomlKeyEquals(lines[index].Trim(), "rmcp_client"))
			{
				lines[index] = "rmcp_client = true";
				return;
			}
		}
		lines.Insert(featuresStart + 1, "rmcp_client = true");
	}

	private static int FindTomlTable(List<string> lines, string tableName)
	{
		for (int index = 0; index < lines.Count; index++)
		{
			string trimmed = lines[index].Trim();
			if (IsTomlTableHeader(trimmed) && string.Equals(GetTomlTableName(trimmed), tableName, StringComparison.OrdinalIgnoreCase))
			{
				return index;
			}
		}
		return -1;
	}

	private static int FindTomlTableEnd(List<string> lines, int tableStart)
	{
		int index = tableStart + 1;
		while (index < lines.Count && !IsTomlTableHeader(lines[index].Trim()))
		{
			index++;
		}
		return index;
	}

	private static bool IsTomlTableHeader(string trimmedLine)
	{
		return trimmedLine.StartsWith("[", StringComparison.Ordinal)
			&& trimmedLine.EndsWith("]", StringComparison.Ordinal)
			&& !trimmedLine.StartsWith("[[", StringComparison.Ordinal);
	}

	private static string GetTomlTableName(string trimmedHeader)
	{
		return trimmedHeader.Substring(1, trimmedHeader.Length - 2).Trim();
	}

	private static bool TomlKeyEquals(string trimmedLine, string key)
	{
		int equalsIndex = trimmedLine.IndexOf('=');
		if (equalsIndex < 0)
		{
			return false;
		}
		string candidate = trimmedLine.Substring(0, equalsIndex).Trim();
		return string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase);
	}

	private static string ParseTomlValue(string rawValue)
	{
		string value = rawValue.Trim();
		if (value.StartsWith("\"", StringComparison.Ordinal))
		{
			StringBuilder builder = new StringBuilder();
			bool escaped = false;
			for (int index = 1; index < value.Length; index++)
			{
				char ch = value[index];
				if (escaped)
				{
					builder.Append(ch);
					escaped = false;
					continue;
				}
				if (ch == '\\')
				{
					escaped = true;
					continue;
				}
				if (ch == '"')
				{
					return builder.ToString();
				}
				builder.Append(ch);
			}
			return builder.ToString();
		}
		if (value.StartsWith("'", StringComparison.Ordinal))
		{
			int end = value.IndexOf('\'', 1);
			return end < 0 ? value.Substring(1) : value.Substring(1, end - 1);
		}
		int commentIndex = value.IndexOf('#');
		if (commentIndex >= 0)
		{
			value = value.Substring(0, commentIndex);
		}
		return value.Trim();
	}

	private static string ToTomlString(string value)
	{
		return "\"" + (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
	}

	private static string[] SplitLines(string value)
	{
		return (value ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
	}

	private static void TrimTrailingBlankLines(List<string> lines)
	{
		while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
		{
			lines.RemoveAt(lines.Count - 1);
		}
	}

	private static void BackupExistingFile(string path)
	{
		if (!File.Exists(path))
		{
			return;
		}
		string backupPath = path + "." + DateTime.Now.ToString("yyyyMMddHHmmss") + ".bak";
		File.Copy(path, backupPath, overwrite: false);
	}

	private static Dictionary<string, object> FindUnityServer(Dictionary<string, object> root, ClientDefinition definition)
	{
		Dictionary<string, object> container = GetObject(root, definition.VsCodeLayout ? "servers" : "mcpServers");
		if (container == null && definition.VsCodeLayout)
		{
			Dictionary<string, object> mcp = GetObject(root, "mcp");
			container = mcp == null ? null : GetObject(mcp, "servers");
		}
		if (container == null)
		{
			return null;
		}
		return GetObject(container, "unityMCP");
	}

	private static Dictionary<string, object> ReadJsonObject(string path)
	{
		string json = File.ReadAllText(path, Encoding.UTF8);
		if (string.IsNullOrWhiteSpace(json))
		{
			return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
		}
		object value = Json.DeserializeObject(json);
		Dictionary<string, object> dictionary = value as Dictionary<string, object>;
		if (dictionary == null)
		{
			throw new InvalidOperationException("Config root is not a JSON object.");
		}
		return dictionary;
	}

	private static Dictionary<string, object> EnsureObject(Dictionary<string, object> parent, string key)
	{
		Dictionary<string, object> existing = GetObject(parent, key);
		if (existing != null)
		{
			return existing;
		}
		Dictionary<string, object> created = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
		parent[key] = created;
		return created;
	}

	private static Dictionary<string, object> GetObject(Dictionary<string, object> parent, string key)
	{
		if (parent == null || !parent.ContainsKey(key))
		{
			return null;
		}
		return parent[key] as Dictionary<string, object>;
	}

	private static string GetString(Dictionary<string, object> parent, string key)
	{
		if (parent == null || !parent.ContainsKey(key) || parent[key] == null)
		{
			return null;
		}
		return Convert.ToString(parent[key]);
	}

	private static bool ParentDirectoryExists(string configPath)
	{
		try
		{
			string directory = Path.GetDirectoryName(configPath);
			return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory);
		}
		catch
		{
			return false;
		}
	}

	private static bool UrlsEqual(string left, string right)
	{
		if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
		{
			return false;
		}
		Uri leftUri;
		Uri rightUri;
		if (Uri.TryCreate(left.Trim(), UriKind.Absolute, out leftUri) && Uri.TryCreate(right.Trim(), UriKind.Absolute, out rightUri))
		{
			return Uri.Compare(leftUri, rightUri, UriComponents.HttpRequestUrl, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0;
		}
		return string.Equals(left.Trim().TrimEnd('/'), right.Trim().TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
	}

	private static string GetMcpUrl()
	{
		string url = null;
		Match match = HttpUrlRegex.Match(AppConfig.Load().CommandArgs ?? string.Empty);
		if (match.Success)
		{
			url = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
		}
		if (string.IsNullOrWhiteSpace(url))
		{
			url = "http://127.0.0.1:" + PortTools.SuggestedPortFromConfig();
		}
		url = url.Trim().TrimEnd('/');
		if (!url.EndsWith("/mcp", StringComparison.OrdinalIgnoreCase))
		{
			url += "/mcp";
		}
		return url;
	}

	private static ClientDefinition FindDefinition(string name)
	{
		foreach (ClientDefinition definition in GetDefinitions())
		{
			if (string.Equals(definition.Name, name, StringComparison.OrdinalIgnoreCase))
			{
				return definition;
			}
		}
		throw new InvalidOperationException("Unknown client: " + name);
	}

	private static List<ClientDefinition> GetDefinitions()
	{
		string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		List<ClientDefinition> clients = new List<ClientDefinition>();
		clients.Add(new ClientDefinition("Codex", Path.Combine(user, ".codex", "config.toml")) { TomlLayout = true });
		clients.Add(new ClientDefinition("Cursor", Path.Combine(user, ".cursor", "mcp.json")));
		clients.Add(new ClientDefinition("VS Code GitHub Copilot", Path.Combine(appData, "Code", "User", "mcp.json")) { VsCodeLayout = true });
		clients.Add(new ClientDefinition("VS Code Insiders GitHub Copilot", Path.Combine(appData, "Code - Insiders", "User", "mcp.json")) { VsCodeLayout = true });
		clients.Add(new ClientDefinition("Windsurf", Path.Combine(user, ".codeium", "windsurf", "mcp_config.json")) { UrlProperty = "serverUrl", DefaultFields = { { "disabled", false } } });
		clients.Add(new ClientDefinition("Cline", Path.Combine(appData, "Code", "User", "globalStorage", "saoudrizwan.claude-dev", "settings", "cline_mcp_settings.json")) { StreamableHttpType = true, DefaultFields = { { "disabled", false }, { "autoApprove", new object[0] } } });
		clients.Add(new ClientDefinition("Kilo Code", Path.Combine(appData, "Code", "User", "globalStorage", "kilocode.kilo-code", "settings", "mcp_settings.json")));
		clients.Add(new ClientDefinition("Kiro", Path.Combine(user, ".kiro", "settings", "mcp.json")) { EnsureEnvObject = true, DefaultFields = { { "disabled", false } } });
		clients.Add(new ClientDefinition("Trae", Path.Combine(appData, "Trae", "mcp.json")));
		clients.Add(new ClientDefinition("Rider GitHub Copilot", Path.Combine(localAppData, "github-copilot", "intellij", "mcp.json")) { VsCodeLayout = true });
		clients.Add(new ClientDefinition("GitHub Copilot CLI", Path.Combine(user, ".copilot", "mcp-config.json")));
		clients.Add(new ClientDefinition("Gemini CLI", Path.Combine(user, ".gemini", "settings.json")) { UrlProperty = "httpUrl" });
		clients.Add(new ClientDefinition("Qwen Code", Path.Combine(user, ".qwen", "settings.json")));
		clients.Add(new ClientDefinition("Antigravity 2.0", Path.Combine(user, ".gemini", "config", "mcp_config.json")) { UrlProperty = "serverUrl", DefaultFields = { { "disabled", false } } });
		clients.Add(new ClientDefinition("Antigravity IDE", Path.Combine(user, ".gemini", "antigravity-ide", "mcp_config.json")) { UrlProperty = "serverUrl", DefaultFields = { { "disabled", false } } });
		clients.Add(new ClientDefinition("Claude Desktop", Path.Combine(appData, "Claude", "claude_desktop_config.json")) { SupportsHttp = false });
		clients.Add(new ClientDefinition("Cherry Studio", Path.Combine(appData, "Cherry Studio", "config")) { SupportsHttp = false });
		return clients;
	}

	private static string SerializePretty(object value)
	{
		StringBuilder builder = new StringBuilder();
		WriteJsonValue(builder, value, 0);
		builder.AppendLine();
		return builder.ToString();
	}

	private static void WriteJsonValue(StringBuilder builder, object value, int indent)
	{
		if (value == null)
		{
			builder.Append("null");
			return;
		}
		Dictionary<string, object> dictionary = value as Dictionary<string, object>;
		if (dictionary != null)
		{
			WriteJsonObject(builder, dictionary, indent);
			return;
		}
		IDictionary rawDictionary = value as IDictionary;
		if (rawDictionary != null)
		{
			Dictionary<string, object> normalized = new Dictionary<string, object>();
			foreach (DictionaryEntry entry in rawDictionary)
			{
				normalized[Convert.ToString(entry.Key)] = entry.Value;
			}
			WriteJsonObject(builder, normalized, indent);
			return;
		}
		IEnumerable enumerable = value as IEnumerable;
		if (enumerable != null && !(value is string))
		{
			WriteJsonArray(builder, enumerable, indent);
			return;
		}
		builder.Append(Json.Serialize(value));
	}

	private static void WriteJsonObject(StringBuilder builder, Dictionary<string, object> dictionary, int indent)
	{
		builder.Append("{");
		if (dictionary.Count > 0)
		{
			bool first = true;
			foreach (KeyValuePair<string, object> item in dictionary)
			{
				if (first)
				{
					first = false;
				}
				else
				{
					builder.Append(",");
				}
				builder.AppendLine();
				AppendIndent(builder, indent + 2);
				builder.Append(Json.Serialize(item.Key));
				builder.Append(": ");
				WriteJsonValue(builder, item.Value, indent + 2);
			}
			builder.AppendLine();
			AppendIndent(builder, indent);
		}
		builder.Append("}");
	}

	private static void WriteJsonArray(StringBuilder builder, IEnumerable enumerable, int indent)
	{
		List<object> values = new List<object>();
		foreach (object value in enumerable)
		{
			values.Add(value);
		}
		builder.Append("[");
		if (values.Count > 0)
		{
			for (int i = 0; i < values.Count; i++)
			{
				if (i > 0)
				{
					builder.Append(",");
				}
				builder.AppendLine();
				AppendIndent(builder, indent + 2);
				WriteJsonValue(builder, values[i], indent + 2);
			}
			builder.AppendLine();
			AppendIndent(builder, indent);
		}
		builder.Append("]");
	}

	private static void AppendIndent(StringBuilder builder, int indent)
	{
		builder.Append(' ', indent);
	}

	private sealed class ClientDefinition
	{
		public readonly string Name;

		public readonly string ConfigPath;

		public bool SupportsHttp = true;

		public bool TomlLayout;

		public bool VsCodeLayout;

		public bool StreamableHttpType;

		public bool EnsureEnvObject;

		public string UrlProperty = "url";

		public readonly Dictionary<string, object> DefaultFields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

		public ClientDefinition(string name, string configPath)
		{
			Name = name;
			ConfigPath = configPath;
		}
	}

	public sealed class ClientConfigResult
	{
		public string Name;

		public string Status;

		public string ConfigPath;

		public string ConfiguredUrl;

		public bool Installed;

		public bool SupportsHttp;
	}

	public sealed class ClientConfigSummary
	{
		public int Success;

		public int Failed;

		public int Skipped;

		public readonly List<string> Messages = new List<string>();
	}
}
