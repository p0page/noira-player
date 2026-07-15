# Video Render Phase Evidence Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 建立 v0.16 视频渲染阶段证据，在同 manifest baseline 证明瓶颈后，仅对 D3D11 video processor 资源复用做单变量候选。

**Architecture:** `DxDeviceResources` 产生每帧 CPU 提交阶段样本，`PlaybackGraph` 汇总为 native quality metrics，经 WinRT、Core mapper、headless parser 和 strict evaluator 进入报告。证据提交与缓存策略提交分离；cache key 变化、device/swapchain 重建时失效。

**Tech Stack:** C++17、D3D11 Video Processor、C++/WinRT IDL、C#/.NET 10、PowerShell quality runner。

---

### Task 1: 锁定 v0.16 报告合同

**Files:**
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityMetricsSnapshot.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReport.cs`
- Modify: `src/NoiraPlayer.Core/PlaybackQuality/PlaybackQualityReportMapper.cs`
- Modify: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualityReportMapperTests.cs`
- Modify: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/PlaybackQualityReferenceManifestTests.cs`

1. 先写失败测试，要求四类路径计数、五个阶段 sample count 与 P50/P95/P99/Max 完整映射。
2. 将 evaluation version 升为 `playback-quality-v0.16`，完成态 native report 缺任一新增字段必须被 strict validator 拒绝。
3. 运行定向 Core tests，确认 RED 后最小实现到 GREEN。
4. 提交 `feat: define video render phase evidence contract`。

### Task 2: 在 native 渲染路径采集非重叠阶段

**Files:**
- Modify: `src/NoiraPlayer.Native/DxDeviceResources.h`
- Modify: `src/NoiraPlayer.Native/DxDeviceResources.cpp`
- Modify: `src/NoiraPlayer.Native/Media/VideoRenderer.h`
- Modify: `src/NoiraPlayer.Native/Media/VideoRenderer.cpp`
- Modify: `src/NoiraPlayer.Native/Media/PlaybackGraph.cpp`
- Modify: `src/NoiraPlayer.Native/Media/PlaybackQualityMetrics.h`
- Test: `tests/NoiraPlayer.Native.Tests/PlaybackQualityMetricsTests.cpp`
- Test: `tests/NoiraPlayer.Native.Tests/DxDeviceResourcesOffscreenTests.cpp`

1. 写失败原生测试，锁定 histogram、路径计数、reset 和失败渲染不冒充成功样本。
2. 增加 `VideoRenderPhaseSample`；用 `steady_clock` 测量 setup、view/target、clear、blit、post-process CPU 区间。
3. `PlaybackGraph` 只在对应路径实际执行时记录样本；direct-copy/BGRA 不伪造 processor 时间。
4. 运行原生 metrics 与 DX offscreen tests，再运行 Native Debug x64 build。
5. 提交 `feat: capture native video render phase timings`。

### Task 3: 贯通 WinRT、App-free helper 与严格 parser

**Files:**
- Modify: `src/NoiraPlayer.Native/NativePlaybackEngine.idl`
- Modify: `src/NoiraPlayer.Native/NativePlaybackQualityMetrics.h`
- Modify: `src/NoiraPlayer.Native/NativePlaybackEngine.cpp`
- Modify: `tools/NoiraPlayer.PlaybackQuality.Headless/Program.cs`
- Modify: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/NativeQualityMetricsBridgeContractTests.cs`
- Modify: `tests/NoiraPlayer.Core.Tests/PlaybackQuality/NativePlaybackGraphDecouplingContractTests.cs`

1. 先扩展 bridge/parser 缺字段负例并确认 RED。
2. 逐字段透传 counts 与 P50/P95/P99/Max，不允许从总 render 时长推算阶段值。
3. helper raw output 缺字段、负值、NaN 或路径计数矛盾必须失败。
4. 运行 Core 定向测试、headless parser contracts 和完整 native smoke。
5. 提交 `feat: expose video render phase evidence`。

### Task 4: 建立默认行为 v0.16 baseline

**Files:**
- Modify: `docs/STATUS.md`
- Modify: `docs/DECISIONS.md`
- Artifacts: ignored `artifacts/quality-run/playback-core-v016-render-phases-<sha>.local/`

1. 用当前默认 5ms、无缓存策略运行 `New-PlaybackCoreTuningBaseline.ps1`。
2. 对 force-SDR、公开 SDR、本地 HDR/SDR、私有 DV8 各重复至少三轮目标 case。
3. 输出阶段占比、跨轮 spread、transport wait 与 render phase 的区分；不修改阈值。
4. 若 setup 不占主导，停止缓存方案并按证据另写候选。
5. 提交 baseline 结论文档，不提交私有报告。

### Task 5: 有条件实现 processor/enumerator 缓存候选

**Files:**
- Modify: `src/NoiraPlayer.Native/DxDeviceResources.h`
- Modify: `src/NoiraPlayer.Native/DxDeviceResources.cpp`
- Test: `tests/NoiraPlayer.Native.Tests/DxDeviceResourcesOffscreenTests.cpp`

1. 仅当 Task 4 支持假设时，先写 cache key、hit/miss、device/swapchain invalidation 的失败测试。
2. 只缓存 enumerator 与 processor；input/output view 和 intermediate texture 保持原行为。
3. 运行定向原生测试和 Native Debug x64 build并提交候选。
4. 用 Task 4 exact manifest 生成 candidate 与三轮目标重复，运行 comparator；任何新增回归立即 `git revert`。

### Task 6: 最终门禁与 App 复核

1. 运行 `powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\run-playback-core-checks.ps1 -AppDiffBase main`。
2. 若候选被采纳，完整编译 Modern App Debug x64，并运行代表性 App-hosted SDR/HDR fallback case。
3. 更新 `docs/STATUS.md` 与 `docs/DECISIONS.md`，明确 accepted/rejected、软件证据边界和剩余风险。
4. 提交最终结论；不把 ignored 私有凭据、manifest 或报告加入 Git。
