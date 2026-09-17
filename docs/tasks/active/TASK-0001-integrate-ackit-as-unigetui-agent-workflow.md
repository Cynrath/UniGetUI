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

## Runtime feedback round 2 (2026-09-17): CS2012 diagnosis + download throughput

CS2012 (`UniGetUI.PackageEngine.Managers.WinGet.dll` locked) teşhisi: repo problemi DEĞİL. Teşhis anında çalışan UniGetUI/testhost/vstest süreci yoktu; dotnet süreçleri yalnızca MSBuild nodeReuse worker'larıydı; `handle.exe` kilitli DLL için "No matching handles found" döndürdü; broad kill yapılmadı, hiçbir stale süreç kapatmaya gerek kalmadı. WinGet manager projesi temiz derlendi (0 hata). Sonuç: transient stale-process lock; kanıtlandı, PR'da kod değişikliği gerekmedi.

Warning sınıflandırması (hepsi `origin/main` baseline, PR kaynaklı değil, PR'da rastgele düzeltilmedi): NU1903 (`SQLitePCLRaw.lib.e_sqlite3` güvenlik açığı, transitif bağımlılık), NU1510 (`System.Text.Encoding.CodePages` PackageReference, `origin/main` ile birebir aynı), CA2008 (`Core.Tools/Tools.cs:1525`) + CA1822 (`PackageOperations.cs:998/1014`, testlerde benzerleri) — PR diff'inde hiç `.csproj`/ilgili kod yok (`git diff --name-only origin/main...HEAD` kanıtlı).

Download speed eklendi (fake/synthetic yok, `deltaBytes/deltaTime`): `OperationProgress.BytesPerSecond` (`double?`, yalnızca Downloading + byte counter varken), `WinGetProgressMapper` stateless kaldı, hesaplama generic katmanda (`AbstractOperation.ReportProgress` + yeni `DownloadThroughputTracker`, EMA alpha=0.3, gerekçe XML doc'ta: ~3 örnek hafıza, jitter sönümü + ~1sn tepki, O(1) deterministik state). Kurallar: ilk örnek null; yalnızca Downloading; deltaBytes<=0 önceki hızı korur (state'e dokunmaz); timeDelta<=0 previous-safe; geri giden sayaç reset; stage/unknown/retry değişiminde reset + sanitize; terminal sonrası kart `WithProgress` yoksayar; thread-safe (lock). Formatter: hız varsa `Downloading · 21% · 10,0 MB / 46,7 MB · 1,2 MB/s`, yoksa legacy format aynen; birimler B/s–GB/s (`FormatAsSize` konvansiyonu, F1 + current culture); ETA yok. Native log satırı zenginleşmiş progress'i formatlar (`PackageOperations.cs`); HTTP `DownloadOperation` aynı `ReportProgress` yolundan otomatik hız alır.

Testler (hedefli 212/212 yeşil, her iki TFM): first-null, delta hesabı, EMA determinizmi (1.3 MiB/s), zero-time, backward reset, repeated-preserve, stage/retry/unknown reset, non-Downloading sanitize, normalize reddi, formatter with/without + B/KB/MB/GB + NaN/Infinity omit, 8×50 paralel thread-safety, WinGet mapped→downstream hız, loopback HTTP uçtan uca (`DownloadOperationThroughputTests`, 3MB throttled server), kart terminal yoksayma. Tam süit: 1595 geçti / 7 başarısız — 7'si de stash'lenmiş baseline'da birebir tekrarlandı (pre-existing, çevresel): 5 locale (tr-TR makinede İngilizce bekleyen `OperationHistory` + 4 `WinGetManagerTests.Explain*`), 2 launcher-script discovery (`OperationCallArgsWiringTests`); PR regresyonu yok. `dotnet format whitespace` pass; `dotnet format style UniGetUI.Windows.slnx` pass (exit 0).

Gerçek runtime (local Debug x64 `UniGetUI.exe --headless`, pipe `unigetui-rt-speed`, `UNIGETUI_WINGET_COM=enabled`, FDM): (1) `package download` 0→100% + success + 46.7MB dosya (HTTP yolu; hız kart katmanında, IPC liveLine log-odaklı olduğu için unit/loopback ile kanıtlı). (2) Native COM `package update` op 8371548 canlı log kanıtı: `Starting native WinGet upgrade...`, ilk örnek hızsız (`Downloading · 2% · 1,0 MB / 46,7 MB`), ardından canlı değişen `· 1,2 MB/s … · 985,8 KB/s … · 775,7 KB/s … · 1,3 MB/s … · 915,9 KB/s`, `Downloading · 100% · 46,7 MB / 46,7 MB`, hızsız `Installing...`/`Installing · 1%` geçişi, `Finalizing...`, `Native WinGet result: Ok`, success. Gözlem (PR dışı): kurulu sürüm 6.34'te kaldı çünkü vendor `.../6/latest/fdm_x64_setup.exe` payload'unun kendisi 6.34 (FileVersion kanıtlı, SHA256 kayıtlı); stok `winget upgrade` CLI da aynı "Successfully installed" + değişmeyen sürümü verdi — WinGet/UniGetUI hattı doğru raporladı, vendor/manifest sorunu. Daemon `app quit` ile kapatıldı, `C:\Temp\unigetui-rt` temizlendi; tam op çıktısı `C:\Temp\unigetui-rt-speed-evidence-8371548.json` (repo dışı).

Bu turun kapıları: `ackit config check` OK; `ackit policy check` OK; `ackit skills validate` 16/0; `ackit task doctor` integrity OK; `ackit scan --changed` 9 dosya 0 bulgu; `ackit readiness --strict` 89 pass.

Commits cherry-picked: 0dc030a0 chore init, 6bb9efb0 docs skills, cb30b3ff ci workflow, 00f769f3 evidence sync. Progress side: bfa97850 determinate progress + cb18d904 OperationViewModel operation-card progress mapping tests (present in-PR, not missing). ACKit files intentionally INCLUDED in this PR (not excluded). Strict translation skill issue FIXED (not pre-existing). Combined PR diff covers src progress/test files plus ACKit repo workflow, not src-only. Local main 38bbfbbe untouched, never pushed. Localization evaluated: 6 translation skills reused, no duplicate generic skill. No ACKit product changes (no separate repo work justified beyond documentation-gap findings).
