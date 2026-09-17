---
name: ackit-repo-workflow
description: Run the UniGetUI ACKit start-of-task sequence, lifecycle gates, scans, readiness, packs, and evidence correctly. Use at task start, during work, and before completion.
---

# ackit repo workflow

Use for every UniGetUI task so ACKit gates stay green.

## Start of task

```powershell
ackit instructions --explain
ackit task list
ackit task show TASK-0001
ackit pack --profile codex --max-tokens 50000
```

Keep one active task with a single `[~]` item. Implementation lives under `docs/tasks/active/`; archive lives under `docs/tasks/archive/`.

## During work

```powershell
ackit scan --changed
ackit scan --staged
```

Boost or limit packs with `ackit pack --changed` and task-aware `ackit pack --task TASK-0001` when needed.

## Before done

```powershell
ackit config check
ackit policy check
ackit skills validate
ackit task doctor
ackit scan --ci
ackit readiness --strict
ackit optimize --explain
ackit diagnostics --json
```

Record exact outputs in task Completion notes. Complete only with verified evidence; archive with `ackit task archive <id>` after final proof.

## References

- Full contributor and agent guide is `docs/ACKIT.md`.
- Task lifecycle details are in the [ackit-workflow](../ackit-workflow/SKILL.md) builtin skill.
- Scan triage uses the [ackit-scan-and-fix](../ackit-scan-and-fix/SKILL.md) builtin skill.
