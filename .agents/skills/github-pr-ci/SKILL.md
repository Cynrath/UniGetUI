---
name: github-pr-ci
description: Keep UniGetUI branches clean, open focused PRs, and investigate CI without pushing local main. Use when branching, pushing to fork, or debugging dotnet-test and ACKit workflows.
---

# github pr ci

Use for branch hygiene, fork workflow, and CI triage. Upstream is `Devolutions/UniGetUI` as `origin`; fork is `Cynrath/UniGetUI` as `fork`.

## Branch hygiene

- Branch from clean `origin/main`; never merge local `main` or unrelated feature branches.
- Keep one logical change per branch; use the PR template and link issues without placeholders.
- Never push local `main` upstream. Push feature branches to `fork` for review.
- For rebased branches use `git push --force-with-lease`; never force-push `main`.

## CI triage

- `dotnet-test` runs whitespace/style verify, Windows x64 build, tests, full-trim and NativeAOT publish reports.
- ACKit workflow runs `ackit config check`, `ackit policy check`, `ackit skills validate`, `ackit task doctor`, `ackit scan --ci`, `ackit readiness --strict`.
- Pull logs first; classify as infra flake only with rerun evidence. Do not weaken thresholds to get green.
- Validate locally with the same commands before pushing.

## Approval boundary

- Fork workflow approval may be required before CI runs on new branches; request review rather than pushing workarounds.
- Do not open an upstream PR merely to create one; keep dogfooding branches on the fork with a written reason.
