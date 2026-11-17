using System.Globalization;
using System.Text;

namespace Trysac.UnityDialogueSystem;

/// <summary>
/// Resolves localization keys across a locale fallback chain and interpolates variable placeholders.
/// </summary>
public sealed class Localizer
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _tables;
    private readonly List<string> _localeChain;

    /// <summary>
    /// Initializes a new instance of the <see cref="Localizer"/> class.
    /// </summary>
    /// <param name="tables">Map of locale codes (e.g. <c>"en"</c>, <c>"es"</c>) to key-value translation tables.</param>
    /// <param name="locale">The active locale tag (e.g. <c>"es-MX"</c>).</param>
    /// <param name="fallbackLocale">The fallback locale tag (defaults to <c>"en"</c>).</param>
    public Localizer(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> tables,
        string locale,
        string fallbackLocale = "en")
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(locale);
        ArgumentNullException.ThrowIfNull(fallbackLocale);

        _tables = tables;
        _localeChain = BuildLocaleChain(locale, fallbackLocale);
    }

    /// <summary>
    /// Resolves the localized text for <paramref name="key"/> and substitutes <c>{variable}</c> placeholders.
    /// </summary>
    /// <param name="key">The translation key.</param>
    /// <param name="variables">The variable store used for placeholder substitution.</param>
    /// <returns>The resolved and formatted text, or <c>[key]</c> if the key is missing from all fallback locales.</returns>
    public string Text(string key, VariableStore variables)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(variables);

        string? template = ResolveTemplate(key);
        if (template is null)
        {
            return $"[{key}]";
        }

        return Interpolate(template, variables);
    }

    private static List<string> BuildLocaleChain(string locale, string fallbackLocale)
    {
        var chain = new List<string>(3);
        if (!string.IsNullOrEmpty(locale))
        {
            chain.Add(locale);
            int dashIndex = locale.IndexOf('-', StringComparison.Ordinal);
            if (dashIndex > 0)
            {
                var parent = locale[..dashIndex];
                if (!chain.Contains(parent, StringComparer.Ordinal))
                {
                    chain.Add(parent);
                }
            }
        }

        if (!string.IsNullOrEmpty(fallbackLocale) && !chain.Contains(fallbackLocale, StringComparer.Ordinal))
        {
            chain.Add(fallbackLocale);
        }

        return chain;
    }

    private string? ResolveTemplate(string key)
    {
        foreach (var loc in _localeChain)
        {
            if (_tables.TryGetValue(loc, out var table) && table is not null && table.TryGetValue(key, out var val))
            {
                return val;
            }
        }

        return null;
    }

    private static string Interpolate(string template, VariableStore variables)
    {
        if (!template.Contains('{', StringComparison.Ordinal) && !template.Contains('}', StringComparison.Ordinal))
        {
            return template;
        }

        var sb = new StringBuilder(template.Length + 16);
        int i = 0;
        int n = template.Length;

        while (i < n)
        {
            char c = template[i];
            if (c == '{')
            {
                if (i + 1 < n && template[i + 1] == '{')
                {
                    sb.Append('{');
                    i += 2;
                    continue;
                }

                int close = template.IndexOf('}', i + 1);
                if (close > i + 1)
                {
                    var varName = template[(i + 1)..close];
                    if (variables.TryGetValue(varName, out var val) && val is not null)
                    {
                        sb.Append(FormatValue(val));
                    }
                    else
                    {
                        sb.Append('{').Append(varName).Append('}');
                    }
                    i = close + 1;
                    continue;
                }
            }
            else if (c == '}')
            {
                if (i + 1 < n && template[i + 1] == '}')
                {
                    sb.Append('}');
                    i += 2;
                    continue;
                }
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }

    private static string FormatValue(object val)
    {
        return val switch
        {
            int i => i.ToString(CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            string s => s,
            _ => Convert.ToString(val, CultureInfo.InvariantCulture) ?? string.Empty,
        };
    }
}
