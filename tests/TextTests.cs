using Xunit;

namespace Trysac.UnityDialogueSystem.Tests;

/// <summary>Tests of the public Text helper and additional expression/graph edge cases.</summary>
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

    [Theory]
    [InlineData("unterminated 'string", "invalid_expression")]
    [InlineData("1 + @", "invalid_expression")]
    [InlineData("(1 + 2", "invalid_expression")]
    [InlineData("1 2", "invalid_expression")]
    [InlineData("!5", "type_mismatch")]
    [InlineData("-true", "type_mismatch")]
    [InlineData("1 && true", "type_mismatch")]
    [InlineData("true < false", "type_mismatch")]
    [InlineData("'a' - 'b'", "type_mismatch")]
    [InlineData("'a' + 1", "type_mismatch")]
    [InlineData("1 + 2", "type_mismatch")]
    [InlineData("99999999999999999999 == 1", "overflow")]
    public void ExpressionParserAndTypeCheckerRejectsInvalidInputs(string expr, string expectedCode)
    {
        var vars = new VariableStore();
        var ex = Assert.Throws<DialogueException>(() => Condition.Evaluate(expr, vars));
        Assert.Equal(expectedCode, ex.Code);
    }

    [Fact]
    public void BranchNodeSelectsThenAndElsePaths()
    {
        var json = """
        {
          "start": "b",
          "variables": { "flag": { "type": "bool", "default": true } },
          "nodes": [
            { "id": "b", "kind": "branch", "condition": "flag", "then": "l1", "else": "l2" },
            { "id": "l1", "kind": "line", "speaker": "S", "text": "k1", "next": "e" },
            { "id": "l2", "kind": "line", "speaker": "S", "text": "k2", "next": "e" },
            { "id": "e", "kind": "end" }
          ]
        }
        """;
        var graph = DialogueGraph.Load(json);
        var tables = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string> { ["k1"] = "Then branch", ["k2"] = "Else branch" },
        };
        var loc = new Localizer(tables, "en");

        var vars1 = new VariableStore(graph);
        var runner1 = new DialogueRunner(graph, vars1, loc);
        Assert.Equal("Then branch", runner1.Start().Text);

        var vars2 = new VariableStore(graph);
        vars2.Set("flag", false);
        var runner2 = new DialogueRunner(graph, vars2, loc);
        Assert.Equal("Else branch", runner2.Start().Text);
    }
}
