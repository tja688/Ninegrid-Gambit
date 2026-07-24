# Diagnosis — text blur or sprite crawl returned

Work top-down. Name the layer before changing architecture.

| Symptom | Likely layer | Fix on the sprite-owned path |
|---------|--------------|------------------------------|
| Small TMP soft/steppy; sprites OK | **sample snap** revived on shared camera | Zero / skip fullscreen UV quantize; do not add text masks as the primary fix |
| Sprites crawl on subpixel move; text OK | Missing **geometry snap** on those sprites | Assign snap material / include helper in custom sprite shader |
| One VFX/card crisp, another crawls | Custom shader without geometry snap | Port `SnapClipToPixelGrid` into that shader |
| Text blurry only when scaled 1.03 / non-int font size | Glyph transform, not snap | Prefer integer font sizes / parent motion; keep text off snap mats |
| Text sharp in Overlay camera but cards cannot occlude it | Old “second camera for text” split | Prefer single camera + sprite-owned snap; sorting is separate work |
| Mild scanline on text looks fine; heavy grille + any snap looks worse | Scanline strength | Soften grille; confirm sample snap still off |

## Quick probes

- Look / blit material: `_PixelSnap` (or equivalent) must be **0** while text shares that buffer.
- Sprite material: `_PixelSnap` **1**, shader contains clip-space snap.
- `TMP_Text.fontSharedMaterial.shader` name must not be the sprite snap shader.
- Pixel Perfect Camera “Pixel Snapping” that re-samples the frame counts as **sample snap** if text is in that image — treat like fullscreen quantize.

## Anti-pattern

Building a NoSnap mask / stencil / second camera **so sample snap can stay on** is the long way around. Turn sample snap off; own snap on sprites.
