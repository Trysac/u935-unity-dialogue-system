namespace Trysac.UnityDialogueSystem;

/// <summary>Text helpers of this library.</summary>
public static class Text
{
    /// <summary>Trims surrounding whitespace and lower-cases the text.</summary>
    /// <param name="input">The text to normalize.</param>
    /// <returns>The trimmed, lower-cased text.</returns>
    /// <exception cref="BlankInputException">Nothing is left after trimming, instead of an empty string.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
    public static string Normalize(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var trimmed = input.Trim();
        if (trimmed.Length == 0)
        {
            throw new BlankInputException();
        }
        return trimmed.ToLowerInvariant();
    }
}
