# Kodi DXGI Color Pipeline v2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the native Xbox renderer expose Kodi-style DXGI input/output color-space mapping and conversion validation diagnostics.

**Architecture:** Keep the existing FFmpeg -> D3D11VA -> video processor -> SwapChainPanel pipeline. Tighten the color mapping helper to match Kodi semantics, carry chroma location through decoded frames, record conversion diagnostics in native status, and verify Xbox HDR still enters PQ/BT.2020 output.

**Tech Stack:** UWP C++/WinRT, FFmpeg AV color metadata, D3D11/DXGI video processor, xUnit core tests, Xbox Device Portal.

---

## Task 1: Surface Video Processor Diagnostics To C#

**Files:**

- Modify: `src/NextGenEmby.Core/Playback/PlaybackDisplayStatus.cs`
- Modify: `tests/NextGenEmby.Core.Tests/Playback/PlaybackBackendDiagnosticsTests.cs`
- Modify: `src/NextGenEmby.Native/NativePlaybackEngine.idl`
- Modify: `src/NextGenEmby.Native/NativePlaybackStatus.h`
- Modify: `src/NextGenEmby.Native/NativePlaybackStatus.cpp`
- Modify: `src/NextGenEmby.Native/NativePlaybackEngine.cpp`
- Modify: `src/NextGenEmby.App/Playback/WinRtNativePlaybackEngine.cs`

Steps:

- [ ] Write failing C# test proving `PlaybackDisplayStatus` stores video processor input color space, output color space, and conversion status.
- [ ] Run the focused test and confirm it fails because the constructor/properties do not exist.
- [ ] Add the C# properties and constructor parameters.
- [ ] Extend native IDL/status with the three strings.
- [ ] Populate native status from `DxDeviceResources`.
- [ ] Map and log these strings in `WinRtNativePlaybackEngine`.
- [ ] Run focused tests and build.

## Task 2: Align Native Mapping With Kodi `AvToDxgiColorSpace`

**Files:**

- Modify: `src/NextGenEmby.Native/Media/DxgiColorSpaceMapper.h`
- Modify: `src/NextGenEmby.Native/Media/DxgiColorSpaceMapper.cpp`
- Modify: `src/NextGenEmby.Native/Media/VideoDecoder.cpp`
- Modify: `src/NextGenEmby.Native/Media/VideoDecoder.h`

Steps:

- [ ] Add `ChromaLocation`, `IsSupported`, `AlternativeInputColorSpace`, `HasAlternativeInputColorSpace`, `RequiresToneMapping`, and `Reason` fields.
- [ ] Fill `ChromaLocation` from `AVFrame::chroma_location`.
- [ ] Implement Kodi-style mapping rules for RGB, BT.2020 PQ, BT.2020 HLG, BT.2020 G22, BT.601, JPEG/SMPTE170M, and P709 fallback.
- [ ] Build native project to verify enum availability and signatures.

## Task 3: Validate Primary And Alternative Conversions

**Files:**

- Modify: `src/NextGenEmby.Native/DxDeviceResources.h`
- Modify: `src/NextGenEmby.Native/DxDeviceResources.cpp`

Steps:

- [ ] Add last video processor input/output/status fields and getters.
- [ ] Reset conversion diagnostics at the start of every video processor attempt.
- [ ] If mapping is unsupported, record reason and return false.
- [ ] Validate primary input/output conversion with `CheckVideoProcessorFormatConversion`.
- [ ] If primary fails and alternative chroma siting exists, validate the alternative and use it if supported.
- [ ] For missing `ID3D11VideoContext1`/`ID3D11VideoProcessorEnumerator1`, allow legacy SDR only and mark status `legacy-unvalidated`; reject HDR.
- [ ] Build native project.

## Task 4: Verify And Deploy To Xbox

**Files:**

- Modify: `docs/kodi-color-pipeline-comparison.md`
- Modify: `docs/native-playback-smoke-tests.md`

Steps:

- [ ] Run focused core tests.
- [ ] Build Debug x64 MSIX package.
- [ ] Sign and deploy package to Xbox through Device Portal.
- [ ] Launch HDR10 dev command and collect diagnostics.
- [ ] Confirm logs show HDR display active, 10-bit swapchain, `RGB_FULL_G2084_NONE_P2020`, and video processor conversion status.
- [ ] Update docs with exact observed status and any remaining gaps.
