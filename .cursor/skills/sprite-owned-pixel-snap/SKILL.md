---
name: sprite-owned-pixel-snap
description: >-
  Ship sprite-owned pixel snap that keeps SDF/TMP text crisp: turn off
  fullscreen sample snap, put geometry snap on sprite materials only, leave
  text materials alone. Use when the user wants pixel-art snap without blurring
  text, mentions geometry vs sample snap, vertex pixel snap, or asks to port
  this look to another Unity URP 2D project. Portable — copy this folder into
  another repo's .cursor/skills/.
---

# Sprite-owned pixel snap (portable)

**Contract:** pixel feel is **sprite-owned** (geometry snap on sprite materials). Text never enters a snap shader. Soft fullscreen scanline may still hit everyone.

**Leading words** (use them while working):

| Word | Meaning |
|------|---------|
| **sample snap** | Fullscreen / blit UV quantize of the color buffer. Hits every pixel already drawn — including glyphs. |
| **geometry snap** | Vertex / clip-space snap of a mesh. Moves that draw call's corners only. |
| **sprite-owned** | Snap lives on sprite materials (or equivalent), not on a global post that re-samples the frame. |

Hard rule expressed positively: **text materials stay on the non-snap path**; sprites carry **geometry snap**.

## Transplant

Copy the whole folder:

```text
.cursor/skills/sprite-owned-pixel-snap/
  SKILL.md
  reference/hlsl-geometry-snap.md
  reference/diagnosis.md
```

Target project needs: Unity **URP 2D** (or able to host a Sprite Lit/Unlit variant), pixel art sprites, world or screen text that must stay readable (TMP SDF or similar).

Do **not** rename files to a game title. Keep generic shader/material names in the host project (e.g. `Sprite-Lit-PixelSnap`, `PixelSnapVertex.hlsl`).

## When to stop and refuse the old path

If the user asks to “exclude text from fullscreen snap” while keeping **sample snap** on the shared framebuffer, redirect to this skill’s contract. That combination fights occlusion and follow: either text shares the quantized buffer (blur) or text leaves the shared buffer (occlusion/follow break). **Sprite-owned** is the default fix.

## Steps

### 1. Audit where snap lives

Inventory the host project for:

- Fullscreen / Renderer Feature / blit materials that quantize UV (`floor(uv * res)`)
- Camera Pixel Snapping that re-samples the frame (vs transform-only helpers)
- Existing sprite materials / default Lit-Unlit assignments
- Text stack (TMP SDF materials, layers, second cameras)

Write a one-line verdict: **sample snap present?** **geometry snap present?** **text on which path?**

**Done when:** every active snap site is listed and classified as sample or geometry (or “none”).

### 2. Kill sample snap on the shared frame

Disable or zero every **sample snap** that runs on the camera that also draws text (fullscreen pass `_PixelSnap = 0`, feature skip, remove blit). Keep soft scanline/CRT if desired — it is not the blur culprit at mild strength.

**Done when:** the shared look material / feature no longer quantizes UVs while text is in that camera’s color buffer.

### 3. Add geometry snap to sprites

Create (or paste from [reference/hlsl-geometry-snap.md](reference/hlsl-geometry-snap.md)):

1. A small HLSL helper: clip → NDC → pixel grid → clip
2. A URP Sprite Lit (or Unlit) shader that applies the helper after the common vertex transform, and refreshes any screen UV used for 2D lights
3. A shared material with `_PixelSnap = 1` and a pixel resolution matching art/reference (often the Pixel Perfect reference resolution)

Set Renderer2D **default custom material** to that material when most sprites use the default. Batch-assign any serialized `Sprite-Lit-Default` (or host equivalent) references on scenes/prefabs.

Special sprite shaders (hit flash, dissolve, …) that replace the default must include the **same** geometry-snap helper — or they silently leave the contract.

**Done when:** default + gameplay sprites use a shader whose name/path embeds geometry snap; text materials do not reference it.

### 4. Prove the split

In Editor (Play or eval):

- Count SpriteRenderers on the snap shader vs not
- Confirm TMP / text materials are **not** on that shader
- Confirm fullscreen look `_PixelSnap` (or equivalent) is 0

**Done when:** sprites snap-owned, text count on snap shader is 0, sample snap off.

### 5. Regression checklist

If blur or crawl returns, follow [reference/diagnosis.md](reference/diagnosis.md) before inventing a new mask/camera split.

**Done when:** the failing layer is named (sample snap revived / wrong material / non-integer text scale / etc.) and fixed on the **sprite-owned** path.

## Out of scope (unless the user asks)

- SortingGroup / draw-order card stacks
- Layout-time `RoundToPixelGrid` systems
- Low-res RT world + full-res text dual paths
- Binding names, layers, or folders to a specific game title
