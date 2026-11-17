# Architecture

## Purpose

`Trysac.UnityDialogueSystem` is a small, dependency-free .NET library: Run branching game dialogues from JSON graphs with typed variables, a bounded condition language, assignment nodes and localized lines with locale fallback

## Shape

```text
caller code --> namespace Trysac.UnityDialogueSystem (src/)
                    |
                    +-- static classes with pure functions; no I/O, no global mutable state
                    +-- internal helpers (optional)
```

- **Public API**: every `public` type and member of the assembly. `internal` types may change
  without a major version.
- **Errors**: invalid input throws an `ArgumentException` subclass of the library (such as
  `BlankInputException`) documented with `<exception>`; null arguments throw
  `ArgumentNullException`. The library never writes to the console or exits.
- **Side effects**: none. If a feature needs I/O, accept a `Stream`, a `TextReader` or a small
  interface instead of opening resources, and record the choice in an ADR.

## Quality contract

`scripts/Check/` is the single definition of the gates; `fabrica.json` tells the factory and CI how
to run it. See [TESTING.md](TESTING.md).

## Decisions

Architecture decisions are recorded in [decisions/](decisions/) as numbered ADRs.
