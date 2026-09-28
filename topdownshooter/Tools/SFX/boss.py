"""The Jiangshi Magistrate's attacks (BossMagistrate), sound designed and synthesised from scratch (no
samples) with the Command Token's instruments and the fortune envelope's drums. it's a hopping
corpse in an official's robes with a talisman on its face, so: bronze and stone, paper, corpse fire,
thunder, and a dead man's voice.

  bm_seal     the leap's seal lighting up under the player, as long as the crouch and the flight
              (1.35 s, played faster with the torn seal): a low bronze bowl swelling, a heartbeat
              drum speeding up, a whoosh drawn in, cutting off right as it lands
  bm_leap     it springs: the robes' snap, a thud off the ground, air rushing up
  bm_slam     the landing: the big drum and a sub drop, stone cracking, grit falling, and the ring
              of corpse fire bursting out
  bm_storm    it plants itself and raises its arms (0.45 s), then the arms of corpse fire turn for
              2.6 s: a roaring flame, crackle, and a whoosh for every volley
  bm_raise    it casts and the seals glow round the player (1.3 s): a dead man's chant, a low bell,
              paper talismans fluttering, rising to the strike
  bm_strike   lightning into one seal and the dead clawing up out of the earth: a crack, a thunder
              roll, soil breaking (played once per spot, pitched a little apart)
  bm_tear     the talisman on its face tears at half health: paper ripping, a roar, fire bursting

python boss.py writes them into Assets/### Different Engine/Sounds/Boss/, each at the game's
reference loudness (normalize.py), plus out/boss_fight.wav: a few attacks in a row to listen to.
"""
import os

import numpy as np

import command_token as ct
from command_token import SR, times, env, glide, modal, noise, brown, filt, swept, sat, reverb, fade_out, bell
import fortune as fo
from fortune import pan, hall, fade, rustle, drum, sub, write, level
import normalize as nz

rng = np.random.default_rng(4412)
DEST = os.path.join(ct.HERE, "..", "..", "Assets", "### Different Engine", "Sounds", "Boss")


def whoosh(t, start, length, f0, f1, q=1.2, level=1.0):
    """air moving: noise through a band sweeping from f0 to f1, swelling and dying over `length`"""
    u = np.clip((t - start) / length, 0, 1)
    shape = np.sin(np.pi * u) ** 2 * ((t >= start) & (t < start + length))
    band = swept(noise(len(t)), lambda s: f0 + (f1 - f0) * np.clip((s - start) / length, 0, 1), q=q)
    return level * band * shape


def crackle(t, start, length, rate=40.0, level=1.0):
    """fire: sparse sharp pops over a hiss"""
    x = np.zeros_like(t)
    k = start
    while k < start + length:
        x += filt(noise(len(t)), "highpass", 2500) * env(t, rng.uniform(0.002, 0.006), 0.0002, start=k) * rng.uniform(0.3, 1.0)
        k += rng.exponential(1 / rate)
    return level * x


def roar(t, start, length, f=240.0, level=1.0):
    """a flame's body: brown noise in the low mids, breathing"""
    u = t - start
    live = (u >= 0) & (u < length)
    breath = 0.7 + 0.3 * np.sin(2 * np.pi * 3.1 * t + 1.0) * np.sin(2 * np.pi * 1.3 * t)
    body = filt(brown(len(t)), "bandpass", [f * 0.4, f * 3]) * 3
    return level * body * breath * live


def voice(t, start, length, f0, formants=(520, 900), level=1.0, bend=0.0):
    """a dead man's voice: a buzzing glottal saw through two formants, a slow wobble"""
    u = np.clip((t - start) / length, 0, 1)
    f = f0 * (1 + bend * u) * (1 + 0.012 * np.sin(2 * np.pi * 4.6 * t))
    saw = 2 * ((np.cumsum(f) / SR) % 1) - 1
    v = swept(saw, lambda s: formants[0], q=5) + 0.6 * swept(saw, lambda s: formants[1], q=6)
    shape = np.sin(np.pi * u) ** 0.7 * ((t >= start) & (t < start + length))
    return level * v * shape


def bowl(t, f0, start=0.0, swell=0.0, level=1.0, decay=2.5):
    """a large bronze bowl, bowed up (swell) or struck"""
    ratios = np.array([1, 2.71, 5.1, 8.2])
    u = np.maximum(t - start, 0)
    x = np.zeros_like(t)
    for r_, a, d in zip(ratios, [1, 0.45, 0.2, 0.08], [decay, decay * 0.55, decay * 0.3, decay * 0.18]):
        x += a * np.sin(2 * np.pi * f0 * r_ * u + rng.uniform(0, 6.28)) * np.exp(-u / d)
        x += a * 0.6 * np.sin(2 * np.pi * (f0 * r_ + 1.3) * u + rng.uniform(0, 6.28)) * np.exp(-u / d)
    rise = np.clip(u / swell, 0, 1) ** 2 if swell > 0 else np.clip(u / 0.002, 0, 1)
    return level * x * rise * (t >= start)


def stone(t, at, level=1.0):
    """stone cracking: a hard snap and a few low knocks"""
    x = filt(noise(len(t)), "bandpass", [700, 5000]) * env(t, 0.012, 0.0002, start=at)
    for k in range(5):
        s = at + 0.01 + k * rng.uniform(0.015, 0.04)
        x += modal(np.maximum(t - s, 0), rng.uniform(300, 900) * np.array([1, 1.63, 2.4]), [0.04, 0.025, 0.015], [0.6, 0.35, 0.2]) * (t >= s) * rng.uniform(0.3, 0.7)
    return level * x


def grit(t, start, length, level=1.0):
    """soil and gravel falling: many tiny ticks thinning out"""
    x = np.zeros_like(t)
    k = start
    while k < start + length:
        thin = 1 - (k - start) / length
        x += filt(noise(len(t)), "bandpass", [1500, 6000]) * env(t, 0.003, 0.0003, start=k) * rng.uniform(0.2, 1.0) * thin
        k += rng.exponential(1 / (30 + 90 * thin))
    return level * x


def thunder(t, at, length=1.4, level=1.0):
    """a thunder roll: brown noise rumbling in bursts, darkening"""
    u = t - at
    live = u >= 0
    bursts = np.zeros_like(t)
    k = at
    while k < at + length:
        bursts += env(t, rng.uniform(0.08, 0.25), 0.02, start=k) * rng.uniform(0.4, 1.0) * np.exp(-(k - at) / (length * 0.5))
        k += rng.uniform(0.06, 0.2)
    return level * filt(brown(len(t)), "lowpass", 380, order=3) * 4 * bursts * live


# ---------------------------------------------------------------- the leap

def seal():
    """1.35 s: the crouch (0.8) and the flight (0.55). it builds to the landing and stops dead"""
    T = 1.35
    t = times(T + 0.05)
    x = np.zeros((len(t), 2))
    # the seal flaring on the ground: a paper flare and a small bell, far off
    x += pan(0.5 * rustle(t, 0.0, 0.12, rate=70) + 0.18 * bell(t, fo.note(2, 293.66), start=0.0), 0)
    # a low bronze bowl bowed up under everything
    x += pan(0.55 * bowl(t, 73.4, 0.0, swell=1.2, decay=3.0), -0.1)
    x += pan(0.25 * bowl(t, 110.0, 0.1, swell=1.1, decay=3.0), 0.15)
    # a heartbeat drum speeding up into the landing
    at, gap = 0.05, 0.36
    while at < T - 0.08:
        x += pan(0.55 * drum(t, at, 58, decay=0.16) + 0.3 * drum(t, at + 0.09, 52, decay=0.12), 0)
        at += gap
        gap = max(0.09, gap * 0.8)
    # the air drawn in behind it: a whoosh climbing in pitch through the flight
    x += pan(0.6 * whoosh(t, 0.55, 0.8, 250, 1800, q=1.4), 0)
    x += pan(0.35 * filt(brown(len(t)), "lowpass", 90, order=4) * np.clip(t / T, 0, 1) ** 2, 0)
    x = hall(x, 1.2, 0.25, seed=41, tone=4500)
    # cut dead on the landing (a very short fade so it doesn't click)
    cut = int(T * SR)
    x[cut:] = 0
    x[cut - 200:cut] *= np.linspace(1, 0, 200)[:, None]
    return x


def leap():
    t = times(0.7)
    x = np.zeros((len(t), 2))
    x += pan(0.9 * drum(t, 0.0, 70, decay=0.12), 0)                         # pushing off the ground
    x += pan(0.45 * filt(noise(len(t)), "bandpass", [600, 3000]) * env(t, 0.03, 0.001, start=0.005), 0)   # the robes snapping
    x += pan(0.8 * whoosh(t, 0.0, 0.5, 400, 2600, q=1.1), 0.1)             # air rushing up
    x += pan(0.2 * grit(t, 0.02, 0.25), -0.2)
    return fade(hall(x, 0.8, 0.2, seed=43), 0.2)


def slam():
    t = times(2.0)
    x = np.zeros((len(t), 2))
    x += pan(1.1 * drum(t, 0.0, 46, decay=0.55), 0)
    x += pan(0.6 * sub(t, 0.0, 110, 34, decay=0.5), 0)
    x += pan(0.9 * stone(t, 0.004), -0.25) + pan(0.7 * stone(t, 0.02), 0.3)
    x += pan(0.6 * filt(noise(len(t)), "bandpass", [300, 1500]) * env(t, 0.08, 0.001), 0)   # the crunch of it
    x += pan(0.5 * filt(noise(len(t)), "lowpass", 2500) * env(t, 0.06, 0.001), 0)     # the blast of air
    x += pan(0.35 * grit(t, 0.08, 1.1), -0.5) + pan(0.35 * grit(t, 0.1, 1.1), 0.5)
    # the ring of corpse fire bursting out: a whoosh opening out to both sides, crackle, a roar
    x += pan(0.9 * whoosh(t, 0.02, 0.5, 2200, 500, q=0.9), -0.6) + pan(0.9 * whoosh(t, 0.03, 0.5, 2000, 450, q=0.9), 0.6)
    x += pan(0.35 * roar(t, 0.02, 0.7, f=200) * env(t, 0.35, 0.02, start=0.02), 0)
    x += pan(0.4 * crackle(t, 0.05, 0.8, rate=50), -0.3) + pan(0.4 * crackle(t, 0.05, 0.8, rate=50), 0.3)
    return fade(hall(x, 1.8, 0.3, seed=47, tone=4000), 0.6)


# ---------------------------------------------------------------- the storm

def storm():
    """0.45 s of it raising its arms, then 2.6 s of turning arms of fire, shots 6 a second"""
    cast, T = 0.45, 2.6
    t = times(cast + T + 0.5)
    x = np.zeros((len(t), 2))
    # raising its arms: a voice rising, the bowl struck, the fire catching
    x += pan(0.3 * voice(t, 0.0, cast + 0.3, 82, (560, 950), bend=0.35), 0)
    x += pan(0.5 * bowl(t, 98.0, 0.0, decay=1.8) + 0.3 * drum(t, cast - 0.02, 60, decay=0.3), 0)
    x += pan(0.6 * whoosh(t, cast - 0.2, 0.35, 300, 2400, q=1.0), 0)
    # the flame roaring the whole time, swept round the field as the arms turn
    for i, p in enumerate([-0.6, 0.6]):
        body = roar(t, cast, T, f=210 + 40 * i)
        spin = 0.6 + 0.4 * np.sin(2 * np.pi * 0.6 * t + i * np.pi)
        x += pan(0.45 * body * spin, p)
    x += pan(0.35 * crackle(t, cast, T, rate=45), -0.4) + pan(0.35 * crackle(t, cast, T, rate=45), 0.4)
    # a whoosh for every volley, panned round
    k, n = cast, 0
    while k < cast + T:
        x += pan(0.3 * whoosh(t, k, 0.16, 1600, 700, q=1.6), np.sin(n * 1.3))
        k += 1 / 6
        n += 1
    # it dies back into embers
    tail = np.clip(1 - (t - (cast + T)) / 0.4, 0, 1)
    x *= tail[:, None]
    x += pan(0.25 * crackle(t, cast + T, 0.4, rate=20), 0)
    return fade(hall(x, 1.4, 0.25, seed=53, tone=4500), 0.3)


# ---------------------------------------------------------------- raising the dead

def raise_cast():
    """1.3 s: the cast (0.4) and the seals glowing (0.9), rising to the strikes"""
    T = 1.3
    t = times(T + 0.1)
    x = np.zeros((len(t), 2))
    # the chant: two dead voices a fifth apart, sinking, and a low bell
    x += pan(0.55 * voice(t, 0.0, T, 98, (480, 820), bend=-0.08), -0.3)
    x += pan(0.42 * voice(t, 0.05, T - 0.05, 147, (620, 1050), bend=-0.08), 0.3)
    x += pan(0.45 * bowl(t, 65.4, 0.0, decay=2.2), 0)
    # talismans fluttering round the player
    for i in range(4):
        x += pan(0.35 * rustle(t, 0.4 + i * 0.1, 0.25, rate=30), -0.8 + 0.53 * i)
    # the seals charging: a static hiss swelling, a sub rising
    x += pan(0.3 * filt(noise(len(t)), "bandpass", [3000, 9000]) * np.clip((t - 0.4) / 0.9, 0, 1) ** 3, 0)
    x += pan(0.35 * np.sin(2 * np.pi * np.cumsum(40 + 30 * np.clip(t / T, 0, 1)) / SR) * np.clip(t / T, 0, 1) ** 2, 0)
    x = hall(x, 1.5, 0.3, seed=59, tone=4000)
    cut = int(T * SR)
    x[cut:] *= np.linspace(1, 0, len(x) - cut)[:, None] ** 2
    return x


def strike():
    t = times(1.8)
    x = np.zeros((len(t), 2))
    # the crack: a bright snap and a ripping sizzle
    x += pan(1.2 * filt(noise(len(t)), "highpass", 1800) * env(t, 0.008, 0.0002), 0)
    x += pan(0.7 * filt(noise(len(t)), "bandpass", [800, 3000]) * env(t, 0.03, 0.0003), 0)
    x += pan(0.9 * swept(noise(len(t)), lambda s: 7000 * np.exp(-s / 0.05) + 1200, q=2.5) * env(t, 0.08, 0.001), 0.1)
    x += pan(0.5 * sub(t, 0.0, 140, 40, decay=0.25), 0)
    # the thunder rolling after it
    x += pan(0.45 * thunder(t, 0.05, 1.3), -0.3) + pan(0.4 * thunder(t, 0.08, 1.3), 0.3)
    # the earth breaking and the dead clawing out
    x += pan(0.5 * stone(t, 0.06), 0) + pan(0.35 * grit(t, 0.1, 0.6), 0)
    return fade(hall(x, 1.4, 0.2, seed=61, tone=3500), 0.5)


# ---------------------------------------------------------------- the torn seal

def tear():
    t = times(2.2)
    x = np.zeros((len(t), 2))
    # the talisman ripping: a long tear in bursts
    rip = np.zeros_like(t)
    k = 0.0
    while k < 0.35:
        rip += env(t, rng.uniform(0.01, 0.03), 0.001, start=k) * rng.uniform(0.5, 1.0)
        k += rng.uniform(0.012, 0.03)
    x += pan(0.8 * filt(noise(len(t)), "bandpass", [1500, 7000]) * rip, 0)
    # the roar: the dead voice opened up, low and growling
    growl = voice(t, 0.2, 1.3, 62, (420, 760), bend=-0.2) * (1 + 0.6 * np.sin(2 * np.pi * 28 * t))
    x += pan(0.6 * sat(growl * 1.5, 2.0), 0)
    x += pan(0.3 * voice(t, 0.22, 1.2, 93, (600, 1000), bend=-0.2), 0.2)
    # the fire bursting out of it
    x += pan(0.9 * drum(t, 0.2, 50, decay=0.5) + 0.7 * sub(t, 0.2, 100, 32, decay=0.5), 0)
    x += pan(0.6 * whoosh(t, 0.2, 0.6, 2200, 400, q=0.9), -0.5) + pan(0.6 * whoosh(t, 0.21, 0.6, 2000, 400, q=0.9), 0.5)
    x += pan(0.35 * crackle(t, 0.22, 1.0, rate=50), 0)
    return fade(hall(x, 2.0, 0.3, seed=67, tone=4000), 0.6)


# ---------------------------------------------------------------- write

def main():
    clips = {
        "bm_seal": level(seal(), -3.0),
        "bm_leap": level(leap(), -3.0),
        "bm_slam": level(slam(), 1.5),
        "bm_storm": level(storm(), -2.0),
        "bm_raise": level(raise_cast(), -3.0),
        "bm_strike": level(strike(), -1.5),
        "bm_tear": level(tear(), 1.0),
    }
    for name, x in clips.items():
        write(os.path.join(DEST, name + ".wav"), x)
        print(f"{name}: {len(x) / SR:.2f}s, loudest 100ms {nz.loudest(x):.1f} dB")

    # a leap, a storm, a raise of four and the seal tearing, as the fight plays them
    timeline = [("bm_seal", 0.0, 1.0), ("bm_leap", 0.8, 1.0), ("bm_slam", 1.35, 1.0),
                ("bm_storm", 4.0, 1.0), ("bm_raise", 8.0, 1.0)]
    for i in range(4): timeline.append(("bm_strike", 9.3 + i * 0.07, 1 + (i - 1.5) * 0.04))
    timeline.append(("bm_tear", 12.0, 1.0))
    total = np.zeros((int(15 * SR), 2))
    for name, at, pitch in timeline:
        x = clips[name]
        if pitch != 1.0:
            n = int(len(x) / pitch)
            x = np.stack([np.interp(np.arange(n) * pitch, np.arange(len(x)), x[:, c]) for c in range(2)], axis=1)
        s = int(at * SR)
        total[s:s + len(x)] += x[:len(total) - s] * (0.6 if name == "bm_strike" else 1.0)
    total = nz.limit(total * 0.7, nz.CEILING)
    write(os.path.join(ct.OUT, "boss_fight.wav"), total)
    print("out/boss_fight.wav: a few attacks in a row")


if __name__ == "__main__":
    main()
