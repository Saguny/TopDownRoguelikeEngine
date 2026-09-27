"""The Cinnabar Ink Brush's evolution (the Calligraphic Seal Grid) going off, sound designed and
synthesised from scratch, timed to what it draws (CinnabarInkBrush.Detonate):

  0.00s  the last flick of the brush closing the loop: bristles whipping through the air
  0.03s  the seal slamming down on it: a heavy thump, stone on paper, and a bronze bell (A, the
         Command Token's bells are D-F-A, so the two weapons ring in the same chord)
  0.05s  the loop catching fire: a fwoomp swelling under a ripple of ignitions spreading out over
         the 0.35s the blasts take to reach the loop's edge, and a sub boom
  0.3s+  crackling fire and burning ink hissing, dying down

python cinnabar_brush.py writes Assets/### Different Engine/Sounds/CinnabarInkBrush/brush_seal.wav
(mono, 48 kHz, 16 bit), its loudest 100 ms at the game's reference loudness (normalize.py).
it reuses command_token.py's building blocks.
"""
import os

import numpy as np
import scipy.signal as sg

import command_token as ct
from command_token import SR, times, env, glide, modal, noise, filt, swept, sat, reverb, fade_out, bell

rng = np.random.default_rng(2718)
DEST = os.path.join(ct.HERE, "..", "..", "Assets", "### Different Engine", "Sounds", "CinnabarInkBrush")
RIPPLE = 0.35      # CinnabarInkBrushData.rippleSeconds


def flick(t):
    """a big brush whipping through the air: a band of noise rushing up in pitch, gone in 70ms"""
    x = swept(noise(len(t)), lambda s: 900 + 6000 * np.clip(s / 0.06, 0, 1) ** 1.5, q=1.8)
    x *= np.clip(t / 0.02, 0, 1) ** 2 * np.where(t < 0.055, 1, np.exp(-(t - 0.055) / 0.012))
    # a few bristles ticking against each other
    ticks = np.zeros_like(t)
    for _ in range(9):
        at = rng.uniform(0.01, 0.06)
        ticks += filt(noise(len(t)), "bandpass", [3500, 9000]) * env(t, 0.002, 0.0002, start=at) * rng.uniform(0.3, 0.8)
    return x + 0.4 * ticks


def seal(t, at=0.03):
    u = np.maximum(t - at, 0)
    live = (t >= at).astype(float)
    thump = sat(glide(u, 210, 44, 0.018) * env(u, 0.14, 0.0015) * 1.4, 2.0) * live
    low = filt(noise(len(t)), "lowpass", 150) * env(t, 0.09, 0.002, start=at)
    knock = modal(u, np.array([380, 990, 1660, 2700, 3900]), [0.055, 0.032, 0.018, 0.011, 0.007], [1, 0.7, 0.45, 0.3, 0.18]) * live
    paper = filt(noise(len(t)), "lowpass", 2600) * env(t, 0.035, 0.0008, start=at)
    ring = bell(t, 880.0, start=at + 0.006)
    return thump + 0.35 * low + 0.5 * knock + 0.35 * paper + 0.18 * ring


def ignition(t, at=0.05):
    """the inside of the loop catching: one big fwoomp under a ripple of small ones"""
    u = np.maximum(t - at, 0)
    live = (t >= at).astype(float)
    # the fwoomp: air rushing into the fire, a low roar opening up
    roar = swept(noise(len(t)), lambda s: 180 + 2600 * np.clip((s - at) / 0.22, 0, 1) ** 0.7 * np.exp(-max(0.0, s - at - 0.22) / 0.5), q=0.7, kind="low")
    roar *= live * np.clip(u / 0.08, 0, 1) * np.exp(-np.maximum(u - 0.1, 0) / 0.45)
    sub = sat(glide(u, 110, 42, 0.09) * env(u, 0.26, 0.01) * 1.2, 2.2) * live

    # the ripple: small ignitions, most of them in the middle of the spread, each a puff of fire
    puffs = np.zeros_like(t)
    for _ in range(26):
        when = at + RIPPLE * rng.beta(2.2, 2.0) + rng.uniform(-0.02, 0.02)
        body = filt(noise(len(t)), "bandpass", [rng.uniform(250, 500), rng.uniform(2500, 6000)]) * env(t, rng.uniform(0.05, 0.11), 0.003, start=when)
        thud = glide(np.maximum(t - when, 0), rng.uniform(120, 170), rng.uniform(50, 70), 0.02) * env(t, 0.06, 0.003, start=when)
        puffs += rng.uniform(0.5, 1.0) * (body + 0.5 * thud)
    return 0.6 * roar + 0.5 * sub + 0.6 * puffs


def burning(t, at=0.2):
    """what's left: fire crackling and ink hissing as it burns off, dying down"""
    n = len(t)
    crackle = np.zeros(n)
    for _ in range(260):
        when = at + rng.uniform(0, 1) ** 1.6 * 1.7
        i = int(when * SR)
        if i < n: crackle[i] += rng.uniform(0.2, 1.0)
    kernel = np.exp(-np.arange(int(0.003 * SR)) / (0.0006 * SR))
    crackle = sg.fftconvolve(crackle, kernel)[:n] * filt(noise(n), "highpass", 1200)
    hiss = filt(noise(n), "bandpass", [2500, 9000]) * (0.6 + 0.4 * np.sin(2 * np.pi * 9 * t + rng.uniform(0, 6)))
    hiss *= env(t, 0.5, 0.15, start=at)
    rumble = filt(noise(n), "lowpass", 300, order=4) * env(t, 0.7, 0.1, start=at)
    return 0.9 * crackle + 0.14 * hiss + 0.15 * rumble


def brush_seal():
    t = times(2.6)
    mix = 0.55 * flick(t) + 1.0 * seal(t) + 1.0 * ignition(t) + 0.8 * burning(t)
    mix = reverb(mix, 1.8, 0.35, predelay=0.015, tone=5000, seed=31)[:len(t)]
    return fade_out(mix, 0.7)


def main():
    import normalize as nz
    x = brush_seal()
    x = x[:, None]
    x *= 10 ** ((nz.REFERENCE - nz.loudest(x)) / 20)
    x = nz.limit(x, nz.CEILING)
    path = os.path.join(DEST, "brush_seal.wav")
    ct.write(path, x[:, 0])
    print(f"brush_seal.wav: {len(x) / SR:.2f}s, loudest 100ms {nz.loudest(x):.1f} dB, peak {20 * np.log10(nz.true_peak(x)):.1f} dBTP")
    np.save(os.path.join(ct.OUT, "brush_seal.npy"), x[:, 0])


if __name__ == "__main__":
    main()
