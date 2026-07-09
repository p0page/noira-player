# UI Development Data Sources

Date: 2026-07-09

This document is the canonical policy for App/UI development data sources.

## Current Rule

Use real Emby data for new UI development and validation.

The supported repeatable local path is:

1. Keep private sample definitions in ignored local files, normally `docs/qa/private/ui-real-samples.local.json`.
2. Select a sample with `tools/Write-AppUiSampleCommand.ps1`.
3. Let the script write the selected route to the installed Noira UWP package `LocalState\dev-command.json`.
4. Launch the app and validate the real route.

The committed template is `docs/qa/private/ui-real-samples.template.json`.

## Deprecated Routes

Do not use, restore, or add new dependencies on these development routes:

- `*-fixture`
- `details-real-sample`
- `details-real-bright-sample`

These routes are retired. They were removed from active App/Core code because mock fixture data and automatic local item picking do not represent real Emby browsing conditions reliably.

## Why Fixture Routes Were Retired

Fixture routes were useful for early deterministic UI work, but they became misleading for the current product stage:

- They do not expose real Emby library density, metadata gaps, artwork shape, title length, source count, audio tracks, subtitle tracks, or progress state.
- They can make a page look complete while failing against realistic server data.
- They encourage UI decisions around synthetic samples instead of the user's actual media library.
- They add maintenance surface across App, Core, diagnostics, tests, screenshots, and docs.

`details-real-*` routes were also retired because route code should not decide which private server item is representative. That choice belongs in local ignored sample manifests.

## Historical Documentation

Older QA logs, design checklists, operation matrices, and implementation plans may still mention fixture routes. Treat those mentions as historical evidence only.

Historical fixture text can answer "what was validated at that time"; it must not be used as current implementation guidance or as a command to run.

When current guidance conflicts with historical fixture references, this document wins.

## Supported Routes

New UI samples should target real app routes such as:

- `home`
- `login`
- `movies`
- `tv`
- `search`
- `settings`
- `livetv`
- `music`
- `photos`
- `playlists`
- `favorites`
- `unwatched`
- `details`
- `photo`
- `playback`
- `manual-playback`
- `quality-run`

Routes that open item-specific pages must use real sample identifiers in ignored local files. Do not commit real `itemId`, `mediaSourceId`, private stream URLs, titles that identify private media, server URLs, credentials, tokens, screenshots, or downloaded artwork.

## Example Command

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Write-AppUiSampleCommand.ps1 -ManifestPath docs\qa\private\ui-real-samples.local.json -SampleId 'movies/example-details'
```

## Maintenance Rule

If a future task needs repeatable UI data, add or update a private real sample manifest. Do not reintroduce mock fixture routes.
