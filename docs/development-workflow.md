# Noira 开发调试流程

本文记录当前推荐的本地开发调试方式。它只覆盖开发期流程，不替代最终 MSIX 打包、签名、真机或 Xbox 验证。

## XAML Hot Reload

推荐路径是在 Visual Studio 2022 中使用 `Debug|x64` F5 启动 `NoiraPlayer.App`。UWP XAML Hot Reload 依赖 Debug 构建中的 XBF line info；项目文件已在 Debug x64/x86 显式设置：

```xml
<UseDotNetNativeToolchain>false</UseDotNetNativeToolchain>
<DisableXbfLineInfo>False</DisableXbfLineInfo>
```

限制：

- 新增 XAML 文件、C# 类型、资源字典结构变化通常仍需要停止并重新启动。
- attach 到已经运行的进程时，Visual Studio 不一定自动设置 XAML 诊断环境变量；优先使用 F5。
- 不要用 Release/.NET Native 构建判断 Hot Reload 是否正常。

## 本机 Loose File Deploy

Loose file deploy 用未打包的 `bin\<Platform>\<Configuration>\AppxManifest.xml` 注册应用，适合快速验证 XAML、资源和静态文件改动。默认命令会先 clean/build，避免旧包名或旧二进制残留在 layout 中：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Register-NoiraLooseApp.ps1 `
  -Configuration Debug `
  -Platform x64 `
  -Launch
```

只验证 loose layout 是否存在且 manifest 可解析，不注册系统包：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Register-NoiraLooseApp.ps1 `
  -Configuration Debug `
  -Platform x64 `
  -SkipBuild `
  -ValidateOnly
```

常用参数：

- `-SkipBuild`：复用现有 `bin\<Platform>\<Configuration>` layout。
- `-SkipClean`：构建前不清理输出目录；只有确认没有旧 layout 残留时才使用。
- `-Launch`：注册后通过 `shell:AppsFolder` 启动应用。
- `-MsBuildPath`：显式指定 Visual Studio MSBuild 路径。

## Xbox / 远程 Loose File Deploy

Microsoft 支持 Xbox 上的 loose file registration，但它适合开发期快速验证，不适合最终验证或分发。推荐边界：

1. 先在本机生成干净的 `Debug|x64` loose layout。
2. 将 `src\NoiraPlayer.App\bin\x64\Debug\` 放到 Xbox 可访问的网络共享。
3. 通过 Xbox Device Portal 的 Apps Manager 使用 Register from Network Share，或使用 `WinAppDeployCmd registerfiles` 指向该共享路径。

最终确认 HDR、显示刷新、音频、输入和包身份问题时，仍应使用正常 MSIX 包部署。

## 当前取舍

- Hot Reload 用于缩短 XAML/样式迭代时间。
- Loose deploy 用于减少完整 MSIX 打包等待，但不作为发布或质量结论依据。
- 播放 core/native 的可复现评测仍走 `tools\quality-run\run-playback-core-checks.ps1` 和 playback-quality report-set，不因 App 开发流程改变评测规则。
