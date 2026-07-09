# Public Test Media Catalog

This catalog lists public media sources suitable for building local Emby test cases. Do not commit private Emby item IDs, source IDs, server URLs, credentials, or downloaded media into this repository.

## Recommended Sources

### Netflix Open Content

URL: https://opencontent.netflix.com/

Use for:

- HDR-oriented professional content;
- Dolby Vision / HDR metadata experiments where assets are available;
- animation and live-action stress samples.

Notes:

- Netflix states the open source content is available under Creative Commons Attribution 4.0.
- Prefer short derived local clips for repeatable quality-run cases.
- Do not put a Netflix sample into the executable manifest until its exact download URL returns HTTP 200 and ffprobe confirms codec, resolution, frame rate, and HDR metadata.

### Jellyfin Test Videos

URL: https://repo.jellyfin.org/test-videos/

Use for:

- public direct-link SDR/HDR/Dolby Vision playback smoke tests;
- HEVC Main10 decode coverage;
- HDR10 output and force-SDR validation;
- Dolby Vision Profile 5 reject and Profile 8.1 HDR10 fallback classification;
- 4K/60fps/high-bitrate buffering stress.

Current executable manifest links:

- SDR HEVC Main10 1080p60 3M: https://repo.jellyfin.org/test-videos/SDR/HEVC%2010bit/Test%20Jellyfin%201080p%20HEVC%2010bit%203M.mp4
- HDR10 HEVC Main10 1080p60 10M: https://repo.jellyfin.org/test-videos/HDR/HDR10/HEVC/Test%20Jellyfin%201080p%20HEVC%20HDR10%2010M.mp4
- HDR10 HEVC Main10 4K60 50M: https://repo.jellyfin.org/test-videos/HDR/HDR10/HEVC/Test%20Jellyfin%204K%20HEVC%20HDR10%2050M.mp4
- Dolby Vision Profile 5 4K60: https://repo.jellyfin.org/test-videos/HDR/Dolby%20Vision/Test%20Jellyfin%204K%20DV%20P5.mp4
- Dolby Vision Profile 8.1 4K60: https://repo.jellyfin.org/test-videos/HDR/Dolby%20Vision/Test%20Jellyfin%204K%20DV%20P8.1.mp4

Notes:

- These links are suitable for `direct-uri` run plans when the network can reach `repo.jellyfin.org`.
- The 4K60 HDR10 50M file is the current public buffering/HDR stress case.
- Public Jellyfin samples cover the main SDR/HDR/DV classifications, but not the current 23.976 HDR cadence case; keep that as a local Emby-bound reference case until a stable public 23.976 HDR10 URL is verified.

### DVB HDR Test Content

URL: https://dvb.org/specifications/verification-validation/hdr-test-content/

Use for:

- 2160p50 PQ10 transport streams;
- HDR dynamic metadata signalling;
- HDR switching stress cases.

Notes:

- Several files are direct downloads from DVB.
- Some streams include dynamic metadata formats the app may not fully support. That is useful for unsupported-source diagnostics.

### Fraunhofer HHI Berlin Test Sequences

URL: https://www.hhi.fraunhofer.de/en/departments/vca/research-groups/video-coding-systems/8k-sequences.html

Use for:

- BT.2020 SDR and BT.2100 PQ HDR source pairs;
- UHD/HDR stress material;
- comparing SDR/HDR pipeline classification.

Notes:

- Source material may need local encoding into Xbox-direct-playable HEVC.

### SVT Open Content

URL: https://svt.github.io/en/content/

Use for:

- 3840x2160p50 professional SDR/high-motion material;
- 50 Hz cadence and high-motion frame pacing.

Notes:

- SVT says the material may be distributed, modified, and used freely under its Creative Commons terms.
- Current access is via SVT FTP.

### Ultra Video Group UVG Dataset

URL: https://ultravideo.fi/dataset.html

Use for:

- 4K 50/120 fps motion and compression stress cases;
- high frame-rate downconversion experiments.

Notes:

- UVG is CC BY-NC, so keep usage non-commercial.
- Files are research-oriented and may require local encoding before Emby playback.

### Xiph.org Test Media

URL: https://media.xiph.org/

Use for:

- lossless Blender open movie sources;
- SDR reference clips;
- generating local short clips with known frame counts.

Notes:

- Many source files are huge. Do not use full sequences for routine Xbox smoke tests.
- Useful as source material for local encoded snippets.

## First Local Case Set

Start with at most five local cases:

1. SDR 23.976 or 24 fps, short clip.
2. HDR10 PQ 23.976 or 24 fps, short clip.
3. HDR10 force-SDR, same clip as case 2.
4. 2160p50 high-motion SDR or HDR clip.
5. Unsupported or difficult HDR/DV/dynamic metadata clip.

## Local Encoding Rule

If a public source is raw, EXR, Y4M, or otherwise not directly playable, create a local HEVC MKV/MP4 derivative and add it to Emby manually. The repository stores only the case template and source URL, not the media.

## Private Emby Sources

Private Emby servers are valid runtime test sources for Xbox and playback Core validation, but their URL, credentials, item IDs, media source IDs, and captured private reports must stay outside committed files.

Use environment variables, a system temporary directory, or ignored local files such as `docs/qa/private/*.json`, `tools/quality-run/private/*.json`, `*.private.json`, `*.local.json`, `*.secrets.json`, or `.env`. Public manifests should contain only public direct links or non-sensitive placeholder IDs.
