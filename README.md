# Q80A Debloat — Samsung QN55Q80AAGXZD (NikeM2, Tizen 6.0)

Trabalho de debloat/customização de uma Samsung Q80A 55" 2021 (firmware `T-NKM2UABC` 2301).

## Estado atual

- **Root obtido** via injeção de comando no fluxo Remote PC/CIFS (`System::Privileged`, volátil — some no reboot)
- **12 apps removidos** via `pkgcmd -u` (ver `docs/DEBLOAT.md`)
- **TizenBrew** instalado + catálogo visual servido do PC
- **FreeMyTV** (launcher alternativo) instalado — pkgid `FreeMyTVio`
- **AdGuard Home** com listas de bloqueio de ads/telemetria Samsung
- Chaves SWU do firmware capturadas (`swu_keys/`) — AES cifrado por TrustZone

## Estrutura

```
apps/          — catálogo TizenBrew, WGTs, listas AdGuard
docs/          — documentação (root, debloat, HDMI/CEC)
inventory/     — inventário completo de pacotes (355 pkgs)
kit/           — kit de re-root (stage2, bridge, listener)
swu_keys/      — chaves SWU extraídas da TV
tools/         — ferramentas de análise (firmware .msd)
.tools/        — ferramentas de trabalho (não versionado)
```

## Avisos

- Firmware NikeM2 é cifrado + assinado RSA — **não existe flash de firmware modificado**. Todo o trabalho é em runtime.
- O root é **volátil**: reboot mata o shell. Remoções de apps são permanentes (feitas pelo package manager).
- `/` é write-protected em hardware. Escrita possível em `/opt` e `/mnt/systemrw`.
- Só usar no seu próprio aparelho.
