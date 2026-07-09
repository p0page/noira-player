# Notices

This file records third-party software notices for Noira and release-time
compliance notes. It is not legal advice.

## Project License

Noira source code is licensed under the MIT License. See `LICENSE`.

Third-party dependencies, tools, SDKs, generated files, and binary components
remain under their own licenses.

## FFmpeg

Noira uses FFmpeg libraries through the NuGet package
`FFmpegInteropX.FFmpegUWP` version `5.1.100`.

Package role:

- Provides FFmpeg DLLs, import libraries, headers, and license files for
  Windows 10 UWP apps.
- Used by `src/NoiraPlayer.Native` for demuxing, decoding, audio conversion,
  subtitle decoding, and media metadata extraction.
- The project links to FFmpeg through DLLs such as `avcodec-59.dll`,
  `avformat-59.dll`, `avutil-57.dll`, `swresample-4.dll`, and `swscale-6.dll`.

License information from the package metadata:

- `LGPL-2.1-or-later AND Zlib AND MIT`

License files included by the restored package:

- `licenses/ffmpeg.txt`
- `licenses/bzip2.txt`
- `licenses/dav1d.txt`
- `licenses/iconv.txt`
- `licenses/liblzma.txt`
- `licenses/libxml2.txt`
- `licenses/openssl.txt`
- `licenses/zlib.txt`

References:

- FFmpeg legal page: https://www.ffmpeg.org/legal.html
- FFmpeg source license file: https://github.com/FFmpeg/FFmpeg/blob/master/LICENSE.md
- NuGet package: https://www.nuget.org/packages/FFmpegInteropX.FFmpegUWP/5.1.100

Before publishing binary builds, verify and preserve FFmpeg compliance:

- Keep FFmpeg dynamically linked through DLLs.
- Do not rename FFmpeg DLLs to obscure their origin.
- Do not replace the package with a GPL or nonfree FFmpeg build unless the
  whole release is reviewed for the resulting license obligations.
- Include third-party notices in the app package, release page, or About /
  acknowledgements screen.
- Provide access to the corresponding FFmpeg source, build configuration, and
  any local FFmpeg changes that match the distributed binaries.
- Do not add EULA terms that prohibit reverse engineering for debugging
  modifications to LGPL-covered libraries.

## Microsoft Components

The project uses Microsoft platform SDKs and NuGet packages for UWP, WinUI,
C++/WinRT, and Windows media/graphics APIs. Their licenses apply separately.

Current direct package references include:

- `Microsoft.NETCore.UniversalWindowsPlatform`
- `Microsoft.UI.Xaml`
- `Microsoft.Windows.CppWinRT`
- `System.Text.Json`

## Test Dependencies

Test projects use packages such as:

- `Microsoft.NET.Test.Sdk`
- `xunit`
- `xunit.runner.visualstudio`

These are development/test dependencies and are not expected to be part of
normal app distribution.

## Borrowed Or Adapted Code

If code, assets, shaders, scripts, or substantial text are copied or adapted
from another open-source project, record the upstream project here and preserve
the upstream copyright and license notices required by that project.

Current recorded direct code imports:

- None recorded in this file yet.

Research references, UI inspiration, API behavior comparisons, and playback
architecture notes are documented under `docs/`. Inspiration alone is not the
same as copying source code, but direct source reuse must be attributed here.
