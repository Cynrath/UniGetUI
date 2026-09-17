---
name: dotnet-build-test
description: Build and test UniGetUI .NET/Avalonia solutions safely on Windows x64 with evidence-based failure classification. Use when building, testing, or validating formatting for UniGetUI C# changes.
---

# dotnet build test

Use for any UniGetUI C# build, test, or format check. Windows-first, x64, .NET 10.

## Solutions

- `src/UniGetUI.Windows.slnx` is the official Windows solution.
- `src/UniGetUI.Avalonia.slnx` is the cross-platform Avalonia solution.
- Target framework is `net10.0-windows10.0.26100.0` (min `10.0.19041`); tests use xUnit.

## Commands

Restore and test from `src/`:

```powershell
dotnet restore UniGetUI.Windows.slnx
dotnet test UniGetUI.Windows.slnx --verbosity q --nologo /p:Platform=x64
```

Read-only format gates (never mutate blindly):

```powershell
dotnet format whitespace src --folder --verify-no-changes
dotnet format style UniGetUI.Windows.slnx --no-restore --verify-no-changes
```

## Rules

- Do not run a broad mutating `dotnet format` across the solution. Use the verified whitespace/style verify commands and inspect the diff.
- Run the repo pre-commit hook setup once after cloning with `pwsh ./scripts/install-git-hooks.ps1`.
- Build affected projects first, then the full Windows solution; run targeted tests before the full suite.
- Treat every `IL2xxx` trim and `IL3xxx` AOT warning as a defect; do not blanket-suppress.

## Failure classification

- Baseline first: capture failing tests on clean `origin/main` before attributing failures to the change.
- Classify as pre-existing only with matching baseline output; otherwise treat as regression.
- Record exact commands plus pass/fail counts in task Completion notes.

## Completion gate

- Relevant whitespace/style verify passes.
- Relevant build passes on x64.
- Relevant tests pass; full suite pass or baseline-classified failures documented.
