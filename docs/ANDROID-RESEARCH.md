# Research — Android na Q80A (Tizen 6.0 / NikeM2)

Pergunta: dá pra rodar Android na TV? Mesmo que por payload USB?
Veredito por camada, do impossível ao possível.

## 1. Boot de Android via USB — IMPOSSÍVEL

A cadeia de boot é fechada em hardware:

```
BootROM (mask ROM, verifica assinatura RSA)
  → BL1 → BL2 (cada estágio assinado pela Samsung)
    → kernel (partição de firmware, cifrada aes-256-cbc-sha512, chaves no TrustZone)
```

- O BootROM só carrega o BL assinado — não existe caminho "boot de USB"
- O firmware (upgrade.msd) é cifrado; as chaves AES extraídas estão embrulhadas pelo TrustZone
- Não existe u-boot acessível, console serial exposto no produto, nem recovery alternativo
- Conclusão: nenhum payload USB executa no boot. Ponto. O USB só monta como storage após o Tizen subir.

## 2. Substituir Tizen por Android — IMPOSSÍVEL na prática

Mesmo com kernel root, Android precisa de:
- Drivers de display do SoC SDP (pipeline sdp-hdmi/pqe — proprietários, sem fonte público)
- Decoders MFC/dvde, tuner TZTV, Wi-Fi/BT — todos binários Tizen-only
- Device tree do board — não público

Ninguém na comunidade (SamyGO 2010→hoje, samsung-tv-root 2022+) jamais portou Android pra TV Samsung. O hardware é capaz; o ecossistema de drivers não existe.

## 3. Android userspace em chroot — TEÓRICO, o único caminho "mission impossible"

Arquitetura: manter o kernel Tizen rodando, carregar o que o Android precisa, e fazer chroot num rootfs Android ARMv7 no USB.

Requisitos e status:

| Requisito | Status |
|---|---|
| `binder` + `ashmem` no kernel | ❓ kernel 4.1.10 — Samsung TVs ANTIGAS tinham CONFIG_MODULES=y (SamyGO carregava FUSE/*.ko; o próprio kernel 14_DTV_NT14 listava "binder kernel driver" como componente!) — **checar `/proc/modules` e `/proc/config.gz` na NikeM2** |
| Fonte do kernel (GPL) | ⚠ opensource.samsung.com existe (padrão `NN_DTV_<codename>`, ex. 25_DTV_RoseL) mas bloqueia curl — verificar "21_DTV_NikeM2" no navegador, ou pedir via oss.request@samsung.com |
| Compilar binder.ko/ashmem.ko p/ 4.1.10 armv7 | ✓ zig cc já validado no repo (hdmi-timings compilou) |
| `insmod` com nosso root | provável — uid0 System::Privileged deve ter CAP_SYS_MODULE |
| Rootfs Android arm32 | ✓ AOSP/Lineage armv7 images existem |
| GPU p/ Android | ✗ sem drivers → render por software (SwiftShader) |
| Display | chroot não acessa o compositor Tizen → VNC server dentro do chroot → visualizar pelo browser da TV ou PC |
| Input | via VNC (controle da TV não chega lá sem work extra) |

Resultado esperado se TUDO der certo: apps Android rodando ON the TV, software-rendered, numa janela VNC — herói de demo, uso prático baixo (quad A53 ~1.1GHz não leva SwiftShader longe).

## 4. Adaptar o exploit QN90B (sdp_pqe_fdet) — CAMINHO REAL de alto valor

O projeto samsung-tv-root (QN90B) escala root via `/dev/sdp_pqe_fdet` (device de display processing, **world-rw**, mmap sem validação de PFN → /dev/mem disfarçado). Requisitos vs. NikeM2:

| | QN90B | Q80A |
|---|---|---|
| arquitetura | armv7l 32-bit | **armv7l 32-bit — igual** |
| kernel | 5.4.77 | 4.1.10 (família diferente, mas o exploit ESCANEIA RAM física dinamicamente atrás de structs `cred` — offsets não são hardcoded) |
| plataforma | PontusM (cmdline `model=4kbtin`) | NikeM2 (cmdline diferente — só afeta o probe, não a essência) |
| dotnet signed | ✓ | ✓ CoBA/.NET presente (`/usr/bin/dotnet-launcher*`) |
| fdet device | ✓ world-rw | **❓ desconhecido — teste #1 quando re-armar** |

Se `/dev/sdp_pqe_fdet` (ou parente sdp_*) existir world-rw na NikeM2, dá pra portar o payload .NET (que já existe no repo clonado) com ajustes de range de scan. O que se ganha além do nosso CIFS-root:

- **Patch de kernel em RAM** → UEP desativado de verdade (persistente na sessão), instalar qualquer coisa sem assinatura
- Habilitar capabilities que o uid0 CIFS não tem (se houver)
- Caminho pra carregar binder.ko mesmo que /proc/modules diga não (patch em memória)

## 5. Android "de verdade" hoje — REALISTA

- **Moonlight (já instalado) + emulador Android no PC** — apps Android streamados em 4K, latência baixa, GPU de PC. É a resposta prática.
- **Stick Android** na HDMI 3 (a do receiver) — solução definitiva de hardware.

## Plano de teste (1 sessão root, 5 comandos)

```sh
# 1. Existe o device do exploit QN90B aqui?
ls -la /dev/sdp_pqe* /dev/sdp_* 2>/dev/null

# 2. Kernel modular?
cat /proc/config.gz | gunzip | grep -E "CONFIG_MODULES|CONFIG Binder|BINDER"  # se /proc/config.gz existir
cat /proc/modules | head; ls /lib/modules 2>/dev/null

# 3. Specs reais (fechar a ficha técnica)
cat /proc/cpuinfo | head -20; cat /proc/meminfo | head -3

# 4. cmdline (layout NikeM2)
cat /proc/cmdline

# 5. dotnet
ls /usr/bin/dotnet*
```

Com esses resultados, decidimos entre: portar exploit (se fdet existe) → kernel root → aí sim avaliar binder/chroot com dados reais.

## Referências

- samsung-tv-root (QN90B/F): `.tools/tvroot/docs/research-notes/EXPLOIT_CHAIN.md` — técnica completa do fdet-mmap
- Kernel antigos TV Samsung modulares: opensource.samsung.com/opensource/14_DTV_NT14 (lista "binder kernel driver", samsung_mali.ko)
- SamyGO E-series: kernels carregavam .ko (FUSE), ext4 falhava por falta de símbolos exportados
- GPL: fonte NikeM2 = opensource.samsung.com (tentar no browser) ou oss.request@samsung.com
