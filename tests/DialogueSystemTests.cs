using System.Text.Json;
using Xunit;

namespace Trysac.UnityDialogueSystem.Tests;

/// <summary>
/// Acceptance and unit tests for the dialogue system.
/// </summary>
public class DialogueSystemTests
{
    private static string ReadFixture(string fileName)
    {
        var baseDir = AppContext.BaseDirectory;
        var candidate = Path.Combine(baseDir, "fixtures", fileName);
        if (File.Exists(candidate))
        {
            return File.ReadAllText(candidate);
        }

        var repoPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "fixtures", fileName));
        return File.ReadAllText(repoPath);
    }

    private static Dictionary<string, string> LoadTable(string fileName)
    {
        var json = ReadFixture(fileName);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
    }

    private static Localizer CreateGuardLocalizer(string locale = "en")
    {
        var tables = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = LoadTable("guard.en.json"),
            ["es"] = LoadTable("guard.es.json"),
        };
        return new Localizer(tables, locale, "en");
    }

    [Fact]
    public void Acceptance1GuardSampleWithLowGoldHidesPayChoice()
    {
        var graph = DialogueGraph.Load(ReadFixture("guard.json"));
        Assert.Equal("start", graph.StartNode);
        Assert.Equal(["start", "ask", "pay", "thanks", "talk", "end"], graph.NodeIds);

        var vars = new VariableStore(graph);
        vars.Set("gold", 5);
        var runner = new DialogueRunner(graph, vars, CreateGuardLocalizer("en"));

        var step1 = runner.Start();
        Assert.Equal(StepKind.Line, step1.Kind);
        Assert.Equal("Guard", step1.Speaker);
        Assert.Equal("Halt! The toll is 10 gold.", step1.Text);

        var step2 = runner.Advance();
        Assert.Equal(StepKind.Choice, step2.Kind);
        Assert.Equal(["Talk", "Leave"], step2.Choices);

        // Choosing "Talk" loops back to "ask"
        var step3 = runner.Choose(0);
        Assert.Equal(StepKind.Line, step3.Kind);
        Assert.Equal("Rules are rules.", step3.Text);

        var step4 = runner.Advance();
        Assert.Equal(StepKind.Choice, step4.Kind);
        Assert.Equal(["Talk", "Leave"], step4.Choices);

        // Choosing "Leave" ends the conversation
        var step5 = runner.Choose(1);
        Assert.Equal(StepKind.End, step5.Kind);
    }

    [Fact]
    public void Acceptance2GuardSampleWithEnoughGoldPaysAndEnds()
    {
        var graph = DialogueGraph.Load(ReadFixture("guard.json"));
        var vars = new VariableStore(graph);
        vars.Set("gold", 25);
        var runner = new DialogueRunner(graph, vars, CreateGuardLocalizer("en"));

        var step1 = runner.Start();
        Assert.Equal("Halt! The toll is 10 gold.", step1.Text);

        var step2 = runner.Advance();
        Assert.Equal(StepKind.Choice, step2.Kind);
        Assert.Equal(["Pay", "Talk", "Leave"], step2.Choices);

        var step3 = runner.Choose(0); // "Pay"
        Assert.Equal(15, vars.Get("gold"));
        Assert.Equal(true, vars.Get("paid"));
        Assert.Equal(StepKind.Line, step3.Kind);
        Assert.Equal("Guard", step3.Speaker);
        Assert.Equal("Thank you, traveller.", step3.Text);

        var step4 = runner.Advance();
        Assert.Equal(StepKind.End, step4.Kind);
    }

    [Fact]
    public void Acceptance3ConditionsAndIntegerDivision()
    {
        var vars = new VariableStore()
            .Declare("gold", 15)
            .Declare("paid", true)
            .Declare("name", "Ana");

        Assert.True(Condition.Evaluate("gold >= 10 && !(paid == false) || name == 'Ana'", vars));
        Assert.True(Condition.Evaluate("gold % 4 == 3", vars));
        Assert.True(Condition.Evaluate("name + '!' == 'Ana!' && name != 'Bob'", vars));
        Assert.True(Condition.Evaluate("-gold == -15 && gold > 10 && gold <= 15 && gold < 20 && 3 * 4 - 2 == 10", vars));
        Assert.False(Condition.Evaluate("false && (1 / 0 == 0)", vars)); // short-circuit &&
        Assert.True(Condition.Evaluate("true || (1 / 0 == 0)", vars)); // short-circuit ||

        var json = """
        {
          "start": "s",
          "variables": { "x": { "type": "int", "default": 0 } },
          "nodes": [
            { "id": "s", "kind": "set", "assign": [ "x = 7 / 2" ], "next": "e" },
            { "id": "e", "kind": "end" }
          ]
        }
        """;
        var graph = DialogueGraph.Load(json);
        var runVars = new VariableStore(graph);
        var runner = new DialogueRunner(graph, runVars, CreateGuardLocalizer());
        var endStep = runner.Start();
        Assert.Equal(StepKind.End, endStep.Kind);
        Assert.Equal(3, runVars.Get("x"));
    }

    [Fact]
    public void Acceptance4LocalizationFallbackAndPlaceholders()
    {
        var tables = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["es"] = new Dictionary<string, string>
            {
                ["only.es"] = "Tienes {gold} monedas ({{literal}}) y {unknown} {paid} {name}",
            },
            ["en"] = new Dictionary<string, string>
            {
                ["only.en"] = "Fallback English {gold}",
            },
        };

        var vars = new VariableStore()
            .Declare("gold", 42)
            .Declare("paid", true)
            .Declare("name", "Ana");

        var localizer = new Localizer(tables, "es-MX", "en");
        Assert.Equal("Tienes 42 monedas ({literal}) y {unknown} true Ana", localizer.Text("only.es", vars));
        Assert.Equal("Fallback English 42", localizer.Text("only.en", vars));
        Assert.Equal("[guard.missing]", localizer.Text("guard.missing", vars));
    }

    [Fact]
    public void Acceptance5LoadValidationErrors()
    {
        // unknown_node
        var unknownNodeJson = """
        {
          "start": "n1",
          "nodes": [
            { "id": "n1", "kind": "line", "speaker": "G", "text": "t", "next": "missing_target" }
          ]
        }
        """;
        var ex1 = Assert.Throws<DialogueException>(() => DialogueGraph.Load(unknownNodeJson));
        Assert.Equal("unknown_node", ex1.Code);
        Assert.Contains("missing_target", ex1.Message);

        // invalid_expression
        var invalidExprJson = """
        {
          "start": "n1",
          "variables": { "gold": { "type": "int", "default": 0 } },
          "nodes": [
            { "id": "n1", "kind": "branch", "condition": "gold >= == 10", "then": "e", "else": "e" },
            { "id": "e", "kind": "end" }
          ]
        }
        """;
        var ex2 = Assert.Throws<DialogueException>(() => DialogueGraph.Load(invalidExprJson));
        Assert.Equal("invalid_expression", ex2.Code);

        // unknown_variable
        var unknownVarJson = """
        {
          "start": "n1",
          "variables": { "gold": { "type": "int", "default": 0 } },
          "nodes": [
            { "id": "n1", "kind": "branch", "condition": "silver >= 10", "then": "e", "else": "e" },
            { "id": "e", "kind": "end" }
          ]
        }
        """;
        var ex3 = Assert.Throws<DialogueException>(() => DialogueGraph.Load(unknownVarJson));
        Assert.Equal("unknown_variable", ex3.Code);

        // type_mismatch (comparing int with bool)
        var typeMismatchJson = """
        {
          "start": "n1",
          "variables": {
            "gold": { "type": "int", "default": 0 },
            "paid": { "type": "bool", "default": false }
          },
          "nodes": [
            { "id": "n1", "kind": "branch", "condition": "gold == paid", "then": "e", "else": "e" },
            { "id": "e", "kind": "end" }
          ]
        }
        """;
        var ex4 = Assert.Throws<DialogueException>(() => DialogueGraph.Load(typeMismatchJson));
        Assert.Equal("type_mismatch", ex4.Code);

        // duplicate_node
        var dupNodeJson = """
        {
          "start": "n1",
          "nodes": [
            { "id": "n1", "kind": "end" },
            { "id": "n1", "kind": "end" }
          ]
        }
        """;
        var ex5 = Assert.Throws<DialogueException>(() => DialogueGraph.Load(dupNodeJson));
        Assert.Equal("duplicate_node", ex5.Code);
    }

    [Fact]
    public void Acceptance6LoopLimitInputTooLargeAndTooDeep()
    {
        // Cycle of set and branch running 1_000 nodes
        var loopJson = """
        {
          "start": "line0",
          "variables": { "i": { "type": "int", "default": 0 } },
          "nodes": [
            { "id": "line0", "kind": "line", "speaker": "G", "text": "t", "next": "inc" },
            { "id": "inc", "kind": "set", "assign": [ "i = i + 1" ], "next": "check" },
            { "id": "check", "kind": "branch", "condition": "i >= 0", "then": "inc", "else": "end" },
            { "id": "end", "kind": "end" }
          ]
        }
        """;
        var loopGraph = DialogueGraph.Load(loopJson);
        var loopRunner = new DialogueRunner(loopGraph, new VariableStore(loopGraph), CreateGuardLocalizer());
        loopRunner.Start();
        var loopEx = Assert.Throws<DialogueException>(() => loopRunner.Advance());
        Assert.Equal("loop_limit", loopEx.Code);

        // JSON of 1_048_577 bytes throws input_too_large
        var largePayload = new string(' ', 1_048_577);
        var sizeEx = Assert.Throws<DialogueException>(() => DialogueGraph.Load(largePayload));
        Assert.Equal("input_too_large", sizeEx.Code);

        // Expression nested 33 levels deep throws too_deep
        var deepExpr = new string('(', 33) + "true" + new string(')', 33);
        var vars = new VariableStore();
        var deepEx = Assert.Throws<DialogueException>(() => Condition.Evaluate(deepExpr, vars));
        Assert.Equal("too_deep", deepEx.Code);
    }

    [Fact]
    public void Acceptance7InvalidChoiceFinishedAndStateRestoreRoundTrip()
    {
        var graph = DialogueGraph.Load(ReadFixture("guard.json"));
        var vars = new VariableStore(graph);
        vars.Set("gold", 5);
        var localizer = CreateGuardLocalizer("en");
        var runner = new DialogueRunner(graph, vars, localizer);

        runner.Start();
        var choiceStep = runner.Advance();
        Assert.Equal(2, choiceStep.Choices.Count);

        // Choose(2) when only 2 choices are visible throws invalid_choice
        var choiceEx = Assert.Throws<DialogueException>(() => runner.Choose(2));
        Assert.Equal("invalid_choice", choiceEx.Code);

        // Snapshot state at the choice step, then mutate gold so Pay becomes visible on a restored runner
        vars.Set("gold", 25);
        var savedState = runner.State();
        var stateJson = JsonSerializer.Serialize(savedState);
        var deserializedState = JsonSerializer.Deserialize<RunnerState>(stateJson)!;

        // Original runner leaves
        var endStep = runner.Choose(2); // now 3 choices are visible because gold = 25; index 2 is Leave
        Assert.Equal(StepKind.End, endStep.Kind);

        // Advance after the end throws finished
        var finEx = Assert.Throws<DialogueException>(() => runner.Advance());
        Assert.Equal("finished", finEx.Code);

        // Restore into a second runner and verify remaining steps match
        var vars2 = new VariableStore(graph);
        var runner2 = new DialogueRunner(graph, vars2, localizer);
        runner2.Restore(deserializedState);

        var payStep = runner2.Choose(0); // Pay
        Assert.Equal(StepKind.Line, payStep.Kind);
        Assert.Equal("Thank you, traveller.", payStep.Text);
        Assert.Equal(15, vars2.Get("gold"));
        Assert.Equal(true, vars2.Get("paid"));

        var endStep2 = runner2.Advance();
        Assert.Equal(StepKind.End, endStep2.Kind);
    }

    [Fact]
    public void RuntimeArithmeticOverflowAndDivisionByZero()
    {
        var vars = new VariableStore().Declare("x", int.MaxValue).Declare("zero", 0);

        var overflowEx = Assert.Throws<DialogueException>(() => Condition.Evaluate("x + 1 > 0", vars));
        Assert.Equal("overflow", overflowEx.Code);

        var subOverflowEx = Assert.Throws<DialogueException>(() => Condition.Evaluate("-x - 2 > 0", vars));
        Assert.Equal("overflow", subOverflowEx.Code);

        var mulOverflowEx = Assert.Throws<DialogueException>(() => Condition.Evaluate("x * 2 > 0", vars));
        Assert.Equal("overflow", mulOverflowEx.Code);

        var divZeroEx = Assert.Throws<DialogueException>(() => Condition.Evaluate("10 / zero == 0", vars));
        Assert.Equal("division_by_zero", divZeroEx.Code);

        var modZeroEx = Assert.Throws<DialogueException>(() => Condition.Evaluate("10 % zero == 0", vars));
        Assert.Equal("division_by_zero", modZeroEx.Code);
    }

    [Fact]
    public void VariableStoreAndErrorHandlingEdgeCases()
    {
        var store = new VariableStore()
            .Declare("a", 1)
            .Declare("b", false)
            .Declare("c", "hello");

        store.Set("a", 2);
        store.Set("b", true);
        store.Set("c", "world");
        Assert.Equal(2, store.Get("a"));
        Assert.Equal(true, store.Get("b"));
        Assert.Equal("world", store.Get("c"));

        Assert.Equal("unknown_variable", Assert.Throws<DialogueException>(() => store.Get("nope")).Code);
        Assert.Equal("unknown_variable", Assert.Throws<DialogueException>(() => store.Set("nope", 1)).Code);
        Assert.Equal("type_mismatch", Assert.Throws<DialogueException>(() => store.Set("a", true)).Code);
        Assert.Equal("type_mismatch", Assert.Throws<DialogueException>(() => store.Set("b", "str")).Code);
        Assert.Equal("type_mismatch", Assert.Throws<DialogueException>(() => store.Set("c", 10)).Code);
        Assert.Equal("type_mismatch", Assert.Throws<DialogueException>(() => store.Declare("a", true)).Code);

        // Exception constructors
        var defaultEx = new DialogueException();
        Assert.Equal("invalid_graph", defaultEx.Code);
        var msgEx = new DialogueException("custom msg");
        Assert.Equal("custom msg", msgEx.Message);
        var inner = new InvalidOperationException("inner");
        var wrappedEx = new DialogueException("wrapped", inner);
        Assert.Same(inner, wrappedEx.InnerException);
        var longMsgEx = new DialogueException("code", new string('x', 250));
        Assert.Equal(200, longMsgEx.Message.Length);
    }
}
