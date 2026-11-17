# Testing

## Running

```sh
dotnet test Library.sln
```

## Policy

- Tests exercise the public API including return values and thrown exceptions (`DialogueException` with specific codes).
- Property-based tests are included where applicable to ensure data invariants.
- All tests must be deterministic; no network access or clock dependence.

## Coverage

- Tests cover `DialogueGraph.Load` (parsing, limits, validation), `ExpressionParser` (lexer, AST, type checking), `VariableStore` (storage, seeding), `Localizer` (interpolation, fallback chain), and `DialogueRunner` (stepping, choice handling, state serialization).
- `Condition.Evaluate` is tested for boolean expressions across various variable states.
