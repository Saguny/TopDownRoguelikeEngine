"""Yama's health, from what a build of six evolved weapons does to him.

He's fought alone, so every weapon's attacks go to him; the question is what each evolved weapon
does to one big target (his hurtbox is 1.3 units round) that all its attacks can reach. the homing
ones are measured (the benchmark's single target, weapon_dps.csv); the ones that go where they
go are worked out from their evolved stats below, since the benchmark's small circling dummy is
no measure of them. all before the player's stats (Might, crit and the rest shorten the fight).

he's fought as a duel now (BossDuel): the player's weapons are put away and the starting weapon
comes back in a duel form (DuelWeapon), each of which is set to the damage a second this works
out for six evolved weapons, about 1600, so the health below still gives the fight its length.

    python boss.py              the table and the health it comes to
    python boss.py --apply      writes it into Prefabs/Yama.prefab
"""
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PREFAB = os.path.join(HERE, "..", "..", "Assets", "### Different Engine", "Prefabs", "Yama.prefab")

FIGHT_SECONDS = 150.0       # from his bar coming up to his end
WEAPONS_IN_BUILD = 6

# evolved damage per second on him, every attack landing, and where it comes from
EVOLVED = {
    "Heaven-Piercing Bow":      (208, "an arrow every 0.1s at 26 x 0.8; aimed, all land (benchmark single: 205)"),
    "Peach Talismans":          (1173, "8 talismans stuck and burning; homing (benchmark single: 1173)"),
    "Seven Star Swords":        (431, "7 swords x 8 stars x 10 every 1.3s; homing. the swords' own cut can't reach him at range"),
    "Sovereign Blade Array":    (80, "5 blades x 6 a hit, bouncing off him up to 6 times each every 2s, about 2/3 landing; the lasers rarely cross him"),
    "Coiling Azure Dragon":     (65, "every 6s: the head (165) and a fire or two (110) as it coils past him, the body twice (40)"),
    "Gourd of Heaven and Earth": (70, "a sphere every ~6.5s: 40 + 25 for each of his bullets it swallowed (~15), and its burn"),
    "Calligraphic Seal Grid":   (70, "a boss takes 400 a sealed loop; one drawn round him every ~6s while dodging"),
    "Frost Tornado":            (20, "the tornado's spray (15 every 1.2s) while it's up, and the clouds' snow when they drift over him"),
}

# the time he can't be hurt: a spell card's declaration (1.5s x 4), the start of a plain phase
# (0.9s x 2), each phase's break (1.8s x 6), and the moment after his bar comes up (1s)
INVULNERABLE = 1.5 * 4 + 0.9 * 2 + 1.8 * 6 + 1.0


def main():
    names = sorted(EVOLVED, key=lambda n: -EVOLVED[n][0])
    print(f"{'evolved weapon':28} {'dps':>6}   how")
    for n in names:
        print(f"{n:28} {EVOLVED[n][0]:>6}   {EVOLVED[n][1]}")
    each = sum(v for v, _ in EVOLVED.values()) / len(EVOLVED)
    build = each * WEAPONS_IN_BUILD
    best = sum(sorted((v for v, _ in EVOLVED.values()), reverse=True)[:WEAPONS_IN_BUILD])
    worst = sum(sorted(v for v, _ in EVOLVED.values())[:WEAPONS_IN_BUILD])
    hurtable = FIGHT_SECONDS - INVULNERABLE
    health = round(build * hurtable / 5000) * 5000
    print()
    print(f"an evolved weapon on him: {each:.0f} dps on average; six of them: {build:.0f} dps")
    print(f"{FIGHT_SECONDS:.0f}s of fight, {INVULNERABLE:.1f}s of it untouchable: {hurtable:.1f}s of damage")
    print(f"health: {health:,}")
    for label, dps in (("the strongest six", best), ("the weakest six", worst)):
        print(f"  {label} ({dps:.0f} dps): {health / dps + INVULNERABLE:.0f}s")

    if "--apply" in sys.argv:
        s = open(PREFAB).read()
        if not re.search(r"^  health: [0-9.]+$", s, re.M):
            raise SystemExit("Yama.prefab has no health field to write")
        open(PREFAB, "w").write(re.sub(r"^  health: [0-9.]+$", f"  health: {health}", s, flags=re.M))
        print("written to Prefabs/Yama.prefab")


if __name__ == "__main__":
    main()
