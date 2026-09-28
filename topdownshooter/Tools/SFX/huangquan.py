"""Huangquan Road, sound designed and synthesised from scratch (no samples) with the Command Token's,
the envelope's and the Magistrate's instruments. the road to the underworld: souls sighing, paper
and spirit money burning, bronze bells and iron chains, and the two gate guardians, a bull and a
horse, big and loud.

the horde (played often and many at once: short, soft at the top, varied in pitch by the game)
  hq_soul_death      a wandering soul let go: a breathy sigh rising away and a glassy shimmer
  hq_lily_rise       a spider lily bursting up through the road: soil cracking, a stem creaking
  hq_lily_charge     its petals clenching (0.55 s): a hollow suck drawn in, a pluck climbing
  hq_lily_fire       its ring leaving it: a soft round pop and eight spirit pings spreading
  hq_lily_death      snapped: a wet stem breaking, petals scattering
  hq_paper_dash      a paper servant slicing off: a sharp swish of paper and its flutter
  hq_paper_death     torn and catching: a rip, then paper flaring up
  hq_burner_ignite   a burner lighting its wad (0.5 s): a match struck, paper catching
  hq_burner_throw    the throw: an arm swung, the fire fluttering away
  hq_ember_land      an ember landing: a soft thump blooming into flame
  hq_ember_burn      the player standing in the fire: a hiss and sizzle
  hq_burner_death    a burner gone to ash: a sigh, ash collapsing, the brazier clanging over
  hq_wisp            a lantern's wisp leaving it: an airy whistle rising, a glass chime
  hq_lantern_appear  a soul lantern come onto the road: a far, clear bell and a breath of wind
  hq_lantern_out     a lantern hunted down: the soul fire roaring up, paper burning, a bell fading
Ox-Head
  hq_bull_snort      his telegraph: two heavy snorts and a hoof scraping the road
  hq_bull_charge     he goes: a bellow, the first pounding hoofbeats, the ground rumbling
  hq_bull_trample    something of the horde under his hooves: a crunch
  hq_bull_gore       his charge catching the player: a heavy body blow
  hq_bull_crash      into a wall: drum and sub, stone cracking, grit falling, his armour clanging
  hq_bull_skid       the charge running out: hooves skidding, a stumble
  hq_bull_dazed      stunned: little bells circling, wobbling
  hq_bull_death      his end: a bellow breaking off, fire rushing through him, embers
Horse-Face
  hq_horse_raise     the chain swung up (0.5 s): links rattling, a bronze hum swelling
  hq_horse_pulse     the pulse: a great bell struck, a whoomp of air, a shimmer ringing off
  hq_horse_death     his end: a ghostly whinny rising and breaking, the chain falling, mist
the statues
  hq_statue_wake     a guardian breaking out of his statue: stone cracking faster, a boom, rubble
  hq_statue_restore  the statue made whole: a shimmer drawn in, stone settling

python huangquan.py writes them into Assets/### Different Engine/Resources/Sfx/ (the game loads
them from there), each at the game's reference loudness (normalize.py), plus out/huangquan.wav to
listen to them one after another.
"""
import os

import numpy as np

import command_token as ct
from command_token import SR, times, env, glide, noise, brown, filt, swept, sat, bell
import fortune as fo
from fortune import pan, hall, fade, rustle, drum, sub, write, level, metal_link
from boss import crackle, roar, voice, bowl, whoosh, stone, grit
import normalize as nz

rng = np.random.default_rng(1905)
DEST = os.path.join(ct.HERE, "..", "..", "Assets", "### Different Engine", "Resources", "Sfx")


def st(x, p=0.0):
    """a mono sound into stereo at `p`"""
    return pan(x, p)


def sigh(t, start, length, f0=320.0, rise=0.6, level=1.0):
    """a breath through a hollow: noise through a formant gliding up, shaped like an exhale"""
    u = np.clip((t - start) / length, 0, 1)
    shape = np.sin(np.pi * u ** 0.6) ** 1.5 * ((t >= start) & (t < start + length))
    band = swept(noise(len(t)), lambda s: f0 * (1 + rise * np.clip((s - start) / length, 0, 1)), q=3.5)
    return level * band * shape


def paper(t, start, length, bright=1.0, level=1.0):
    """paper moving fast: crisp noise in the highs, crackly"""
    u = np.clip((t - start) / length, 0, 1)
    shape = np.sin(np.pi * u) ** 0.8 * ((t >= start) & (t < start + length))
    crisp = filt(noise(len(t)), "bandpass", [2200 * bright, 9000]) * (0.6 + 0.4 * (rng.random(len(t)) < 0.08))
    return level * crisp * shape


def hoof(t, at, level=1.0):
    """a heavy hoof on stone: a low thud and a click"""
    return level * (drum(t, at, f0=70, level=0.9, decay=0.14) + 0.5 * stone(t, at, level=0.6))


def glass(t, f0, start, level=1.0, decay=0.9):
    """a small glassy chime"""
    ratios, amps = [1, 2.76, 5.4], [1, 0.4, 0.15]
    u = np.maximum(t - start, 0)
    x = sum(a * np.sin(2 * np.pi * f0 * r * u) * np.exp(-u / (decay / (1 + i))) for i, (r, a) in enumerate(zip(ratios, amps)))
    return level * x * (t >= start)


# ---------------------------------------------------------------- the horde

def soul_death():
    t = times(0.5)
    x = sigh(t, 0.0, 0.42, f0=420, rise=0.9, level=0.9)
    x += 0.35 * glass(t, 1760 * rng.uniform(0.97, 1.03), 0.02, decay=0.35)
    x += 0.25 * glass(t, 2640, 0.06, decay=0.25)
    x += 0.4 * glide(t, 520, 260, 0.05) * env(t, 0.06, 0.002)
    return hall(st(fade(x, 0.08)), 0.8, 0.2, 11)


def lily_rise():
    t = times(0.6)
    x = 0.8 * stone(t, 0.0, level=0.9) + 0.6 * grit(t, 0.02, 0.35, level=0.6)
    creak = voice(t, 0.08, 0.35, 140, formants=(700, 1500), level=0.25, bend=0.8)
    x += creak + 0.5 * paper(t, 0.25, 0.25, bright=0.7, level=0.4)
    return hall(st(fade(x, 0.1)), 0.6, 0.18, 12)


def lily_charge():
    t = times(0.6)
    k = np.clip(t / 0.55, 0, 1)
    suck = whoosh(t, 0.0, 0.55, 3200, 600, q=2.0, level=0.8) * k
    pluck = sum(0.35 * np.sin(2 * np.pi * f * t) * env(t, 0.12, 0.002, start=s) for f, s in [(392, 0.05), (494, 0.22), (587, 0.38), (740, 0.5)])
    beat = drum(t, 0.1, f0=58, level=0.35, decay=0.12) + drum(t, 0.4, f0=62, level=0.45, decay=0.12)
    return hall(st(suck + pluck + beat), 0.7, 0.2, 13)


def lily_fire():
    t = times(0.7)
    pop = sat(glide(t, 300, 90, 0.03) * env(t, 0.09, 0.002) * 1.2, 1.5)
    air = filt(noise(len(t)), "bandpass", [200, 1500]) * env(t, 0.08, 0.004) * 0.5
    x = st(pop + air)
    for k in range(8):
        f = 1320 * 2 ** (rng.integers(0, 5) / 12)
        x += pan(0.18 * glass(t, f, 0.015 + k * 0.012, decay=0.4), np.sin(k / 8 * 6.28))
    return hall(x, 0.9, 0.22, 14)


def lily_death():
    t = times(0.5)
    snap = filt(noise(len(t)), "bandpass", [900, 5000]) * env(t, 0.025, 0.0004) + 0.6 * glide(t, 700, 200, 0.01) * env(t, 0.03, 0.0005)
    wet = swept(noise(len(t)), lambda s: 3000 * np.exp(-s / 0.03) + 400, q=0.9, kind="low") * env(t, 0.06, 0.001, start=0.01)
    petals = rustle(t, 0.04, 0.35, rate=30, bright=0.8) * 0.5
    return hall(st(snap + wet + petals), 0.5, 0.15, 15)


def paper_dash():
    t = times(0.22)
    swish = swept(noise(len(t)), lambda s: 2500 + 7000 * np.clip(s / 0.1, 0, 1), q=1.6) * np.sin(np.pi * np.clip(t / 0.18, 0, 1)) ** 1.5 * (t < 0.18)
    flutter = paper(t, 0.02, 0.18, level=0.5) * (0.6 + 0.4 * np.sign(np.sin(2 * np.pi * 38 * t)))
    return st(swish + flutter)


def paper_death():
    t = times(0.55)
    rip = paper(t, 0.0, 0.12, bright=1.2, level=1.0) * (0.5 + 0.5 * (rng.random(len(t)) < 0.3))
    flare = whoosh(t, 0.05, 0.35, 400, 1800, q=0.9, level=0.6) + crackle(t, 0.08, 0.35, rate=55, level=0.35)
    return hall(st(rip + flare), 0.5, 0.15, 16)


def burner_ignite():
    t = times(0.55)
    strike = filt(noise(len(t)), "bandpass", [1500, 7000]) * env(t, 0.05, 0.001) * 0.9
    catch = whoosh(t, 0.04, 0.45, 300, 2200, q=0.8, level=0.8) + roar(t, 0.1, 0.45, f=260, level=0.25)
    return hall(st(strike + catch + crackle(t, 0.12, 0.4, rate=45, level=0.4)), 0.5, 0.15, 17)


def burner_throw():
    t = times(0.4)
    swing = whoosh(t, 0.0, 0.3, 700, 250, q=1.3, level=0.9)
    flame = roar(t, 0.02, 0.32, f=300, level=0.3) * np.exp(-t / 0.2)
    return st(swing + flame + crackle(t, 0.05, 0.25, rate=35, level=0.3))


def ember_land():
    t = times(0.5)
    thump = drum(t, 0.0, f0=80, level=0.7, decay=0.12)
    bloom = whoosh(t, 0.01, 0.4, 250, 1500, q=0.8, level=0.7) + crackle(t, 0.03, 0.4, rate=50, level=0.45)
    return hall(st(thump + bloom), 0.5, 0.12, 18)


def ember_burn():
    t = times(0.35)
    hiss = filt(noise(len(t)), "highpass", 3500) * env(t, 0.22, 0.01) * 0.7
    return st(hiss + crackle(t, 0.0, 0.3, rate=90, level=0.5))


def burner_death():
    t = times(0.8)
    x = sigh(t, 0.0, 0.6, f0=260, rise=-0.35, level=0.8)
    x += grit(t, 0.08, 0.5, level=0.5) + filt(brown(len(t)), "lowpass", 400) * env(t, 0.25, 0.02, start=0.05) * 0.8
    x += metal_link(t, 0.18, level=0.5, low=0.35) + metal_link(t, 0.3, level=0.35, low=0.3)
    return hall(st(fade(x, 0.15)), 0.7, 0.18, 19)


def wisp():
    t = times(0.5)
    whistle = swept(noise(len(t)), lambda s: 900 + 1400 * np.clip(s / 0.4, 0, 1), q=12) * np.sin(np.pi * np.clip(t / 0.45, 0, 1)) * 1.4
    return hall(st(whistle + 0.3 * glass(t, 2093, 0.05, decay=0.3)), 0.8, 0.25, 20)


def lantern_appear():
    t = times(2.0)
    x = st(0.6 * bell(t, fo.note(4, base=fo.D5) / 2, start=0.0) + 0.25 * bell(t, fo.note(8, base=fo.D5), start=0.08))
    x += pan(whoosh(t, 0.1, 1.4, 300, 900, q=0.7, level=0.3), -0.3)
    return hall(fade(x, 0.5), 2.2, 0.4, 21)


def lantern_out():
    t = times(1.8)
    flare = whoosh(t, 0.0, 0.5, 400, 3000, q=0.9, level=0.9) + roar(t, 0.0, 0.6, f=320, level=0.5) * np.exp(-t / 0.4)
    burn = crackle(t, 0.1, 0.8, rate=60, level=0.5) + paper(t, 0.15, 0.6, bright=0.8, level=0.35)
    toll = 0.5 * bell(t, fo.note(0, base=fo.D5) / 2, start=0.35)
    x = st(flare + burn) + st(toll, 0.1)
    return hall(fade(x, 0.5), 2.0, 0.35, 22)


# ---------------------------------------------------------------- Ox-Head

def bull_snort():
    t = times(0.9)
    x = np.zeros(len(t))
    for s in (0.0, 0.32):
        x += sat(swept(noise(len(t)), lambda q: 700 + 400 * np.exp(-(q - s) / 0.1), q=1.2) * env(t, 0.16, 0.012, start=s) * 1.4, 1.5)
        x += filt(brown(len(t)), "lowpass", 300) * env(t, 0.18, 0.01, start=s) * 1.2
    scrape = filt(noise(len(t)), "bandpass", [600, 3500]) * env(t, 0.2, 0.03, start=0.55) * 0.5 + stone(t, 0.55, level=0.5)
    return hall(st(x + scrape), 0.6, 0.15, 30)


def bull_charge():
    t = times(1.2)
    bellow = voice(t, 0.0, 0.8, 92, formants=(420, 820), level=1.1, bend=-0.25)
    bellow = sat(bellow + 0.5 * voice(t, 0.0, 0.8, 138, formants=(500, 1100), level=0.5, bend=-0.3), 1.6)
    beats = sum(hoof(t, 0.12 + k * 0.16, level=1.0 - k * 0.08) for k in range(6))
    rumble = filt(brown(len(t)), "lowpass", 120) * np.clip(t / 0.3, 0, 1) * np.exp(-np.maximum(t - 0.6, 0) / 0.3) * 1.5
    return hall(st(bellow) + st(beats + rumble), 0.8, 0.15, 31)


def bull_trample():
    t = times(0.25)
    crunch = filt(noise(len(t)), "bandpass", [300, 2600]) * env(t, 0.05, 0.001) * (0.5 + 0.5 * (rng.random(len(t)) < 0.25))
    return st(crunch + drum(t, 0.0, f0=90, level=0.6, decay=0.07))


def bull_gore():
    t = times(0.5)
    blow = drum(t, 0.0, f0=55, level=1.2, decay=0.2) + sub(t, 0.0, 110, 38, level=0.9, decay=0.25)
    smack = filt(noise(len(t)), "bandpass", [400, 3000]) * env(t, 0.04, 0.0008) * 0.9
    return hall(st(sat(blow + smack, 1.4)), 0.5, 0.12, 32)


def bull_crash():
    t = times(1.8)
    boom = drum(t, 0.0, f0=48, level=1.4, decay=0.5) + sub(t, 0.0, 120, 30, level=1.3, decay=0.6)
    crack = sum(stone(t, 0.01 + k * 0.035, level=0.9 - k * 0.1) for k in range(6))
    clang = sum(metal_link(t, s, level=0.6, low=0.3) for s in (0.02, 0.09, 0.2))
    fall = grit(t, 0.15, 1.2, level=0.7)
    x = st(sat(boom + crack, 1.3)) + pan(clang, 0.2) + pan(fall, -0.2)
    return hall(fade(x, 0.4), 1.4, 0.25, 33)


def bull_skid():
    t = times(0.8)
    skid = swept(noise(len(t)), lambda s: 1800 - 1200 * np.clip(s / 0.5, 0, 1), q=1.1) * np.clip(t / 0.03, 0, 1) * np.exp(-t / 0.35) * 0.9
    stumble = hoof(t, 0.42, level=0.9) + hoof(t, 0.55, level=0.6)
    return hall(st(skid + stumble + grit(t, 0.05, 0.5, level=0.4)), 0.6, 0.15, 34)


def bull_dazed():
    t = times(1.5)
    x = np.zeros((len(t), 2))
    for k in range(6):
        f = [1568, 1760, 2093][k % 3] * (1 + 0.01 * np.sin(2 * np.pi * 5 * t))
        x += pan(0.3 * glass(t, f, k * 0.2, decay=0.4), np.sin(k * 1.9))
    return hall(fade(x, 0.3), 1.0, 0.3, 35)


def bull_death():
    t = times(2.6)
    bellow = sat(voice(t, 0.0, 1.1, 86, formants=(400, 780), level=1.1, bend=-0.45), 1.6) * np.exp(-np.maximum(t - 0.7, 0) / 0.2)
    rush = whoosh(t, 0.25, 1.4, 250, 2600, q=0.7, level=0.9) + roar(t, 0.3, 1.4, f=220, level=0.7) * np.exp(-np.maximum(t - 1.2, 0) / 0.4)
    embers = crackle(t, 0.4, 1.8, rate=70, level=0.5)
    boom = drum(t, 0.3, f0=45, level=1.1, decay=0.6) + sub(t, 0.3, 100, 28, level=1.0, decay=0.8)
    return hall(fade(st(bellow + rush + embers + boom), 0.6), 2.2, 0.3, 36)


# ---------------------------------------------------------------- Horse-Face

def horse_raise():
    t = times(0.6)
    rattle = sum(metal_link(t, 0.02 + k * 0.045 + rng.uniform(0, 0.02), level=0.5, low=0.6) for k in range(9))
    hum = bowl(t, 196, start=0.0, level=0.5, decay=1.2) * np.clip(t / 0.5, 0, 1)
    return hall(pan(rattle, 0.2) + st(hum), 0.9, 0.25, 40)


def horse_pulse():
    t = times(2.2)
    gong = bowl(t, 98, start=0.0, level=1.0, decay=1.6) + 0.6 * bell(t, fo.note(0, base=fo.D5) / 4, start=0.0)
    whoomp = sat(glide(t, 180, 50, 0.08) * env(t, 0.25, 0.004) * 1.2, 1.4) + filt(noise(len(t)), "lowpass", 900) * env(t, 0.18, 0.01) * 0.6
    shimmer = sum(pan(0.15 * glass(t, 2349 * 2 ** (k / 12), 0.04 + k * 0.03, decay=0.8), np.sin(k)) for k in range(5))
    return hall(fade(st(gong + whoomp) + shimmer, 0.6), 2.4, 0.35, 41)


def horse_death():
    t = times(2.6)
    whinny = voice(t, 0.0, 1.0, 420, formants=(900, 2100), level=0.8, bend=0.6) * (1 + 0.3 * np.sin(2 * np.pi * 11 * t))
    whinny += voice(t, 0.6, 0.6, 600, formants=(800, 1800), level=0.5, bend=-0.7)
    chain = sum(metal_link(t, 0.9 + k * 0.06 + rng.uniform(0, 0.03), level=0.55 - k * 0.03, low=0.5) for k in range(10))
    mist = whoosh(t, 0.3, 2.0, 1200, 300, q=0.6, level=0.6)
    return hall(fade(st(whinny + mist) + pan(chain, -0.2), 0.6), 2.4, 0.35, 42)


# ---------------------------------------------------------------- the statues

def statue_wake():
    t = times(2.4)
    k = np.clip(t / 0.7, 0, 1)
    cracks = np.zeros(len(t))
    s = 0.0
    while s < 0.7:
        cracks += stone(t, s, level=0.4 + 0.6 * s / 0.7)
        s += 0.12 * (1 - s / 0.8) + 0.02
    boom = drum(t, 0.72, f0=44, level=1.4, decay=0.6) + sub(t, 0.72, 120, 28, level=1.3, decay=0.8)
    burst = filt(noise(len(t)), "bandpass", [300, 4000]) * env(t, 0.12, 0.002, start=0.72) * 1.2
    rubble = grit(t, 0.78, 1.4, level=0.8)
    groan = voice(t, 0.3, 0.9, 70, formants=(380, 700), level=0.6 * k.max(), bend=0.25)
    return hall(fade(st(sat(cracks + boom + burst + groan, 1.2)) + pan(rubble, 0.15), 0.5), 1.8, 0.3, 50)


def statue_restore():
    t = times(1.6)
    rise = sum(pan(0.2 * glass(t, 1175 * 2 ** (k / 12), 0.9 - k * 0.12, decay=0.5), np.sin(k * 1.3)) for k in range(7))
    draw = st(whoosh(t, 0.0, 1.0, 3000, 500, q=1.2, level=0.5))
    settle = st(stone(t, 1.0, level=0.6) + drum(t, 1.0, f0=70, level=0.6, decay=0.2))
    return hall(fade(rise + draw + settle, 0.4), 1.6, 0.35, 51)


SOUNDS = {
    # the horde: under the fighting
    "hq_soul_death": (soul_death, -9.0), "hq_lily_rise": (lily_rise, -8.0), "hq_lily_charge": (lily_charge, -9.0),
    "hq_lily_fire": (lily_fire, -7.0), "hq_lily_death": (lily_death, -8.0), "hq_paper_dash": (paper_dash, -9.0),
    "hq_paper_death": (paper_death, -8.0), "hq_burner_ignite": (burner_ignite, -7.0), "hq_burner_throw": (burner_throw, -7.0),
    "hq_ember_land": (ember_land, -7.0), "hq_ember_burn": (ember_burn, -6.0), "hq_burner_death": (burner_death, -6.0),
    "hq_wisp": (wisp, -9.0), "hq_lantern_appear": (lantern_appear, -3.0), "hq_lantern_out": (lantern_out, -2.0),
    # the guardians: over it
    "hq_bull_snort": (bull_snort, -2.0), "hq_bull_charge": (bull_charge, 0.0), "hq_bull_trample": (bull_trample, -8.0),
    "hq_bull_gore": (bull_gore, -1.0), "hq_bull_crash": (bull_crash, 1.0), "hq_bull_skid": (bull_skid, -3.0),
    "hq_bull_dazed": (bull_dazed, -6.0), "hq_bull_death": (bull_death, 1.0),
    "hq_horse_raise": (horse_raise, -3.0), "hq_horse_pulse": (horse_pulse, -1.0), "hq_horse_death": (horse_death, 0.0),
    "hq_statue_wake": (statue_wake, 1.0), "hq_statue_restore": (statue_restore, -3.0),
}


def main():
    reel = []
    for name, (make, offset) in SOUNDS.items():
        x = level(make(), offset)
        write(os.path.join(DEST, name + ".wav"), x)
        print(f"{name:20s} {len(x) / SR:.2f}s, loudest 100ms {nz.loudest(x):.1f} dB")
        reel.append(x if x.ndim == 2 else np.stack([x, x], axis=1))
        reel.append(np.zeros((int(SR * 0.35), 2)))
    total = nz.limit(np.concatenate(reel) * 0.8, nz.CEILING)
    write(os.path.join(ct.OUT, "huangquan.wav"), total)


if __name__ == "__main__":
    main()
