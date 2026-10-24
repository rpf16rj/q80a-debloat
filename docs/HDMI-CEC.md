# HDMI / CEC / Video — what exists on the Q80A

## Sysfs (root)

- `/sys/class/extcon/hdmi_det_info` — per-port cable detection:
  `H1_c..H6_c` (1 = connected), `ARC`, `EARC`, `MULTI_PBP`
- `/sys/devices/platform/soc/590000.hdmi/video4linux/` — V4L2 nodes of the
  HDMI receiver → input signal resolution/timings via `v4l2-ctl` (if present)
  or `VIDIOC_QUERY_DV_TIMINGS` (see `tools/hdmi-timings`)
- `/sys/bus/platform/devices/tztv_hdmi_sw`, `hdmi_sw` — HDMI switch in the TrustZone
- `/dev/tztv_frc`, `/dev/tztv_tcon`, `/dev/dvb`, `/dev/v4l`, `/dev/micom-htv`

## CEC via D-Bus — `hdmicec-service`

Interface: `org.tizen.tv.HdmiCec` at `/org/tizen/tv/hdmicec`
(definition: `/usr/bin/hdmicec-api.xml`)

Useful methods:
- `GetDeviceCount(is_receiver_included)` → number of CEC devices
- `GetDeviceName(idx)`, `GetDevicePortNum(idx)`, `GetDeviceType(idx)`
- `GetActiveDeviceName()`, `IsActiveSource(idx)`
- `SendKeyToHdmicec(key)`, `ReleaseKeyToHdmicec(key)` — send CEC keys
- `IsTunerDevice()`, `GetDepthDeviceCount()`, `GetDeviceNameByLogicalAddress(la)`

With this a panel can show: ports with cable, device name/type,
active source, source switching and CEC device control. (`modules/hdmi-panel` implements it.)

## Public web APIs (no root)

- `tizen.systeminfo.getPropertyValue('VIDEOSOURCE', cb)` — connected sources (type, port, `signal`)
- `tizen.systeminfo.addPropertyValueChangeListener('VIDEOSOURCE', cb)` — hot-plug
- `tizen.tvwindow.getSource()` / `setSource()` — current source / input switching
- `tizen.tvwindow.show()` — PiP/live view of a source

Not exposed to normal apps: EDID, HDCP version, exact signal resolution,
TMDS bandwidth, arbitrary CEC frames.

## Hardware limits (not workaroundable)

- Only **HDMI 4** is HDMI 2.1 (4K120/VRR/ALLM). The others are 2.0.
- Input Signal Plus (full bandwidth) is per-port: Settings → General →
  External Device Manager → Input Signal Plus
- HDR only when the source sends HDR — there is no "force"
- Game Bar (hold Play/Pause in game mode) shows real VRR/HDR/fps
