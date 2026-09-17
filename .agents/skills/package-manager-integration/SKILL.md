---
name: package-manager-integration
description: Extend UniGetUI package managers correctly via PackageManager base, helper abstractions, and capability contracts. Use when adding or fixing a WinGet, Scoop, Chocolatey, Pip, npm, or other manager integration.
---

# package manager integration

Use when touching `src/UniGetUI.PackageEngine.Managers.*` or shared engine contracts.

## Reference implementation

- Follow `src/UniGetUI.PackageEngine.Managers.Scoop/Scoop.cs` as the clean example.
- Extend `PackageManager`; override `FindPackages_UnSafe`, `GetAvailableUpdates_UnSafe`, `GetInstalledPackages_UnSafe`.
- Provide three helpers in `Helpers/`: details helper from `BasePkgDetailsHelper`, operation helper from `BasePkgOperationHelper`, source helper from `BaseSourceHelper`.
- Set `Capabilities` and `Properties` in the constructor and wire the helpers.

## Contracts

- Program against `IPackageManager`, `IPackage`, `IManagerSource`, `IPackageDetails`; do not leak manager-specific types into generic UI.
- Filter CLI noise with `FALSE_PACKAGE_NAMES`, `FALSE_PACKAGE_IDS`, `FALSE_PACKAGE_VERSIONS`.
- Follow `Initialize` flow: executable file, version, extra loading steps.
- Return `OperationVeredict` (codebase spelling) for fallible operations.

## Behavior

- Support cancellation, retry, and explicit errors; provide native/CLI fallback where the manager supports it.
- Pick the version comparator the registry uses: default numeric, `SemanticVersion` for SemVer/NuGet ecosystems, `PythonVersion` for PEP 440. Wrong choice hides updates or offers downgrades.
- Localize user-facing strings; log via `UniGetUI.Core.Logging`.

## Verification

- Build affected manager plus the Windows solution per the [dotnet-build-test](../dotnet-build-test/SKILL.md) skill.
- Cover search, installed, updates, details, operations, and sources for the touched manager.
