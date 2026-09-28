"""The Treasure Gourd (TreasureGourd) and its evolution, the Gourd of Heaven and Earth, sound designed
and synthesised from scratch (no samples) with the Command Token's, the fortune envelope's and the
boss's instruments. a gourd is a hollow bottle: it pops like a cork, it hums like a bottle blown
across, and what it swallows are spirits, so they wail on the way in. its fire is holy, so the
roar carries a shimmer of small bells; evolved, what it gives back is plasma, crackling and
humming, and it bursts with a gong under it (heaven and earth).

  tg_uncork   the cork popping: a hollow knock, the bottle's note, air rushing out, a small bell
  tg_pull     the pull (1.5 s, the Suction Seconds): an inhale swelling like a vacuum, a whirl
              turning round the field, the bottle's hum climbing, spirits wailing as they're dragged in
  tg_flame    the holy fire (0.9 s): a whoomp as it catches, a roaring gout with crackle and hiss,
              a shimmer of bells over it, dying away
  tg_charge   evolved, the pull with the sphere gathering (2 s): the same inhale, an electric hum
              climbing under it, arcs crackling more and more, a bowl bowed up
  tg_absorb   a bullet swallowed: a zip down into the mouth, a gulp, a glint
  tg_launch   the sphere thrown: a thump, a plasma zap sweeping down, an electric whoosh
  tg_blast    the sphere bursting: a heavy boom and sub, an electric crack, arcs spitting, a gong
              and a hall

python gourd.py writes them into Assets/### Different Engine/Sounds/Gourd/, each at the game's
reference loudness (normalize.py), plus out/gourd_cast.wav (a cast, then an evolved one) to listen
to outside Unity.
"""
import os

import numpy as np

import command_token as ct
from command_token import SR, times, env, glide, modal, noise, brown, filt, swept, sat, reverb, fade_out, bell
import fortune as fo
from fortune import pan, hall, fade, drum, sub, write, level
import boss as bo
from boss import whoosh, crackle, roar, voice, bowl, thunder
import normalize as nz

rng = np.random.default_rng(3301)
DEST = os.path.join(ct.HERE, "..", "..", "Assets", "### Different Engine", "Sounds", "Gourd")


def bottle(t, start, length, f0, f1, level=1.0, breath=0.5):
    """a bottle blown across: a sine at the cavity's note gliding f0 to f1, a breathy band of noise
    round it, swelling in and out"""
    u = np.clip((t - start) / length, 0, 1)
    f = f0 + (f1 - f0) * u ** 1.5
    tone = np.sin(2 * np.pi * np.cumsum(f) / SR)
    air = swept(noise(len(t)), lambda s: f0 + (f1 - f0) * np.clip((s - start) / length, 0, 1) ** 1.5, q=6)
    shape = np.sin(np.pi * u) ** 0.8 * ((t >= start) & (t < start + length))
    return level * (tone * 0.7 + air * breath * 3) * shape


def zap(t, at, f0, f1, tau, level=1.0, decay=0.15):
    """an electric zap: a buzzy saw gliding from f0 to f1, through a band that follows it"""
    u = np.maximum(t - at, 0)
    f = f1 + (f0 - f1) * np.exp(-u / tau)
    saw = 2 * ((np.cumsum(f) / SR) % 1) - 1
    return level * sat(saw * 0.8, 2.0) * env(t, decay, 0.002, start=at)


def arcs(t, start, length, rate0, rate1, level=1.0):
    """electric arcs spitting, from rate0 to rate1 a second: short bursts of bright buzzing noise"""
    x = np.zeros_like(t)
    k = start
    while k < start + length:
        u = (k - start) / length
        n = int(rng.uniform(0.004, 0.018) * SR)
        s = int(k * SR)
        if s + n >= len(t): break
        burst = filt(noise(n), "bandpass", [2500, 9000]) * np.sign(np.sin(np.arange(n) * 2 * np.pi * rng.uniform(80, 240) / SR))
        x[s:s + n] += burst * np.hanning(n) * rng.uniform(0.4, 1.0) * (0.4 + 0.6 * u)
        k += rng.exponential(1 / (rate0 + (rate1 - rate0) * u))
    return level * x


def wails(t, start, length, n, level=1.0):
    """spirits dragged in: thin ghostly voices sliding down in pitch as they're pulled, each passing
    across the field and into the mouth"""
    x = np.zeros((len(t), 2))
    for i in range(n):
        s = start + rng.uniform(0, length * 0.6)
        ln = rng.uniform(0.35, 0.6)
        f0 = rng.uniform(420, 700)
        v = voice(t, s, ln, f0, (rng.uniform(700, 900), rng.uniform(1300, 1700)), bend=-0.45)
        v = v * (0.5 + 0.5 * np.sin(2 * np.pi * rng.uniform(5, 7) * t))
        x += pan(v, rng.uniform(-0.8, 0.8) * (1 - 0.6 * np.clip((t - s) / ln, 0, 1)))
    return level * x


def inhale(t, length, level=1.0):
    """the pull's body: noise swept up like air being sucked through a neck, getting stronger, with a
    whirl turning round the field (a slow tremolo panned in a circle)"""
    u = np.clip(t / length, 0, 1)
    band = swept(noise(len(t)), lambda s: 300 + 2400 * np.clip(s / length, 0, 1) ** 1.4, q=1.2)
    swell = np.clip(t / 0.25, 0, 1) * (0.55 + 0.45 * u) * (t < length)
    whirl = 2 * np.pi * (5.5 + 3 * u) * t
    left = band * swell * (0.6 + 0.4 * np.sin(whirl))
    right = band * swell * (0.6 + 0.4 * np.sin(whirl + np.pi))
    low = filt(brown(len(t)), "lowpass", 180, order=3) * swell * 1.2
    return level * np.stack([left + low, right + low], axis=1)


# ---------------------------------------------------------------- the gourd

def uncork():
    t = times(0.7)
    x = np.zeros((len(t), 2))
    # the pop: a sharp click and a hollow wooden knock
    x += pan(0.9 * filt(noise(len(t)), "highpass", 2500) * env(t, 0.004, 0.0002), 0)
    x += pan(0.9 * modal(t, np.array([340, 810, 1460]), [0.05, 0.03, 0.02], [1, 0.6, 0.35]), 0)
    # the bottle's own note ringing as the cork leaves it, dropping
    x += pan(0.7 * glide(t, 620, 410, 0.03) * env(t, 0.09, 0.001), 0)
    # the air inside rushing out, and a breath of spirit with it
    x += pan(0.45 * filt(noise(len(t)), "bandpass", [1500, 6000]) * env(t, 0.06, 0.004, start=0.01), 0.1)
    x += pan(0.25 * whoosh(t, 0.02, 0.35, 1800, 500, q=1.4), -0.2)
    # a small bell: it's a treasure
    x += pan(0.22 * bell(t, fo.note(7), start=0.015), 0.2)
    return fade(hall(x, 0.8, 0.18, seed=71), 0.25)


def pull():
    T = 1.5
    t = times(T + 0.15)
    x = inhale(t, T, 0.55)
    x += pan(0.45 * bottle(t, 0.05, T, 150, 260, breath=0.35), 0)          # the bottle's hum climbing
    x += wails(t, 0.15, T - 0.3, 5, 0.18)
    # motes rushing past into the mouth: small whooshes panned in toward the middle
    for i in range(7):
        s = rng.uniform(0.2, T - 0.3)
        x += pan(0.25 * whoosh(t, s, 0.22, 2600, 900, q=2.0), rng.choice([-1, 1]) * rng.uniform(0.3, 0.8))
    # the swallow at the end: a gulp into the bottle
    x += pan(0.5 * glide(np.maximum(t - (T - 0.08), 0), 300, 110, 0.03) * env(t, 0.08, 0.004, start=T - 0.08), 0)
    x = hall(x, 1.0, 0.2, seed=73, tone=5000)
    return fade(x, 0.15)


def flame():
    t = times(1.1)
    x = np.zeros((len(t), 2))
    # the whoomp as it catches: air thrown out and lit, a low thump under it
    x += pan(1.0 * drum(t, 0.0, 70, decay=0.2) + 0.6 * sub(t, 0.0, 120, 45, decay=0.2), 0)
    x += pan(0.8 * whoosh(t, 0.0, 0.25, 500, 2600, q=0.8), 0)
    # the gout: a roaring body, crackle and a hot hiss, sustained, then dying back
    body = roar(t, 0.03, 0.85, f=260) * np.clip((0.85 - t) / 0.25, 0, 1)
    x += pan(0.6 * body, -0.3) + pan(0.6 * roar(t, 0.04, 0.83, f=300) * np.clip((0.85 - t) / 0.25, 0, 1), 0.3)
    hiss = filt(noise(len(t)), "bandpass", [3000, 9000]) * np.clip(t / 0.05, 0, 1) * np.clip((0.8 - t) / 0.3, 0, 1)
    x += pan(0.25 * hiss, 0)
    x += pan(0.4 * crackle(t, 0.05, 0.75, rate=55), -0.4) + pan(0.4 * crackle(t, 0.05, 0.75, rate=55), 0.4)
    # holy: a shimmer of small bells over the ignition (the level up's scale, so it never clashes)
    for i, at in enumerate([0.01, 0.04, 0.08]):
        x += pan(0.18 * bell(t, fo.note(9 + i * 2), start=at), [-0.4, 0.4, 0][i])
    return fade(hall(x, 1.0, 0.22, seed=79, tone=5500), 0.25)


# ---------------------------------------------------------------- the evolution

def charge():
    T = 2.0
    t = times(T + 0.15)
    x = inhale(t, T, 0.45)
    x += pan(0.3 * bottle(t, 0.05, T, 140, 230, breath=0.3), 0)
    x += wails(t, 0.2, T - 0.4, 6, 0.14)
    # the plasma gathering: an electric hum climbing, two saws beating, through a closing lowpass
    u = np.clip(t / T, 0, 1)
    f = 55 + 55 * u ** 1.3
    hum = sum(2 * ((np.cumsum(f * d) / SR) % 1) - 1 for d in (1.0, 1.007))
    hum = swept(hum, lambda s: 300 + 2200 * np.clip(s / T, 0, 1) ** 2, kind="low", q=0.9)
    x += pan(0.35 * sat(hum * u ** 1.2, 1.5) * (t < T), 0)
    x += pan(0.35 * bowl(t, 98.0, 0.1, swell=1.8, decay=3.0), 0)
    x += pan(0.5 * arcs(t, 0.3, T - 0.35, 3, 26), -0.3) + pan(0.5 * arcs(t, 0.35, T - 0.4, 3, 26), 0.3)
    x = hall(x, 1.1, 0.2, seed=83, tone=5000)
    # cut on the launch, a hair of fade so it doesn't click
    cut = int(T * SR)
    x[cut:] = 0
    x[cut - 300:cut] *= np.linspace(1, 0, 300)[:, None]
    return x


def absorb():
    t = times(0.4)
    x = np.zeros((len(t), 2))
    # zipped in: a buzzy glide down, a gulp, a glint
    x += pan(0.6 * zap(t, 0.0, 1900, 380, 0.025, decay=0.06), 0)
    x += pan(0.6 * glide(np.maximum(t - 0.05, 0), 260, 120, 0.02) * env(t, 0.06, 0.003, start=0.05), 0)
    x += pan(0.25 * bell(t, fo.note(12), start=0.06), 0.2)
    return fade(hall(x, 0.6, 0.15, seed=89), 0.15)


def launch():
    t = times(1.0)
    x = np.zeros((len(t), 2))
    x += pan(0.9 * drum(t, 0.0, 60, decay=0.25) + 0.7 * sub(t, 0.0, 140, 40, decay=0.3), 0)
    x += pan(0.6 * zap(t, 0.0, 1400, 90, 0.08, decay=0.3), 0)
    x += pan(0.7 * whoosh(t, 0.0, 0.6, 2200, 300, q=1.0), 0.2)
    x += pan(0.4 * arcs(t, 0.0, 0.5, 30, 6), 0)
    return fade(hall(x, 1.0, 0.2, seed=97, tone=5000), 0.3)


def blast():
    t = times(2.6)
    x = np.zeros((len(t), 2))
    # the boom
    x += pan(1.1 * drum(t, 0.0, 44, decay=0.6) + 0.9 * sub(t, 0.0, 120, 30, decay=0.6), 0)
    # the electric crack and the plasma tearing out
    x += pan(1.0 * filt(noise(len(t)), "highpass", 1500) * env(t, 0.01, 0.0002), 0)
    x += pan(0.6 * zap(t, 0.005, 2400, 160, 0.05, decay=0.35), -0.2) + pan(0.5 * zap(t, 0.01, 2000, 140, 0.06, decay=0.3), 0.2)
    x += pan(0.6 * whoosh(t, 0.0, 0.7, 2600, 350, q=0.8), -0.5) + pan(0.6 * whoosh(t, 0.01, 0.7, 2400, 350, q=0.8), 0.5)
    # arcs spitting off as it spreads, thinning out
    x += pan(0.45 * arcs(t, 0.05, 1.2, 40, 4), -0.4) + pan(0.45 * arcs(t, 0.06, 1.2, 40, 4), 0.4)
    x += pan(0.35 * thunder(t, 0.1, 1.2), 0)
    # heaven and earth: a gong under it, low, and a small bell far over it
    gong = modal(np.maximum(t - 0.02, 0), 110 * np.array([1, 1.48, 2.02, 2.76, 3.52]), [2.2, 1.6, 1.1, 0.7, 0.45], [1, 0.6, 0.45, 0.3, 0.2]) * (t >= 0.02)
    x += pan(0.45 * gong, 0)
    x += pan(0.2 * bell(t, fo.note(10), start=0.05), 0.3)
    return fade(hall(x, 2.2, 0.3, seed=101, tone=4500), 0.7)


# ---------------------------------------------------------------- write

def main():
    clips = {
        "tg_uncork": level(uncork(), -2.0),
        "tg_pull": level(pull(), -4.0),
        "tg_flame": level(flame(), -1.0),
        "tg_charge": level(charge(), -3.5),
        "tg_absorb": level(absorb(), -4.0),
        "tg_launch": level(launch(), -1.0),
        "tg_blast": level(blast(), 1.0),
    }
    for name, x in clips.items():
        write(os.path.join(DEST, name + ".wav"), x)
        print(f"{name}: {len(x) / SR:.2f}s, loudest 100ms {nz.loudest(x):.1f} dB")

    # a cast (uncork, pull, fire), then an evolved one (uncork, charge with three bullets
    # swallowed, launch, burst), as the game plays them
    timeline = [("tg_uncork", 0.0), ("tg_pull", 0.0), ("tg_flame", 1.5),
                ("tg_uncork", 3.5), ("tg_charge", 3.5), ("tg_absorb", 4.2), ("tg_absorb", 4.7), ("tg_absorb", 5.1),
                ("tg_launch", 5.5), ("tg_blast", 6.0)]
    total = np.zeros((int(9 * SR), 2))
    for name, at in timeline:
        x = clips[name]
        s = int(at * SR)
        total[s:s + len(x)] += x[:len(total) - s]
    total = nz.limit(total * 0.7, nz.CEILING)
    write(os.path.join(ct.OUT, "gourd_cast.wav"), total)
    print("out/gourd_cast.wav: a cast, then an evolved one")


if __name__ == "__main__":
    main()
