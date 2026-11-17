# Stack

## Used

| Tool | Why |
|---|---|
| .NET 10 (LTS) + C# 14 | `net10.0`, nullable reference types; the SDK version is pinned in `global.json`. |
| dotnet CLI + MSBuild | The standard build; shared settings in `Directory.Build.props`, one solution `Library.sln`. |
| dotnet format | Whitespace, code style (`.editorconfig`) and analyzer fixes; no options to argue about. |
| .NET analyzers (`latest-recommended`) | Quality, reliability and security rules plus code style in the build, warnings as errors. |
| xUnit + FsCheck | Example-based and property-based tests. |
| coverlet | Line coverage of the library; the gate reads its Cobertura report. |
| NuGet lock files | `packages.lock.json` per project, restored in locked mode: reproducible builds. |
| Semgrep CE + OSV-Scanner | The factory's own rules; OSV advisories over the lock files. |
| gitleaks | Secret scanning locally and in CI. |
| GitHub Actions + Dependabot | Thin CI wrapper over the local gates; weekly dependency updates. |

## Discarded

| Tool | Why not |
|---|---|
| `AnalysisMode` All | Hundreds of style-only rules that fight generated code; `latest-recommended` keeps the rules that find bugs. |
| StyleCop.Analyzers | `dotnet format` and `.editorconfig` cover layout without another package. |
| Central package management | One library and one test project; each project lists its own packages. |
| Shell scripts for gates | Not portable to Windows; the gates are a C# program in `scripts/Check/`. |
