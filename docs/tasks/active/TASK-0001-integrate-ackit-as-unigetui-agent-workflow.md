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

Integrate ACKit 0.5.2 into UniGetUI as a first-class repository workflow from clean `origin/main` (57585652) on branch `chore/ackit-integration`, separate from PR #5390. Baseline: 1010 files scanned, 71 findings (7 high / 57 medium / 7 low), readiness 89/100 (Instructions 90, Security 90, Context 70, Task 100, Skills 100, Policy 100).

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

- PR #5390 (`feature/operation-progress`) code changes
- Local `main` merge or push; upstream PR creation in this round
- Broad `dotnet format` mutation across solution
- ACKit product source changes (separate repo if justified)

## Affected files

- `ackit.yml` (new)
- `AGENTS.md` (extend, add managed ACKit block)
- `GEMINI.md` (new via init)
- `.github/copilot-instructions.md` (new via init)
- `.agents/skills/*` (4 builtin + 7 custom + 6 existing translation skills)
- `.agents/policy/unigetui-policy.json` (new)
- `docs/tasks/active/TASK-0001-*` (this task)
- `docs/ACKIT.md` (new)
- `.github/workflows/ackit.yml` (new CI)
- `docs/ackit/scan-baseline.json` (new baseline, 71 pre-existing findings)
- `docs/ackit/scan-baseline.json` (new baseline)

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

- [x] Clean `origin/main` base, separate branch, no PR #5390 contamination
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

Focused commit revert on `chore/ackit-integration`; never touch `main` or `feature/operation-progress`.

## Completion notes

2026-09-17, branch `chore/ackit-integration` (cb30b3ff + evidence), base `origin/main` 57585652, ACKit 0.5.2, Node v24.13.0.

Before (clean origin/main): 1010 files, 71 findings (7 high / 57 medium / 7 low); readiness 89 (Inst 90, Sec 90, Ctx 70, Task 100, Skills 100, Policy 100); skills 6 with 1 strict (`translation-source-sync` ref `src/Languages/lang_en.json`); doctor 1 failed; sync 2 refused + 4 would-create; instructions 8 nodes; tasks 0 active; optimize 2 suggestions.

After (3 commits): 1032 files (+22), 74 findings (+3, all ACKIT070 mutable-pin on new `.github/workflows/ackit.yml`, repo-policy consistent, SHAs not guessed); readiness 89 stable (strict pass); skills 16, 0 issues; doctor ALL PASS; sync all up-to-date; instructions 20 nodes; tasks 1 active; optimize 1 suggestion; `scan --changed` 0 files/0 findings; `scan --staged` 0/0; `scan --changed --ci` on workflow commit showed the 3 mediums (visible regression signal).

Exact gates: `ackit config check` OK (digest b6faab36d972); `ackit policy check` OK chain 0; `ackit skills validate` 16 OK; `ackit task doctor` OK; `ackit scan --ci` exit 1 (pre-existing threshold, baseline documents); `ackit readiness` 89 pass; `ackit readiness --strict` exit 0; `ackit optimize --explain` 1 low; `ackit diagnostics --json` ok (20 instructions, 1 active task); `ackit pack --profile codex --max-tokens 50000` 50000/50000 with TASK-0001; `ackit instructions --explain` 20 nodes, expected copilot-shim shadowing + translation cycle diagnostic; `dotnet format whitespace src --folder --verify-no-changes` pass (no output); full `dotnet test`/publish not run (no C# changes).

Commits: 0dc030a0 chore init, 6bb9efb0 docs skills, cb30b3ff ci workflow. PR #5390 branch cb18d904 untouched; local main 38bbfbbe untouched, never pushed. Localization evaluated: 6 translation skills reused, no duplicate generic skill. No ACKit product changes (no separate repo work justified beyond documentation-gap findings).
