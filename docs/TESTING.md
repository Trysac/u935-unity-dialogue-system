# Testing

## Running

```sh
dotnet test Library.sln                # xUnit and FsCheck tests
dotnet run --project scripts/Check --  # every gate, as CI and the factory run it
```

## Policy

- Line coverage of the library (measured by coverlet) must stay at or above 80 %; the gate fails
  below it.
- Tests exercise the public API. They assert every observable effect: returned values, thrown
  exceptions (`Assert.Throws`) and their messages.
- Property tests (FsCheck `[Property]`) state invariants such as idempotence or round trips.
- Each bug fix starts with a failing test that reproduces it.
- Tests are deterministic: no network, no wall clock, no dependence on test order.

## Tiers

- Local tier (every gate run, coverage measured here): unit and property tests, plus conformance
  on small fixtures kept in the repository.
- CI tier: conformance that needs an external tool or a download. Mark the test class or method
  with `[Trait("Category", "conformance")]`; the tests gate filters the trait out
  (`Category!=conformance`) unless `scripts/Check` runs with `FABRICA_CI=1`, while the lint gate
  always compiles it. The CI `conformance` job sets `FABRICA_CI=1` and runs the full suite. Tools
  are installed by the optional `ci/conformance-setup.sh` (bash, CI only; verify every download
  against a pinned SHA-256), and tests return early with a clear message when their tool is
  missing.

## Layout

Tests live in `tests/` in the namespace `Trysac.UnityDialogueSystem.Tests`, one `*Tests.cs` class per
area. Fixtures and golden files go in `tests/fixtures/` and are copied next to the test assembly
(read them from `AppContext.BaseDirectory`).
