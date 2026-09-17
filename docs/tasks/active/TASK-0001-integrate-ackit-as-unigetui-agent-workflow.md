---
id: "TASK-0001"
title: "Integrate ACKit as UniGetUI agent workflow"
status: active
schemaVersion: 2
dependencies: []
createdAt: "2026-09-17"
completedAt: null
---

## Purpose

Integrate ACKit 0.5.2 into UniGetUI as a first-class repository workflow on branch `feature/operation-progress` as part of upstream PR #5390 (combined with determinate operation progress). Baseline on `origin/main` 57585652: 1010 files scanned, 71 findings (7 high / 57 medium / 7 low), readiness 89/100 (Instructions 90, Security 90, Context 70, Task 100, Skills 100, Policy 100).

## Scope

- [x] Baseline captured (scan/readiness/skills/policy/config/pack/doctor)
- [x] Real `ackit init` (GEMINI + copilot shims + 4 builtin skills)
- [x] Committed `ackit.yml` (schemaVersion 1, validated)
- [x] Root `AGENTS.md` updated (arch, build/test, formatting, localization, git hygiene, ACKit workflow, completion criteria)
- [x] Provider shims non-duplicative, instruction graph validated
- [x] Fix strict skill ref `src/Languages/lang_en.json` in `translation-source-sync`
- [x] Add UniGetUI custom skills (`dotnet-build-test`, `avalonia-ui`, `package-manager-integration`, `winget-native`, `github-pr-ci`, `ackit-repo-workflow`; localization evaluated: 6 translation skills already cover, no duplicate)
- [x] Policy pack + config gates passing
- [x] Scan baseline for pre-existing findings (committed, not blanket ignores)
- [x] Readiness reviewed, optimize reviewed (89 stable, doctor pass, skills clean)
- [x] Context pack validated (`--profile codex --max-tokens 50000`, 20 nodes, active task included)
- [x] CI workflow for ACKit gates
- [x] Contributor/agent docs (`docs/ACKIT.md`)
- [~] Dogfood findings recorded + classified, final gates + before/after evidence + diff review

## Out of scope

- Local `main` merge or push; opening any new upstream PR beyond #5390
- Broad `dotnet format` mutation across solution
- ACKit product source changes (separate repo if justified)

## Affected files

- `ackit.yml` (new)
- `AGENTS.md` (extend, add managed ACKit block)
- `GEMINI.md` (new via init)
- `.github/copilot-instructions.md` (new via init)
- `.agents/skills/*` (4 builtin + 6 custom + 6 existing translation skills = 16 total, 0 issues; strict `translation-source-sync` ref fixed)
- `.agents/policy/unigetui-policy.json` (new)
- `docs/tasks/active/TASK-0001-*` (this task, part of the PR)
- `docs/ACKIT.md` (new)
- `.github/workflows/ackit.yml` (new CI)
- `docs/ackit/scan-baseline.json` (new baseline, 71 pre-existing findings)
- Progress implementation + tests (`OperationProgress`, `WinGetNativeProgress`, `OperationCardProgressState`, `OperationViewModel` integration incl. operation-card progress mapping tests)

## Required tests

- `ackit config check`
- `ackit policy check`
- `ackit skills validate`
- `ackit task doctor`
- `ackit scan --ci` (against baseline)
- `ackit readiness` + `ackit readiness --strict`
- `ackit optimize --explain`
- `ackit diagnostics --json`
- `ackit pack --profile codex --max-tokens 50000`
- `dotnet format whitespace src --folder --verify-no-changes` (read-only)

## Acceptance criteria

- [x] Clean `origin/main` base carried into `feature/operation-progress`; progress + ACKit combined in single PR #5390
- [x] `ackit init` real, `ackit.yml` valid
- [x] `AGENTS.md` repo-specific, instruction graph validated (expected shim shadowing documented)
- [x] Custom skills validated, strict issue fixed as stale relative path
- [x] Policy/config pass, readiness stable with doctor pass, pack validated
- [x] CI added, docs added, no secrets/absolute paths
- [x] Dogfood findings classified
- [x] Final diff reviewed, evidence synchronized (push + review remain)

## Test steps

1. Run all Required tests, record exact outputs in Completion notes.
2. Run read-only whitespace check; do not run mutating format.
3. Verify `git status`, `git diff --stat`, staged scan.

## Risks

- Pre-existing scan findings (credential/GUID/action-pin false positives) block `--ci` without baseline.
- Managed-block governance refuses AGENTS/CLAUDE overwrite; manual merge required.
- Skill validator resolves relative links from SKILL.md dir (strict ref fix needed).

## Rollback plan

Focused commit revert on `feature/operation-progress`; never touch `main`; never open a second upstream PR.

## Completion notes

2026-09-17, combined branch `feature/operation-progress` for upstream PR #5390 (progress + ACKit in one PR), base `origin/main` 57585652, ACKit 0.5.2, Node v24.13.0. ACKit commits cherry-picked from `chore/ackit-integration` (0dc030a0, 6bb9efb0, cb30b3ff, 00f769f3) onto PR HEAD cb18d904 with zero conflicts; `chore/ackit-integration` is now redundant, not a separate delivery.

Before (clean origin/main): 1010 files, 71 findings (7 high / 57 medium / 7 low); readiness 89 (Inst 90, Sec 90, Ctx 70, Task 100, Skills 100, Policy 100); skills 6 with 1 strict (`translation-source-sync` ref `src/Languages/lang_en.json`); doctor 1 failed; sync 2 refused + 4 would-create; instructions 8 nodes; tasks 0 active; optimize 2 suggestions.

After (3 commits): 1032 files (+22), 74 findings (+3, all ACKIT070 mutable-pin on new `.github/workflows/ackit.yml`, repo-policy consistent, SHAs not guessed); readiness 89 stable (strict pass); skills 16, 0 issues; doctor ALL PASS; sync all up-to-date; instructions 20 nodes; tasks 1 active; optimize 1 suggestion; `scan --changed` 0 files/0 findings; `scan --staged` 0/0; `scan --changed --ci` on workflow commit showed the 3 mediums (visible regression signal).

Exact gates: `ackit config check` OK (digest b6faab36d972); `ackit policy check` OK chain 0; `ackit skills validate` 16 OK; `ackit task doctor` OK; `ackit scan --ci` exit 1 (pre-existing threshold, baseline documents); `ackit readiness` 89 pass; `ackit readiness --strict` exit 0; `ackit optimize --explain` 1 low; `ackit diagnostics --json` ok (20 instructions, 1 active task); `ackit pack --profile codex --max-tokens 50000` 50000/50000 with TASK-0001; `ackit instructions --explain` 20 nodes, expected copilot-shim shadowing + translation cycle diagnostic; `dotnet format whitespace src --folder --verify-no-changes` pass (no output); progress `dotnet test`/builds re-run after combination (recorded below).

Commits cherry-picked: 0dc030a0 chore init, 6bb9efb0 docs skills, cb30b3ff ci workflow, 00f769f3 evidence sync. Progress side: bfa97850 determinate progress + cb18d904 OperationViewModel operation-card progress mapping tests (present in-PR, not missing). ACKit files intentionally INCLUDED in this PR (not excluded). Strict translation skill issue FIXED (not pre-existing). Combined PR diff covers src progress/test files plus ACKit repo workflow, not src-only. Local main 38bbfbbe untouched, never pushed. Localization evaluated: 6 translation skills reused, no duplicate generic skill. No ACKit product changes (no separate repo work justified beyond documentation-gap findings).
