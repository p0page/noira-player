# 播放评测真实执行证据契约设计

## 背景

当前正式 baseline 会先对公开和私有 manifest 的所有 case 运行 `core-probe`。`core-probe` 不打开网络、native playback graph、demux、解码器或渲染器，却会用 manifest 的 `expected` 构造 diagnostic source 和确定性指标。通用 evaluator 随后可能把这些报告判为 `pass`，report-set validator 也只检查 case、字段和结果形状，因此未播放媒体的 case 可以进入有效 baseline。

这使“报告结构完整”和“媒体真实执行”失去边界。旧 `v0.1-core-probe` 只能作为评测器及 orchestration 自测，不能作为播放器质量 baseline。

## 目标

建立机器强制执行的证据契约，使正式播放 baseline 中的每个 stable/challenge case 都能回答：

1. 哪个 runner 在什么时候尝试了该 case；
2. runner 实际打开的源是否与 manifest locator 关联；
3. native graph、demux、decoder 和 playback sample 是否真实发生；
4. case 是完成、失败、不支持，还是根本没有执行；
5. 该证据是否达到 case 要求的最低等级。

本阶段只处理纯软件可闭环证据，不宣称验证 HDMI、电视面板、HDR InfoFrame 或肉眼颜色准确性。

## 非目标

- 不调整播放质量阈值来迁就当前 Core。
- 不重写 cadence、A/V sync、buffering、seek、track、subtitle 或 color evaluator。
- 不自动回滚既有 Core/native 优化。
- 不用 collector 名称黑名单判断证据真假。
- 不把 App-hosted 设为每次 Core 调优的必经步骤；它只用于代表性集成复核。

## 核心原则

### 1. Expected 与 actual 单向隔离

manifest `expected` 只能参与判定，不能填充 report 的 source、timing、buffer、sync、track、color 或 execution actual。actual 必须来自 runner、native helper 或 App runtime observation。

### 2. 执行结果与质量结果分离

`report.result` 继续表达播放质量结果：`pass`、`fail`、`unsupported`、`error`、`skip`。

新增 `report.execution.status` 表达执行过程：`completed`、`failed`、`unsupported`、`cancelled`、`timed-out`、`skipped`。

播放器真实失败可以形成有效评测证据。`report-set.isValid = true` 表示证据链完整，不表示所有 case 播放通过。

### 3. 证据等级是显式契约

证据等级按能力递增：

1. `orchestration`：只执行 Core 控制状态机，不打开媒体；
2. `native-playback`：真实打开 native graph 和媒体源，并产生 native runtime observation；
3. `app-hosted`：通过完整 App adapter 驱动 native playback。

manifest case 通过 `executionRequirement.minimumEvidenceLevel` 声明最低等级。正式公开/私有播放 case 默认要求 `native-playback`；评测器自测 manifest 才能明确要求 `orchestration`。

### 4. Probe 与正式 baseline 物理隔离

`core-probe` 保留，用于验证 PlaybackOrchestrator 生命周期和报告 plumbing。它只能生成 `orchestration` 证据，产物进入 evaluator-self-test，不得与播放 baseline 合并，也不得满足 `native-playback` case。

### 5. 不保存私有源和凭据

报告不得保存完整 URL、query、token、服务器地址、用户名或密码。执行关联使用：

- `sourceLocatorHash`：manifest locator 原文的 SHA-256；
- `openedSourceHash`：runner 交给 native helper 的最终源 locator 的 SHA-256。

两者只用于同一次运行内的关联和防串 case，不是安全认证。私有 Emby 凭据只从环境变量或 ignored 本地文件读取。

## 数据契约

### Manifest case

```json
{
  "caseId": "jellyfin/hdr10-hevc-main10-4k60-50m",
  "uri": "https://example.invalid/video.mp4",
  "executionRequirement": {
    "minimumEvidenceLevel": "native-playback"
  }
}
```

manifest schema 升级为 v2。v1 可继续用于读取历史资料，但正式 baseline 命令必须拒绝没有 execution requirement 语义的旧 manifest。

### Report execution

```json
{
  "execution": {
    "attemptId": "uuid",
    "runner": "native-headless",
    "evidenceLevel": "native-playback",
    "status": "completed",
    "sourceLocatorHash": "sha256:...",
    "openedSourceHash": "sha256:...",
    "startedAtUtc": "2026-07-11T00:00:00Z",
    "durationMs": 5000,
    "sourceOpenAttempted": true,
    "sourceOpened": true,
    "nativeGraphOpened": true,
    "demuxStarted": true,
    "decoderOpened": true,
    "playbackSampleObserved": true
  }
}
```

执行布尔值必须来自实际过程或明确 runtime observation。validator 还会与现有 `runtimeMetrics.hasPlaybackSample`、decoded/rendered frame、error/skip envelope 交叉检查，避免仅填一组布尔值就获得通过。

## Validator 规则

report-set validation 输出：

- `structureValid`：schema、case/report 对应、字段和值域是否正确；
- `executionValid`：case 是否达到最低证据等级；
- `isValid`：两者都成立；
- `declared/attempted/opened/decoded/rendered/completed/failed/unsupported/skipped/missing` 计数。

规则如下：

1. stable/challenge case 必须有真实 attempt；`skip`、缺报告或 probe 证据使 `executionValid = false`。
2. quarantine 可以缺席，但不得计入执行覆盖率；存在报告时仍必须通过结构和证据检查。
3. 普通视频 case 的 `pass`/`fail` 必须具有 source opened、native graph、demux、decoder、playback sample、decoded frame 和 rendered frame 证据。
4. `error` 可以在 source open 前结束，但必须保留真实 attempt、执行状态和稳定错误分类。
5. `unsupported` 必须来自真实解析/分类 attempt，不得由 manifest expected 直接生成。
6. `orchestration` 报告永远不能满足 `native-playback` 或 `app-hosted` case。
7. locator hash 不匹配、attemptId 缺失、时间无效、证据字段相互矛盾时均拒绝进入正式 baseline。

## Runner 设计

新增一个 manifest runner，执行流程为：

```text
manifest v2
  -> validate manifest
  -> resolve case locator
  -> invoke native-headless for every selected case
  -> persist one raw report per case, including failures
  -> strict validate-report-set
  -> analyze/compare
```

约束：

- 不允许自动回退到 core-probe。
- 不允许整段静默重试；单个底层有界恢复可以记录在 native metrics 中。
- direct URI 必须逐个传给 native helper。
- Emby item 通过 `EmbyApiClient` 和环境凭据解析指定 `itemId/mediaSourceId`，避免复制 App 的 direct-stream URL 规则。
- 每个 case 无论成功、失败或不支持，都必须产生一个与 attempt 对应的报告。
- helper 构建一次后复用，避免每个 case 重编译。

## Baseline 与 comparison

`New-PlaybackCoreTuningBaseline.ps1` 不再调用 `materialize-core-probe-report-set` 填充播放 case。probe 自测单独运行，正式 baseline 只消费 manifest runner 的真实报告。

baseline/candidate 比较前必须匹配：

- manifest schema/version 与 manifest 内容 hash；
- caseId；
- 最低证据等级；
- metric/evaluator version；
- source locator/opened source hash；
- runner 的可比较执行范围。

任一侧缺乏真实执行证据时，comparison 必须输出 `insufficient-evidence`，不能产生 improvement/regression 结论。

## Timeline 与 seek 必须成为一等证据

用户已观察到部分素材的进度位置错误，拖动后也没有实际落到目标位置。当前链路存在三个需要用证据区分的时间域：

1. Emby item/media source 声明的 `RunTimeTicks`；
2. FFmpeg container/stream 的 duration、`start_time`、time-base 和原始 PTS；
3. App 对外显示的从零开始逻辑 position，以及 native 实际 seek/present position。

当前 App 使用 Emby request 的 `RuntimeTicks` 设置 Slider 最大值，当前位置来自 native backend；native `VideoDecoder::Seek` 则直接把逻辑 position 换算到 stream time-base。对具有非零 container/stream start time、编辑列表或异常时间戳的素材，这些时间域可能不一致。

本地 Kodi `CDVDDemuxFFmpeg::SeekTime` 会把 format start time 纳入 seek target，并在播放器状态中用 `ptsStart` 把内部 PTS 归一化为对外时间。后续实现还要对照 VLC 和 mpv 的 demux timeline/seek 代码，并记录所参考的仓库 commit；只提炼时间轴原则，不整体移植其复杂状态机。

评测报告需要补齐：

- Emby/item duration 与 native/container duration；
- container/stream timeline origin；
- seek requested logical position；
- 交给 demuxer 的实际 timestamp；
- seek 后第一帧/第一音频样本 position；
- seek landing error；
- seek 后 position 是否继续按墙钟推进；
- seek 失败、不可 seek 和超时的稳定分类。

必须增加至少三类 case：零 start time、非零 start time、带 resume position 的 seek。只有提交目标、landing 和 post-seek advancement 都成立，才能判断 seek 有效；UI 把 Slider 值改到目标位置不算播放成功。

## 迁移与旧成果处理

1. 保留历史报告，但标记 v1 playback baseline 为 legacy/untrusted-for-playback。
2. 不删除 core-probe；将其测试和产物迁到 evaluator-self-test。
3. 先用公开 Jellyfin HEVC/HDR/4K URI 生成第一套 v2 native baseline。
4. 再用 ignored 私有 manifest 加入“一战再战”和“哈姆奈特”。
5. 用新证据链重放当前 HEAD，并按真实结果重新确认既有 Core/native 行为改动。
6. 最后完整构建 App，对代表性 SDR、HDR 和复杂音轨 case 做 App-hosted 抽样复核。

## 验收条件

1. native case 使用 core-probe 报告时 strict validation 必须失败。
2. 删除任一 stable/challenge case 的真实报告后 baseline 必须失败。
3. 无网络、坏文件、decoder failure 均产生真实 error attempt，不产生 pass。
4. 公开 manifest 的 URI 逐个进入 native helper，并具有非零真实执行覆盖计数。
5. 私有 Emby locator 与最终 opened source 只以 hash 出现在报告中。
6. baseline/candidate 不能跨证据等级或跨源比较。
7. 新 baseline 能诚实显示 pass、fail、unsupported 和 error，不以全部通过为完成条件。
8. 完整 App 构建成功，代表性 case 完成 App-hosted 复核；该步骤不替代 App-free Core 日常评测。
9. 非零 start time 素材的逻辑 position 从零开始，seek landing 与 post-seek advancement 可复现且符合阈值。
