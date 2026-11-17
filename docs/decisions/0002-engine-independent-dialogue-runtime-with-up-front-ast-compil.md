# Engine-independent dialogue runtime with up-front AST compilation and type checking

Status: accepted (generated, pending human review)

## Context

Narrative games require branching conversations gated by game state and localized across languages, but errors in node links, variable names, or condition types are often only discovered at runtime during manual playtesting, and Unity-coupled dialogue assets are slow to test outside the editor.

## Decision

Implement an engine-independent C# runtime using System.Text.Json and a hand-written recursive-descent expression parser that compiles and statically type-checks all conditions and assignments during DialogueGraph.Load, paired with bounded execution limits and a serializable RunnerState.

## Consequences

Every broken node reference, syntax error, undeclared variable, and type mismatch is caught immediately on load before gameplay begins, and full dialogue flows can be verified deterministically in fast xUnit tests without requiring the Unity editor.
