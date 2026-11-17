using FsCheck;
using FsCheck.Xunit;

namespace Trysac.UnityDialogueSystem.Tests;

/// <summary>
/// Property-based tests for text normalization, expressions, and localization.
/// </summary>
public class TextPropertiesTests
{
    [Property]
    public bool NormalizedTextIsTrimmedAndLowercase(NonEmptyString raw)
    {
        var s = raw.Get;
        if (string.IsNullOrWhiteSpace(s))
        {
            return true;
        }

        var normalized = Text.Normalize(s);
        return string.Equals(normalized, normalized.Trim(), StringComparison.Ordinal) &&
               string.Equals(normalized, normalized.ToLowerInvariant(), StringComparison.Ordinal);
    }

    [Property]
    public bool IntegerAdditionCommutesInConditions(short a, short b)
    {
        var vars = new VariableStore()
            .Declare("a", a)
            .Declare("b", b);

        return Condition.Evaluate("a + b == b + a", vars);
    }

    [Property]
    public bool MissingLocalizationKeyAlwaysRendersBracketed(NonEmptyString rawKey)
    {
        var key = rawKey.Get;
        var localizer = new Localizer(
            new Dictionary<string, IReadOnlyDictionary<string, string>>(),
            "en");
        var vars = new VariableStore();
        return localizer.Text(key, vars) == $"[{key}]";
    }
}
