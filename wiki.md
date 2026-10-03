# Wanjian — Wiki

A Vampire Survivors–style roguelike in Chinese mythology. Unity 6 (6000.0.44f1), by OFF-BY-ONE.
The project lives in `topdownshooter/`; game code and data are under `Assets/### Different Engine/`.

---

## A run

| | |
|---|---|
| **Waves** | 3 minutes each. The run clock runs during a wave and stops for everything between waves. |
| **Final Rush** | At the end of every wave, a ring of spirit seals rises around the player; you can't leave it. The rush's bosses come in on the far side of the ring. Beat them to win the rush. |
| **After a rush** | A seal wave wipes the horde, all qi on the ground is pulled in, and the rush boss drops a fortune envelope. The next wave waits until you open it. |
| **Empowered horde** | From 24:00 (when most players are maxed out) every ordinary enemy spawns empowered: 1.15× size, 3× health, 1.5× damage, steadier against knockback, outlined in crimson. Like an elite, but without the envelope. |
| **Final boss** | Wave 10, or at 28:00 on the run clock at the latest. After the last rush you're healed to full. |
| **End of the night** | At 30:00 the Wuchang (the black and white guards of the dead) come for the player; that counts as surviving. Beating the final boss wins the run. |
| **Endless** | Unlocks after finishing 5 normal runs. Enemy scaling has no limit, overcharge picks appear (see below), bombardment strikes rain down, and your best time is recorded. |

**Qi** is XP. Every kill gives 1 qi (times Growth), and some enemies drop more as pickups in three tiers: azure **qi sparks**, jade **qi beads** and golden **dragon pearls**. The thicker the crowd, the fewer pieces drop, each worth more. Qi only fills the level bar.

**Coins** (wen, the currency) come only from **fortune envelopes**, plus the String of Wen gift once you're maxed. The HUD's coin counter shows what this run's envelopes have paid.

---

## Characters

Each starts with a weapon that has a **signature** bonus. Characters after the first cost coins: 6,000, then 1,800 more for each one bought.

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
| **Command Token** (**E**) | An **ability**: a shockwave that hits every enemy on screen and clears every enemy shot (the game's "bomb"). Takes no slot, so it's still offered with every slot full. 8 levels: shorter recharge, a stun, a qi pull, and from level 5 it also cuts 25–70% of each ordinary enemy's max health (not bosses or elites) | — |
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

**Level-up tools:** Reroll, Skip and Banish, charges bought in the shop. When everything is maxed, the level-up offers gifts instead: a **String of Wen** (200 coins before Greed) or a **Peach of Immortality** (heals 30%).

---

## Stats

Max Health, Recovery, Armor, Move Speed, Might, Cooldown, Area, Weapon Speed, Arrow Count, Pierce, Crit Chance, Crit Damage, Magnet, Growth (more qi), Greed (more coins from envelopes), Revival, Reroll, Skip, Banish, Armour Piercing.

## Shop (permanent upgrades, bought with coins)

Rank *n* costs the first rank's price × *n*^1.5, so first ranks are cheap (a run that ends around 15:00 buys one or two) and the last ones are steep.

| Upgrade | Per rank | Ranks | First rank | All ranks |
|---|---|---|---|---|
| Max Health | +20 | 5 | 1,200 | 33,850 |
| Recovery | +0.1/s | 5 | 1,500 | 42,300 |
| Armor | +1 | 5 | 2,000 | 56,400 |
| Might | +5% | 5 | 2,000 | 56,400 |
| Cooldown | −5% | 5 | 2,500 | 70,500 |
| Area | +5% | 5 | 1,500 | 42,300 |
| Crit Chance | +2% | 10 | 800 | 114,100 |
| Crit Damage | +5% | 5 | 1,800 | 50,750 |
| Growth | +10% | 8 | 1,000 | 84,100 |
| Greed | +10% | 8 | 1,000 | 84,100 |
| Reroll | +1 | 3 | 3,000 | 27,100 |
| Skip | +1 | 5 | 1,500 | 42,300 |
| Banish | +1 | 1 | 12,000 | 12,000 |
| Revival | +1 | 1 | 25,000 | 25,000 |

---

## Credits

Tickets banked between runs, **3 at most**. A run that's won, or lasts 10:00 on the run clock, earns one (if there's room). Before a run, spend credits on weapons, passives or the Command Token; you can put several on one item. For that run only:

- the item is drawn a little more often in level ups and envelopes: **+15% per credit**
- **pity**: if it could have come up but didn't for 6 level ups in a row (5 with 2 credits, 4 with 3), the next level up shows it. This lasts until you take it

Credits are spent when the run starts. Taking one back off an item before then returns it.

---

## Fortune envelopes

Red envelopes dropped by elites, rush bosses and final bosses, opened with a reveal sequence. They give upgrades (1 / 3 / 5 by rarity) and **evolutions** (the only way to get one), and they are the **only source of coins**:

| Rarity | Coins (before Greed) | Upgrades |
|---|---|---|
| Common | 150–300 | 1 |
| Rare | 400–700 | 3 |
| Legendary | 1,000–1,600 | 5 |

An upgrade slot with nothing left to level pays a String of Wen (200) instead. A Final Rush drops one envelope, from the last of its bosses to fall, so a full run holds about 15. Odds by source: elites mostly common, rush bosses 45% common / 42% rare / 13% legendary, final bosses 35% rare / 65% legendary.

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
When the final boss arrives, all your weapons are put away and your **starting weapon** returns in a duel-only form. You move at +80% speed and the boss's bullets fly at twice their speed. You keep the Command Token as a bomb. Your build only adds up to +40% damage. Clean spell cards (no hits taken) add a coin bonus to the envelope the boss drops (Yama 400–1,000 a card, Meng Po 450–1,250).

| Starting weapon | Duel form |
|---|---|
| Bow | Sun-Shooter's Bow: a stream of arrows plus a charged sun shot, every 9th sun bigger |
| Seven Star Swords | Big Dipper Sword Formation: seven lunges, then a seven-sword volley |
| Peach Talismans | Peach Wood Decree: talismans that stick, burn, then detonate |

- **Yama, King of Hell** (Courtyard). Touhou-style danmaku over several health bars, with named spell cards. His last phase is a mirror fight with his reflection.
- **Meng Po, the Lady of Forgetting** (Huangquan). Soup, lanterns and the river's current (walls of bone from upstream), the Crossing corridor, then her true form (Six Paths). Her last card, "Drink, and Forget Everything," is a whirlpool that pulls you toward her while gapped rings pour out. The "forgetting" briefly veils her bullets.

Every spell card opens with a Persona-style **cut-in**: a white tear rips across the screen, the boss's eyes appear in a torn band with a brushed character beside them (Yama 判 "judgement", Meng Po 忘 "forget"), then it shatters away. About a second, and the fight doesn't pause for it.

Every health bar after the first starts fresh: you're drawn back to the middle and the boss returns above you. Then comes a Genshin-style **phase burst**; the last phase gets the biggest.

---

## Options

- **Player shot opacity** (20–100%): draws your own weapons see-through, so enemy shots read through a busy screen.
- Your health bar only shows after you've been hurt, and fades a moment after you're back to full. Taking a hit flashes the screen's edges red, harder for bigger hits; under 35% health the edges pulse like a heartbeat.

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
| `Resources/CutIn/` | The cut-in's eye art: `yama.png`, `mengpo.png`, `mengpo_true.png` (any size, framed ~3.2:1; see the README there). Without one, the boss's pixel portrait stands in |
| `Tools/Balance/` | `balance.py` (health curve), `pacing.py` (level curve), `boss.py` |
| `Tools/Huangquan/` | Huangquan prefabs, archetypes and timeline generators |
| `Data/Spawning/*Timeline.asset` | Beats (crowd caps, spawn rates, rosters), events, late-evolution pressure |
| `Data/Curves/*Difficulty.asset` | Enemy health, speed, damage and the qi needed per level |
| Game over → **Log This Run** | Writes a run report to `%USERPROFILE%\AppData\LocalLow\OFF-BY-ONE\Wanjian\Runs` |
