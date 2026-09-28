"""Fits the enemy health curve to the damage the player can actually be expected to do.

The chain, minute by minute through a 30 minute run:

  weapon DPS by level   measured by Tools > Balance > Weapon DPS Benchmark (Benchmarks/weapon_dps.csv,
                        its crowd numbers, without the player's stats) where there are measurements,
                        and a paper model of each weapon (below, from its asset's level table) where
                        there aren't
  a typical build       the player's level at each minute, how many weapons they hold and at what
                        level, and what their passives add (BUILD, below)
  expected player DPS   the sum over the build's weapons, times the passives; evolutions are left
                        out on purpose: SpawnDirector's evolution pressure answers those
  the crowd             how many enemies the timeline keeps on screen at that minute (its beat's cap)
                        and their average health, counted by head (wisps come in packs, so a beat
                        that favours wisps is mostly wisps), armour included
  clear time            how long the build should take to burn through a whole screen of them
                        (CLEAR, below): the design's one real knob

  health multiplier = clear time x expected DPS / (crowd size x average health)

  python balance.py            prints the table and what it would change
  python balance.py --apply    writes the curve into both maps' difficulty curves (the Huangquan road
                               keeps its margin over the Courtyard)
  --pure                       the fit as it is. without it, the curve never goes below BASELINE (the
                               curve before any fit): the paper model's late game is its least sure
                               part, and a guess shouldn't make the end of the run easier. once the
                               benchmark has measured the weapons, --pure is the one to trust

The paper model assumes a crowd packed round the player at DENSITY enemies a square unit, as the
benchmark's standing crowd is; its weakest guesses are the ones marked "rough". Run the benchmark
in Unity and run this again: measured numbers replace the guesses weapon by weapon.
"""
import csv
import glob
import math
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
GAME = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "### Different Engine"))
BENCH = os.path.normpath(os.path.join(HERE, "..", "..", "Benchmarks", "weapon_dps.csv"))

DENSITY = 1.0          # enemies a square unit in the crowd pressed round the player
CROWD_CAP = 80         # the benchmark's crowd; the paper model never counts more than this in reach
NEAR_SHARE = 0.6       # in play, of the horde on screen, the share pressed round the player
STARTING = {"Bow", "Peach Talismans", "Seven Star Swords"}   # the cast's starting weapons: a build's first

# the design: seconds a typical build should need to burn through a whole screen of the horde.
# quick early (the power fantasy, and the first levels one-shot anyway), a real fight by the
# middle of the run, a wall to push against at the end
CLEAR = [(0, 2.5), (5, 3.5), (10, 5.0), (15, 6.0), (20, 7.0), (25, 7.5), (30, 8.0)]

# a typical run, minute by minute: the player's level, from the level curve and a steady kill rate
LEVEL = [(0, 1), (1, 3), (2, 5), (3, 7), (5, 10), (7.5, 14), (10, 18), (12.5, 21), (15, 24), (20, 30), (25, 35), (30, 40)]
WEAPON_SHARE = 0.6     # of the picks, how many go to weapons (the rest to passives)
NEW_WEAPON_EVERY = 4   # picks between new weapons, until the slots are full
WEAPON_SLOTS = 6
PASSIVE_PER_PICK = 0.025   # what each passive pick adds to damage, all told (Might, Area, Cooldown, crit)


# the Courtyard's health curve before any fit (seconds, multiplier)
BASELINE = [(0, 1.0), (300, 1.25), (600, 2.0), (900, 3.3), (1200, 5.0), (1500, 7.2), (1800, 9.8), (2100, 12.6), (2400, 15.8), (2700, 19.2)]


def lerp_table(table, x):
    if x <= table[0][0]: return table[0][1]
    for (x0, y0), (x1, y1) in zip(table, table[1:]):
        if x <= x1: return y0 + (y1 - y0) * (x - x0) / (x1 - x0)
    return table[-1][1]


# ---------------------------------------------------------------- reading the assets

def read(path):
    with open(path, encoding="utf-8") as f: return f.read()


def scalars(text):
    return {k: float(v) for k, v in re.findall(r"^  (\w+): (-?[0-9.]+)$", text, re.M)}


def levels(text):
    m = re.search(r"^  levels:\n((?:  [- ] .*\n)+)", text, re.M)
    if not m: return []
    out, cur = [], None
    for line in m.group(1).splitlines():
        kv = re.match(r"^  (-| ) (\w+): (-?[0-9.]+)$", line)
        if not kv: continue
        if kv.group(1) == "-":
            cur = {}
            out.append(cur)
        cur[kv.group(2)] = float(kv.group(3))
    return out


def weapons():
    found = {}
    for path in glob.glob(os.path.join(GAME, "Data", "Weapons", "*.asset")):
        text = read(path)
        title = re.search(r"^  title: (.*)$", text, re.M).group(1).strip()
        pool = re.search(r"^  includeInPool: (\d)", text, re.M)
        found[title] = {"levels": levels(text), "s": scalars(text), "pool": pool is None or pool.group(1) == "1"}
    return found


def archetypes():
    by_guid = {}
    for path in glob.glob(os.path.join(GAME, "Data", "Curves", "Archetypesd", "*.asset")):
        guid = re.search(r"guid: (\w+)", read(path + ".meta")).group(1)
        s = scalars(read(path))
        by_guid[guid] = {"name": os.path.basename(path)[:-6], "hp": s.get("baseHealth", 10), "armour": s.get("armour", 0), "cost": s.get("cost", 2)}
    return by_guid


def beats(timeline, arch):
    text = read(os.path.join(GAME, "Data", "Spawning", timeline + ".asset"))
    body = text.split("  events:")[0]
    out = []
    for b in re.split(r"\n  - label: ", body)[1:]:
        s = scalars("\n" + "\n".join("  " + l.strip() for l in b.splitlines()))
        mix = [(g, float(w)) for g, w in re.findall(r"guid: (\w+), type: 2\}\n\s+weight: ([0-9.]+)", b)]
        out.append({"from": s["fromMinute"], "cap": s["cap"], "mix": [(arch[g], w) for g, w in mix if g in arch]})
    return out


# ---------------------------------------------------------------- the paper model

_CAP = [CROWD_CAP]


def reach(r):
    """enemies of the packed crowd within r units of the player"""
    return min(_CAP[0], DENSITY * math.pi * r * r)


def paper(title, w, level):
    """a weapon's DPS at a level against the packed crowd, before the player's stats"""
    L = w["levels"]
    if not L: return 0.0
    v = L[min(level, len(L)) - 1]
    s = w["s"]
    if title == "Bow":
        return v["arrows"] * v["damage"] / v["cooldown"]
    if title == "Electrical Aura":
        return v["damage"] / v["interval"] * reach(v["radius"])
    if title == "Meteorite":
        return v["meteors"] * v["damage"] * reach(v["radius"]) * (1.0 if v.get("smart") else 0.7) / v["cooldown"]
    if title == "Peach Talismans":
        return (v["impactDamage"] + v["damagePerSecond"] * s.get("stickSeconds", 5)) / v["interval"]
    if title == "Seven Star Swords":
        # rough: each sword cuts about two enemies a second on its way round
        return level * v["contactDamage"] * 2.0
    if title == "Flying Sword":
        # rough: a blade crosses the crowd once a bounce, cutting ~4 enemies each time, harder each bounce
        crossings = v["bounces"] + 1
        bonus = 1 + s.get("bonusPerBounce", 0.12) * v["bounces"] / 2
        return v["blades"] * crossings * 4 * v["damage"] * bonus / v["cooldown"]
    if title == "Dragon Line":
        # rough: a dragon's path crosses ~5 units of crowd; the head hits x3, the body once more
        hit = DENSITY * 2 * s.get("bodyRadius", 0.35) * 5
        return v["dragons"] * hit * v["damage"] * (s.get("headMultiplier", 3) + 1) / v["cooldown"]
    if title == "Cinnabar Ink Brush":
        # rough: ~3 units of wet ink behind the player with the crowd walking over it
        on_ink = DENSITY * v["width"] * 3
        return v["damage"] / s.get("tickSeconds", 0.3) * on_ink
    if title == "Treasure Gourd":
        # the pull's cone gathers the crowd in reach; the fire hits it once and it burns
        cone = min(_CAP[0], DENSITY * 0.5 * v["suctionRadius"] ** 2 * math.radians(2 * s.get("suctionHalfAngle", 36)))
        cycle = v["cooldown"] + s.get("suctionSeconds", 1.5) + s.get("spraySeconds", 0.75)
        return cone * (v["damage"] + v["burnDps"] * v["burnSeconds"]) / cycle
    if title == "Command Token":
        return v["damage"] * _CAP[0] / v["cooldown"]
    if title == "Ice Cloud":
        return v["clouds"] * v["damage"] * reach(v["radius"]) * s.get("driftSeconds", 7) / s.get("snowTick", 1) / v["cooldown"]
    return 0.0


# weapons changed since Benchmarks/weapon_dps.csv was measured: their measured damage times this,
# until the benchmark is run again (then empty this)
ADJUST = {
    "Electrical Aura": 0.35,     # damage x0.35
    "Meteorite": 0.5,            # damage x0.5 (its levels 1-4 measured 0: meteors fell on random spots)
    "Cinnabar Ink Brush": 0.6,   # damage x0.6
    "Peach Talismans": 2.0,      # impact and burn x2
}


def measured():
    """weapon -> [(level, crowd dps, single dps)] from the benchmark (not evolved), without the
    player's stats, with ADJUST applied. a measurement of nothing is taken as missing"""
    if not os.path.exists(BENCH): return {}
    rows = {}
    with open(BENCH, newline="", encoding="utf-8") as f:
        for r in csv.DictReader(f):
            if r["evolved"] == "1": continue
            key = (r["weapon"], int(r["level"]))
            rows.setdefault(key, {})[r["scenario"]] = float(r["normalised_dps"]) * ADJUST.get(r["weapon"], 1.0)
    out = {}
    for (weapon, level), sc in rows.items():
        crowd = sc.get("crowd", 0.0)
        if crowd <= 0: continue
        out.setdefault(weapon, []).append((level, crowd, sc.get("single", 0.0)))
    return {k: sorted(v) for k, v in out.items()}


def dps_at(title, w, level, bench):
    """a weapon's DPS in play at a level: measured where there's a measurement. the benchmark's
    crowd is 80 unkillable enemies pressed round the player, more than play keeps in reach, so the
    part of its damage that comes from hitting many at once is scaled to the crowd in reach now"""
    if title in bench:
        pts = bench[title]
        crowd = lerp_table([(l, c) for l, c, _ in pts], level)
        singles = [(l, s1) for l, _, s1 in pts if s1 > 0]
        single = lerp_table(singles, level) if singles else 0.0
        if level < pts[0][0]:
            crowd *= level / pts[0][0]
            single *= level / pts[0][0]
        share = min(1.0, _CAP[0] / CROWD_CAP)
        return single + max(0.0, crowd - single) * share
    return paper(title, w, level)


# ---------------------------------------------------------------- the build and the fit

def build_dps(minute, table, cap):
    """the expected DPS of a typical build at a minute: its weapons at their levels, times the
    passives. the first weapon is a starting weapon (the average of the three), the rest the
    average of the other weapons in the pool, each at the same share of its own levels. only the
    part of the horde pressed round the player is in reach"""
    _CAP[0] = min(CROWD_CAP, cap * NEAR_SHARE)
    level = lerp_table(LEVEL, minute)
    picks = max(0.0, level - 1)
    held = min(WEAPON_SLOTS, 1 + int(picks * WEAPON_SHARE // NEW_WEAPON_EVERY))
    weapon_levels = 1 + picks * WEAPON_SHARE          # every level of every weapon held, added up
    each = weapon_levels / held
    total = 0.0
    for i in range(held):
        # a weapon at `each` of 8 levels: every weapon's DPS at that share of its own table
        kind = [t for t in table if (t in STARTING) == (i == 0)] or list(table)
        shares = [dps_for_share(t, min(1.0, each / 8.0)) for t in kind]
        total += sum(shares) / len(shares)
    passives = 1 + PASSIVE_PER_PICK * picks * (1 - WEAPON_SHARE)
    return total * passives, level, held, each


_TABLE = {}


def dps_for_share(title, share):
    w, bench = _TABLE[title]
    top = max(1, len(w["levels"]))
    lv = max(1, min(top, round(share * top)))
    return dps_at(title, w, lv, bench)


def crowd(minute, bts):
    """the beat's cap and the average health of its enemies, counted by head, armour included"""
    b = [x for x in bts if x["from"] <= minute][-1]
    return b["cap"], beat_hp(b, minute)


def beat_hp(b, minute):
    pack = 10 + (21 - 10) * min(1.0, minute * 60 / 600)     # SpawnDirector's pack sizes, ramping in
    heads = hp = 0.0
    for a, weight in b["mix"]:
        n = pack if a["cost"] <= 1 else 1
        heads += weight * n
        hp += weight * n * a["hp"] / max(0.1, 1 - a["armour"])
    return hp / heads


def reference_hp(bts):
    """the run's typical enemy: the beats' average health, each weighted by how long it runs. the
    fit uses this one enemy, so a beat full of Nian stays harder than one full of wisps, as designed"""
    total = weight = 0.0
    for i, b in enumerate(bts):
        end = bts[i + 1]["from"] if i + 1 < len(bts) else 30.0
        span = max(0.0, min(end, 30.0) - b["from"])
        total += beat_hp(b, (b["from"] + min(end, 30.0)) / 2) * span
        weight += span
    return total / weight


def curve_keys(path):
    text = read(path)
    m = re.search(r"  enemyHealth:\n    serializedVersion: 2\n    m_Curve:\n(.*?)\n    m_PreInfinity", text, re.S)
    return [(float(t), float(v)) for t, v in re.findall(r"time: (\S+)\n\s+value: (\S+)", m.group(1))]


def write_curve(path, keys):
    text = read(path)
    m = re.search(r"(  enemyHealth:\n    serializedVersion: 2\n    m_Curve:\n)(.*?)(\n    m_PreInfinity)", text, re.S)
    body = []
    for i, (t, v) in enumerate(keys):
        # slopes from the neighbours, so the curve passes smoothly through every key
        t0, v0 = keys[max(0, i - 1)]
        t1, v1 = keys[min(len(keys) - 1, i + 1)]
        slope = (v1 - v0) / (t1 - t0) if t1 > t0 else 0.0
        body.append(f"""    - serializedVersion: 3
      time: {t:g}
      value: {v:.3f}
      inSlope: {slope:.6f}
      outSlope: {slope:.6f}
      tangentMode: 0
      weightedMode: 0
      inWeight: 0.33333334
      outWeight: 0.33333334""")
    text = text[:m.start()] + m.group(1) + "\n".join(body) + m.group(3) + text[m.end():]
    with open(path, "w", encoding="utf-8") as f: f.write(text)


def main():
    apply = "--apply" in sys.argv
    ws = weapons()
    bench = measured()
    for title, w in ws.items():
        if w["pool"] and title != "Command Token": _TABLE[title] = (w, bench)

    print("weapon DPS against the benchmark's crowd of 80, before the player's stats (M = measured, p = paper)")
    _CAP[0] = CROWD_CAP
    print(f"{'weapon':22}{'L1':>8}{'mid':>8}{'max':>8}")
    for title, (w, b) in sorted(_TABLE.items()):
        top = len(w["levels"])
        vals = [dps_at(title, w, lv, b) for lv in (1, max(1, math.ceil(top / 2)), top)]
        print(f"{title:22}" + "".join(f"{x:8.0f}" for x in vals) + ("  M" if title in b else "  p"))

    arch = archetypes()
    bts = beats("CourtyardTimeline", arch)
    court = os.path.join(GAME, "Data", "Curves", "CourtyardDifficulty.asset")
    road = os.path.join(GAME, "Data", "Curves", "HuangquanDifficulty.asset")
    old = curve_keys(court)

    ref = reference_hp(bts)
    print(f"\nthe run's typical enemy: {ref:.1f} health (armour included)")
    print("minute  level weapons  each   build dps  crowd  beat hp  clear s   health x   (was)")
    minutes = [0, 2.5, 5, 7.5, 10, 12.5, 15, 17.5, 20, 22.5, 25, 27.5, 30]
    fit = []
    for m in minutes:
        cap, hp = crowd(m, bts)
        dps, level, held, each = build_dps(m, _TABLE, cap)
        clear = lerp_table(CLEAR, m)
        h = clear * dps / (cap * ref)
        fit.append((m, h))
        print(f"{m:6.1f}{level:7.0f}{held:8d}{each:7.1f}{dps:12.0f}{cap:7.0f}{hp:8.1f}{clear:8.1f}{h:11.2f}   ({lerp_table(old, m * 60):.2f})")

    # smoothed (in log space, each point with its neighbours) so a new weapon in the model doesn't
    # become a step in the game; it starts at 1 (the first minutes are one-shot anyway), never goes
    # down, and without --pure never below the baseline. past 30 minutes (endless) it keeps the
    # baseline's growth
    pure = "--pure" in sys.argv
    logs = [math.log(max(0.05, h)) for _, h in fit]
    smooth = [logs[0]] + [(logs[i - 1] + 2 * logs[i] + logs[i + 1]) / 4 for i in range(1, len(logs) - 1)] + [logs[-1]]
    keys, top = [], 1.0
    for (m, _), l in zip(fit, smooth):
        h = math.exp(l) if m > 0 else 1.0
        if not pure: h = max(h, lerp_table(BASELINE, m * 60))
        top = max(top, h)
        keys.append((m * 60, round(top, 2)))
    for extra in (35, 40, 45):
        t = extra * 60
        keys.append((t, round(keys[-1][1] * lerp_table(BASELINE, t) / lerp_table(BASELINE, t - 300), 2)))

    # the road keeps the margin it had over the Courtyard, key by key
    road_old = curve_keys(road)
    ratio = lambda t: lerp_table([(k, rv / lerp_table(old, k)) for k, rv in road_old], t)
    road_keys = [(t, round(v * ratio(t), 2)) for t, v in keys]

    print("\nCourtyard health curve:", ", ".join(f"{t/60:g}m {v:.2f}" for t, v in keys))
    print("Huangquan health curve:", ", ".join(f"{t/60:g}m {v:.2f}" for t, v in road_keys))
    if apply:
        write_curve(court, keys)
        write_curve(road, road_keys)
        print("written into CourtyardDifficulty and HuangquanDifficulty")
    else:
        print("(python balance.py --apply writes them)")


if __name__ == "__main__":
    main()
