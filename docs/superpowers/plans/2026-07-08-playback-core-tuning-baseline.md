# Playback Core Tuning Baseline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first reproducible playback Core tuning baseline from the normalized sample system, then use that baseline for small Core/native tuning candidates.

**Architecture:** Add a PowerShell orchestration script that merges the committed public manifest, ignored private Emby manifests, and native-headless generated local sample manifest into one local baseline corpus. It materializes core-probe reports for public/private cases, imports native-headless materialized local reports, validates/analyzes the unified report-set, and writes all private outputs under ignored `docs/qa/private/baselines/...`.

**Tech Stack:** PowerShell quality-run scripts, `tools/NextGenEmby.PlaybackQuality.Cli`, existing native-headless harness, JSON report-set artifacts.

## Global Constraints

- Do not change evaluator rules or sample expected values to make results pass.
- Do not commit private Emby server URLs, credentials, item IDs, media source IDs, local media paths, or private captured reports.
- Keep private baseline outputs under ignored `docs/qa/private/`.
- Use the same manifest for baseline/candidate comparison after any Core/native tuning change.
- Do not touch `src/NextGenEmby.App/Package.appxmanifest`; it is unrelated dirty state.

---

### Task 1: Baseline Orchestration Script

**Files:**
- Create: `tools/quality-run/New-PlaybackCoreTuningBaseline.ps1`

**Interfaces:**
- Consumes: `docs/qa/playback-quality-reference-manifest.example.json`, optional `docs/qa/private/*.local.json`, optional native-headless artifacts.
- Produces: ignored local baseline directory with `manifests/`, `reports/`, `summaries/`, and `baseline-summary.local.json`.

- [ ] **Step 1: Implement script parameters**

Support `-PublicManifestPath`, `-PrivateManifestPath`, `-AdditionalManifestPath`, `-NoPrivateManifest`, `-RequirePrivateManifest`, `-SkipNativeHeadless`, `-OutputRoot`, `-Clean`, `-PlayerCoreVersion`, `-BuildConfiguration`, and `-SourceRevision`.

- [ ] **Step 2: Implement public/private manifest merge**

Use `tools/quality-run/Merge-ReferenceManifests.ps1` with `-DuplicateCaseIdMode skip`.

- [ ] **Step 3: Materialize core-probe reports**

Run `materialize-core-probe-report-set` into the final `reports/` directory.

- [ ] **Step 4: Import native-headless local generated sample reports**

Unless `-SkipNativeHeadless` is set, run `tools/quality-run/run-native-headless-harness-smoke-test.ps1`, merge its `native-manifest.json`, and copy `native-materialized/` reports into the final `reports/` directory.

- [ ] **Step 5: Validate, analyze, and summarize**

Run `validate-report-set`, `analyze-report-set`, and `plan-runs`. Write a machine-readable `baseline-summary.local.json`.

### Task 2: Script Test

**Files:**
- Create: `tools/quality-run/New-PlaybackCoreTuningBaseline.tests.ps1`
- Modify: `tools/quality-run/run-playback-core-checks.ps1`
- Modify: `tools/quality-run/run-playback-core-checks.tests.ps1`

**Interfaces:**
- Consumes: the new script.
- Produces: a quick validation path that does not use private data or native-headless.

- [ ] **Step 1: Add test script**

Run the baseline script with `-NoPrivateManifest -SkipNativeHeadless` into a temp directory and assert manifest/report-set/analyze outputs exist and are valid.

- [ ] **Step 2: Add test to playback-core check plan**

Add `playback-core-tuning-baseline-test` to `run-playback-core-checks.ps1`, and assert its presence in `run-playback-core-checks.tests.ps1`.

### Task 3: Generate First Local Baseline

**Files:**
- Generated ignored output: `docs/qa/private/baselines/playback-core-tuning-baseline.local/`

**Interfaces:**
- Consumes: public manifest, current private Emby manifest if present, native-headless generated samples.
- Produces: first local unified baseline report-set.

- [ ] **Step 1: Run full baseline command**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\New-PlaybackCoreTuningBaseline.ps1 -Clean
```

- [ ] **Step 2: Inspect summary**

Confirm `baseline-summary.local.json` reports a valid unified manifest and report-set.

### Task 4: Verification

**Files:**
- Read changed files only.

**Interfaces:**
- Produces: verified baseline workflow before any Core/native tuning change.

- [ ] **Step 1: Run focused tests**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\New-PlaybackCoreTuningBaseline.tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-playback-core-checks.tests.ps1
```

- [ ] **Step 2: Run sensitive scan**

Run a local sensitive-value scan over the changed files and committed docs.

- [ ] **Step 3: Run diff check**

```powershell
git diff --check
```

### Task 5: Candidate Comparison Gate

**Files:**
- Create: `tools/quality-run/Compare-PlaybackCoreTuningCandidate.ps1`
- Create: `tools/quality-run/Compare-PlaybackCoreTuningCandidate.tests.ps1`
- Modify: `tools/quality-run/run-playback-core-checks.ps1`
- Modify: `tools/quality-run/run-playback-core-checks.tests.ps1`

**Interfaces:**
- Consumes: an existing baseline root and candidate root, each with `manifests/unified-reference-manifest.local.json` and `reports/`.
- Produces: ignored local comparison directory with `candidate-evaluation.local.json`, individual comparisons, and `comparison-summary.local.json`.

- [ ] **Step 1: Add comparison script**

Validate baseline and candidate report-sets against the baseline manifest, compare by `runId`, and summarize `evaluate-candidate` evidence without changing evaluator rules.

- [ ] **Step 2: Add comparison test**

Generate public-only baseline and candidate temp report-sets with different source revisions, then assert the comparison output is valid, references the same manifest case set, and blocks on insufficient native playback evidence instead of producing false tuning evidence.

- [ ] **Step 3: Add test to playback-core check plan**

Add `playback-core-tuning-candidate-comparison-test` to `run-playback-core-checks.ps1`, and assert its presence in `run-playback-core-checks.tests.ps1`.
