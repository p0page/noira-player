# Native End-of-Stream Evidence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让 stable native case 真实播放到自然 EOF，并只依据 `PlaybackGraph` 的自然结束状态生成 `lifecycle.endOfStream`。

**Architecture:** 在既有单场景执行契约中加入 `end-of-stream`，由 native helper 监听 graph 状态并输出严格结构化字段，headless collector 负责解析和生成 lifecycle。manifest、runner、helper、report 和 validator 保持一一对应，任何超时、失败、字段缺失或矛盾都不得退化为推断式通过。

**Tech Stack:** C#/.NET 9、C++20/C++/WinRT、PowerShell、xUnit、原生 headless smoke harness。

## Global Constraints

- 不得根据 duration、position、expected、固定 sleep 或进程退出合成 EOF。
- 每个 case 只执行一个主动场景；EOS 不与 seek、切轨、字幕或暂停恢复混合。
- 不修改播放器阈值和正常播放策略。
- 完成后必须运行同一 manifest 的真实 native case、完整 Core gate 和 Modern App x64 Native AOT Publish。

---

### Task 1: 场景与 manifest 契约

**Files:**
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityExecutionEvidence.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReferenceManifest.cs`
- Test: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualityReferenceManifestTests.cs`

**Interfaces:**
- Produces: `PlaybackQualityExecutionScenario.EndOfStream = "end-of-stream"`
- Consumes: case purpose `end-of-stream` and `executionRequirement.scenario`

- [ ] 写入失败测试：EOS purpose 只接受 EOS scenario，未知或混合意图仍失败。
- [ ] 运行 `dotnet test tests/NoiraPlayer.Core.Tests/NoiraPlayer.Core.Tests.csproj --filter "FullyQualifiedName~PlaybackQualityReferenceManifestTests"`，确认因场景尚未支持而失败。
- [ ] 最小实现常量、允许值和意图映射。
- [ ] 重跑测试并确认通过。

### Task 2: runner 与 native helper 自然结束证据

**Files:**
- Modify: `tools/quality-run/NativeHeadlessHarness.psm1`
- Modify: `tools/quality-run/Invoke-PlaybackQualityManifest.ps1`
- Modify: `tests/NoiraPlayer.Native.Tests/NativePlaybackGraphHeadlessSmokeTests.cpp`
- Test: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/NativePlaybackGraphDecouplingContractTests.cs`

**Interfaces:**
- Consumes: CLI `--scenario end-of-stream`
- Produces: `endOfStreamAttempted`, `endOfStreamObserved`, `endOfStreamStatus`, `endOfStreamPositionTicks`

- [ ] 写入失败 contract 测试，要求 runner 接受 EOS、helper 监听自然 `Stopped / Playback ended.` 并输出四个字段。
- [ ] 运行对应 Core contract 测试，确认失败原因是 EOS 路径缺失。
- [ ] helper 增加自然结束原子状态和有界等待；自然结束后冻结 snapshot，最后才调用 `graph.Stop()` 清理。
- [ ] runner 仅透传场景，不生成 EOS 字段。
- [ ] 重跑 contract 测试并确认通过。

### Task 3: 严格解析与 lifecycle 生成

**Files:**
- Modify: `tools/NoiraPlayer.PlaybackQuality.Headless/Program.cs`
- Modify: `tools/quality-run/run-native-headless-harness-smoke-test.ps1`
- Test: `tools/quality-run/run-playback-quality-cli-smoke-test.ps1`

**Interfaces:**
- Consumes: helper 四个 EOS 字段和 selected scenario
- Produces: 真实 `lifecycle.endOfStream` 或明确 collector failure

- [ ] 添加完整字段通过、缺字段拒绝、矛盾字段拒绝三个失败测试。
- [ ] 运行 `powershell -NoProfile -ExecutionPolicy Bypass -File tools/quality-run/run-playback-quality-cli-smoke-test.ps1`，确认新增 fixture 先失败。
- [ ] 最小实现 EOS parser；只有 attempted=1、observed=1、status=completed 才写 lifecycle。
- [ ] 重跑 CLI smoke 并确认所有 fixture 通过。

### Task 4: 真实 stable case 与全量验证

**Files:**
- Modify: `tools/quality-run/run-native-headless-harness-smoke-test.ps1`
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`

**Interfaces:**
- Consumes: 本地生成的约 5 秒媒体
- Produces: stable EOS report 和版本化结论

- [ ] 在 native smoke manifest 加入独立 stable EOS case，timeout 大于样本时长但保持有界。
- [ ] 运行 native smoke，检查 helper、report、materialize、validate 全链路均包含真实 EOS。
- [ ] 运行 `powershell -NoProfile -ExecutionPolicy Bypass -File tools/quality-run/run-playback-core-checks.ps1 -AppDiffBase main`。
- [ ] 运行 Modern App Debug x64 Native AOT Publish，确认完整 App 编译通过。
- [ ] 用中文更新 STATUS/DECISIONS，明确软件证据边界和未验证风险。
- [ ] 提交实现与文档。
