# Private Local Sample Spec Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Define a repeatable private/local playback-quality sample convention without committing private media, Emby locators, credentials, or captured reports.

**Architecture:** Keep the existing `PlaybackQualityReferenceManifest` contract. Add committed documentation and a committed template under `docs/qa/private/`, while `.gitignore` continues to exclude real `*.local.json`, secrets, and private captured reports.

**Tech Stack:** Markdown documentation, JSON manifest template, existing PowerShell quality-run scripts, existing .NET playback-quality CLI.

## Global Constraints

- Do not change evaluator behavior, manifest schema, playback Core, or native playback code.
- Do not read, copy, normalize, or commit real private sample IDs, server URLs, credentials, tokens, or captured private reports.
- Keep real private manifests as `docs/qa/private/*.local.json`.
- Keep committed examples non-sensitive and clearly marked as templates.

---

### Task 1: Document Private And Local Sample Convention

**Files:**
- Create: `docs/qa/private/README.md`

**Interfaces:**
- Consumes: existing `PlaybackQualityReferenceManifest` fields.
- Produces: contributor-facing rules for placing private/local manifests and reports.

- [ ] **Step 1: Create README with required fields, naming, safety rules, and commands**

Create `docs/qa/private/README.md` with sections for directory layout, case naming, required manifest fields, private Emby generation, local-file/direct-uri cases, validation, merge, and forbidden committed data.

- [ ] **Step 2: Review README for private-data leakage**

Run a local sensitive-value scan against `docs/qa/private/README.md` using the known private server URL, credentials, device addresses, and temporary access codes from the local environment.

Expected: no matches.

### Task 2: Add A Non-Sensitive Manifest Template

**Files:**
- Create: `docs/qa/private/reference-manifest.template.json`

**Interfaces:**
- Consumes: existing `validate-manifest` minimum fields.
- Produces: a copyable template that can be renamed to `*.local.json` and filled with real local values.

- [ ] **Step 1: Create template manifest**

Create a JSON manifest with placeholder `private-emby/...` cases for SDR smoke, HDR10 cadence, DV fallback/reject, timeline, buffering, and error handling. Use placeholder IDs only.

- [ ] **Step 2: Validate template shape**

Run:

```powershell
dotnet run --project tools\NextGenEmby.PlaybackQuality.Cli\NextGenEmby.PlaybackQuality.Cli.csproj -- validate-manifest --manifest docs\qa\private\reference-manifest.template.json --output artifacts\quality-run\private-template-validation.json
```

Expected: exit code 0.

### Task 3: Allow Templates While Ignoring Real Private Data

**Files:**
- Modify: `.gitignore`

**Interfaces:**
- Consumes: current private QA ignore rules.
- Produces: ignore behavior where `README.md` and `*.template.json` are trackable, while `*.local.json`, report output, and secrets remain ignored.

- [ ] **Step 1: Replace broad private directory ignore rule**

Change `docs/qa/private/` to `docs/qa/private/*`, then add negation rules for `README.md` and `*.template.json`.

- [ ] **Step 2: Verify tracked candidate list**

Run: `git status --short`

Expected: new README/template and `.gitignore` are visible; real `*.local.json` files under `docs/qa/private/` are not.

### Task 4: Final Verification

**Files:**
- Read: changed files only.

**Interfaces:**
- Produces: confidence that the convention is usable and did not leak private data.

- [ ] **Step 1: Run sensitive string scan**

Run:

```powershell
rg -n "<known-private-value-pattern>" .gitignore docs\qa\private docs\superpowers\plans\2026-07-08-private-local-sample-spec.md
```

Expected: no matches.

- [ ] **Step 2: Run git diff check**

Run: `git diff --check`

Expected: no whitespace errors.
