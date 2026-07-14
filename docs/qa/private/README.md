# 私有与本地播放质量样本

这个目录用于放置本机私有播放质量样本定义。目标是让每个开发者都能用自己的 Emby 库、本地文件和采集报告运行同一套评测流程，同时不把真实私有信息提交进仓库。

可以提交的文件：

- `README.md`：本规范。
- `*.template.json`：不含敏感信息的可复制模板。

必须保持忽略的文件：

- `*.local.json`：真实本地 manifest、合并 manifest、run plan、validation 和 probe 结果。
- `*.private.json`、`*.secrets.json`：私有数据或密钥数据。
- `reports/`：私有采集报告集。

## 目录布局

推荐使用以下路径：

```text
docs/qa/private/
  reference-manifest.template.json
  ui-real-samples.template.json
  emby-reference-manifest.local.json
  ui-real-samples.local.json
  title-reference-manifest.local.json
  combined-reference-manifest.local.json
  combined-run-plan.local.json
  reports/
```

仓库只保存模板和规范。真实本地 manifest 与报告保持 ignored。

## Timeline 样本规则

`timeline` case 必须同时声明：

- `startPositionTicks`：起播位置，单位为 100ns tick。
- `seekTargetPositionTicks`：本次操作的绝对逻辑目标，必须非负且与起播位置不同。
- `expected.maxSeekPositionErrorMs`：允许的首帧落点误差。

私有 Emby 生成器根据服务端返回的真实 `RunTimeTicks` 选择确定性目标：长片通常从前段起播并跳到约 50% 位置，同时与起点、片尾保留安全距离。运行时不得根据文件名、当前进度或固定 1 秒偏移改写目标。manifest、run plan、native/App 命令和 report 中的目标必须完全一致；旧的隐式目标报告不能进入 v0.19 baseline。

如果运行期间还有其他视频占用网络、CPU、GPU 或硬件解码器，本轮必须标记为性能环境受干扰。seek 是否调用、目标是否一致、首帧落点和后续推进仍可用于功能诊断；启动、buffering、starvation、cadence、A/V sync 和恢复耗时不得用于接受性能候选。

## UI 真实样本 Manifest

UI 开发数据源的权威规则见 `docs/qa/ui-development-data-sources.md`。当前规则是：不再使用 `*-fixture` 或 `details-real-*` route。需要打开真实 Emby 条目时，在 ignored 的 `ui-real-samples.local.json` 中维护样本列表，然后写入 app 的 `dev-command.json`。

从模板复制：

```powershell
Copy-Item docs\qa\private\ui-real-samples.template.json docs\qa\private\ui-real-samples.local.json
```

写入当前安装包的 LocalState：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Write-AppUiSampleCommand.ps1 -ManifestPath docs\qa\private\ui-real-samples.local.json -SampleId 'movies/example-details'
```

支持的 UI route 只包括真实页面入口，例如 `home`、`movies`、`tv`、`search`、`details`、`photo`、`playback`、`quality-run`。`details`、`photo`、`playback` 必须提供真实 `itemId`；`quality-run` 必须提供 `itemId` 或 `streamUrl`。

`itemId`、`mediaSourceId`、私有标题、私有 stream URL 和任何服务器信息只能出现在 ignored 的 `ui-real-samples.local.json` 中，不得写入模板、文档示例或测试。

## Manifest 格式

私有/本地样本沿用现有 `PlaybackQualityReferenceManifest` JSON 格式：

```json
{
  "schemaVersion": 1,
  "cases": []
}
```

每个 case 至少需要：

- `caseId`：稳定的报告匹配 ID。
- `category`：`stable`、`challenge` 或 `quarantine`。
- `severity`：`info`、`low`、`medium`、`high` 或 `critical`。
- `stability`：`stable`、`variable`、`flaky` 或 `unknown`。
- `uri`：`emby://items/<item-id>`、`file:///...`、`http://...` 或 `https://...`。
- `tier`：`0` 到 `4` 的整数。
- `purpose`：一个或多个能力标签。
- `executionRequirement.minimumEvidenceLevel`：正式播放 case 通常为 `native-playback`。
- `executionRequirement.scenario`：唯一执行场景，取 `playback`、`timeline`、`audio-switch`、`subtitle-switch` 或 `pause-resume`。
- `expected.codec`、`expected.width`、`expected.height`、`expected.frameRate`、`expected.hdrKind`。

可直接播放的 HDR case 必须保留主 HDR 颜色期望，并声明 `expected.sdrDisplayFallback`。fallback 只描述无 HDR 显示或 force-SDR 时的颜色输出，至少包含 `hdrOutput`、`dxgiInputAnyOf`、`dxgiOutput` 和精确的 `requiredConversionStatus` token；只有环境确实要求固定位深时才填写可选 `isTenBitSwapChain`。不要把主期望直接改成 SDR，也不要用文件名推断该对象。

`itemId` 和 `mediaSourceId` 只能出现在 ignored 的本地 manifest 中。不要把真实 Emby ID 写进可提交文件。

## Case 命名

推荐 `caseId` 格式：

```text
private-emby/<short-label>/<purpose>
local-file/<short-label>/<purpose>
public-local/<short-label>/<purpose>
```

示例：

```text
private-emby/kashi-dv/dv-fallback
private-emby/narcos-s03e01/hdr10-cadence
local-file/chimera-23976/hdr10-cadence
```

label 应该稳定、可读。不要在 `caseId` 里包含服务器名、账号名、完整文件路径、token 或私有 URL。

## Purpose 标签

优先使用现有标签，这样 evaluator 才能推导 required signals：

- `sdr-smoke`
- `hdr-output`
- `hdr-force-sdr`
- `dv-reject`
- `dv-fallback`
- `cadence-23.976`
- `cadence-24`
- `cadence-30`
- `cadence-60`
- `frame-pacing`
- `av-sync`
- `buffering`
- `timeline`
- `tracks`
- `subtitles`
- `audio-switch`
- `subtitle-switch`
- `subtitle-off`
- `end-of-stream`
- `error-handling`
- `unsupported-source`

如果确实需要新 purpose，应先在 evaluator policy 中明确加入。不要在私有 manifest 里临时创造标签并期待模型分析能正确理解。

一个 case 最多只能表达一种主动执行意图。`timeline`、`audio-switch`、`subtitle-switch`、`pause-resume` 不得混在同一个 case；`tracks`、`subtitles`、`sdr-smoke` 等被动观测标签可以与对应场景共存。需要验证多个动作时应拆成多个 case，并保持同一 `itemId` / `mediaSourceId`。

## Expected 证据

`expected` 表达参考预期，不是 actual runtime evidence。

最低字段示例：

```json
{
  "codec": "hevc",
  "width": 3840,
  "height": 2160,
  "frameRate": 23.976,
  "hdrKind": "Hdr10"
}
```

只有当值来自 Emby playback-info、`ffprobe` 或真实解析结果时，才加入 source/color 预期：

```json
{
  "videoRange": "HDR10",
  "colorPrimaries": "bt2020",
  "colorTransfer": "smpte2084",
  "colorSpace": "bt2020nc",
  "hdrOutput": "Hdr10",
  "dxgiInput": "YCBCR_STUDIO_G2084_TOPLEFT_P2020",
  "dxgiOutput": "RGB_FULL_G2084_NONE_P2020",
  "requireValidatedConversion": true
}
```

不要从文件名、展示标题、`caseId` 或人工猜测倒填 actual 行为。实际 source color、DXGI mapping、timing、buffering、A/V sync 和 track 证据必须来自 collector report。

## 创建私有 Emby Manifest

账号信息只允许放在环境变量或其他 ignored secret store：

```powershell
$env:NOIRAPLAYER_QA_SERVER_URL = '<private-emby-url>'
$env:NOIRAPLAYER_QA_USERNAME = '<private-user>'
$env:NOIRAPLAYER_QA_PASSWORD = '<private-password>'
```

生成 ignored manifest：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\New-PrivateEmbyReferenceManifest.ps1 -OutputPath docs\qa\private\emby-reference-manifest.local.json -Limit 1000
```

定位特定片源：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\New-PrivateEmbyReferenceManifest.ps1 -SearchTerm '<title>' -OutputPath docs\qa\private\<title>-reference-manifest.local.json -Limit 50
```

生成器会从 `MediaSources`、`MediaStreams` 和 `/Items/{ItemId}/PlaybackInfo` 读取真实 stream metadata。Dolby Vision 和 HDR 分类必须来自 stream metadata，不得依赖文件名。

## 本地文件或 Direct URI Case

本地文件 case 在 ignored 的 `*.local.json` manifest 中使用 `file:///` URI。不要把完整本地路径写入可提交文件。

公共 URL 如果复制到本地 manifest，可使用 `http://` 或 `https://`。依赖该 case 前先探测 metadata：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\Test-PublicReferenceMedia.ps1 -CaseId '<case-id>' -OutputPath docs\qa\private\<case-id-safe-name>-probe.local.json
```

## 校验与合并

校验本地 manifest：

```powershell
dotnet run --project tools\NoiraPlayer.PlaybackQuality.Cli\NoiraPlayer.PlaybackQuality.Cli.csproj -- validate-manifest --manifest docs\qa\private\emby-reference-manifest.local.json --output docs\qa\private\emby-reference-manifest-validation.local.json
```

合并公开与私有 manifest：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\quality-run\Merge-ReferenceManifests.ps1 -ManifestPath "docs\qa\playback-quality-reference-manifest.example.json,docs\qa\private\emby-reference-manifest.local.json" -OutputPath docs\qa\private\combined-reference-manifest.local.json -DuplicateCaseIdMode skip
```

创建 run plan：

```powershell
dotnet run --project tools\NoiraPlayer.PlaybackQuality.Cli\NoiraPlayer.PlaybackQuality.Cli.csproj -- plan-runs --manifest docs\qa\private\combined-reference-manifest.local.json --reports-dir docs\qa\private\reports\baseline --duration 60 --output docs\qa\private\combined-run-plan.local.json
```

## 提交规则

禁止提交：

- 私有 Emby 服务器 URL；
- 用户名、密码、API key、access token 或 cookie；
- 真实 `itemId` 或 `mediaSourceId`；
- 私有 direct stream URL；
- 指向私有媒体的本地完整文件路径；
- 私有播放采集报告；
- 生成的 `*.local.json`、`*.private.json` 或 `*.secrets.json` 文件。

提交样本相关改动前，先对 changed files 执行敏感字符串扫描，并检查 `git status --short`。`docs/qa/private/` 下通常只有 `README.md` 和 `*.template.json` 应该进入 tracked 状态。
