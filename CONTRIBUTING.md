# Contributing to MintedTextEditor

Thank you for your interest in contributing! This document outlines how to get involved.

## Code of Conduct

This project follows the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md). By participating, you agree to uphold it.

## Ways to Contribute

- **Bug reports** — Open a [GitHub Issue](https://github.com/TheEightBot/MintedTextEditor/issues) with reproduction steps, expected behavior, actual behavior, and platform/OS info.
- **Bug fixes** — Fork → branch → fix → tests → PR.
- **New features** — Open an issue first to discuss the design before writing code.
- **Documentation** — Improvements to `docs/`, `README.md`, or XML doc comments are always welcome.
- **Performance** — Layout, rendering, and text-shaping improvements with benchmark evidence.

## Development Setup

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- For `MintedTextEditor.Maui` and the sample app only: the .NET MAUI workload (`dotnet workload install maui`), plus macOS with Xcode for the iOS/Mac Catalyst targets, or Windows for the Windows target (which is only added when building on Windows)

`MintedTextEditor.Core`, `MintedTextEditor.SkiaSharp` and their tests are plain `net10.0` projects and build on any OS.

### Building

```bash
git clone https://github.com/TheEightBot/MintedTextEditor.git
cd MintedTextEditor
dotnet build MintedTextEditor.Core.slnf        # Core, SkiaSharp and their tests; any OS
dotnet build src/MintedTextEditor.Maui/        # needs the MAUI workload
```

`MintedTextEditor.Core.slnf` is a solution filter over `MintedTextEditor.slnx` with only the platform-independent projects; open it in Visual Studio or Rider when you don't have the MAUI workload. A plain `dotnet build` at the root builds the whole `MintedTextEditor.slnx`, which includes the MAUI library and the sample app, so it needs the MAUI workload.

### Running the Tests

```bash
dotnet test MintedTextEditor.Core.slnf
```

The SkiaSharp tests draw text and emoji with real fonts. Install a color emoji font first, or the emoji tests return early without checking anything; on Debian/Ubuntu: `sudo apt-get install fonts-noto-color-emoji`. On Linux the test project brings in the SkiaSharp and HarfBuzzSharp native assets itself.

All tests must pass before submitting a PR.

### Continuous Integration

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) runs on pushes and pull requests to `main` and `develop` (changes that only touch `*.md` files or `docs/` are skipped):

- **Build & Test (Core + SkiaSharp)** on Ubuntu: builds in Release, installs `fonts-noto-color-emoji`, runs both test projects, and does a pack dry-run of both packages.
- **Build (MAUI)** on Windows: restores the MAUI workload, builds `MintedTextEditor.Maui` in Release, and does a pack dry-run.

### Running the Sample App

```bash
cd samples/SampleApp.Maui
dotnet build -t:Run -f net10.0-maccatalyst  # macOS
dotnet build -t:Run -f net10.0-android       # Android (emulator required)
```

## Pull Request Process

1. **Fork** the repository and create a feature branch from `main`:
   ```bash
   git checkout -b feature/my-feature
   ```

2. **Write code** following the coding guidelines below.

3. **Write tests** — new features must include tests; bug fixes should add a regression test.

4. **Update documentation** — update `docs/` or `README.md` if your change affects public API or user-facing behavior.

5. **Add a changelog entry** — see [Changelog](#changelog) below.

6. **Ensure tests pass**:
   ```bash
   dotnet test tests/MintedTextEditor.Core.Tests/
   dotnet test tests/MintedTextEditor.SkiaSharp.Tests/
   ```

7. **Commit** using the format below.

8. **Open a Pull Request** against the `main` branch, describing what changed and why.

## README links

`README.md` is packed into every NuGet package and shown on nuget.org, where relative links don't resolve. Use absolute `https://github.com/TheEightBot/MintedTextEditor/blob/main/...` links (and `https://raw.githubusercontent.com/...` for images) in the README; CI rejects relative ones. Pages under `docs/` can link to each other relatively.

## Changelog

Every pull request that changes anything under `src/` must add an entry to [CHANGELOG.md](CHANGELOG.md) under `## [Unreleased]`, in the matching `### Added`, `### Changed`, `### Fixed` or `### Removed` section (add the section if it isn't there yet). Write it for users of the packages: what changed and how it affects them.

A CI check enforces this: it fails a pull request that changes `src/` without changing `CHANGELOG.md`, unless the PR has the `skip-changelog` label (for changes users won't notice, such as internal refactoring).

## Commit Message Format

Use the [Conventional Commits](https://www.conventionalcommits.org/) format:

```
<type>(<scope>): <summary>

[optional body]

[optional footer]
```

**Types**: `feat`, `fix`, `docs`, `test`, `refactor`, `perf`, `chore`, `ci`

**Examples**:
```
feat(tables): add cell merge/split support
fix(layout): correct line height for mixed font sizes
docs(formatting): add RTL direction examples
test(undo): add regression test for multi-step undo
```

## Coding Guidelines

- **Language**: C# 14 (the .NET 10 default), targeting `net10.0` (plus the `net10.0-*` platform targets in `MintedTextEditor.Maui`)
- **Nullable**: `<Nullable>enable</Nullable>` — all public APIs must be nullable-annotated
- **Style**: Follow the existing style (4-space indent, Allman braces)
- **XML docs**: All public types and members must have `<summary>` XML doc comments
- **Immutability**: `TextStyle` is an immutable class — create modified copies with its `With…` methods. `ParagraphStyle` is mutable; change it through `DocumentEditor.ApplyParagraphStyle` (or call `Document.NotifyChanged`) so the layout updates
- **No platform code in Core**: `MintedTextEditor.Core` must not reference any platform APIs or SkiaSharp
- **Platform input code**: goes in the matching `KeyboardProxyHandler.<Platform>.cs` partial file in `MintedTextEditor.Maui/Input` (`Android`, `Apple` for iOS and Mac Catalyst, `Windows`); the csproj compiles each file only for its platforms

## Licensing

By contributing, you agree that your contributions will be licensed under the MIT License.
