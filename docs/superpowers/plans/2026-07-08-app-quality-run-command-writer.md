# App Quality-Run Command Writer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a tested helper that writes a selected `plan-runs` `devCommand` into the installed App LocalState `dev-command.json`.

**Architecture:** Keep the capture contract unchanged. The new PowerShell script consumes an existing run plan JSON, selects one case with a `devCommand`, locates the latest `NextGenEmby.App_*` package folder, and writes only that command JSON to LocalState. It does not launch the App, execute playback, or fabricate captured reports.

**Tech Stack:** PowerShell, existing playback-quality CLI run-plan JSON, existing DEBUG App `DevelopmentNavigationCommand`.

## Global Constraints

- Do not change App playback behavior, native graph behavior, report thresholds, or expected behavior.
- Do not commit private Emby server data, credentials, real item IDs, captured private reports, or personal paths.
- Keep output under App LocalState or caller-provided temp/private paths.
- The helper must be testable with a fake packages root and fake run plan.

---

### Task 1: Add Command Writer Script

**Files:**
- Create: `tools/quality-run/Write-AppQualityRunCommand.ps1`
- Create: `tools/quality-run/Write-AppQualityRunCommand.tests.ps1`
- Modify: `tools/quality-run/run-playback-core-checks.ps1`
- Modify: `tools/quality-run/run-playback-core-checks.tests.ps1`
- Modify: `docs/qa/playback-core-quality-validation.md`

**Interfaces:**
- Consumes: run-plan JSON with `cases[].devCommand`
- Produces: App LocalState `dev-command.json` and optional summary JSON

- [x] **Step 1: Write failing test**

Create `tools/quality-run/Write-AppQualityRunCommand.tests.ps1` with fake package root and run plan. Assert that the script writes `LocalState\dev-command.json`, preserves `route = quality-run`, selects by `caseId`, and writes a summary.

- [x] **Step 2: Run test and verify RED**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\Write-AppQualityRunCommand.tests.ps1
```

Expected before implementation: failure because `Write-AppQualityRunCommand.ps1` does not exist.

Result: failed with `CommandNotFoundException` for `Write-AppQualityRunCommand.ps1`.

- [x] **Step 3: Implement script**

Create `Write-AppQualityRunCommand.ps1` with parameters:

- `RunPlanPath`
- `CaseId`
- `PackagesRoot`
- `PackageNamePrefix`
- `SummaryPath`

Behavior:

- Load run plan JSON.
- Select exactly one case: by `CaseId` when supplied, otherwise the first case with `devCommand`.
- Fail if the selected case has no `devCommand`.
- Locate latest package folder matching `PackageNamePrefix`.
- Create `LocalState` if needed.
- Write selected `devCommand` as JSON to `LocalState\dev-command.json`.
- Emit or write summary with `schemaVersion`, `caseId`, `runId`, `packageRoot`, `commandPath`, and `route`.

- [x] **Step 4: Verify GREEN**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\Write-AppQualityRunCommand.tests.ps1
```

Expected: pass.

Result: `write-app-quality-run-command tests ok`.

- [x] **Step 5: Add script to playback-core checks**

Add a command-plan entry so `run-playback-core-checks.ps1` runs the new script test. Update the plan test to expect it.

- [x] **Step 6: Update validation docs**

Document the command writer between `plan-runs` and App launch/export steps.

- [x] **Step 7: Run verification**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\quality-run\run-playback-core-checks.ps1
git diff --check
```

Result: `run-playback-core-checks.ps1` passed and included `write-app-quality-run-command-test`.

- [x] **Step 8: Commit**

Commit message:

```bash
feat: write app quality-run commands from plans
```

Result: committed with message `feat: write app quality-run commands from plans`.
