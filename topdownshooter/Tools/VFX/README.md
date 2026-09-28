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

## The newer weapons, the arena's seals and the back weapons

`weapons2/weapons2.js` draws the Dragon Line, the Ice Cloud and the Flying Sword (their sprites,
attack effects and level up icons), the Final Rush arena's spirit seals, and the weapons the
characters wear on their backs (`back_*`, at the characters' pixel size; nobody holds a weapon in
their hands any more, see `characters/characters.js`). It also writes `out/preview_dragon.png`
(the whole dragon assembled the way the game lays it out) and `out/preview_backs.png` (the cast
wearing their weapons).

Without Aseprite, `write-ase.js` writes the layered `.aseprite` files straight from any
generator's `out/`, the same files `assemble.lua` would have Aseprite build:

    node weapons2/weapons2.js
    node write-ase.js weapons2/out "../../Assets/### Different Engine/NewSprites/Asesprites/VFX/Weapons"
    node characters/characters.js
    node write-ase.js characters/out "../../Assets/### Different Engine/NewSprites/Asesprites"

Then **Tools > VFX > Build Weapon FX** in Unity points everything at the new art (it also runs
once by itself the first time the new art is in the project).

## The Treasure Gourd

`gourd/gourd.js` draws the Treasure Gourd (the gourd at the player's shoulder and aimed, the cork
popping, the pull, the holy fire, an enemy burning, the evolution's plasma sphere, its burst and a
swallowed bullet, and the level up icons), in the Flying Sword's and the Command Token's style:

    node gourd/gourd.js
    node write-ase.js gourd/out "../../Assets/### Different Engine/NewSprites/Asesprites/VFX/Weapons"

Its sounds are synthesised by `../SFX/gourd.py` (into `Sounds/Gourd`). **Tools > VFX > Build Weapon
FX** points the weapon at both (it also runs once by itself the first time the gourd's art is in the
project).

## The fortune envelope and the end of a run

`fortune/fortune.js` draws the fortune envelope that elites and bosses drop (on the ground, its beam,
its markers, and everything its opening shows: the charging seal, the flap, the light, the rarity
bursts, the scroll, the reward frames, the coin and peach icons) and the Wuchang's taking of the
player (the chain, the shackle, the soul, the ink whirl, the ink wipe):

    node fortune/fortune.js
    node write-ase.js fortune/out "../../Assets/### Different Engine/NewSprites/Asesprites/VFX"

Their sounds are synthesised by `../SFX/fortune.py` (into `Sounds/Fortune`). **Tools > VFX > Build
Weapon FX** points the VfxLibrary at both (it also runs once by itself the first time the art is in
the project). `fortune/preview.py <common|rare|legendary> <out.gif>` renders an opening outside Unity
from the art and the sounds, as a GIF and an MP4 with sound.

## The pointer and the menus' flair

`ui/ui.js` draws the game's pointer (and its pressed look), the star that traces a hovered button's
edge and a click's burst, straight into `Resources/UI` as PNGs (animation frames side by side), which
`UiFlair` slices at run time; nothing to set up in Unity:

    node ui/ui.js

## The warnings: the boss medallion and Heaven's thunder

`alerts/alerts.js` draws the medallion that points the way to a boss off the screen (its rim, the
window the boss's own sprite shows through, the arrowhead; `Resources/UI/boss_*`) and Heaven's
thunder, the strike that falls on a player standing still in the endless mode (the thunder seal,
its countdown fill, the bolt and the burst; `Resources/Hazards/strike_*`). Like the Yama and Final
Rush art, the PNG strips go straight into Resources and the game slices them at run time, so
there's nothing to set up in Unity:

    node alerts/alerts.js

The strike's sounds are synthesised by `../SFX/thunder.py` (into `Resources/Sfx`).
