# Security & Threat Model

Dialogue files and localization tables may originate from external assets. The library mitigates risks as follows:

- **Input Size Caps**: `DialogueGraph.Load` rejects JSON payloads over `MaxJsonBytes` (default 1 MiB), graphs with more than `MaxNodes` (10,000), choice nodes with more than `MaxOptionsPerChoice` (32), and expressions longer than `MaxExpressionLength` (1,000 characters) with `DialogueException` (`Code = "input_too_large"`).
- **Stack Overflow Protection**: The recursive-descent expression parser tracks AST depth and rejects expressions nested deeper than `MaxExpressionDepth` (32 levels) with `Code = "too_deep"`.
- **Infinite Loop Protection**: `DialogueRunner` caps consecutive non-interactive (`set` and `branch`) node transitions at `MaxLoopSteps` (1,000) and throws `Code = "loop_limit"`.
- **Arithmetic Safety**: `int` operations use checked arithmetic; overflows throw `Code = "overflow"`, division/modulo by zero throw `Code = "division_by_zero"`.
- **No Dynamic Code**: The expression engine uses a fixed AST and static type-checking.
- **Safe Error Messages**: `DialogueException` messages are truncated to 200 characters.
