
---
name: diagnostics
description: Use when something does not work and the model needs to diagnose, debug, or investigate the issue with iterative user interaction.
metadata:
  tags: [diagnostics, debug, troubleshooting, investigation, user-interaction]
---

# Clipboard handling

## Purpose
Minimize user interaction by automating data transfer through the clipboard when appropriate.

---

## Principles

- Prefer scripts that produce results directly usable by the user
- Avoid requiring repeated manual copy/paste
- Treat the clipboard as an output channel, not a reliable input source

---

## Rules

- Scripts SHOULD copy relevant results to the clipboard
- Use cross-platform clipboard methods when possible
- Do NOT rely on clipboard contents as the only input source
- Do NOT assume clipboard content is up-to-date or correct
- Ask for manual input only when necessary

---

## Implementation

- Prefer platform-aware clipboard tools:
  - macOS: `pbcopy`
  - Linux (X11): `xclip` or `xsel`
  - Linux (Wayland): `wl-copy`
- Terminal escape sequences (e.g. OSC52) MAY be used for portability

---

## Behavior

- Outputs should still be printed to stdout
- Clipboard copy is an additional convenience, not the only output
- Scripts must remain usable without clipboard support