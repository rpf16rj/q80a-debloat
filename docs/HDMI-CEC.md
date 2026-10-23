# HDMI / CEC / Vídeo — o que existe na Q80A

## Sysfs (root)

- `/sys/class/extcon/hdmi_det_info` — detecção de cabo por porta:
  `H1_c..H6_c` (1 = conectado), `ARC`, `EARC`, `MULTI_PBP`
- `/sys/devices/platform/soc/590000.hdmi/video4linux/` — nodes V4L2 do
  receptor HDMI → resolução/timings do sinal de entrada via `v4l2-ctl` (se presente)
  ou `VIDIOC_QUERY_DV_TIMINGS`
- `/sys/bus/platform/devices/tztv_hdmi_sw`, `hdmi_sw` — switch HDMI no TrustZone
- `/dev/tztv_frc`, `/dev/tztv_tcon`, `/dev/dvb`, `/dev/v4l`, `/dev/micom-htv`

## CEC via D-Bus — `hdmicec-service`

Interface: `org.tizen.tv.HdmiCec` em `/org/tizen/tv/hdmicec`
(definição: `/usr/bin/hdmicec-api.xml`)

Métodos úteis:
- `GetDeviceCount(is_receiver_included)` → nº de dispositivos CEC
- `GetDeviceName(idx)`, `GetDevicePortNum(idx)`, `GetDeviceType(idx)`
- `GetActiveDeviceName()`, `IsActiveSource(idx)`
- `SendKeyToHdmicec(key)`, `ReleaseKeyToHdmicec(key)` — envia teclas CEC
- `IsTunerDevice()`, `GetDepthDeviceCount()`, `GetDeviceNameByLogicalAddress(la)`

Com isso dá pra montar um painel: portas com cabo, nome/tipo do dispositivo,
fonte ativa, troca de fonte e controle de dispositivos CEC.

## APIs web públicas (sem root)

- `tizen.systeminfo.getPropertyValue('VIDEOSOURCE', cb)` — fontes conectadas (tipo, porta, `signal`)
- `tizen.systeminfo.addPropertyValueChangeListener('VIDEOSOURCE', cb)` — hot-plug
- `tizen.tvwindow.getSource()` / `setSource()` — fonte atual / troca de input
- `tizen.tvwindow.show()` — PiP/live da fonte

Não exposto a apps normais: EDID, versão HDCP, resolução exata do sinal,
banda TMDS, frames CEC arbitrários.

## Limites de hardware (não contornáveis)

- Só a **HDMI 4** é HDMI 2.1 (4K120/VRR/ALLM). As demais são 2.0.
- Input Signal Plus (largura de banda total) é por porta: Config → Geral →
  Gerenciador de Disp. Externo → Input Signal Plus
- HDR só quando a fonte envia HDR — não há "forçar"
- Game Bar (segurar Play/Pause no modo jogo) mostra VRR/HDR/fps reais

## SWU — chaves do firmware

Em `/usr/share/org.tizen.tv.swu/` (copiadas para `swu_keys/`):
- `itemsAESPassphraseEncrypted.txt` (416B) — passphrase AES-256 do pacote,
  cifrada pelo TrustZone — decifrar exige o caminho do `SWUStandalone` + TA
- `itemsPublicRSAKey.txt` / `itemsPublicSubModelRSAKey.txt` — RSA pub de verificação
- `OpenAPI*AESPassphraseEncrypted.txt` (80B) — chaves do OpenAPI
- `SWUStandalone`, `SWUAutoUpgrade`, `SWUCoreTV`, `SWUService`, `SWUVerifierTV` em `/usr/bin`
