# HDR Refresh Frame Pacing Evidence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build software-only evidence for HDR, display refresh policy, cadence, and frame pacing in native-headless/report-set without tuning playback behavior.

**Architecture:** Reuse the existing native `HdrDisplayRefreshRatePolicy`, Core `PlaybackRefreshRatePolicy`, native-headless helper, and report-set analyzer. Add policy/snapshot evidence as telemetry, generated local samples for SDR/HDR10 frame-rate coverage, and tests that fail when evidence is missing.

**Tech Stack:** C#/.NET Core project tests, PowerShell quality-run scripts, native C++ helper tests, FFmpeg-generated local media samples.

## Global Constraints

- Do not tune playback strategy or frame pacing behavior in this phase.
- Hardware HDMI output, panel EOTF, luminance, and real display mode changes remain unverified and must be reported as limitations.
- Generated sample media must stay under `artifacts/` and must not be committed.
- Do not stage or revert unrelated dirty files, especially `src/NextGenEmby.App/Package.appxmanifest`.

---

### Task 1: Native Refresh Policy Snapshot

**Files:**
- Modify: `src/NextGenEmby.Native/HdrDisplayRefreshRatePolicy.h`
- Modify: `tests/NextGenEmby.Native.Tests/DisplayRefreshRatePolicyTests.cpp`
- Modify: `tests/NextGenEmby.Native.Tests/NativePlaybackGraphHeadlessSmokeTests.cpp`
- Modify: `tools/NextGenEmby.PlaybackQuality.Headless/Program.cs`

**Interfaces:**
- Consumes: `HdrDisplayRefreshRatePolicy::IsBetterRefreshRateForVideo(double, double, double)`
- Produces: `HdrDisplayRefreshRatePolicy::SelectSoftwareOnlyRefreshRateSnapshot(double)` returning a matched target refresh rate for headless cadence analysis.

- [ ] **Step 1: Write the failing native policy test**

Add assertions:

```cpp
assert(HdrDisplayRefreshRatePolicy::SelectSoftwareOnlyRefreshRateSnapshot(23.976) == 23.976024);
assert(HdrDisplayRefreshRatePolicy::SelectSoftwareOnlyRefreshRateSnapshot(24.0) == 24.0);
assert(HdrDisplayRefreshRatePolicy::SelectSoftwareOnlyRefreshRateSnapshot(30.0) == 60.0);
assert(HdrDisplayRefreshRatePolicy::SelectSoftwareOnlyRefreshRateSnapshot(60.0) == 60.0);
assert(HdrDisplayRefreshRatePolicy::SelectSoftwareOnlyRefreshRateSnapshot(0.0) == 0.0);
```

- [ ] **Step 2: Verify RED**

Run:

```powershell
tools\quality-run\run-playback-core-checks.ps1
```

Expected: native display refresh policy compilation fails because `SelectSoftwareOnlyRefreshRateSnapshot` is not defined.

- [ ] **Step 3: Implement minimal policy method**

Add a small candidate list and select the best cadence match:

```cpp
static double SelectSoftwareOnlyRefreshRateSnapshot(double videoFrameRate) noexcept
```

Candidates: `23.976024`, `24.0`, `25.0`, `29.97003`, `30.0`, `50.0`, `59.94006`, `60.0`, `100.0`, `119.88012`, `120.0`.

- [ ] **Step 4: Emit and import native helper evidence**

Native helper prints:

```cpp
displayRefreshRateHz=<policy snapshot>
displayRefreshPolicy=software-only-cadence-policy
```

C# headless parses these into `NativeHeadlessDisplayInfo` and passes `refreshRateHz` to `PlaybackDisplayStatus`.

- [ ] **Step 5: Verify GREEN**

Run:

```powershell
tools\quality-run\run-playback-core-checks.ps1
```

Expected: native policy test and existing smoke checks pass.

### Task 2: Frame-Rate And HDR10 Report-Set Coverage

**Files:**
- Modify: `tools/quality-run/run-native-headless-harness-smoke-test.ps1`
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`

**Interfaces:**
- Consumes: existing `New-NativePlaybackSample`, native helper executable, `materialize-native-harness-report-set`, and `analyze-report-set`.
- Produces: generated local samples for SDR 23.976/24/30/60fps and HDR10 challenge coverage, plus report assertions for source color, DXGI mapping, cadence, frame interval, dropped/wait/starvation, and limitations.

- [ ] **Step 1: Write failing smoke assertions**

Assert that native report-set analysis no longer marks frame-pacing partial for missing `display.refreshRateHz`, and that generated reports include:

```powershell
$report.report.display.refreshRateHz -gt 0
$report.modelAnalysis.cadence.status -in @('matched','mismatch')
$report.report.limitations -contains 'native-headless: display refresh is a software policy snapshot; HDMI/display output is not verified'
```

- [ ] **Step 2: Verify RED**

Run:

```powershell
tools\quality-run\run-native-headless-harness-smoke-test.ps1
```

Expected: failure on missing positive display refresh evidence or missing limitation.

- [ ] **Step 3: Add generated sample matrix**

Create functions to generate short local samples:

```powershell
New-NativePlaybackSdrSample -Name native-headless-sdr-23976 -Rate '24000/1001'
New-NativePlaybackSdrSample -Name native-headless-sdr-24 -Rate '24'
New-NativePlaybackSdrSample -Name native-headless-sdr-smoke -Rate '30'
New-NativePlaybackSdrSample -Name native-headless-sdr-60 -Rate '60'
New-NativePlaybackHdr10Sample -Name native-headless-hdr10-23976 -Rate '24000/1001'
New-NativePlaybackHdr10Sample -Name native-headless-hdr10-24 -Rate '24'
New-NativePlaybackHdr10Sample -Name native-headless-hdr10-30 -Rate '30'
New-NativePlaybackHdr10Sample -Name native-headless-hdr10-60 -Rate '60'
```

Use `libx264` for SDR and `libx265` 10-bit HDR10 metadata for the HDR10 challenge samples. If HDR10 encode is unavailable, record the cases as quarantine/skip with a structured environment reason rather than deleting coverage.

- [ ] **Step 4: Materialize and assert report-set evidence**

Add cases with purposes:

```json
["sdr-smoke","frame-pacing","cadence-23.976"]
["sdr-smoke","frame-pacing","cadence-24"]
["sdr-smoke","frame-pacing"]
["hdr10","color-pipeline","frame-pacing"]
```

Assert source color, DXGI mapping, cadence signals, timing interval signals, dropped/wait/starvation counters, and software-only limitations are present.

- [ ] **Step 5: Update status docs**

Record that the stage is about evidence, not Core tuning, and document the remaining limitation: real HDMI/display output is out of scope for this software loop.

- [ ] **Step 6: Verify GREEN**

Run:

```powershell
tools\quality-run\run-native-headless-harness-smoke-test.ps1
tools\quality-run\run-playback-core-checks.ps1
git diff --check
```

Expected: commands exit 0; native-analysis reports native playback evidence and no missing `display.refreshRateHz` for the generated matrix.

### Task 3: Commit

**Files:**
- Stage only files touched by this plan.

- [ ] **Step 1: Inspect diff**

Run:

```powershell
git status --short
git diff -- docs src tools tests
```

- [ ] **Step 2: Commit**

Run:

```powershell
git add docs src/NextGenEmby.Native tests/NextGenEmby.Native.Tests tools/NextGenEmby.PlaybackQuality.Headless tools/quality-run
git commit -m "test: add hdr refresh frame pacing evidence"
```

Do not stage `src/NextGenEmby.App/Package.appxmanifest`.
