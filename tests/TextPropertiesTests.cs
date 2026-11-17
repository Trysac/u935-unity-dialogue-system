using FsCheck;
using FsCheck.Xunit;

namespace Trysac.UnityDialogueSystem.Tests;

/// <summary>Property tests (FsCheck) of the invariants of <see cref="Text.Normalize"/>.</summary>
public class TextPropertiesTests
{
    [Property]
    public bool NormalizingTwiceChangesNothing(NonNull<string> input)
    {
        if (string.IsNullOrWhiteSpace(input.Get))
        {
            return true;
        }
        var once = Text.Normalize(input.Get);
        return once == Text.Normalize(once);
    }

    [Property]
    public bool OutputHasNoSurroundingWhitespace(NonNull<string> input)
    {
        if (string.IsNullOrWhiteSpace(input.Get))
        {
            return true;
        }
        var output = Text.Normalize(input.Get);
        return output == output.Trim();
    }
}
