"""The end boss's duel (BossDuel, DuelWeapon), sound designed and synthesised from scratch (no samples)
with the Command Token's, the envelope's, the Magistrate's and Meng Po's instruments. the duel's own
weapons fire all fight long, so the stream's sounds are small and soft and the big ones rare.

  duel_manifest   the pillar of light coming down: a rushing descent, a bright strike, a temple
                  bell and a rising chord as the weapon forms in it
  sun_loose       the Sun-Shooter's stream: a light, airy flick of a bowstring
  sun_charge      a sun gathering on the string (0.6 s): a rising glassy whine, a heat shimmer
  sun_shot        the sunshot loosed: a deep twang, a fiery rush
  sun_hit         it bursts: a bright flare, a boom, a crow's caw pitched into a shriek of heat
  sun_ninth       the Ninth Sun: the same, bigger, a gong and the sky ringing after
  dipper_lunge    a star-sword lunging: a quick steel swish
  dipper_hit      its strike: a clean ring of steel with a star's chime in it
  dipper_volley   the seven go at once: a chord of seven chimes, a sweeping whoosh
  dipper_seal     the Dipper stamped over him: a struck bell, the seven chimes in a run, a thump
  decree_throw    a talisman flung: a paper flutter
  decree_stick    it slaps on and catches: a paper slap, a small flare of fire
  decree_ignite   the ring lights: a whoomp of fire catching all round, a bowl bell
  decree_blast    the decree carried out: a great boom of fire, the bell cracking over it

python duel.py writes them into Assets/### Different Engine/Resources/Sfx/, each at the game's
reference loudness (normalize.py), plus out/duel.wav to listen to them one after another.
"""
import os

import numpy as np

import command_token as ct
from command_token import SR, times, env, noise, brown, filt, swept, sat, bell
import fortune as fo
from fortune import pan, hall, fade, drum, sub, write, level
from boss import bowl, whoosh, crackle
from huangquan import glass, paper
from mengpo import temple_bell, shimmer, choir
import normalize as nz

rng = np.random.default_rng(4410)
DEST = os.path.join(ct.HERE, "..", "..", "Assets", "### Different Engine", "Resources", "Sfx")


def st(x, p=0.0):
    return pan(x, p)


def twang(t, at, f0=110.0, level=1.0, decay=0.35):
    """a bowstring released: a plucked tone bending down a touch, with its buzz"""
    u = np.maximum(t - at, 0)
    f = f0 * (1 + 0.15 * np.exp(-u / 0.02))
    ph = 2 * np.pi * np.cumsum(f) / SR
    x = (np.sin(ph) + 0.4 * np.sin(2 * ph) + 0.2 * np.sin(3 * ph)) * np.exp(-u / decay)
    return level * sat(x, 1.4) * (t >= at)


def fire(t, start, length, level=1.0, low=300.0, high=3000.0):
    """flame: a roaring band of noise with crackle in it"""
    u = np.clip((t - start) / length, 0, 1)
    shape = np.sin(np.pi * u) ** 0.6 * ((t >= start) & (t < start + length))
    roar = filt(noise(len(t)), "bandpass", [low, high]) * shape
    return level * (roar + 0.5 * crackle(t, start, length, rate=50, level=0.6))


def steel(t, at, f0=1800.0, level=1.0, decay=0.5):
    """a blade's ring: inharmonic, bright, beating"""
    ratios, amps = [1, 1.51, 2.37, 3.62], [1, 0.6, 0.35, 0.2]
    u = np.maximum(t - at, 0)
    x = sum(a * np.sin(2 * np.pi * f0 * r * u * (1 + 0.002 * i)) * np.exp(-u / (decay / (1 + 0.6 * i))) for i, (r, a) in enumerate(zip(ratios, amps)))
    return level * x * (t >= at)


def duel_manifest():
    t = times(3.2)
    descent = whoosh(t, 0.0, 0.35, 5000, 700, q=0.9, level=0.9)
    strike = filt(noise(len(t)), "highpass", 1800) * env(t, 0.08, 0.001, start=0.33) * 1.3
    boom = drum(t, 0.33, f0=58, level=1.0, decay=0.5) + sub(t, 0.33, 110, 36, level=0.8, decay=0.5)
    gong = temple_bell(t, 130.8, start=0.33, level=0.7, decay=2.5)
    rise = shimmer(t, 0.3, 0.9, f0=1760, level=0.7, reverse=True)
    chord = choir(t, 0.5, 2.4, notes=(261.6, 329.6, 392.0, 523.3), level=0.55)
    x = st(descent + strike + boom + gong) + pan(rise, 0.3) + st(chord)
    return hall(fade(x, 0.9), 2.6, 0.4, 91)


def sun_loose():
    t = times(0.3)
    flick = twang(t, 0.0, f0=392, level=0.4, decay=0.06)
    air = whoosh(t, 0.0, 0.12, 5200, 2600, q=1.3, level=0.5)
    return hall(st(flick + air), 0.3, 0.1, 92)


def sun_charge():
    t = times(0.8)
    k = np.clip(t / 0.6, 0, 1)
    whine = sum(np.sin(2 * np.pi * np.cumsum(f * (1 + 1.2 * k ** 1.5)) / SR) * a for f, a in ((700, 0.35), (1050, 0.2), (1400, 0.12))) * k ** 1.2
    heat = filt(noise(len(t)), "bandpass", [1500, 6000]) * k ** 2 * 0.25
    x = (whine + heat) * np.where(t < 0.62, 1, np.exp(-(t - 0.62) / 0.04))
    return hall(st(x), 0.5, 0.2, 93)


def sun_shot():
    t = times(0.9)
    string = twang(t, 0.0, f0=98, level=0.9, decay=0.3)
    rush = fire(t, 0.0, 0.45, level=0.6, low=500, high=4000) + whoosh(t, 0.0, 0.3, 4000, 900, q=1.0, level=0.6)
    thump = drum(t, 0.0, f0=70, level=0.6, decay=0.15)
    return hall(fade(st(string + rush + thump), 0.3), 0.8, 0.22, 94)


def _burst(t, big):
    flare = filt(noise(len(t)), "highpass", 2200) * env(t, 0.06 if not big else 0.12, 0.001) * 1.2
    boom = drum(t, 0.0, f0=54 if not big else 44, level=1.1, decay=0.45 if not big else 0.8) + sub(t, 0.0, 120, 32, level=1.0, decay=0.5 if not big else 0.9)
    blaze = fire(t, 0.0, 0.7 if not big else 1.4, level=0.55, low=250, high=3500)
    # the crow: a caw bent up into a shriek of heat
    u = np.maximum(t - 0.05, 0)
    caw = swept(noise(len(t)), lambda s: 900 + 1600 * np.clip((s - 0.05) / 0.25, 0, 1), q=6) * np.exp(-u / 0.18) * (t >= 0.05) * 0.5
    return sat(boom, 1.2) + flare + blaze + caw


def sun_hit():
    t = times(1.4)
    return hall(fade(st(_burst(t, False)), 0.5), 1.2, 0.3, 95)


def sun_ninth():
    t = times(3.6)
    ring = sum(pan(0.25 * bell(t, fo.note(k + 5), start=0.15 + k * 0.09), np.sin(k * 1.7)) for k in range(9))
    gong = temple_bell(t, 87.3, start=0.0, level=0.9, decay=2.6)
    x = st(_burst(t, True) + gong) + ring
    return hall(fade(x, 1.2), 2.8, 0.4, 96)


def dipper_lunge():
    t = times(0.35)
    swish = whoosh(t, 0.0, 0.2, 2400, 5200, q=1.6, level=0.7)
    edge = steel(t, 0.0, f0=3100, level=0.12, decay=0.12)
    return hall(st(swish + edge), 0.3, 0.12, 97)


def dipper_hit():
    t = times(0.8)
    ring = steel(t, 0.0, f0=1650, level=0.6, decay=0.35)
    chime = glass(t, 2637, 0.01, level=0.35, decay=0.5)
    tick = filt(noise(len(t)), "highpass", 3000) * env(t, 0.015, 0.0005) * 0.8
    return hall(st(ring + chime + tick), 0.7, 0.22, 98)


def dipper_volley():
    t = times(1.4)
    chord = sum(pan(glass(t, fo.note(k * 2), 0.0 + k * 0.012, level=0.28, decay=0.9), (k - 3) / 4) for k in range(7))
    sweep = whoosh(t, 0.0, 0.5, 1500, 6000, q=0.8, level=0.8)
    x = chord + st(sweep + steel(t, 0.0, f0=2200, level=0.2, decay=0.4))
    return hall(fade(x, 0.5), 1.4, 0.3, 99)


def dipper_seal():
    t = times(2.6)
    strike = bowl(t, 220, level=0.8, decay=1.6) + drum(t, 0.0, f0=60, level=0.8, decay=0.3)
    run = sum(pan(glass(t, fo.note(7 - k), 0.05 + k * 0.06, level=0.3, decay=0.8), (3 - k) / 4) for k in range(7))
    x = st(strike) + run + st(shimmer(t, 0.4, 1.4, f0=2200, level=0.35))
    return hall(fade(x, 0.9), 2.0, 0.4, 100)


def decree_throw():
    t = times(0.35)
    flutter = paper(t, 0.0, 0.25, bright=1.1, level=0.7)
    air = whoosh(t, 0.0, 0.18, 1800, 3500, q=1.2, level=0.35)
    return hall(st(flutter + air), 0.3, 0.1, 101)


def decree_stick():
    t = times(0.6)
    slap = filt(noise(len(t)), "bandpass", [800, 4000]) * env(t, 0.025, 0.0005) * 1.1
    catch = fire(t, 0.02, 0.35, level=0.45, low=600, high=4000)
    return hall(st(slap + catch + drum(t, 0.0, f0=120, level=0.25, decay=0.05)), 0.5, 0.15, 102)


def decree_ignite():
    t = times(1.3)
    whoomp = fire(t, 0.0, 0.8, level=0.9, low=150, high=2500) * np.clip(t / 0.08, 0, 1)
    suck = whoosh(t, 0.0, 0.25, 400, 1400, q=0.9, level=0.5)
    ring = bowl(t, 293.7, start=0.05, level=0.6, decay=1.2)
    return hall(fade(st(whoomp + suck + ring), 0.4), 1.2, 0.3, 103)


def decree_blast():
    t = times(2.4)
    boom = sat(drum(t, 0.0, f0=46, level=1.3, decay=0.7) + sub(t, 0.0, 130, 30, level=1.2, decay=0.8), 1.3)
    blaze = fire(t, 0.0, 1.5, level=0.8, low=200, high=4500)
    crack = filt(noise(len(t)), "highpass", 2000) * env(t, 0.1, 0.001) * 1.1
    bellcrack = bowl(t, 293.7, level=0.5, decay=1.6) * (1 + 0.5 * np.sin(2 * np.pi * 13 * t))
    return hall(fade(st(boom + blaze + crack + bellcrack), 0.8), 2.0, 0.35, 104)


SOUNDS = {
    "duel_manifest": (duel_manifest, 0.0), "sun_loose": (sun_loose, -12.0), "sun_charge": (sun_charge, -7.0),
    "sun_shot": (sun_shot, -3.0), "sun_hit": (sun_hit, -2.0), "sun_ninth": (sun_ninth, 0.5),
    "dipper_lunge": (dipper_lunge, -9.0), "dipper_hit": (dipper_hit, -6.0), "dipper_volley": (dipper_volley, -2.0),
    "dipper_seal": (dipper_seal, -1.0), "decree_throw": (decree_throw, -10.0), "decree_stick": (decree_stick, -7.0),
    "decree_ignite": (decree_ignite, -2.0), "decree_blast": (decree_blast, 0.0),
}


def main():
    reel = []
    for name, (make, offset) in SOUNDS.items():
        x = level(make(), offset)
        write(os.path.join(DEST, name + ".wav"), x)
        print(f"{name:22s} {len(x) / SR:.2f}s, loudest 100ms {nz.loudest(x):.1f} dB")
        reel.append(x if x.ndim == 2 else np.stack([x, x], axis=1))
        reel.append(np.zeros((int(SR * 0.4), 2)))
    write(os.path.join(ct.OUT, "duel.wav"), nz.limit(np.concatenate(reel) * 0.8, nz.CEILING))


if __name__ == "__main__":
    main()
