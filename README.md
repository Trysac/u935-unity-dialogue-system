# Trysac.UnityDialogueSystem

Engine-independent branching game dialogue runtime for Unity and .NET. Loads JSON dialogue graphs with typed variables (`int`, `bool`, `string`), validates every node reference and expression at load time, evaluates bounded conditions and assignments, and resolves localized text with locale fallback and placeholder interpolation.

## Why versus Ink or Yarn Spinner

- **Zero custom compiler step**: Dialogue graphs are plain JSON validated in memory when loaded.
- **Static type-checking at load**: Typos in node IDs, undeclared variable names, or comparing an `int` with a `bool` throw `DialogueException` during `DialogueGraph.Load` before play starts.
- **Bounded execution**: Enforces configurable caps on JSON payload size (1 MiB), node count (10,000), options per choice (32), expression length (1,000 chars), AST depth (32), and non-interactive node loops (1,000 steps).

## Usage

```csharp
using Trysac.UnityDialogueSystem;

var graph = DialogueGraph.Load(File.ReadAllText("tests/fixtures/guard.json"));
var vars = new VariableStore(graph);
vars.Set("gold", 25);
var tables = new Dictionary<string, IReadOnlyDictionary<string, string>> {
    ["en"] = new Dictionary<string, string> { ["guard.halt"] = "Halt! The toll is 10 gold.", ["choice.pay"] = "Pay" }
};
var runner = new DialogueRunner(graph, vars, new Localizer(tables, "en"));
DialogueStep step = runner.Start();
```

## Public API Summary

- **`DialogueGraph`**: Loads and validates JSON graphs. Properties: `IReadOnlyList<string> NodeIds`, `string StartNode`.
- **`DialogueRunner`**: Executes dialogue steps via `Start`, `Advance`, `Choose`, `State`, and `Restore`.
- **`DialogueStep`**: Data record for steps (`Line`, `Choice`, `End`).
- **`VariableStore`**: Typed storage (`int`, `bool`, `string`) for conversation state.
- **`Localizer`**: Handles translation tables and variable interpolation.
- **Errors**: All operations may throw `DialogueException` with codes like `unknown_node`, `invalid_expression`, `unknown_variable`, `type_mismatch`, `loop_limit`, `input_too_large`, `too_deep`, `invalid_choice`, `finished`, `invalid_graph`, `duplicate_node`, `overflow`, or `division_by_zero`.

See [docs/SECURITY.md](docs/SECURITY.md) for threat modeling and resource limits.
