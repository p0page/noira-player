# Xbox Fluent UI Redesign Design

日期：2026-07-05

## 目标

把当前 Xbox Emby 客户端从“能连上服务器并播放”的工程雏形，改造成一个 Xbox 手柄可日用的媒体中心雏形。

本轮只关注基础页面交互和视觉骨架，不展开新的播放内核工作。完成后应用应满足：

- 首页不是静态展示，电影和剧集 Library 都能打开。
- Library 能浏览真实 Emby 内容，并进入详情页。
- 详情页能承担播放前决策，包括继续播放、版本摘要、剧集列表、音轨和字幕概览。
- 播放页是真全屏体验，不再像调试工具。
- 切版本、切字幕、切音轨、查看播放信息都能在播放覆盖层内完成。
- 全部核心操作仅靠 Xbox 手柄可达，重点优化 D-pad、A、B、Menu。

## 设计方向

采用已确认的 **A：Xbox Hub + 横向内容带**。

应用启动后进入一个面向电视的 Home Hub：顶部只保留少量全局入口，主体用横向内容带承载继续观看、最近添加、电影和剧集。Movies/TV 不是装饰卡片，而是可进入的 Library。播放时默认只显示视频，按键后才浮出覆盖层。

视觉上使用影院黑、石墨面板和高对比青色焦点。海报、backdrop 和媒体内容承担主要色彩，应用本身保持安静。控件圆角控制在 8px 以内，焦点边框足够粗，远距离可见。

## 参考依据

- Microsoft 的 Xbox/UWP 输入文档要求 10-foot 应用支持 gamepad/remote，功能需要可发现、可访问，并强调清晰焦点、可预测导航和最短操作路径。
- UWP 会把方向键映射到 D-pad，也会把左摇杆映射到方向键；A/Select 对应确认，B/Back 对应返回，Menu 可打开上下文菜单。这解释了为什么左摇杆误触需要特别处理。
- Kodi 的播放动作模型偏即时跳转：`StepForward`、`StepBack`、`BigStepForward`、`BigStepBack` 和 `Seek(##)` 都是直接改变播放位置的动作。
- Apple TV 的时间轴 scrub 模型偏预览提交：拖动时间轴时显示预览缩略图，确认后从新位置播放，Back/Menu 可取消并回到原位置。

本项目采用混合策略：**离散小步跳转保持即时，时间轴拖动和摇杆 seek 进入可取消的预览提交模式。**

参考链接：

- Microsoft Gamepad and remote control interactions: https://learn.microsoft.com/en-us/windows/uwp/ui-input/gamepad-and-remote-interactions
- Microsoft Reveal Focus: https://learn.microsoft.com/en-us/windows/uwp/ui-input/reveal-focus
- Kodi Action IDs: https://kodi.wiki/view/Action_IDs
- Kodi Modify time seeking: https://kodi.wiki/view/HOW-TO%3AModify_time_seeking
- Apple TV Control video playback: https://support.apple.com/guide/tv/control-video-playback-atvb7944597f/tvos

## 画布和视觉系统

目标显示设备可以是 4K，但 UI 按 1080p 逻辑画布设计，由系统缩放到电视。原因是 Xbox 10-foot UI 的可读性比像素密度更重要；4K 主要用于视频内容本身，而不是让 UI 变得更密。

色彩：

- Cinema Black `#070A0E`：主背景和播放页底色。
- Graphite Panel `#111923`：浮层、设置面板、详情页信息面。
- Slate Surface `#172231`：列表项、卡片、抽屉背景。
- Focus Cyan `#00A6D6`：唯一主焦点色，用于 Reveal Focus 风格边框和选中态。
- Action Green `#66D17A`：播放、继续观看等正向主操作。
- Text Primary `#F3F7FB`，Text Muted `#A9B8C8`。

排版：

- 使用 Segoe UI Variable / Segoe UI，符合 UWP/Xbox 生态。
- 首页标题和详情标题用较大字号，但卡片、抽屉、按钮内文字保持紧凑，不把工具界面做成落地页。
- 不使用负字距，不随 viewport 宽度缩放字体。

布局：

- 电视安全区内保留 48px 到 64px 的主要内容边距。
- 海报、横向行、网格、按钮和 OSD 控件都有固定或响应式稳定尺寸，焦点出现时不能挤压布局。
- 页面不使用嵌套卡片。重复媒体项可以是卡片；页面段落本身使用全宽布局或无框布局。

## 页面结构

### App Shell

当前桌面式 `NavigationView` 不适合 Xbox。本轮改为电视端 Shell：

- 默认全屏。
- 顶部或左上角保留轻量导航：Home、Movies、TV、Search、Settings。
- 全局导航不是常驻大侧栏，避免侵占内容空间。
- B 从子页面返回上一级；如果在 Home，不主动退出应用。
- Menu 在非播放页打开当前页面动作菜单，例如排序、筛选、刷新。
- Y 可作为搜索加速键，但搜索入口必须在 UI 上可见，不能只靠快捷键。

### Home

Home 是媒体中心入口，不是营销页。

内容顺序：

1. 继续观看 Hero：展示最近一个可续播项目，包含播放/详情入口。
2. Continue Watching 行：横向滚动，优先显示未看完项目。
3. Recently Added 行：最近添加电影和剧集。
4. Movies 行：展示最近或推荐电影，并提供“全部电影”入口。
5. TV 行：展示最近或推荐剧集，并提供“全部剧集”入口。

Home 上的 Movies/TV Library 入口必须可点击、可聚焦、可打开。焦点在横向行内左右移动，上下切换行。每一行都要保证当前焦点始终可见。

### Library

Library 分为 Movies Library 和 TV Library，两者共用 TV Grid 模式。

Movies Library：

- 标题、数量、排序状态。
- 排序：标题、添加时间、年份、播放状态。
- 筛选：全部、未观看、已观看、继续观看。
- 海报网格，A 进入详情。
- 分页或增量加载，不能一次性假设库很小。

TV Library：

- 网格展示剧集系列。
- 进入详情后显示季和集。
- 支持继续观看入口，优先指向下一集或未看完集。

空状态：

- 没有库：显示“没有找到电影/剧集库”，提供刷新和返回设置。
- 加载失败：显示服务器错误或网络错误，提供重试。
- 图片加载失败：用稳定占位图，不改变卡片尺寸。

### Detail

详情页负责播放前决策。

电影详情：

- backdrop 背景，左侧/下方是海报和主信息。
- 主操作：继续播放或播放。
- 次操作：版本、音轨、字幕、更多信息。
- 元数据：年份、时长、分辨率、HDR/SDR、视频编码、音频摘要、字幕摘要。
- 版本列表：例如 4K HDR、1080p SDR、导演剪辑版等。

剧集详情：

- 系列详情显示季列表。
- 季内显示集列表，包含集号、标题、进度、时长。
- 主操作指向继续观看或下一集。

从详情页进入播放时，Playback Orchestrator 接收明确的播放描述：ItemId、MediaSourceId、起始位置、默认音轨、默认字幕。

### Player

播放页默认是真全屏视频。正常观看时不显示调试文本框、URL、永久按钮栏或诊断信息。

OSD 触发：

- 播放中按 A：显示/隐藏基础 OSD。
- 播放中按 Menu：显示 OSD 并打开右侧 More 抽屉。
- 播放中按 B：如果有抽屉先关抽屉；如果有 OSD 先隐藏 OSD；如果都没有，返回详情页前需要二次确认或通过长按/菜单动作触发，避免误退。

基础 OSD：

- 底部半透明面板。
- 播放/暂停、进度条、当前时间、剩余时间、标题。
- 快退/快进或小步跳转。
- 右侧入口：版本、音轨、字幕、信息。

More 抽屉：

- 版本：列出 MediaSource，显示分辨率、HDR、编码、码率、容器。
- 音轨：语言、编码、声道、默认/当前状态。
- 字幕：语言、格式、内嵌/外挂、关闭字幕。
- 信息：播放路径、Direct/Original 状态、HDR/SDR、丢帧/缓冲等诊断。

字幕安全：

- OSD 默认从底部浮出，但不要长期覆盖字幕。
- 字幕打开时，OSD 出现可以临时上移字幕或缩短停留时间。
- More 抽屉优先靠右，不遮挡画面下方字幕区域。

### Settings

Settings 面向个人使用，保持简洁：

- 服务器：URL、当前用户、重新登录、退出登录。
- 播放：默认版本策略、默认字幕策略、默认音轨语言。
- 手柄：是否启用摇杆 seek、seek 提交延迟。
- 诊断：显示播放信息、导出简单日志。

## 手柄交互

核心原则：

- 所有功能仅靠 D-pad、A、B、Menu 可完成。
- View、Y、bumper、trigger 可以加速，但不能承载唯一入口。
- 焦点必须始终可见，使用 Reveal Focus 风格高对比边框。
- 焦点移动不能改变布局尺寸。
- 左摇杆可以用于焦点导航，但不能在 OSD 隐藏时直接改变播放进度。

按键约定：

- A：确认、进入、播放、应用当前选择。
- B：关闭当前层；没有浮层时返回上一页。
- D-pad：移动焦点；播放 OSD 中可用于离散跳转或移动时间轴焦点。
- Menu：当前上下文菜单；播放中打开 More 抽屉。
- View：可作为播放信息快捷键，但播放信息也必须能从 More 抽屉进入。
- Y：可作为搜索快捷键，但搜索也必须有可见入口。

## Seek 安全交互

问题：Xbox 左摇杆容易误触，而 UWP 默认会把左摇杆映射为方向输入。如果播放页把水平输入直接绑定到进度变化，用户可能在拿手柄或放下手柄时误 seek。

设计采用两种 seek 模式。

### 即时小步跳转

用于明确按钮动作：

- D-pad 左：后退 10 秒。
- D-pad 右：前进 30 秒。
- 长按或重复按键连续跳转。
- 每次跳转立即应用，类似 Kodi 的 StepForward/StepBack。

该模式适合“我刚错过一句台词”或“略过片头”等明确意图，不增加确认成本。

### 预览提交 seek

用于容易误触或大跨度移动的动作：

- OSD 可见且进度条获得焦点时，左摇杆水平输入进入预览提交模式。
- D-pad 长按进度条、或用户在进度条上持续左右移动，也可以进入此模式。
- 进入后只移动目标时间标记，不立刻调用播放器 seek。
- 屏幕显示目标时间、偏移量、缩略图占位或未来可接入的预览图，并显示“按 A 跳转 · 按 B 取消”。
- 用户按 A：立即 seek 到目标时间。
- 用户按 B：取消目标时间，回到原播放位置。
- 用户停止操作一小段时间后自动应用，默认 1.8 秒。
- 每次继续移动目标时间都会重置倒计时。
- 左摇杆输入必须有死区，建议绝对值低于 0.55 不进入 seek。
- 如果最终目标和原位置差距小于 5 秒，并且没有按 A，超时后取消而不是应用，用于过滤轻微摇杆漂移。

这个设计保留了电视播放器常见的快捷感，又给摇杆误触留下取消窗口。它借鉴 Apple TV 的“scrub 预览、确认播放、Back/Menu 取消”思路，但为了手柄效率保留了短暂停顿自动应用。

## Emby 数据需求

现有 `EmbyApiClient` 只有登录、最近项目、PlaybackInfo 和进度汇报，无法支撑 Library 和完整详情页。本轮 UI 改造需要补齐以下读接口：

- 获取用户 Views：`/Users/{UserId}/Views`。
- 获取 Library 项目：`/Users/{UserId}/Items?ParentId=...`。
- 获取继续观看：按 Resume/IsResumable 或 Emby 推荐查询。
- 获取最近添加：按 DateCreated 或 Server 返回排序。
- 获取详情：ItemId 查询，包含 People、Genres、Studios、MediaSources 等必要字段。
- 获取季列表和集列表。
- 搜索：按 SearchTerm 和 IncludeItemTypes 查询。

UI 不直接拼接 Emby URL。新增或整理 `EmbyApiClient` 方法后，通过稳定模型交给页面：

- `LibraryView`
- `MediaItemCard`
- `MediaDetail`
- `SeasonSummary`
- `EpisodeCard`
- `PlaybackOption`
- `AudioTrackOption`
- `SubtitleTrackOption`

## 组件边界

### TV Shell

负责全局导航、返回栈、焦点恢复、页面切换动画和全局快捷键。

### Home/Library/Detail ViewModels

负责页面状态、加载、刷新、错误和空状态。页面 XAML 只绑定状态，不直接调用网络。

### Media Card Components

统一海报卡、横向行卡、集卡、Library 网格卡。所有卡片尺寸稳定，并提供清晰焦点视觉。

### Playback Overlay Controller

独立管理 OSD 可见性、More 抽屉、seek 预览提交状态、自动隐藏计时、字幕避让。

### Playback Orchestrator

继续作为 Emby 播放语义和播放器实现之间的边界。UI 发出“切版本、切字幕、切音轨、seek 到时间”等命令，Orchestrator 负责转换为播放器动作并处理失败回滚。

## 错误处理

- Library 加载失败：显示错误说明和重试，不崩回登录页。
- 空 Library：说明没有发现对应库，提供刷新和设置入口。
- 详情加载失败：保留返回路径，提供重试。
- PlaybackInfo 失败：详情页显示“无法获取播放信息”，提供重试。
- 切版本失败：回滚到原版本和原位置，提示失败原因。
- 切音轨失败：回滚到原音轨。
- 切字幕失败：保持播放，标记字幕不可用。
- seek 失败：保留当前位置，OSD 显示短提示。
- 图片失败：显示占位，不改变布局。

错误文案使用用户能行动的语言，不暴露不必要的内部异常。诊断细节放在 More > 信息 或 Settings > 诊断。

## 验收标准

本轮完成后应能在本机 Windows 上验证：

- 登录后 Home 能加载真实 Emby 内容。
- Movies Library 和 TV Library 都能打开。
- Library 中的媒体卡可进入详情页。
- 电影详情可播放；剧集详情可进入季/集并播放。
- 播放页默认全屏，无永久调试面板。
- 播放中 A 显示 OSD，B 按层级关闭，Menu 打开 More 抽屉。
- More 抽屉可查看并切换版本、音轨、字幕。
- D-pad 小步跳转即时生效。
- 左摇杆不会在 OSD 隐藏时改变进度。
- 进度条预览提交 seek 支持 A 应用、B 取消、短暂停顿自动应用。
- 焦点在 Home 横向行、Library 网格、详情操作区、OSD、More 抽屉中都可见且可预测。

Xbox 真机验证留到基础页面交互可用之后。真机阶段再重点验证 4K 电视缩放、手柄输入、HDR/HEVC 路径和 Store/Dev Mode 包行为。

## 本轮不做

- 不新增转码质量选择。
- 不处理音乐、照片、Live TV、DVR。
- 不做非 Xbox 响应式适配。
- 不重写 HDR/native playback core。
- 不做完整主题系统或皮肤系统。
- 不实现在线字幕搜索。
- 不依赖 View/Y/trigger 等 Xbox-only 加速键作为唯一入口。

## 风险

- UWP 默认输入映射会让左摇杆表现得像方向键，需要在播放页明确拦截或限定输入场景。
- 当前页面和 API 边界偏薄，直接在页面里补网络请求会很快变乱，需要先补 ViewModel 和 Emby Client 模型。
- 播放页从调试面板改成 OSD 后，诊断入口不能消失，需要转移到 More > 信息。
- Library 网格如果没有虚拟化或分页，大库会卡顿。
- 4K 电视上 UI 逻辑画布和视频渲染画布要区分清楚，避免 UI 太密或视频被缩放错误。

## 实施顺序建议

1. 补齐 Emby library/detail/search API 和模型。
2. 建立 TV Shell 与导航返回栈，替换桌面式常驻 NavigationView。
3. 实现 Home 内容行和 Library 网格。
4. 重做详情页，接入版本、音轨、字幕摘要和剧集列表。
5. 重做播放页 OSD 与 More 抽屉，把现有调试能力迁移进去。
6. 实现 seek 安全交互。
7. 增加焦点、手柄、API 和播放交互验证。
