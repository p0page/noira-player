# Playback Quality v0.21 Unified Baseline Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 在提交 `700cb3d` 上生成公开、私有 Emby 与本地 native case 一一对应的 v0.21 baseline/repeat，并只根据重复证据推进最小 Core 修正。

**Architecture:** 复用现有 manifest runner、native-headless helper、strict validator、analyzer 与 cadence stability 工具。历史 v0.20 core manifest 只作为 ignored 私有 locator 输入；仓库中的公开 manifest 作为同名 case 的当前权威定义，最终归档 manifest 仅包含实际执行 case。

**Tech Stack:** PowerShell、.NET 10、C++/WinRT、FFmpeg、D3D11、NoiraPlayer.PlaybackQuality CLI。

---

### Task 1: 生成 v0.21 baseline

**Files:**
- Read: `docs/qa/playback-quality-reference-manifest.example.json`
- Read: `docs/qa/private/baselines/playback-core-v020-evidence-complete-4c2da23-repeat-1.local/manifests/core-reference-manifest.local.json`
- Generate ignored: `docs/qa/private/baselines/playback-core-v021-700cb3d-repeat-1.local/**`

**Step 1:** 确认工作树干净、HEAD 为 `700cb3d`，并记录公共/私有 stable/challenge case 数。

**Step 2:** 用 `New-PlaybackCoreTuningBaseline.ps1` 执行 15 秒媒体窗口、120 秒单 case 上限的完整 baseline。

**Step 3:** 验证 runner 的 selected/attempted/report 数一致，strict report-set 的 manifest/report 全部匹配；质量 fail、error、unsupported 保持原样。

### Task 2: 同参数重复并分析稳定性

**Files:**
- Generate ignored: `docs/qa/private/baselines/playback-core-v021-700cb3d-repeat-2.local/**`
- Generate ignored: `docs/qa/private/repeats/playback-core-v021-700cb3d.local/**`

**Step 1:** 使用与 baseline 完全相同的 manifest、提交、媒体窗口、timeout 和 seek-cache 设置重跑。

**Step 2:** 对两轮 report-set 运行严格校验和 cadence stability 汇总。

**Step 3:** 将失败归类为 player-core bug、harness bug、样本/环境问题、unsupported、flaky 或证据不足；禁止用单次网络耗时驱动 Core 策略修改。

### Task 3: 只修复重复证据证明的最小缺口

**Files:**
- Test: 由具体失败归因决定
- Modify: 由具体失败归因决定
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`（仅在产生新决策时）

**Step 1:** 对确定缺口先写会失败的最小测试，并确认失败原因与真实 report 一致。

**Step 2:** 参考 Kodi、VLC 或 mpv 的同类状态机/时间线策略，实施最小修正；不修改 manifest expected 或阈值。

**Step 3:** 运行定向测试、完整 playback-core gate、完整 Debug x64 App 构建，并对代表 case 做 App-hosted 复核。

**Step 4:** 更新中文状态，记录 baseline/candidate 证据、未验证风险和拒绝的候选，然后提交。
