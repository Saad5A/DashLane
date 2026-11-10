using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DashLane.Services;

/// <summary>
/// UI text lookup. English (Canada) strings are the keys; Resources/fr-CA.json holds French,
/// and en-US gets American spellings. Missing keys fall back to the English text.
/// </summary>
public static class Text
{
    private static Dictionary<string, string> _french = new();

    private static readonly (Regex Pattern, string Replacement)[] UsSpelling =
    {
        (new Regex(@"\bLicence\b"), "License"),
        (new Regex(@"\blicence\b"), "license"),
        (new Regex(@"\bHonour\b"), "Honor"),
        (new Regex(@"\bhonour\b"), "honor"),
        (new Regex(@"\btravelling\b"), "traveling"),
        (new Regex(@"\bcentre\b"), "center"),
        (new Regex(@"\bCentre\b"), "Center"),
        (new Regex(@"\bcolour\b"), "color"),
        (new Regex(@"\bfavourite\b"), "favorite"),
        (new Regex(@"\bcancelled\b"), "canceled"),
    };

    public static void Load(string directory)
    {
        var path = Path.Combine(directory, "fr-CA.json");
        if (!File.Exists(path))
        {
            return;
        }

        _french = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? new();
    }

    public static string Culture => CultureInfo.CurrentUICulture.Name;

    public static bool IsFrench => Culture.StartsWith("fr", StringComparison.OrdinalIgnoreCase);

    public static string T(string english)
    {
        if (IsFrench)
        {
            return _french.TryGetValue(english, out var french) && !string.IsNullOrEmpty(french) ? french : english;
        }

        if (Culture == "en-US")
        {
            var text = english;
            foreach (var (pattern, replacement) in UsSpelling)
            {
                text = pattern.Replace(text, replacement);
            }

            return text;
        }

        return english;
    }

    /// <summary>Stored document-check summaries are canonical English; translate the leading sentence for display.</summary>
    private static readonly string[] CheckSentences =
    {
        "Document passed the automatic check.",
        "Document needs a manual review.",
        "Document didn't pass the automatic check.",
        "Automatic check isn't set up. Your document will be reviewed manually.",
        "Automatic checks only run on photos. PDFs are reviewed manually.",
        "The automatic check couldn't run. Your document will be reviewed manually.",
    };

    public static string CheckSummary(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return "";
        }

        foreach (var sentence in CheckSentences)
        {
            if (stored.StartsWith(sentence, StringComparison.Ordinal))
            {
                return T(sentence) + stored[sentence.Length..];
            }
        }

        return stored;
    }

    public static string T(string english, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(english), args);
}
