# Research — Android on the Q80A (Tizen 6.0 / NikeM2)

Question: can Android run on this TV? Even via a USB boot payload?
Verdict by layer, from impossible to possible.

## 1. Booting Android from USB — IMPOSSIBLE

The boot chain is locked in hardware:

```
BootROM (mask ROM, verifies RSA signature)
  → BL1 → BL2 (each stage signed by Samsung)
    → kernel (firmware partition, encrypted aes-256-cbc-sha512, keys in TrustZone)
```

- The BootROM only loads signed BL — there is no "USB boot" path
- The firmware (upgrade.msd) is encrypted; the extracted AES keys are TrustZone-wrapped
- No accessible u-boot, no exposed serial console, no alternative recovery
- Conclusion: no USB payload executes at boot. Period. USB only mounts as storage after Tizen is up.

## 2. Replacing Tizen with Android — IMPOSSIBLE in practice

Even with kernel root, Android needs:
- Display drivers for the SDP SoC (sdp-hdmi/pqe pipeline — proprietary, no public source)
- MFC/dvde decoders, TZTV tuner, Wi-Fi/BT — all Tizen-only binaries
- Board device tree — not public

Nobody in the community (SamyGO 2010→today, samsung-tv-root 2022+) has ever ported
Android to a Samsung TV. The hardware could do it; the driver ecosystem does not exist.

## 3. Android userspace in a chroot — THEORETICAL, the only "mission impossible" path

Architecture: keep the Tizen kernel running, load what Android needs, chroot into
an ARMv7 Android rootfs on USB.

Requirements and status:

| Requirement | Status |
|---|---|
| `binder` + `ashmem` in the kernel | ❓ kernel 4.1.10 — OLD Samsung TV kernels had CONFIG_MODULES=y (SamyGO loaded FUSE/*.ko; the 14_DTV_NT14 kernel itself listed "binder kernel driver" as a component!) — **check `/proc/modules` and `/proc/config.gz` on NikeM2** |
| Kernel source (GPL) | ⚠ opensource.samsung.com exists (pattern `NN_DTV_<codename>`, e.g. 25_DTV_RoseL) but blocks curl — check "21_DTV_NikeM2" in a browser, or request via oss.request@samsung.com |
| Cross-compile binder.ko/ashmem.ko for 4.1.10 armv7 | ✓ zig cc already validated in this repo (hdmi-timings compiled) |
| `insmod` with our root | likely — uid0 System::Privileged should have CAP_SYS_MODULE |
| ARM32 Android rootfs | ✓ AOSP/Lineage armv7 images exist |
| GPU for Android | ✗ no drivers → software rendering (SwiftShader) |
| Display | chroot cannot reach the Tizen compositor → VNC server inside the chroot → view via the TV browser or a PC |
| Input | via VNC (the TV remote does not reach it without extra work) |

Expected result if EVERYTHING works: Android apps running ON the TV,
software-rendered, inside a VNC window — a demo hero, low practical value
(the quad A53 ~1.1GHz will not take SwiftShader far).

## 4. Adapting the QN90B exploit (sdp_pqe_fdet) — the REAL high-value path

The samsung-tv-root project (QN90B) escalates to kernel via `/dev/sdp_pqe_fdet`
(display-processing device, **world-rw**, mmap without PFN validation → a
disguised /dev/mem). Requirements vs. NikeM2:

| | QN90B | Q80A |
|---|---|---|
| architecture | armv7l 32-bit | **armv7l 32-bit — identical** |
| kernel | 5.4.77 | 4.1.10 (different family, but the exploit SCANS physical RAM dynamically for `cred` structs — offsets are not hardcoded) |
| platform | PontusM (cmdline `model=4kbtin`) | NikeM2 (different cmdline — only affects the probe, not the essence) |
| signed dotnet | ✓ | ✓ CoBA/.NET present (`/usr/bin/dotnet-launcher*`) |
| fdet device | ✓ world-rw | **❓ unknown — test #1 when re-armed** |

If `/dev/sdp_pqe_fdet` (or an sdp_* relative) exists world-rw on NikeM2, the .NET
payload (already in the cloned repo) can be ported with scan-range adjustments.
What it gives beyond our CIFS root:

- **Kernel patching in RAM** → UEP genuinely disabled (session-persistent), install anything unsigned
- Capabilities the CIFS uid0 may lack
- A path to load binder.ko even if /proc/modules says no (memory patching)

## 5. "Real" Android today — REALISTIC

- **Moonlight (already installed) + an Android emulator on the PC** — Android apps streamed in 4K, low latency, PC GPU. The practical answer.
- **An Android stick** on HDMI 3 (the receiver port) — the definitive hardware solution.

## Test plan (1 root session, 5 commands)

```sh
# 1. Does the QN90B exploit device exist here?
ls -la /dev/sdp_pqe* /dev/sdp_* 2>/dev/null

# 2. Is the kernel modular?
cat /proc/config.gz | gunzip | grep -E "CONFIG_MODULES|CONFIG_BINDER"  # if /proc/config.gz exists
cat /proc/modules | head; ls /lib/modules 2>/dev/null

# 3. Real specs (close the datasheet)
cat /proc/cpuinfo | head -20; cat /proc/meminfo | head -3

# 4. cmdline (NikeM2 layout)
cat /proc/cmdline

# 5. dotnet
ls /usr/bin/dotnet*
```

With these results we decide between: porting the exploit (if fdet exists) →
kernel root → then evaluate binder/chroot with real data.

## References

- samsung-tv-root (QN90B/F): `.tools/tvroot/docs/research-notes/EXPLOIT_CHAIN.md` — full fdet-mmap technique
- Old modular Samsung TV kernels: opensource.samsung.com/opensource/14_DTV_NT14 (lists "binder kernel driver", samsung_mali.ko)
- SamyGO E-series: kernels loaded .ko (FUSE); ext4 failed for lack of exported symbols
- GPL: NikeM2 source = opensource.samsung.com (try in a browser) or oss.request@samsung.com
