"""Heaven's thunder, the strike that falls on a player standing still in the endless mode
(Bombardment), synthesised from scratch (no samples) with the Command Token's, the envelope's and
the boss's instruments.

  strike_warn   the thunder seal marking the ground, as long as its warning (1.3 s): a paper
                seal slapped down with a small bell, then the storm gathering over it, a low
                rumble swelling, static crackling thicker and a thin whine climbing, cut off
                just as it falls
  strike_hit    the bolt landing (2 s): a white crack of noise, a snap of static, the drum and a
                sub drop under it, and the thunder rolling away

python thunder.py writes them into Assets/### Different Engine/Resources/Sfx/ (the game loads them
from there), each at the game's reference loudness (normalize.py), plus out/thunder.wav to listen to.
"""
import os

import numpy as np

import command_token as ct
from command_token import SR, times, env, glide, noise, brown, filt, sat, bell
import fortune as fo
from fortune import pan, hall, fade, drum, sub, write, level
from boss import crackle
import normalize as nz

rng = np.random.default_rng(3141)
DEST = os.path.join(ct.HERE, "..", "..", "Assets", "### Different Engine", "Resources", "Sfx")
WARNING = 1.3


def strike_warn():
    t = times(WARNING + 0.05)
    x = np.zeros((len(t), 2))
    k = np.clip(t / WARNING, 0, 1)
    # the seal slapped down, and a small bell
    x += pan(0.7 * glide(t, 380, 150, 0.012) * env(t, 0.035, 0.0008), 0)
    x += pan(0.35 * filt(noise(len(t)), "bandpass", [900, 4200]) * env(t, 0.02, 0.0004), 0)
    x += pan(0.25 * bell(t, fo.note(7), start=0.005), 0.1)
    # the storm gathering: a low rumble swelling, rolling left and right
    rumble = filt(brown(len(t)), "lowpass", 180) * (k ** 1.6) * (0.8 + 0.2 * np.sin(2 * np.pi * 3.1 * t))
    x += np.stack([rumble * (0.9 + 0.1 * np.sin(2 * np.pi * 0.7 * t)), rumble * (0.9 - 0.1 * np.sin(2 * np.pi * 0.7 * t))], axis=1) * 0.9
    # static crackling thicker as it comes
    for side in (-0.5, 0.5):
        c = np.zeros_like(t)
        at = 0.15
        while at < WARNING:
            c += filt(noise(len(t)), "highpass", 3000) * env(t, rng.uniform(0.002, 0.006), 0.0002, start=at) * rng.uniform(0.3, 1.0)
            at += rng.exponential(1 / (8 + 70 * (at / WARNING) ** 2))
        x += pan(0.28 * c, side)
    # a thin whine climbing under it
    whine = np.sin(2 * np.pi * np.cumsum(260 + 900 * k ** 2) / SR) * (k ** 2) * 0.12
    x += pan(filt(whine, "bandpass", [200, 2500]), 0)
    # cut off just as it falls
    x *= np.clip((WARNING + 0.05 - t) / 0.05, 0, 1)[:, None]
    return hall(x, 0.6, 0.12, seed=301, tone=6000)


def strike_hit():
    t = times(2.2)
    x = np.zeros((len(t), 2))
    # the crack: a white burst, a snap of static
    x += pan(1.0 * filt(noise(len(t)), "highpass", 1800) * env(t, 0.025, 0.0003), 0)
    x += pan(0.5 * crackle(t, 0.0, 0.18, rate=260), -0.3) + pan(0.5 * crackle(t, 0.01, 0.2, rate=260), 0.3)
    # the weight of it: the drum and a sub drop
    x += pan(0.9 * drum(t, 0.005, 52, decay=0.5) + 1.0 * sub(t, 0.0, 120, 28, decay=0.6), 0)
    # the thunder rolling away: rumbles at a few distances, each later, lower and softer
    for i, (at, cut, lv, p) in enumerate([(0.05, 700, 0.8, -0.3), (0.25, 400, 0.6, 0.35), (0.6, 250, 0.45, -0.1), (1.0, 160, 0.3, 0.2)]):
        roll = filt(brown(len(t)), "lowpass", cut) * env(t, 0.35 + 0.15 * i, 0.03, start=at)
        roll *= 0.75 + 0.25 * np.sin(2 * np.pi * (4 + i) * t + i)
        x += pan(lv * roll, p)
    return fade(hall(sat(x, 1.3), 1.8, 0.28, seed=303, tone=4500), 0.5)


def main():
    clips = {
        "strike_warn": level(strike_warn(), -3.0),
        "strike_hit": level(strike_hit(), 1.0),
    }
    for name, x in clips.items():
        write(os.path.join(DEST, name + ".wav"), x)
        print(f"{name}: {len(x) / SR:.2f}s, loudest 100ms {nz.loudest(x):.1f} dB")

    total = np.zeros((int(4.5 * SR), 2))
    for at, name in ((0.0, "strike_warn"), (WARNING, "strike_hit")):
        s = int(at * SR)
        y = clips[name][:len(total) - s]
        total[s:s + len(y)] += y
    write(os.path.join(ct.OUT, "thunder.wav"), nz.limit(total * 0.8, nz.CEILING))
    print("out/thunder.wav")


if __name__ == "__main__":
    main()
