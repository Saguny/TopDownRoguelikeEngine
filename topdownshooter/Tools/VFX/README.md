# Weapon VFX generator

The weapons' pixel-art effects are drawn by code, frame by frame, in the Command Token's style
(its palette, ordered dithering, fire / azure / peach ramps), at the enemies' pixel size
(28.46 px per unit).

1. `node weapons/weapons.js` draws every element into `weapons/out/` (one PNG per layer per frame,
   plus a `sheet_*.png` preview of each).
2. `node weapons/make-lua.js` writes `weapons/assemble.lua`.
3. `aseprite -b --script weapons/assemble.lua` saves the layered `.aseprite` files to
   `Assets/### Different Engine/NewSprites/Asesprites/VFX/Weapons/<weapon>/`.
4. In Unity, **Tools > VFX > Build Weapon FX** sets the files up for pixel art, rebuilds the
   effect prefabs (`Prefabs/VFX/Weapons`) and points the weapons at them.
5. **Tools > VFX > Weapon Showcase** plays every weapon maxed against a crowd and saves
   screenshots (the log says where).

The `.aseprite` files can also be edited by hand; step 4 picks the changes up.

The Cinnabar Ink Brush has a generator of its own, `brush/brush.js` (the trail's blots, the brush,
its flames, the evolution's seal and blast, the icons). `node brush/brush.js` also writes
`brush/out/preview_trail.png`, a stroke painted the way the game paints it. Then
`node weapons/make-lua.js brush/out`, the same Aseprite step, and **Tools > VFX > Build Cinnabar
Ink Brush** in Unity.
