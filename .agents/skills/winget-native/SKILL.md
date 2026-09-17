---
name: winget-native
description: Work safely on UniGetUI WinGet native integration via Microsoft.Management.Deployment with CLI fallback. Use when changing WinGet COM paths, install options, sources, or Windows guards.
---

# winget native

Use for `src/UniGetUI.PackageEngine.Managers.WinGet/*` native paths.

## Rules

- Prefer `Microsoft.Management.Deployment` native COM paths where available; keep CLI fallback for unsupported operations and startup failures.
- Handle install options, source selection, and version pinning through existing manager helpers; do not bypass capability contracts.
- Keep Windows-only code behind Windows guards; never break the cross-platform Avalonia solution.
- Treat source URLs and executable resolution as untrusted input; validate before use.

## Safe verification

- Verify on Windows with WinGet available; test both native path and forced CLI fallback.
- Cover source add/remove, install options, version handling, and fallback when the selected WinGet executable cannot start.
- Follow the generic manager contracts in the [package-manager-integration](../package-manager-integration/SKILL.md) skill.
- Build and test per the [dotnet-build-test](../dotnet-build-test/SKILL.md) skill; record native versus fallback evidence.

## Out of scope

- Generic manager scaffolding belongs in package-manager-integration.
- UI surfacing belongs in the [avalonia-ui](../avalonia-ui/SKILL.md) skill.
