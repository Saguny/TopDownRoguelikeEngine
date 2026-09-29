"""Meng Po, the Lady of Forgetting (MengPoBoss), sound designed and synthesised from scratch (no samples)
with the Command Token's, the envelope's and the Magistrate's instruments. her world is water and
mist and a temple bell far off, porcelain and soup, and the forgetting: a shimmer drawn backwards,
detuned, smeared, like a memory slipping away.

  mp_bell              a great temple bell tolling over water, far off, its hum hanging
  mp_river             the Wangchuan flooding in: a deep rush of water rising and settling
  mp_bridge            Naihe Bridge rising out of the fog: stone grinding up, a boom, water off it
  mp_ladle             the ladle dipped in the soup: a thick liquid scoop, a bubble
  mp_pour              soup poured into a bowl: a trickle, the bowl ringing
  mp_fling             a ladleful or a bowl flung: a swish and a splash
  mp_bowl              a bowl breaking: porcelain shattering, soup spattering
  mp_glide             her drifting through the mist: an airy breath, a soft chime
  mp_forget            the forgetting: a reversed shimmer rushing in, detuned bells smearing,
                       a hollow hush after
  mp_current           the river's current turning: a surge of water sweeping past
  mp_title             her name: a gong, a low choir of breath, a bell over it
  mp_declare           a card declared: a bowl bell struck, a rising shimmer
  mp_transform_gather  her old form straining (1.35 s): a swelling drone, cracking, a heartbeat
  mp_transform         it breaks off her: a shatter of light, a great gong, a choir opening
  mp_death_bowl        her bowl breaking in her hands at the end, slowed
  mp_death             her end: a lotus of light opening, bells cascading, a long exhale of mist

python mengpo.py writes them into Assets/### Different Engine/Resources/Sfx/, each at the game's
reference loudness (normalize.py), plus out/mengpo.wav to listen to them one after another.
"""
import os

import numpy as np

import command_token as ct
from command_token import SR, times, env, glide, noise, brown, filt, swept, sat, bell
import fortune as fo
from fortune import pan, hall, fade, drum, sub, write, level
from boss import bowl, whoosh, stone, grit, voice
from huangquan import glass, sigh
import normalize as nz

rng = np.random.default_rng(8810)
DEST = os.path.join(ct.HERE, "..", "..", "Assets", "### Different Engine", "Resources", "Sfx")


def st(x, p=0.0):
    return pan(x, p)


def water(t, start, length, f0=500.0, f1=900.0, level=1.0):
    """moving water: noise in a wandering band, bubbling"""
    u = np.clip((t - start) / length, 0, 1)
    shape = np.sin(np.pi * u) ** 0.8 * ((t >= start) & (t < start + length))
    band = swept(noise(len(t)), lambda s: f0 + (f1 - f0) * (0.5 + 0.5 * np.sin(2 * np.pi * 0.7 * s)), q=0.9)
    gurgle = swept(noise(len(t)), lambda s: 300 + 250 * np.sin(2 * np.pi * 5.3 * s) ** 2, q=4) * 0.5
    return level * (band + gurgle) * shape


def bubble(t, at, f=500.0, level=1.0):
    """a bubble popping: a quick rising blip"""
    u = np.maximum(t - at, 0)
    return level * np.sin(2 * np.pi * (f * u + 2500 * u * u)) * np.exp(-u / 0.03) * (t >= at)


def temple_bell(t, f0, start=0.0, level=1.0, decay=4.0):
    """a great bronze temple bell: a low hum and a strike tone, long, beating"""
    return level * (bowl(t, f0, start=start, level=1.0, decay=decay) + 0.35 * bell(t, f0 * 4.2, start=start))


def shimmer(t, start, length, f0=1400.0, level=1.0, reverse=False, detune=0.0):
    """a cluster of glassy partials; reversed, it swells in to its start instead of ringing out"""
    x = np.zeros(len(t))
    for k in range(7):
        f = f0 * 2 ** (rng.integers(0, 12) / 12) * (1 + detune * np.sin(2 * np.pi * (0.7 + k * 0.3) * t))
        x += np.sin(2 * np.pi * np.cumsum(f) / SR + rng.uniform(0, 6.28)) * 0.14
    u = np.clip((t - start) / length, 0, 1)
    shape = (u ** 2.2 if reverse else (1 - u) ** 2.2) * ((t >= start) & (t < start + length))
    return level * x * shape


def choir(t, start, length, notes=(146.8, 220.0, 293.7), level=1.0):
    """a low choir of breath: voices on a chord, soft formants, a slow swell"""
    x = np.zeros(len(t))
    for i, f in enumerate(notes):
        x += voice(t, start + i * 0.05, length, f, formants=(500 + i * 90, 1000 + i * 150), level=0.4)
    u = np.clip((t - start) / length, 0, 1)
    return level * x * np.sin(np.pi * u) ** 0.8


def mp_bell():
    t = times(4.5)
    x = st(temple_bell(t, 98, level=1.0, decay=3.5)) + st(water(t, 0.0, 4.5, 250, 500, level=0.15), -0.2)
    return hall(fade(x, 1.0), 3.5, 0.45, 60)


def mp_river():
    t = times(3.0)
    rise = np.clip(t / 1.2, 0, 1) ** 1.5
    x = water(t, 0.0, 3.0, 300, 1200, level=1.0) * rise + filt(brown(len(t)), "lowpass", 180) * rise * 1.5 * np.exp(-np.maximum(t - 1.6, 0) / 0.8)
    return hall(fade(st(x, 0) + st(water(t, 0.2, 2.6, 500, 1500, level=0.4), 0.4), 0.8), 1.5, 0.25, 61)


def mp_bridge():
    t = times(3.0)
    grind = filt(brown(len(t)), "bandpass", [60, 500]) * np.clip(t / 0.8, 0, 1) * np.exp(-np.maximum(t - 1.1, 0) / 0.3) * 2.0
    crackles = sum(stone(t, 0.1 + k * 0.12, level=0.3 + 0.05 * k) for k in range(8))
    boom = drum(t, 1.15, f0=46, level=1.2, decay=0.6) + sub(t, 1.15, 110, 30, level=1.0, decay=0.7)
    drip = water(t, 1.2, 1.6, 600, 1400, level=0.35) + grit(t, 1.2, 1.2, level=0.3)
    return hall(fade(st(sat(grind + crackles + boom, 1.2)) + st(drip, 0.2), 0.6), 2.0, 0.3, 62)


def mp_ladle():
    t = times(0.6)
    scoop = swept(noise(len(t)), lambda s: 350 + 500 * np.clip(s / 0.2, 0, 1), q=2.5) * env(t, 0.22, 0.02) * 0.9
    return hall(st(scoop + bubble(t, 0.18, 420, 0.4) + bubble(t, 0.3, 560, 0.3)), 0.6, 0.2, 63)


def mp_pour():
    t = times(1.4)
    trickle = water(t, 0.0, 1.0, 1200, 2600, level=0.6)
    ring = 0.4 * glass(t, 1480, 0.1, decay=0.9) + 0.25 * glass(t, 2217, 0.12, decay=0.6)
    return hall(fade(st(trickle + ring), 0.3), 1.0, 0.25, 64)


def mp_fling():
    t = times(0.5)
    swish = whoosh(t, 0.0, 0.25, 900, 300, q=1.2, level=0.8)
    splash = filt(noise(len(t)), "bandpass", [700, 5000]) * env(t, 0.12, 0.003, start=0.12) * 0.7
    return hall(st(swish + splash + bubble(t, 0.16, 700, 0.3)), 0.5, 0.15, 65)


def mp_bowl():
    t = times(1.0)
    crack = filt(noise(len(t)), "highpass", 2500) * env(t, 0.03, 0.0005) * 1.2
    shards = sum(glass(t, rng.uniform(2200, 4800), 0.005 + k * 0.018, decay=0.25) * 0.3 for k in range(10))
    spatter = water(t, 0.03, 0.4, 900, 2200, level=0.5)
    return hall(fade(st(crack + shards + spatter), 0.3), 0.8, 0.22, 66)


def mp_glide():
    t = times(0.9)
    breath = sigh(t, 0.0, 0.8, f0=700, rise=0.4, level=0.7)
    return hall(fade(st(breath + 0.2 * glass(t, 1976, 0.15, decay=0.6)), 0.3), 1.2, 0.3, 67)


def mp_forget():
    t = times(2.4)
    rush = shimmer(t, 0.0, 0.7, f0=1100, level=1.0, reverse=True)
    smear = shimmer(t, 0.7, 1.5, f0=660, level=0.9, detune=0.03)
    hush = filt(noise(len(t)), "bandpass", [300, 1200]) * env(t, 0.9, 0.2, start=0.7) * 0.35
    suck = whoosh(t, 0.0, 0.72, 4000, 800, q=1.0, level=0.5)
    x = st(rush + suck) + pan(smear, -0.4) + pan(hush, 0.4)
    return hall(fade(x, 0.6), 2.6, 0.45, 68)


def mp_current():
    t = times(1.6)
    surge = water(t, 0.0, 1.5, 250, 1400, level=1.0) * np.clip(t / 0.25, 0, 1)
    sweep = whoosh(t, 0.05, 1.2, 300, 1500, q=0.7, level=0.6)
    x = np.stack([surge * np.clip(1 - t / 1.2, 0.2, 1), surge * np.clip(t / 0.8, 0.2, 1)], axis=1) + st(sweep)
    return hall(fade(x, 0.4), 1.2, 0.2, 69)


def mp_title():
    t = times(4.0)
    gong = temple_bell(t, 73.4, level=1.0, decay=3.0) + drum(t, 0.0, f0=50, level=0.7, decay=0.7)
    x = st(gong) + st(choir(t, 0.1, 3.2, level=0.6)) + pan(0.3 * bell(t, fo.note(4), start=0.3), 0.2)
    return hall(fade(x, 1.0), 3.0, 0.4, 70)


def mp_declare():
    t = times(2.0)
    x = st(bowl(t, 196, level=0.9, decay=1.4)) + st(shimmer(t, 0.0, 0.9, f0=1320, level=0.8, reverse=True))
    return hall(fade(x, 0.5), 2.0, 0.35, 71)


def mp_transform_gather():
    t = times(1.5)
    k = np.clip(t / 1.35, 0, 1)
    drone = sum(np.sin(2 * np.pi * f * t * (1 + 0.02 * k)) * 0.2 for f in (73.4, 110, 146.8)) * k ** 1.5
    cracks = sum(stone(t, 0.3 + j * 0.15 * (1 - j * 0.05), level=0.25 + 0.08 * j) for j in range(7))
    beats = sum(drum(t, 0.1 + j * (0.42 - j * 0.07), f0=55, level=0.5 + j * 0.1, decay=0.2) for j in range(4))
    return hall(st(sat(drone * 1.5, 1.3) + cracks + beats), 1.2, 0.25, 72)


def mp_transform():
    t = times(4.0)
    shatter = sum(glass(t, rng.uniform(1800, 5200), rng.uniform(0, 0.08), decay=0.5) * 0.25 for _ in range(16))
    shatter += filt(noise(len(t)), "highpass", 2000) * env(t, 0.1, 0.001) * 1.2
    gong = temple_bell(t, 55, level=1.2, decay=3.5) + sub(t, 0.0, 120, 30, level=1.2, decay=0.8)
    x = st(sat(gong, 1.2) + shatter) + st(choir(t, 0.2, 3.4, notes=(220, 277.2, 329.6, 440), level=0.8))
    return hall(fade(x, 1.0), 3.2, 0.45, 73)


def mp_death_bowl():
    t = times(2.0)
    crack = filt(noise(len(t)), "highpass", 1800) * env(t, 0.08, 0.001) * 1.2
    shards = sum(glass(t, rng.uniform(900, 2400), 0.01 + k * 0.05, decay=0.8) * 0.3 for k in range(10))
    drip = water(t, 0.2, 1.4, 500, 1200, level=0.4)
    return hall(fade(st(crack + shards + drip), 0.6), 2.2, 0.4, 74)


def mp_death():
    t = times(6.0)
    bloom = shimmer(t, 0.0, 1.2, f0=880, level=1.0, reverse=True)
    cascade = sum(pan(0.35 * bell(t, fo.note(k + 3), start=1.2 + k * 0.18), np.sin(k * 1.3)) for k in range(10))
    gong = temple_bell(t, 98, start=1.2, level=1.0, decay=4.0)
    exhale = sigh(t, 1.5, 4.0, f0=500, rise=-0.3, level=0.6)
    x = st(bloom + gong + exhale) + cascade + st(choir(t, 1.2, 4.2, notes=(293.7, 369.9, 440, 587.3), level=0.6))
    return hall(fade(x, 1.5), 4.0, 0.5, 75)


SOUNDS = {
    "mp_bell": (mp_bell, -1.0), "mp_river": (mp_river, -3.0), "mp_bridge": (mp_bridge, 0.0), "mp_ladle": (mp_ladle, -7.0),
    "mp_pour": (mp_pour, -6.0), "mp_fling": (mp_fling, -6.0), "mp_bowl": (mp_bowl, -4.0), "mp_glide": (mp_glide, -8.0),
    "mp_forget": (mp_forget, -2.0), "mp_current": (mp_current, -3.0), "mp_title": (mp_title, 1.0), "mp_declare": (mp_declare, -1.0),
    "mp_transform_gather": (mp_transform_gather, -1.0), "mp_transform": (mp_transform, 1.5), "mp_death_bowl": (mp_death_bowl, -1.0),
    "mp_death": (mp_death, 1.0),
}


def main():
    reel = []
    for name, (make, offset) in SOUNDS.items():
        x = level(make(), offset)
        write(os.path.join(DEST, name + ".wav"), x)
        print(f"{name:22s} {len(x) / SR:.2f}s, loudest 100ms {nz.loudest(x):.1f} dB")
        reel.append(x if x.ndim == 2 else np.stack([x, x], axis=1))
        reel.append(np.zeros((int(SR * 0.4), 2)))
    write(os.path.join(ct.OUT, "mengpo.wav"), nz.limit(np.concatenate(reel) * 0.8, nz.CEILING))


if __name__ == "__main__":
    main()
