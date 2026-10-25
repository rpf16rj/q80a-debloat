# Reddit post — WLED ambilight on Samsung Q80A

Suggested subreddits: r/WLED, r/homeautomation, r/tizenTV, r/DIY

---

**Title:** WLED ambilight on a 2021 Samsung Q80A — real-time screen capture, ~45fps, no camera/splitter

Finally got proper ambilight on my QN55Q80A (Tizen 6.0) — and it's *native screen capture*, not a camera pointing at the TV or an HDMI splitter.

**What works:**
- ~45fps edge-color capture straight from the TV's framebuffer
- Works on every source — HDMI, YouTube, native apps
- One .NET daemon running on the TV pushing DDP to an ESP32 running WLED

**How:** Tizen ships an internal `libvideo-capture` API (used by the TV's own features). A small daemon reads the framebuffer, averages the edges, and pushes DDP packets to the WLED strip. The catch: the capture gate checks the process's SMACK label — it only accepts `User` (or privileged contexts), so the daemon has to run in the right security context, which I get via a developer-mode helper.

Details + tutorial: github.com/rpf16rj/q80a-debloat (`docs/AMBILIGHT-TUTORIAL.md`, `docs/REDDIT-ARTICLE.md`)

Anyone know if the same capture API exists on Tizen 7/8 models? Curious if this ports.

---

## Notes for posting

- Link the repo root, not raw files
- The tutorial doc covers setup; the research doc covers the SMACK findings for the tech-curious
- If a sub has an "exploit/root" rule, frame it as "developer-mode + internal capture API" — that framing is accurate
