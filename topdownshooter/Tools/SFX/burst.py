"""The end bosses' phase bursts (PhaseBurst), sound designed and synthesised from scratch (no samples)
in the manner of a Genshin Impact burst: the air sucked in as the power gathers, a heartbeat of
silence, then everything let go at once.

  burst_charge        the gathering (0.9 s): a reversed shimmer and a rising roar of air, a
                      heartbeat under it, the pitch climbing to the release
  burst_release       the release: a deep boom and a sub drop, a bright crack, a whoosh outward,
                      a shimmering tail ringing off
  burst_yama          Yama's stinger over it: war drums, a great gong, a low choir hit
  burst_mengpo        Meng Po's: a temple bell over water, a glassy shimmer, a choir of breath
  burst_ult_charge    the last phase's gathering (1.8 s): the same, longer, three heartbeats, the
                      roar climbing past where it stopped before
  burst_ult_release   the last phase's release: two booms, the second bigger, glass breaking, a
                      long tail

python burst.py writes them into Assets/### Different Engine/Resources/Sfx/, each at the game's
reference loudness (normalize.py), plus out/burst.wav to listen to them one after another.
"""
import os

import numpy as np

import command_token as ct
from command_token import SR, times, env, noise, brown, filt, swept, sat, bell
import fortune as fo
from fortune import pan, hall, fade, drum, sub, write, level
from boss import bowl, whoosh, voice
from huangquan import glass
from mengpo import temple_bell, shimmer, choir, water
import normalize as nz

rng = np.random.default_rng(5150)
DEST = os.path.join(ct.HERE, "..", "..", "Assets", "### Different Engine", "Resources", "Sfx")


def st(x, p=0.0):
    return pan(x, p)


def gather(t, length, beats):
    """the air sucked in: a band of noise sweeping up and swelling, a reversed shimmer, a tone
    cluster climbing, heartbeats under it"""
    k = np.clip(t / length, 0, 1)
    air = swept(noise(len(t)), lambda s: 300 + 3200 * np.clip(s / length, 0, 1) ** 1.6, q=1.4) * k ** 1.8 * 0.9
    rise = sum(np.sin(2 * np.pi * np.cumsum(f * (1 + 1.6 * k ** 2)) / SR) * a for f, a in ((110, 0.3), (165, 0.2), (220, 0.14))) * k ** 1.5
    rev = shimmer(t, 0.0, length, f0=1400, level=0.8, reverse=True)
    thump = sum(drum(t, length * (i + 0.5) / (beats + 0.5) - 0.1, f0=52, level=0.7 + 0.1 * i, decay=0.2) for i in range(beats))
    cut = np.where(t < length, 1.0, np.exp(-(t - length) / 0.02))
    return (air + sat(rise, 1.3) + rev + thump) * cut


def release(t, at, big):
    """everything let go: a boom and a sub drop, a crack, a whoosh outward, a shimmering tail"""
    boom = drum(t, at, f0=42, level=1.4, decay=0.9 if big else 0.7) + sub(t, at, 140, 26, level=1.4, decay=1.0 if big else 0.8)
    crack = filt(noise(len(t)), "highpass", 1800) * env(t, 0.12, 0.0008, start=at) * 1.4
    out = whoosh(t, at, 0.6, 2600, 300, q=0.9, level=0.9)
    tail = shimmer(t, at + 0.05, 2.2 if big else 1.6, f0=1760, level=0.6)
    return sat(boom, 1.35) + crack + out + tail


def burst_charge():
    t = times(1.0)
    return hall(fade(st(gather(t, 0.9, 2)), 0.08), 1.2, 0.3, 121)


def burst_release():
    t = times(2.6)
    return hall(fade(st(release(t, 0.0, False)), 1.0), 2.4, 0.4, 122)


def burst_yama():
    t = times(3.0)
    drums = sum(drum(t, 0.0 + i * 0.11, f0=64 - i * 4, level=1.0 - i * 0.12, decay=0.35) for i in range(4))
    gong = temple_bell(t, 65.4, start=0.05, level=1.0, decay=2.6)
    hit = choir(t, 0.02, 1.8, notes=(98.0, 146.8, 196.0), level=0.8)
    return hall(fade(st(sat(drums, 1.2) + gong) + st(hit), 1.0), 2.6, 0.4, 123)


def burst_mengpo():
    t = times(3.2)
    ring = temple_bell(t, 130.8, start=0.0, level=0.9, decay=2.8)
    splash = water(t, 0.0, 1.2, 700, 2200, level=0.5)
    glassy = shimmer(t, 0.0, 1.8, f0=2093, level=0.7)
    breath = choir(t, 0.05, 2.2, notes=(261.6, 329.6, 392.0, 523.3), level=0.7)
    return hall(fade(st(ring + splash) + pan(glassy, 0.3) + st(breath), 1.1), 3.0, 0.45, 124)


def burst_ult_charge():
    t = times(1.9)
    return hall(fade(st(gather(t, 1.8, 3)) + st(choir(t, 0.4, 1.4, notes=(73.4, 110.0, 146.8), level=0.5)), 0.08), 1.4, 0.35, 125)


def burst_ult_release():
    t = times(3.6)
    first = release(t, 0.0, False) * 0.7
    second = release(t, 0.32, True)
    shatter = sum(glass(t, rng.uniform(1800, 5200), 0.32 + rng.uniform(0, 0.08), decay=0.5) * 0.25 for _ in range(14))
    return hall(fade(st(first + second + shatter), 1.4), 3.2, 0.45, 126)


SOUNDS = {
    "burst_charge": (burst_charge, -2.0), "burst_release": (burst_release, 1.0), "burst_yama": (burst_yama, -1.0),
    "burst_mengpo": (burst_mengpo, -1.0), "burst_ult_charge": (burst_ult_charge, -1.0), "burst_ult_release": (burst_ult_release, 1.5),
}


def main():
    reel = []
    for name, (make, offset) in SOUNDS.items():
        x = level(make(), offset)
        write(os.path.join(DEST, name + ".wav"), x)
        print(f"{name:22s} {len(x) / SR:.2f}s, loudest 100ms {nz.loudest(x):.1f} dB")
        reel.append(x if x.ndim == 2 else np.stack([x, x], axis=1))
        reel.append(np.zeros((int(SR * 0.4), 2)))
    write(os.path.join(ct.OUT, "burst.wav"), nz.limit(np.concatenate(reel) * 0.8, nz.CEILING))


if __name__ == "__main__":
    main()
