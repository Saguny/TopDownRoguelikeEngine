"""The wen each level costs: Vampire Survivors' experience formula, scaled to our wen.

Vampire Survivors asks 5 XP for level 2, then 10 more each level to 20, 13 more each to 40 and 16
more each after (its walls at 20 and 40 only offset the Growth it hands out there, which we don't,
so they're left out). Scaled by SCALE, since a kill here is worth more wen than a gem there.

SCALE is checked against a model of wen coming in: a typical build clears a screen of the horde
every CLEAR seconds (balance.py fits enemy health to that), so kills run at about cap / CLEAR a
second, each a piece of wen worth 1, less the share the evolutions held cut (SpawnDirector's
evoWenCut, EVOLUTIONS below):

    wen a second = K x cap(t) / CLEAR(t) x (1 - evoWenCut) ^ evolutions(t)

with K measured from a playtest (level 26 at 7:29 on the Courtyard before the crowds went up, the
old caps and health curve kept below: about 9,500 wen; back then a tougher enemy's wen was worth
more, 1 + log2 of its health, which the measurement allows for). The model runs rich in the first
minutes, where kills are held back by a bare build and the horde walking in, so only its middle is
trusted. From BLEND_TO on, each level costs what the model says comes in between the minute the
schedule (balance.py's LEVEL) reaches it and the minute it reaches the next: the level curve runs
on the schedule by construction, Vampire Survivors' shape wherever the income does. Past the
schedule's end (endless), Vampire Survivors' own growth carries it on.

    python pacing.py            the schedule and what each level costs
    python pacing.py --apply    writes it into CourtyardDifficulty and HuangquanDifficulty
"""
import math
import os
import re
import sys

import balance as B

HERE = os.path.dirname(os.path.abspath(__file__))
CURVES = os.path.join(B.GAME, "Data", "Curves")
TIMELINE = os.path.join(B.GAME, "Data", "Spawning", "CourtyardTimeline.asset")

# the playtest the income is measured against: the level reached, when, and the setup it was on
OBSERVED_LEVEL, OBSERVED_SECONDS = 26, 7 * 60 + 29
OLD_WEN_KEYS = [(1, 15), (2, 28), (3, 42), (5, 75), (10, 220), (22.8, 779.5), (48.8, 1196.9), (72.6, 1449.1), (92.5, 1749.6), (99.3, 1999.6)]
OLD_CAPS = [(0, 20), (1.5, 30), (3, 40), (4.5, 50), (7.5, 64), (9, 72), (12, 85), (13.5, 95), (18, 115), (22.5, 135), (27, 155)]
OLD_HEALTH = [(0, 1.0), (150, 1.12), (300, 1.51), (450, 2.49), (600, 4.01), (750, 5.67), (900, 7.28), (1050, 9.13), (1200, 11.08), (1350, 12.95), (1500, 14.54), (1650, 15.17), (1800, 15.83)]


def step(table, x):
    v = table[0][1]
    for t, y in table:
        if x >= t: v = y
    return v


def caps(path):
    text = open(path, encoding="utf-8").read()
    return [(float(m), int(c)) for m, c in re.findall(r"fromMinute: (\S+).*?cap: (\d+)", text, re.S)]


# the playtest's wen: a tougher enemy's worth more back then
def old_worth(health):
    return 1.0 + math.log2(max(1.0, health))


# evolutions held by a typical run (envelopes from 10:00 give them, one each), and the wen each cuts
EVOLUTIONS = [(0, 0), (10, 0), (12, 1), (15, 2), (18, 3), (21, 4), (24, 5), (27, 6)]
EVO_WEN_CUT = 0.1


def banked(cap_table, seconds, health_keys=None, clear=None):
    """wen collected by `seconds`, per unit of K. with `health_keys`, the playtest's economy (a
    tougher enemy worth more); without, today's (each piece 1, cut by the evolutions held)"""
    total = 0.0
    for s in range(int(seconds)):
        m = s / 60.0
        rate = step(cap_table, m) / B.lerp_table(clear or B.CLEAR, m)
        if health_keys: rate *= old_worth(B.lerp_table(health_keys, s))
        else: rate *= (1 - EVO_WEN_CUT) ** B.lerp_table(EVOLUTIONS, m)
        total += rate
    return total


def old_cost(level):
    return B.lerp_table(OLD_WEN_KEYS, level)


FITTED = {}         # level -> cost, from the schedule and the income model (main)
GROWTH = 1.006      # the least each level costs over the one before (the income flattens late; a
                    # level shouldn't feel cheaper for it, it just comes a little behind the schedule)
# the clear times the playtest was played at (balance.py's CLEAR has changed since)
OLD_CLEAR = [(0, 2.5), (5, 3.5), (10, 5.0), (15, 6.0), (20, 7.0), (25, 7.5), (30, 8.0)]

# the opening, where the horde starts at its old size and ramps in (a few wisps, not flocks): a
# little dearer than the old curve, which a playtester found a touch quick, blending into the
# scaled Vampire Survivors curve by BLEND_TO
EARLY = {1: 20, 2: 36, 3: 55, 4: 80, 5: 110, 6: 145, 7: 190, 8: 250, 9: 330, 10: 450}
BLEND_TO = 16


def vs_cost(level):
    """Vampire Survivors' XP from `level` to the next, without its walls at 20 and 40"""
    if level <= 1: return 5
    if level <= 20: return 5 + 10 * (level - 1)
    if level <= 40: return 195 + 13 * (level - 20)
    return 455 + 16 * (level - 40)


def minute_of(level):
    """when the schedule reaches `level`"""
    table = [(l, m) for m, l in B.LEVEL]
    return B.lerp_table(table, level)


def fit(wen_by_minute):
    """each level's cost from BLEND_TO to the schedule's last, then Vampire Survivors' growth"""
    last_level = B.LEVEL[-1][1]
    prev = 0
    for level in range(BLEND_TO, 100):
        if level < last_level:
            c = wen_by_minute(minute_of(level + 1)) - wen_by_minute(minute_of(level))
            prev = max(round(prev * GROWTH), round(c))     # never cheaper than the level before, a little dearer
        else:
            prev = round(FITTED[last_level - 1] * vs_cost(level) / vs_cost(last_level - 1))
        FITTED[level] = prev


def level_cost(level):
    last = max(EARLY)
    if level <= last: return EARLY[level]
    if level >= BLEND_TO: return FITTED[level]
    # geometric from the opening's last level to the fitted curve
    a, b = EARLY[last], FITTED[BLEND_TO]
    return round(a * (b / a) ** ((level - last) / (BLEND_TO - last)))


def main():
    observed = sum(old_cost(l) for l in range(1, OBSERVED_LEVEL))
    k = observed / banked(OLD_CAPS, OBSERVED_SECONDS, OLD_HEALTH, OLD_CLEAR)
    cap_now = caps(TIMELINE)

    # the model's wen by each whole second, for the fit
    running, acc = [0.0], 0.0
    for sec in range(31 * 60):
        m = sec / 60.0
        acc += k * step(cap_now, m) / B.lerp_table(B.CLEAR, m) * (1 - EVO_WEN_CUT) ** B.lerp_table(EVOLUTIONS, m)
        running.append(acc)
    fit(lambda minute: running[min(len(running) - 1, int(minute * 60))])
    cost = {l: level_cost(l) for l in range(1, 100)}
    cumulative = [0]
    for l in range(1, 100): cumulative.append(cumulative[-1] + cost[l])

    def level_with(cumulative, wen):
        return max(l for l in range(1, 100) if cumulative[l - 1] <= wen)

    print(f"measured: {observed:.0f} wen by {OBSERVED_SECONDS // 60}:{OBSERVED_SECONDS % 60:02d} -> K = {k:.4f}")
    print(f"{'minute':>6} {'schedule':>9} {'model wen':>10} {'level it buys':>14}")
    for m in (1, 3, 5, 7.5, 10, 15, 20, 25, 27, 30):
        wen = k * banked(cap_now, m * 60)
        print(f"{m:>6} {B.lerp_table(B.LEVEL, m):>9.0f} {wen:>10.0f} {level_with(cumulative, wen):>14}")
    print("cost of each level: " + ", ".join(f"{l}: {cost[l]}" for l in (1, 2, 5, 10, 15, 20, 25, 30, 40, 50, 60, 80, 99)))

    if "--apply" in sys.argv:
        keys = [(l, cost[l]) for l in list(range(1, 61)) + [70, 80, 90, 99]]
        for name in ("CourtyardDifficulty.asset", "HuangquanDifficulty.asset"):
            write_wen(os.path.join(CURVES, name), keys)
        print("written into CourtyardDifficulty and HuangquanDifficulty")


def write_wen(path, keys):
    text = open(path, encoding="utf-8").read()
    m = re.search(r"(  wenNeeded:\n    serializedVersion: 2\n    m_Curve:\n)(.*?)(\n    m_PreInfinity)", text, re.S)
    body = []
    for i, (t, v) in enumerate(keys):
        t0, v0 = keys[max(0, i - 1)]
        t1, v1 = keys[min(len(keys) - 1, i + 1)]
        slope = (v1 - v0) / (t1 - t0) if t1 > t0 else 0.0
        body.append(f"""    - serializedVersion: 3
      time: {t:g}
      value: {v:g}
      inSlope: {slope:.6f}
      outSlope: {slope:.6f}
      tangentMode: 0
      weightedMode: 0
      inWeight: 0.33333334
      outWeight: 0.33333334""")
    text = text[:m.start()] + m.group(1) + "\n".join(body) + m.group(3) + text[m.end():]
    with open(path, "w", encoding="utf-8") as f: f.write(text)


if __name__ == "__main__":
    main()
