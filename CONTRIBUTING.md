# Contributing

Thanks for helping improve `Trysac.UnityDialogueSystem`.

## Workflow

1. Branch from `main`; never push to `main` directly.
2. `dotnet restore Library.sln --locked-mode` to fetch dependencies.
3. Write a failing test, then the change.
4. `dotnet run --project scripts/Check --` must pass.
5. Update `CHANGELOG.md` under **Unreleased** and the docs the change affects.
6. Open a pull request describing the problem and the solution.

## Commit messages

Use [Conventional Commits](https://www.conventionalcommits.org/):

```text
feat: add strict mode to Normalize
fix: reject tab-only input
docs: document the thrown exceptions
```

`feat` bumps the minor version, `fix` the patch version, and a `BREAKING CHANGE:` footer the major
version. Releases are git tags `vMAJOR.MINOR.PATCH` matching the `<Version>` in `src/Library.csproj`.

## Code of conduct

Be respectful and constructive. Maintainers may close contributions that ignore this guide.
