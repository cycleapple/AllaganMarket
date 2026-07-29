# API13 / Taiwan 7.3 source audit

- Upstream: `Critical-Impact/AllaganMarket`
- Selected upstream commit: `334b5036bc58c3266848c93ed33be01889b05647`
- Commit date: `2025-10-17T10:31:08Z`
- Upstream release version: `1.2.0.5`
- API13 migration commit: `fe6f433b01d7b9972e105a329aa3a9980bbea68a`
- API13 migration date: `2025-08-09T05:29:05Z`
- Audited compatibility window: 2025-08-05 through 2025-12-16
- Target environment: FFXIV Taiwan 7.3 and Dalamud API13

The selected commit is the latest upstream revision inside the requested
compatibility window. It contains the explicit API13 migration, the subsequent
feature-registration fix, and the final 1.2.0.5 signature correction for market
board price updates.

There are no Git submodules. Critical-Impact shared components are consumed as
NuGet packages and bundled into the plugin package:

- `AllaganLib.Data` 1.3.7
- `AllaganLib.Interface` 1.3.7
- `AllaganLib.Shared` 1.3.7
- `AllaganLib.Universalis` 1.3.7

The Taiwan compatibility build is versioned as `1.2.0.6`.
