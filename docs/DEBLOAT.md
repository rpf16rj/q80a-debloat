# Debloat — removed apps

Removal via `pkgcmd -u -n <pkgid>` with root. All returned `key[end] val[ok]`.

## Removed (12)

| App | pkgid | type |
|---|---|---|
| Amazon Alexa | `AQKO41xyKP` | wgt |
| Samsung Promotion | `yL49PNFmjW` | wgt |
| Xbox | `GHI3a0zMSx` | wgt |
| Spotify | `rJeHak5zRg` | wgt |
| e-Manual | `jPPp4KCHp4` | wgt |
| Security Center | `J9fZJDMi1v` | wgt |
| Globoplay | `org.tizen.globoplayapp` | tpk |
| Globoplay web | `LnLzvqrcEY` | wgt |
| SmartOne IPTV | `p9WC6wWKA1` | wgt |
| Apple TV | `com.samsung.tv.aria-video` | tpk |
| AriaVideoBGService | `30IKH0OX8U` | wgt |
| Disney+ | `MCmYXNxgcu` | wgt |

## Post-removal

- Gray icons stay on the Home bar until the tile is removed (hold OK → remove) or a reboot
- Clicking a removed icon: "try again later" — expected
- Alexa companions (`com.samsung.tv.alexa-client`, `wakeup-engine-alexa`) were kept — without the front-end app they are inert

## Candidates NOT removed (telemetry/ads) — moderate risk

Removal is only reversible via factory reset. AdGuard already cuts their network access.

- `com.samsung.tv.acr-service-app` — ACR (recognizes what you watch)
- `com.samsung.tv.canalysis-service` — analytics
- `com.samsung.tizen.samsung-analytics` — analytics
- `com.samsung.tv.nvision-analysis-dbmgr` — data collection
- `com.samsung.tv.adplayer` / `ad-web-framework` / `coba.adagent` — Smart Hub ad engine

## Other identified bloat (not removed yet)

- `com.samsung.tv.bixbycapsuleviewer`, `bixby-context-client`, `bixby-prov`, `wakeup-engine-bixby` — Bixby
- `com.samsung.tv.cnvoice-assistant` — CN voice assistant
- `org.tizen.multi-assistant-service` — multi-assistant
- Ambient Mode templates: `tranquilnature-tpl`, `mountainview-tpl`, `cinemagraph-relaxation`, `bouroullecwidget-tpl`
- `com.samsung.tv.aov-dashboard` — Always On Voice

## How to re-root for more removals

See `docs/REROOT.md`. Full inventory: `inventory/pkglist.txt`.
