# AGENTS.md

Instructions for coding agents (and humans) working on the NuGet package `Trysac.UnityDialogueSystem`
(namespace `Trysac.UnityDialogueSystem`).

## Commands

| Task | Command |
|---|---|
| Restore dependencies | `dotnet restore Library.sln --locked-mode` |
| Run every gate (must pass before a PR) | `dotnet run --project scripts/Check --` |
| Tests only | `dotnet test Library.sln` |
| Format | `dotnet format Library.sln` |
| Lint | `dotnet build Library.sln` (analyzers, nullable and code style, warnings are errors) |
| Coverage | `dotnet run --project scripts/Check -- tests` |

The gate sequence is: `dotnet format --verify-no-changes` (whitespace, code style and analyzer
fixes) after a locked-mode restore, the build with the .NET analyzers (`latest-recommended`),
nullable reference types and the `.editorconfig` code style, every warning an error, Semgrep (the
factory's rules in `.semgrep/`), xUnit and FsCheck tests under coverlet with line coverage >= 80 %
of the library, documentation manifest (`docs-manifest.json`), gitleaks, `dotnet list package
--vulnerable` over every resolved package, and OSV-Scanner over each `packages.lock.json`. It stops
at the first failure.

## Folder map

| Path | Content |
|---|---|
| `src/` | The library (`Library.csproj`, assembly `Trysac.UnityDialogueSystem`); its public types are the public API. |
| `tests/` | xUnit tests (`*Tests.cs`) and FsCheck property tests (`Library.Tests.csproj`). |
| `tests/fixtures/` | Optional fixtures and golden files used by tests (copied next to the test assembly). |
| `scripts/` | The single definition of the quality gates (`Check/`) and the factory's dependency tool. |
| `docs/` | Architecture, decisions (ADRs), testing, security, project record, stack. |
| `fabrica.json` | Install and check commands used by the factory and CI. |
| `docs-manifest.json` | Documentation files the docs gate requires. |
| `.github/` | CI wrapper and Dependabot configuration. |

## Conventions

- C# 14 on .NET 10 LTS (`net10.0`), nullable reference types enabled, file-scoped namespaces, formatted
  by `dotnet format` with `.editorconfig`, clean under the .NET analyzers.
- Standard library only unless an ADR in `docs/decisions/` justifies a dependency.
- Every public type and member has an XML documentation comment; invalid input throws a documented
  `ArgumentException` subclass, never returns null or a sentinel value.
- Write the failing test first; tests use every literal of the acceptance items verbatim, and
  FsCheck properties (`[Property]`) cover invariants.
- CI-only conformance tests (external tools or downloads) carry
  `[Trait("Category", "conformance")]`; only CI runs them.
- Every behavior change updates `README.md` usage, `CHANGELOG.md` (Unreleased) and, if it is a
  design choice, a new ADR.
- Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`).

## Limits

- Never push to `main`; work on a branch and open a pull request.
- Do NOT edit `.github/`, `fabrica.json`, `docs-manifest.json`, `scripts/`, the `.csproj` and `.sln`
  files, `Directory.Build.props`, `global.json`, `.editorconfig`, the `packages.lock.json` files or
  any tool configuration. They are the quality contract; changes to them are made by a human.
- Never lower the coverage threshold, add `#pragma warning disable` or `[SuppressMessage]` to
  silence a warning, or skip a test to make a gate pass.
- Never commit secrets, tokens or `.env` files; gitleaks blocks them.
