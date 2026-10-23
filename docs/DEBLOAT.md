# Debloat — apps removidos

Remoção via `pkgcmd -u -n <pkgid>` com root. Todos retornaram `key[end] val[ok]`.

## Removidos (12)

| App | pkgid | tipo |
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

## Pós-remoção

- Ícones cinzas permanecem na barra da Home até remover o tile (segurar OK → remover) ou reboot
- Ao clicar num ícone removido: "tente mais tarde" — esperado
- Companheiros da Alexa (`com.samsung.tv.alexa-client`, `wakeup-engine-alexa`) ficaram — sem app front-end ficam inertes

## Candidatos NÃO removidos (telemetria/ads) — risco moderado

Remoção só reversível via factory reset. AdGuard já corta a rede deles.

- `com.samsung.tv.acr-service-app` — ACR (reconhece o que você assiste)
- `com.samsung.tv.canalysis-service` — analytics
- `com.samsung.tizen.samsung-analytics` — analytics
- `com.samsung.tv.nvision-analysis-dbmgr` — coleta
- `com.samsung.tv.adplayer` / `ad-web-framework` / `coba.adagent` — motor de ads do Smart Hub

## Outros bloat identificados (não removidos ainda)

- `com.samsung.tv.bixbycapsuleviewer`, `bixby-context-client`, `bixby-prov`, `wakeup-engine-bixby` — Bixby
- `com.samsung.tv.cnvoice-assistant` — assistente de voz CN
- `org.tizen.multi-assistant-service` — multi-assistente
- Ambient Mode templates: `tranquilnature-tpl`, `mountainview-tpl`, `cinemagraph-relaxation`, `bouroullecwidget-tpl`
- `com.samsung.tv.aov-dashboard` — Always On Voice

## Como re-root para mais remoções

Ver `docs/REROOT.md`. Inventário completo: `inventory/pkglist.txt`.
