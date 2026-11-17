# Architecture

`Trysac.UnityDialogueSystem` separates graph validation, state storage, localization, and execution:

1. **`DialogueGraph`**: Parses the JSON document, enforces size and count limits (`DialogueLimits`), validates variable declarations, compiles all condition and assignment expressions into immutable AST nodes, type-checks every expression against the declared variables, and verifies that `start` and every `next` / `then` / `else` target exist.
2. **`ExpressionParser` & `Condition`**: Hand-written lexer and recursive-descent parser supporting `||`, `&&` (with short-circuit evaluation), `==`, `!=`, `<`, `<=`, `>`, `>=`, `+`, `-`, `*`, `/`, `%`, unary `!` and `-`, 32-bit checked integer arithmetic, boolean literals, single-quoted string literals, variable identifiers, and parentheses.
3. **`VariableStore`**: Strongly typed store for `int`, `bool`, and `string` variables.
4. **`Localizer`**: Resolves keys across the locale chain (`locale` -> parent language -> `fallbackLocale`), formats missing keys as `[key]`, and replaces `{name}` placeholders using invariant integer formatting while unescaping `{{` and `}}`.
5. **`DialogueRunner` & `RunnerState`**: Steps through interactive nodes (`line`, `choice`, `end`) while automatically evaluating non-interactive nodes (`set`, `branch`) with a loop limit guard of 1,000 steps.
