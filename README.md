# unity-dialogue-system

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![CI](https://github.com/Trysac/u935-unity-dialogue-system/actions/workflows/ci.yml/badge.svg)](https://github.com/Trysac/u935-unity-dialogue-system/actions/workflows/ci.yml)

Run branching game dialogues from JSON graphs with typed variables, a bounded condition language, assignment nodes and localized lines with locale fallback

## Problem

Describe in two or three sentences the concrete problem this utility solves, who hits it, and what
they do today without it. Replace this paragraph when the first real feature lands.

## Install

NuGet package `Trysac.UnityDialogueSystem`. Until it is published to nuget.org, build it from a release
tag with `dotnet pack src/Library.csproj`, then reference the package:

```xml
<PackageReference Include="Trysac.UnityDialogueSystem" Version="0.1.0" />
```

Requires .NET 10 or newer. No dependencies outside the base class library.

## Usage

```csharp
using Trysac.UnityDialogueSystem;

// Normalize trims surrounding whitespace and lower-cases the text.
var name = Text.Normalize("  Hello World "); // "hello world"

// Blank input is rejected explicitly instead of returning an empty string.
Text.Normalize("   "); // throws BlankInputException ("input is blank")
```

## Documentation

- [Architecture](docs/ARCHITECTURE.md) and [decisions](docs/decisions/)
- [Testing](docs/TESTING.md) and [security](docs/SECURITY.md)
- [Project record](docs/PROJECT.md) and [stack](docs/STACK.md)
- [Changelog](CHANGELOG.md) and [contributing guide](CONTRIBUTING.md)

## License

[MIT](LICENSE) (c) 2026 Miguel Gomez
