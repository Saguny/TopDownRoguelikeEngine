# Wanjian — Wiki

A Vampire Survivors–style roguelike in Chinese mythology. Unity 6 (6000.0.44f1), by OFF-BY-ONE.
The project lives in `topdownshooter/`; game code and data are under `Assets/### Different Engine/`.

---

## A run

| | |
|---|---|
| **Waves** | 3 minutes each. The run clock runs during a wave and stops for everything between waves. |
| **Final Rush** | At the end of every wave, a ring of spirit seals rises around the player; you can't leave it. The rush's bosses come in on the far side of the ring. Beat them to win the rush. |
| **After a rush** | A seal wave wipes the horde, all wen on the ground is pulled in, and the rush boss drops a fortune envelope. The next wave waits until you open it. |
| **Final boss** | Wave 10, or at 28:00 on the run clock at the latest. After the last rush you're healed to full. |
| **End of the night** | At 30:00 the Wuchang (the black and white guards of the dead) come for the player; that counts as surviving. Beating the final boss wins the run. |
| **Endless** | Unlocks after finishing 5 normal runs. Enemy scaling has no limit, overcharge picks appear (see below), bombardment strikes rain down, and your best time is recorded. |

**Wen** is both XP and money. Every kill gives 1 wen (times Growth), and enemies drop more. Wen fills the level bar, and the same wen is banked as **coins** for the shop.

---

## Characters

Each starts with a weapon that has a **signature** bonus. Characters after the first cost coins: 10,000, then 3,000 more for each one bought.

| Character | Starting weapon | Signature |
|---|---|---|
| **Zhuo Lan**, a Qing Dynasty archer | Bow | +1 Arrow Count |
| **Ye Tianshu**, a sword immortal of the Dipper | Seven Star Swords | Stars slow enemies 30% for 2 s |
| **Yun Xi**, a Maoshan exorcist | Peach Talismans | Talismans hit twice as hard |

---

## Maps

Maps unlock in order: win a normal run on one to open the next.

| Map | Horde | Final Rush bosses | Final boss |
|---|---|---|---|
| **The Desecrated Courtyard**: a Taoist temple, its bells fallen | Jiangshi, Ghost Fire, Hungry Ghost, Fox Spirit, Nian Beast | Jiangshi Magistrate | Yama |
| **Huangquan Road**: the road of the dead to the Yellow Springs | Wandering Soul, Paper Servant, Spider Lily Demon, Hell Money Burner, Soul Guiding Lantern, Jiangshi | Ox-Head, Horse-Face (they step down off their statues) | Meng Po |

Timeline events add swarms, stampedes, encirclements and elites (elites drop fortune envelopes). From 12:00 (Huangquan) or 14:00 (Courtyard), every evolution you hold makes the horde tougher.

---

## Weapons

6 weapon slots. Weapons level up from level-up picks. **Evolving:** once a weapon is at its max level and you hold at least one other weapon, its evolution comes out of a fortune envelope.

| Weapon | What it does | Evolution |
|---|---|---|
| **Bow** | Arrows at the nearest enemy | Heaven-Piercing Bow |
| **Seven Star Swords** | Swords circle you and burst stars in every direction | Evolved Seven Star Swords (shooting stars) |
| **Peach Talismans** | Talismans stick to enemies and burn them | Evolved Peach Talismans |
| **Dragon Line** | An azure dragon flies a line cast across the screen | Coiling Azure Dragon (one coil at a time, 10 s between) |
| **Flying Sword** | A jade blade ricochets between enemies and the screen's edges | Sovereign Blade Array |
| **Treasure Gourd** | Pulls the horde together and sprays it with holy fire | Gourd of Heaven and Earth |
| **Cinnabar Ink Brush** | A giant brush at your heels paints burning cinnabar wherever you walk | Calligraphic Seal Grid (close a loop of ink: everything inside is wiped out) |
| **Electrical Aura** | A close-range support field; every other pulse it also clears enemy shots within 2 units. Area grows it by +40% at most | — |
| **Meteorite** | Meteors crash down on the horde | — |
| **Command Token** (**E**) | A shockwave that hits every enemy on screen and clears every enemy shot (the game's "bomb"). Doesn't take a weapon slot | — |
| *Ice Cloud* | Snow clouds that freeze the horde (currently out of the level-up pool) | Frost Tornado |

Attack class: arrows and blades are **Physical**, talismans, spells and summons **Magical**. Some enemies take more of one than the other.

---

## Passives

6 passive slots.

| Passive | Per level | Max level |
|---|---|---|
| Max Health | +20% (and heals you fully) | 3 |
| Might | +10% damage | 5 |
| Area | +10% area | 5 |
| Cooldown | −8% cooldown | 5 |
| Weapon Speed | +10% projectile speed | 5 |
| Light Step | +10% move speed | 5 |
| Steady Hands | +8% crit chance | 5 |
| Executioner | +30% crit damage | 4 |
| Armour Piercing | ignore 25% more armour | 4 |
| Piercing | arrows, stars and flying swords go through +1 enemy | 3 |
| Pickup Radius | +5% | 7 |

**Level-up tools:** Reroll, Skip and Banish, charges bought in the shop. When everything is maxed, the level-up offers gifts instead: a **String of Wen** (2,500 coins before Greed) or a **Peach of Immortality** (heals 30%).

---

## Stats

Max Health, Recovery, Armor, Move Speed, Might, Cooldown, Area, Weapon Speed, Arrow Count, Pierce, Crit Chance, Crit Damage, Magnet, Growth (more wen), Greed (more coins), Revival, Reroll, Skip, Banish, Armour Piercing.

## Shop (permanent upgrades, bought with coins)

Each rank costs its base × the rank number.

| Upgrade | Per rank | Ranks | Base cost |
|---|---|---|---|
| Max Health | +20 | 5 | 24,000 |
| Recovery | +0.1/s | 5 | 30,000 |
| Armor | +1 | 5 | 40,000 |
| Might | +5% | 5 | 40,000 |
| Cooldown | −5% | 5 | 45,000 |
| Area | +5% | 5 | 30,000 |
| Crit Chance | +2% | 10 | 12,000 |
| Crit Damage | +5% | 5 | 25,000 |
| Growth | +10% | 8 | 24,000 |
| Greed | +10% | 8 | 20,000 |
| Reroll | +1 | 3 | 50,000 |
| Skip | +1 | 5 | 30,000 |
| Banish | +1 | 1 | 120,000 |
| Revival | +1 | 1 | 300,000 |

---

## Fortune envelopes

Red envelopes dropped by elites, rush bosses and final bosses, opened with a reveal sequence. They pay out levels, **evolutions** (the only way to get one), or, when nothing's left to level, gifts. Rarities: common, rare, legendary.

---

## Bosses

### Final Rush bosses
- **Jiangshi Magistrate** (Courtyard). Hops after you and takes turns at three attacks:
  - **Leap:** a seal marks your spot, then it lands there in a ring of corpse fire.
  - **Storm:** spinning arms of corpse fire.
  - **Raise:** lightning, and jiangshi climbing out where it struck.

  Below half health everything is doubled.
- **Ox-Head** (Huangquan). A red lane shows where he'll charge. He tramples the horde on the way, skids, and charges straight back with a shorter warning. Hitting a wall dazes him (+20% damage taken).
- **Horse-Face** (Huangquan). Keeps his distance and pulses rings of twelve slow spirit orbs in a steady rhythm.

### Final bosses: duels
When the final boss arrives, all your weapons are put away and your **starting weapon** returns in a duel-only form. You move at +80% speed and the boss's bullets fly at twice their speed. You keep the Command Token as a bomb. Your build only adds up to +40% damage. Clean spell cards (no hits taken) pay a coin bonus.

| Starting weapon | Duel form |
|---|---|
| Bow | Sun-Shooter's Bow: a stream of arrows plus a charged sun shot, every 9th sun bigger |
| Seven Star Swords | Big Dipper Sword Formation: seven lunges, then a seven-sword volley |
| Peach Talismans | Peach Wood Decree: talismans that stick, burn, then detonate |

- **Yama, King of Hell** (Courtyard). Touhou-style danmaku over several health bars, with named spell cards. His last phase is a mirror fight with his reflection.
- **Meng Po, the Lady of Forgetting** (Huangquan). Soup, lanterns and the river's current (walls of bone from upstream), the Crossing corridor, then her true form (Six Paths). Her last card, "Drink, and Forget Everything," is a whirlpool that pulls you toward her while gapped rings pour out. The "forgetting" briefly veils her bullets.

Every health bar after the first starts fresh: you're drawn back to the middle and the boss returns above you. Then comes a Genshin-style **phase burst**; the last phase gets the biggest.

---

## Controls

| | |
|---|---|
| Move | WASD / left stick |
| Command Token | **E** |
| Pause | Esc |
| Dev tools (dev builds) | F1: max level, jump to the final boss ("Duel test"), and more |

---

## For developers

| Where | What |
|---|---|
| `Tools/VFX/` | Pixel-art generators (node). `README.md` explains each set; `icons/stats.js` draws the stat and passive icons; `yama/bullets.js` draws the danmaku |
| `Tools/SFX/` | Sound synthesis (python), e.g. `ui.py`, `burst.py`, `duel.py` |
| `Tools/Balance/` | `balance.py` (health curve), `pacing.py` (level curve), `boss.py` |
| `Tools/Huangquan/` | Huangquan prefabs, archetypes and timeline generators |
| `Data/Spawning/*Timeline.asset` | Beats (crowd caps, spawn rates, rosters), events, late-evolution pressure |
| `Data/Curves/*Difficulty.asset` | Enemy health, speed, damage and the wen needed per level |
| Game over → **Log This Run** | Writes a run report to `%USERPROFILE%\AppData\LocalLow\OFF-BY-ONE\Wanjian\Runs` |
