---
name: avalonia-ui
description: Implement UniGetUI Avalonia UI changes with correct View/ViewModel conventions, UI-thread dispatch, bindings, themes, and accessibility. Use when editing AXAML, controls, or app shell behavior.
---

# avalonia ui

Use for UniGetUI Avalonia work under `src/UniGetUI.Avalonia/`.

## Conventions

- Keep View/ViewModel separation; Views bind, ViewModels own state. Follow existing `Views/MainWindow.axaml` and `Program.cs` patterns.
- Dispatch UI updates on the UI thread; never touch visual state from background package-manager threads directly.
- Prefer compiled bindings and existing theme/resources; do not fork new theme dictionaries for one screen.
- Localize every user-facing string with `CoreTools.Translate`; in XAML use the `TranslatedTextBlock` control.

## Checks

- Keep Avalonia diagnostics gating intact: `EnableAvaloniaDiagnostics` in `src/Directory.Build.props`, compile gate `#if AVALONIA_DIAGNOSTICS_ENABLED` in `Program.cs`, runtime precedence CLI flags then `UNIGETUI_AVALONIA_DEVTOOLS` then `Auto`.
- Keep `Auto` WSL-safe (DevTools off by default on WSL); runtime toggle without build support logs a no-op warning.
- Verify XAML compiles via the relevant build in the [dotnet-build-test](../dotnet-build-test/SKILL.md) skill; run targeted tests before the full suite.
- Check keyboard navigation, contrast, and screen-reader names for new controls.

## Out of scope

- Package-manager logic belongs in the [package-manager-integration](../package-manager-integration/SKILL.md) skill.
- WinGet COM specifics belong in the [winget-native](../winget-native/SKILL.md) skill.
