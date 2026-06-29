using System;
using System.Text.RegularExpressions;

namespace McpForUnityAgent;

internal static class OutputText
{
	private static readonly Regex AnsiRegex = new Regex("\\x1B(?:\\[[0-?]*[ -/]*[@-~]|\\][^\\a]*(?:\\a|\\x1B\\\\))", RegexOptions.Compiled);

	private static readonly Regex UnicodeEscapeRegex = new Regex("\\\\(?:u([0-9a-fA-F]{4})|U([0-9a-fA-F]{8}))", RegexOptions.Compiled);

	public static string Clean(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return string.Empty;
		}
		string input = AnsiRegex.Replace(text, string.Empty);
		return UnicodeEscapeRegex.Replace(input, delegate(Match match)
		{
			string value = (match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
			try
			{
				int utf = Convert.ToInt32(value, 16);
				return char.ConvertFromUtf32(utf);
			}
			catch
			{
				return match.Value;
			}
		});
	}
}

