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
- .NET MAUI workload: `dotnet workload install maui`
- macOS with Xcode (for iOS/macOS targets) or Windows with VS 2022+ (for WinUI target)

### Building

```bash
git clone https://github.com/TheEightBot/MintedTextEditor.git
cd MintedTextEditor
dotnet build
```

### Running the Tests

```bash
dotnet test tests/MintedTextEditor.Core.Tests/
```

All 537+ tests must pass before submitting a PR.

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

5. **Ensure tests pass**:
   ```bash
   dotnet test tests/MintedTextEditor.Core.Tests/
   ```

6. **Commit** using the format below.

7. **Open a Pull Request** against the `main` branch. Fill out the PR template.

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

- **Language**: C# 13, targeting `net10.0`
- **Nullable**: `<Nullable>enable</Nullable>` — all public APIs must be nullable-annotated
- **Style**: Follow the existing style (4-space indent, Allman braces)
- **XML docs**: All public types and members must have `<summary>` XML doc comments
- **Immutability**: `TextStyle` and `ParagraphStyle` are value types — extend them via `with` expressions, not mutation
- **No platform code in Core**: `MintedTextEditor.Core` must not reference any platform APIs or SkiaSharp

## Licensing

By contributing, you agree that your contributions will be licensed under the MIT License.
