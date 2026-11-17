# Security notes

## Threat model

`Trysac.UnityDialogueSystem` is a pure library without I/O. The main risks are:

- **Supply chain**: a compromised NuGet package, SDK or CI action. Mitigation: base class library
  only, every package pinned to an exact version with a committed `packages.lock.json` restored in
  locked mode, `dotnet list package --vulnerable` (the NuGet advisory database) and OSV-Scanner over
  the lock files in the gates, Dependabot weekly updates, actions pinned by commit SHA, minimal
  workflow permissions, the SDK pinned in `global.json`.
- **Leaked secrets**: tokens committed by mistake. Mitigation: `gitleaks` runs in the local gates
  and in CI; `.env` files are git-ignored.
- **Insecure code**: command injection, unsafe deserialization, disabled TLS validation.
  Mitigation: the .NET analyzers (`latest-recommended`, including the security rules) and nullable
  warnings as errors, and the factory's Semgrep rules (`.semgrep/factory-rules.yml`, `nosemgrep`
  ignored); fix the code instead of silencing it.
- **Untrusted input**: callers may pass hostile strings. Mitigation: validate input and throw
  explicit, documented exceptions; never deserialize untrusted bytes with `BinaryFormatter`;
  never start a shell with untrusted data.

## Publishing

Releases are git tags; publishing to nuget.org needs an account and an API key, which are never
stored in the repository.

To report a vulnerability, see [SECURITY.md](../SECURITY.md).
