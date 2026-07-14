# 显式时间线 Seek 契约实施计划

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 让每个 timeline case 声明并真实执行可复现的远距离绝对 seek 目标，禁止固定 1 秒偏移冒充 seek 覆盖。

**Architecture:** `PlaybackQualityReferenceCase` 保存目标；manifest runner 将其逐层传给 headless 和 native helper；helper 只执行该目标并回传首帧证据。私有生成器根据 Emby `RunTimeTicks` 产生安全的远距离目标。

**Tech Stack:** C#/.NET、PowerShell、C++/WinRT、FFmpeg native playback、xUnit/source contract tests。

---

### Task 1: 锁定 manifest 契约

**Files:**
- Modify: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualityReferenceManifestTests.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReferenceManifest.cs`

1. 先写失败测试：timeline 缺少目标、目标等于起播位置时 validation 失败；合法目标会被 clone/summary 保留。
2. 运行定向测试确认 RED。
3. 添加 `SeekTargetPositionTicks` 和最小 validation/clone 实现。
4. 运行定向测试确认 GREEN。

### Task 2: 锁定 runner 到 native 的同值传递

**Files:**
- Modify: `tools/quality-run/Invoke-PlaybackQualityManifest.tests.ps1`
- Modify: `tools/quality-run/Invoke-PlaybackQualityManifest.ps1`
- Modify: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/NativePlaybackGraphDecouplingContractTests.cs`
- Modify: `tools/NoiraPlayer.PlaybackQuality.Headless/Program.cs`
- Modify: `tests/NoiraPlayer.Native.Tests/NativePlaybackGraphHeadlessSmokeTests.cpp`

1. 先写失败测试，要求命令行和 reference case 目标完全一致。
2. 运行测试确认 RED。
3. 增加 `--seek-target-position-ticks`，删除固定 1 秒目标；非 timeline 携带目标或 timeline 缺目标时拒绝运行。
4. 运行 PowerShell、Core contract 和 native smoke 测试确认 GREEN。

### Task 3: 生成真实远距离私有 case

**Files:**
- Modify: `tools/quality-run/New-PrivateEmbyReferenceManifest.tests.ps1`
- Modify: `tools/quality-run/New-PrivateEmbyReferenceManifest.ps1`
- Modify: `docs/qa/private/reference-manifest.template.json`
- Modify: `docs/qa/playback-quality-reference-manifest.example.json`

1. 先写失败测试，要求生成目标来自真实时长、远离片头片尾且明显不同于起播位置。
2. 运行生成器测试确认 RED。
3. 实现确定性的 start/target 计算，并更新模板和公开示例。
4. 运行生成器与 manifest validation 测试确认 GREEN。

### Task 4: 真实运行与归档

**Files:**
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`

1. 重新生成 ignored 私有 manifest。
2. 用同一私有片源执行显式远距离 timeline case，确认 attempt、target、demux target、first presented 和 post-seek 一一对应。
3. 将位置正确性和受环境干扰的耗时分开归因。
4. 运行完整 Core gate 和完整 App Debug x64 build。
5. 更新中文状态与决策文档并提交。
