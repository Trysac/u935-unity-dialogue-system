using Xunit;

namespace Trysac.UnityDialogueSystem.Tests;

/// <summary>Tests of the public API.</summary>
public class TextTests
{
    [Theory]
    [InlineData("  Hello World ", "hello world")]
    [InlineData("ABC", "abc")]
    [InlineData("\tx\n", "x")]
    public void TrimsAndLowercases(string input, string want)
    {
        Assert.Equal(want, Text.Normalize(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void RejectsBlankInput(string input)
    {
        var error = Assert.Throws<BlankInputException>(() => Text.Normalize(input));
        Assert.Equal("input is blank", error.Message);
    }

    [Fact]
    public void RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => Text.Normalize(null!));
    }

    [Fact]
    public void TheExceptionKeepsItsMessageAndCause()
    {
        var cause = new InvalidOperationException("cause");
        Assert.Equal("custom", new BlankInputException("custom").Message);
        Assert.Same(cause, new BlankInputException("custom", cause).InnerException);
    }
}
