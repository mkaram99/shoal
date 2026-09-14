# Shoal

Swim through thousands of images in 3D — in a desktop browser or in a Meta Quest headset (WebXR).

**Live:** https://mkaram99.github.io/shoal/

## Use it on a Quest

1. Open the address above in the **Meta Quest Browser**.
2. Press **Enter VR**.

In VR: hold the trigger to swim where you point, tap it on an image to glide there, left stick moves, right stick snap-turns, grip switches geometry, A/X toggles cruise, B/Y shuffles.

On a computer: drag to look, W A S D to swim, Q/E to sink/rise, Space toggles cruise, 1–9 switch geometry, click an image to glide to it.

## Tune it

The left panel controls image count (12–20,000), geometry (sphere, helix, tunnel, galaxy, lattice, cloud, torus, wall, whirlpool), placement (spread, twist, scatter, facing, order), effects (blending, opacity, size, breathing, border, visibility) and motion (cruise, drift, morph time, stagger, endless). Settings are remembered per browser.

**Add photos** loads up to 256 of your own images. They are read in the browser and never uploaded.

## Files

- `index.html` — the whole app (three.js r128 from cdnjs)
- `manifest.webmanifest`, `icons/` — install metadata
- `sw.js` — offline cache
