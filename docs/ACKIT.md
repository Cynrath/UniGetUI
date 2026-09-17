# ACKit in UniGetUI

ACKit 0.5.2 makes UniGetUI agent-ready with deterministic instructions, skills, scans, tasks, policy, readiness, and context packs. Offline-first; no repo content leaves the machine.

## Install and use

```powershell
npm install --global @cynrath/agent-context-kit
ackit --version
ackit --help
ackit <command> --help
```

Config is `ackit.yml` at root. Validate with `ackit config check`.

## Common commands

```powershell
ackit init --dry-run
ackit config check
ackit policy check
ackit skills validate
ackit skills list
ackit task list
ackit task doctor
ackit scan
ackit scan --ci
ackit scan --changed
ackit scan --staged
ackit readiness
ackit readiness --strict
ackit optimize --explain
ackit instructions --explain
ackit pack --profile codex --max-tokens 50000
ackit diagnostics --json
ackit sync --dry-run
```

## Task workflow

Tasks live in `docs/tasks/active/`; archive in `docs/tasks/archive/`. Keep one `[~]` item active.

```powershell
ackit task create "Title"
ackit task start TASK-0001
ackit task show TASK-0001
ackit evidence sync TASK-0001
ackit task doctor
ackit task archive TASK-0001
```

Complete only with recorded evidence. See the `ackit-repo-workflow` skill for the full start/during/done sequence.

## CI behavior

`.github/workflows/ackit.yml` runs on push and pull requests to `main` for ACKit-owned surfaces:

- `ackit config check`
- `ackit policy check`
- `ackit skills validate`
- `ackit task doctor`
- `ackit scan --ci` against `docs/ackit/scan-baseline.json`
- `ackit readiness --strict`

It uses least privilege (`contents: read`), pinned `actions/checkout` and `actions/setup-node`, and pinned `npx @cynrath/agent-context-kit@0.5.2`. It does not rebuild .NET; `dotnet-test.yml` owns builds and tests.

## Provider instructions

- Canonical source is `AGENTS.md` (Codex surface, ACKit-managed workflow block on top).
- `CLAUDE.md` is the Claude shim (`@AGENTS.md` inside a managed block).
- `GEMINI.md` and `.github/copilot-instructions.md` are ACKit-managed shims pointing at `AGENTS.md`.
- Do not maintain duplicate instruction files. Validate with `ackit instructions --explain`.

## Skills

Builtins (ACKit-owned): `ackit-context-optimization`, `ackit-policy-authoring`, `ackit-scan-and-fix`, `ackit-workflow`.

UniGetUI custom: `dotnet-build-test`, `avalonia-ui`, `package-manager-integration`, `winget-native`, `github-pr-ci`, `ackit-repo-workflow`.

Translation workflow (existing, reused; no duplicate generic localization skill): `translation-diff-export`, `translation-diff-import`, `translation-diff-translate`, `translation-review`, `translation-source-sync`, `translation-status`.

Validate with `ackit skills validate`; list with `ackit skills list`.

## Context pack

Recommended provider-aware pack:

```powershell
ackit pack --profile codex --max-tokens 50000
```

It prioritizes instructions, architecture, affected projects, package-manager abstractions, Avalonia conventions, tests, and the active task. It excludes `bin/obj`, generated outputs, and binaries. Budget is `50000` via `ackit.yml` `context.maxTokens`.

## Troubleshooting

- `refused-non-managed` on `AGENTS.md`/`CLAUDE.md` means ACKit protects user content (REQ-GOV-008). Add the canonical managed block manually; do not force-overwrite.
- `skills validate` broken-ref means a markdown link in `SKILL.md` resolves from the skill directory, not repo root. Use `../../../src/...` for root files or code spans for shell examples.
- `policy check` chain `(0)` is normal; rule packs in `ackit.yml` are evaluated during `ackit scan`, not `policy check`.
- `instructions --explain` shadowing by `.github/copilot-instructions.md` for delegation shims is expected; the shim only points at `AGENTS.md`.
- `.ackit/` is local cache (ignored via `.git/info/exclude`); never commit it. Commit `ackit.yml`, `.agents/skills`, `.agents/policy`, `docs/tasks`, `docs/ACKIT.md`, and `docs/ackit/scan-baseline.json`.
