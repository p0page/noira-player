# Native 交互证据与字幕重同步设计

## 背景

当前 `local/native-headless-av-smoke` 在 manifest 中声明了 `audio-switch`、`subtitles`、`seek/timeline` 等意图，但样本只有一条音轨，native helper 只执行 `Seek(0)`，也没有调用音轨或字幕切换。因此现有报告只能证明轨道发现，不能证明真实切换成功。

同时，`PlaybackGraph::SwitchSubtitleStream` 只关闭旧字幕并注册新字幕流。共享 FFmpeg demuxer 在播放中已经越过当前字幕 cue 的 packet，新注册的字幕流无法取得覆盖当前播放点的历史 packet。Kodi 对嵌入字幕采用同一原则：切换流后回到当前播放时间重新同步 demuxer，使当前字幕 packet 被重新读取。

## 目标

1. 保持 24 个 case ID 不变，把现有 A/V case 升级为真实双音轨、双字幕、非零 seek 的 native 行为案例。
2. cadence 和 A/V 指标继续在交互操作前采集，避免 pause、切轨和 seek 污染原有帧率基线。
3. 报告只在真实操作执行后记录 `audio-switch`、`subtitle-switch`、`subtitle-off` 和非零 `seek`；操作失败必须显示为失败，不能仅凭函数返回或轨道发现伪装成功。
4. 用新 manifest 先生成旧 Core baseline，再做最小 Core 修复，并使用同一 manifest 生成 candidate/repeat/comparison。

## 方案选择

### 方案 A：新增独立 interaction case

优点是职责最清楚；缺点是 manifest case 数和基线结构都会增加，且与现有 A/V case 重复打开同一类样本。

### 方案 B：所有 native helper case 都自动尝试切轨

优点是 runner 改动少；缺点是 video-only 和单轨样本会产生大量无意义 skip，操作语义也无法由 manifest 明确表达。

### 方案 C：强化现有 A/V case（采用）

保留 `local/native-headless-av-smoke`，把其本地生成样本改为双音轨、双字幕，并让 helper 仅在检测到对应轨道时执行交互。这样不增加 case ID，同时让原有 `audio-switch`/`subtitles` purpose 变成真实证据。manifest 内容变化视为一次显式版本升级，旧 artifact 不与新 artifact 混用。

## 样本与执行流程

生成一个 6 秒、30fps、H.264 SDR MP4：

- 视频：`testsrc2`，BT.709 limited range；
- 音轨 1：48kHz 正弦波，默认轨，语言 `eng`；
- 音轨 2：不同频率的 48kHz 正弦波，语言 `jpn`；
- 字幕 1、2：不同文本，各自 cue 覆盖完整 0-6 秒，封装为 `mov_text`。

helper 按以下阶段运行：

1. 打开默认音轨、关闭字幕并播放；
2. 在交互前截取 cadence/A/V/buffering 快照；
3. 执行 pause/resume；
4. 切换到另一条音轨，确认新音频时钟继续前进且新解码帧被提交；
5. 切换到第一条字幕，确认真实文本 overlay 至少成功绘制一次；
6. 切换到第二条字幕并再次确认 overlay；
7. 关闭字幕并确认选择状态清空；
8. seek 到 `1s`，记录实际首个呈现帧位置和误差，再确认播放继续前进；
9. 停止并输出结构化操作结果。

helper 的进程退出与报告生成不能因单个交互失败而丢失全部证据。每个操作输出 attempted/completed、目标 stream index、前后位置及失败原因；C# harness 将其转换为 lifecycle event。`failed` lifecycle 必须进入 evaluator 的失败检查，failure area 分别归入 `tracks`、`subtitles` 或现有的 `timeline`。

## Core 修复边界

预期首先暴露的 Core 问题是嵌入字幕切换后没有当前 cue。修复只调整 `PlaybackGraph::SwitchSubtitleStream`：

1. 注册新字幕流；
2. 在同一 graph lock 下，以当前播放位置执行一次完整 demux 重定位；
3. 清空 pending video、audio renderer、video/audio/subtitle decoder 的旧状态；
4. 设置 video preroll target，恢复新时间线；
5. 保持暂停状态不变，不创建第二条播放线程，不修改 UI 或 App。

该修复复用现有 `VideoDecoder::Seek` 和共享 `FfmpegMediaSource::Seek`，不新增独立 demuxer，不复制 Kodi 的完整消息队列架构。字幕关闭不需要 seek，只关闭 decoder/renderer 并清空选择。

## 测试与证据

测试顺序遵循 TDD：

1. 先让 smoke 断言双音轨、双字幕、非零 seek 和 lifecycle 结果；旧 helper 必须红灯。
2. 补 helper 操作和结构化解析，使旧 Core baseline 能诚实报告字幕切换失败，而不是丢报告。
3. 为 evaluator 增加失败 lifecycle 测试，确认 report result 为 `fail`。
4. 为字幕 overlay 绘制增加最小 testability hook；它只暴露真实绘制计数，不提供改变播放行为的测试后门。
5. 为字幕切换重同步写失败测试，再实现最小 Core 修复。
6. 运行完整 playback-core checks。
7. 用升级后的同一 manifest 生成 baseline、candidate 和至少 3 次 native repeat，输出 matched signals、improved/regressed/mixed、风险和采纳结论。

## 采纳标准

- A/V case 报告包含两条音轨和两条字幕；
- `audio-switch`、两次 `subtitle-switch`、`subtitle-off`、非零 `seek` 均由真实 native 操作产生；
- 两次字幕切换后都有 overlay 绘制证据；
- seek target 为 `1s`，误差不超过现有 `250ms` 标准，且 seek 后播放继续；
- 同 manifest candidate 相对 baseline 修复目标失败，stable case 无新增回归；
- cadence/A/V 指标仍来自操作前快照，评测阈值和样本预期未为通过测试而放宽。

## 明确不做

- 不修改 App/UI；
- 不验证 HDMI、HDR 输出或显示器行为；
- 不在本轮实现 Kodi 完整 RenderManager；
- 不把轨道被发现、selected index 被赋值或函数未抛异常单独当作播放成功；
- 不使用私人 Emby 凭据或把私人地址写入仓库。
