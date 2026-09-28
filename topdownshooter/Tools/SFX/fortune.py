"""The fortune envelope and the Wuchang's taking of the player, sound designed and synthesised from
scratch (no samples), with the Command Token's instruments: bronze bells, stone and paper, the opera
gong, a hall. an envelope is Lunar New Year: coins in paper, a string of firecrackers, bells on the
D major pentatonic (the level up's scale, so they never clash with it). the Wuchang are the other
side of the same world: iron chains, a breath drawn in, a temple bell tolled once.

  fe_pickup            walking into one on the ground: paper, a bright bell, a few coins (mono, 3D)
  fe_idle              the envelope waiting to be opened: a low bowl drone, air, a far chime (loops)
  fe_click             the player opening it: a seal knocked, paper snapping taut
  fe_charge_common     the build before it bursts, as long as its rarity's charge (EnvelopeOpening):
  fe_charge_rare       a drum roll speeding up, the envelope rattling harder, a reversed gong and
  fe_charge_legendary  cymbal swelling, a bowl climbing, sub pressure, a fuse sizzling (rare and up),
                       a brass drone opening (legendary); it draws a breath and cuts dead on the burst
  fe_shake             the envelope shaken: paper rattling and the coins inside it
  fe_tier              the light turning up a rarity: bells climbing into a shimmer and a thump
                       (played pitched up for legendary)
  fe_open              the flap torn open: the seal cracking, paper ripping, air rushing out
  fe_reveal_common     bells: a fifth, bright and small
  fe_reveal_rare       bells climbing the scale over a singing bowl
  fe_reveal_legendary  the opera gong, a sub boom, a string of firecrackers and a cascade of bells
  fe_reward            one reward landing on the scroll: a coin and a small bell (pitched up for
                       each one after the first)
  wc_chain             the soul-catching chain thrown: links rattling out, the shackle catching
  wc_swallow           the soul drawn in and swallowed: a breath in, a low moan, the dark closing,
                       a temple bell tolled under the ink

python fortune.py writes them into Assets/### Different Engine/Sounds/Fortune/, each at the game's
reference loudness (normalize.py), plus out/fortune_legendary.wav: a legendary opening on its
timeline (EnvelopeOpening), to listen to outside Unity.
"""
import os
import wave

import numpy as np
import scipy.signal as sg

import command_token as ct
from command_token import SR, times, env, glide, modal, noise, brown, filt, swept, sat, reverb, fade_out, bell
import level_up as lu
import normalize as nz

rng = np.random.default_rng(88)
DEST = os.path.join(ct.HERE, "..", "..", "Assets", "### Different Engine", "Sounds", "Fortune")

D5 = 587.33
PENTA = [1, 9 / 8, 5 / 4, 3 / 2, 5 / 3]            # D E F# A B


def note(i, base=D5):
    return base * PENTA[i % 5] * 2 ** (i // 5)


def pan(x, p):
    a = (p + 1) * np.pi / 4
    return np.stack([x * np.cos(a), x * np.sin(a)], axis=1)


def hall(x, rt, wet, seed, tone=6500, predelay=0.014):
    """stereo hall: the same tail decorrelated left and right"""
    if x.ndim == 1: x = np.stack([x, x], axis=1)
    n = len(x)
    return np.stack([reverb(x[:, 0], rt, wet, predelay=predelay, tone=tone, seed=seed)[:n],
                     reverb(x[:, 1], rt, wet, predelay=predelay + 0.003, tone=tone, seed=seed + 1)[:n]], axis=1)


def fade(x, seconds):
    if x.ndim == 1: return fade_out(x, seconds)
    return np.stack([fade_out(x[:, 0], seconds), fade_out(x[:, 1], seconds)], axis=1)


def rustle(t, start, length, rate=14.0, bright=1.0):
    """paper handled: noise in the paper's band, broken into crinkles"""
    u = t - start
    live = (u >= 0) & (u < length)
    grains = np.zeros_like(t)
    k = start
    while k < start + length:
        grains += env(t, rng.uniform(0.006, 0.02), 0.0005, start=k) * rng.uniform(0.3, 1.0)
        k += rng.exponential(1 / rate)
    body = filt(noise(len(t)), "bandpass", [1200, 5200 * bright])
    crisp = filt(noise(len(t)), "highpass", 6000) * 0.45
    return (body + crisp) * grains * live


def firecracker(t, at, level=1.0):
    """one banger: a hard crack, a burst of air, a papery tail"""
    u = t - at
    crack = filt(noise(len(t)), "bandpass", [900, 9000]) * env(t, 0.004, 0.0001, start=at)
    boom = glide(np.maximum(u, 0), 380, 90, 0.008) * env(t, 0.02, 0.0005, start=at) * 0.7
    tail = filt(noise(len(t)), "bandpass", [2000, 7000]) * env(t, 0.04, 0.002, start=at) * 0.25
    return level * (crack + boom + tail)


def drum(t, at, f0=62.0, level=1.0, decay=0.42):
    """the big temple drum, da gu: a heavy membrane whose pitch drops as it settles, a second mode
    over it, the slap of the hide and the boom of its body"""
    u = np.maximum(t - at, 0)
    live = t >= at
    head = glide(u, f0 * 1.7, f0, 0.035) * env(t, decay, 0.002, start=at)
    over = np.sin(2 * np.pi * np.cumsum(f0 * 1.59 * (1 + 0.5 * np.exp(-u / 0.03))) / SR) * env(t, decay * 0.4, 0.002, start=at)
    slap = filt(noise(len(t)), "lowpass", 900) * env(t, 0.03, 0.0008, start=at)
    boom = filt(noise(len(t)), "lowpass", 120, order=4) * env(t, decay * 0.8, 0.004, start=at)
    return level * sat((1.0 * head + 0.35 * over + 0.45 * slap + 0.9 * boom) * live, 1.6)


def sub(t, at, f0=90.0, f1=34.0, level=1.0, decay=0.35):
    """a sub drop under a hit"""
    return level * sat(glide(np.maximum(t - at, 0), f0, f1, 0.06) * env(t, decay, 0.003, start=at), 1.8)


def metal_link(t, at, level=1.0, low=1.0):
    """an iron chain link knocking another: bright inharmonic modes, gone fast"""
    f0 = rng.uniform(1900, 3100) * low
    ratios = [1, 1.47, 2.09, 2.83, 3.7]
    u = np.maximum(t - at, 0)
    ring = sum(a * np.sin(2 * np.pi * f0 * r * u + rng.uniform(0, 6.28)) * np.exp(-u / d)
               for r, d, a in zip(ratios, [0.05, 0.035, 0.025, 0.015, 0.01], [1, 0.7, 0.5, 0.35, 0.2]))
    click = filt(noise(len(t)), "highpass", 4000) * env(t, 0.002, 0.0002, start=at) * 0.6
    return level * (ring * np.clip(u / 0.0005, 0, 1) * (t >= at) + click)


# ---------------------------------------------------------------- the envelope

def pickup():
    t = times(1.0)
    x = 0.8 * rustle(t, 0.0, 0.09, rate=60)
    x += 0.7 * drum(t, 0.0, 78, decay=0.18)
    x += 0.5 * bell(t, note(7), start=0.012)                 # A6
    x += 0.25 * bell(t, note(10), start=0.05)                # D7
    for i, at in enumerate([0.02, 0.07, 0.11]):
        x += lu.coin(t, note(8 + i, D5), at, 0.35)
    x = reverb(x, 1.0, 0.22, seed=5)[:len(t)]
    return fade_out(x, 0.35)


def shake():
    t = times(0.9)
    x = np.zeros((len(t), 2))
    # a woodblock tok as it's snatched up
    tok = modal(t, np.array([880, 1420, 2330]), [0.03, 0.02, 0.012], [1, 0.5, 0.25])
    x += pan(0.5 * tok, 0)
    x += pan(0.9 * drum(t, 0.0, 70, decay=0.25), 0)
    x += pan(0.35 * filt(brown(len(t)), "lowpass", 160, order=4) * env(t, 0.45, 0.02), 0)
    # the paper rattling, fast, both hands
    x += pan(0.75 * rustle(t, 0.01, 0.55, rate=38), -0.35)
    x += pan(0.6 * rustle(t, 0.03, 0.5, rate=34, bright=1.2), 0.35)
    # coins inside, knocking together
    for _ in range(11):
        at = rng.uniform(0.02, 0.55)
        x += pan(lu.coin(t, note(int(rng.integers(6, 11))), at, rng.uniform(0.15, 0.35)), rng.uniform(-0.6, 0.6))
    return fade(hall(x, 0.8, 0.18, seed=3), 0.25)


def tier():
    t = times(1.5)
    x = np.zeros((len(t), 2))
    # a shimmer drawn up into the hit, like a reversed cymbal
    u = np.clip(t / 0.34, 0, 1)
    swell = filt(noise(len(t)), "highpass", 3500) * (u ** 3) * (t < 0.34)
    x += pan(0.5 * swell, 0)
    # three bells climbing into it: D F# A
    for i, (k, at) in enumerate([(5, 0.12), (7, 0.2), (8, 0.28)]):
        x += pan(0.42 * bell(t, note(k), start=at), (-0.4, 0.0, 0.4)[i])
    # the hit: a sub thump, a bright bell an octave up, a spray of sparkle
    thump = sat(glide(np.maximum(t - 0.34, 0), 150, 48, 0.03) * env(t, 0.18, 0.002, start=0.34), 1.8)
    x += pan(0.85 * thump, 0)
    drone = (np.sin(2 * np.pi * 73.4 * t) + 0.4 * np.sin(2 * np.pi * 110 * t)) * (u ** 2) * (t < 0.34)
    x += pan(0.45 * drone, 0)
    x += pan(1.0 * drum(t, 0.34, 66), 0)
    x += pan(0.7 * sub(t, 0.34, 100, 36, decay=0.5), 0)
    x += pan(0.5 * bell(t, note(10), start=0.34), 0.1)
    for _ in range(14):
        at = 0.34 + rng.uniform(0, 0.5) ** 1.5
        f = rng.uniform(4200, 9000)
        x += pan(0.06 * np.sin(2 * np.pi * f * np.maximum(t - at, 0)) * env(t, rng.uniform(0.04, 0.12), 0.0006, start=at), rng.uniform(-0.8, 0.8))
    return fade(hall(x, 1.6, 0.35, seed=7), 0.5)


def open_flap():
    t = times(1.1)
    x = np.zeros((len(t), 2))
    # the seal cracking: the stamp's stone knock, higher
    kf = np.array([760, 1650, 2700, 3900])
    crackle = modal(t, kf, [0.03, 0.02, 0.012, 0.008], [1, 0.7, 0.45, 0.3])
    crackle += filt(noise(len(t)), "bandpass", [2000, 9000]) * env(t, 0.006, 0.0002) * 0.8
    x += pan(0.7 * crackle, 0)
    # the paper ripping open: a band of noise sweeping up, broken into fibres
    rip = swept(noise(len(t)), lambda s: 900 + 7000 * np.clip((s - 0.02) / 0.22, 0, 1), q=1.2)
    fibres = np.zeros_like(t)
    k = 0.02
    while k < 0.26:
        fibres += env(t, 0.004, 0.0003, start=k) * rng.uniform(0.4, 1.0)
        k += rng.exponential(1 / 180)
    x += pan(0.9 * rip * (0.35 + fibres) * (t < 0.3), -0.2)
    # the air rushing out of it
    rush = swept(noise(len(t)), lambda s: 400 + 2600 * np.sin(np.clip(s / 0.5, 0, 1) * np.pi), q=0.9)
    x += pan(0.55 * rush * env(t, 0.22, 0.03, start=0.05), 0.25)
    x += pan(0.6 * sub(t, 0.01, 85, 32, decay=0.3), 0)
    x += pan(0.3 * filt(brown(len(t)), "lowpass", 180, order=4) * env(t, 0.25, 0.01), 0)
    return fade(hall(x, 1.1, 0.25, seed=9), 0.4)


def reveal(level):
    lengths = {0: 1.8, 1: 2.4, 2: 3.6}
    t = times(lengths[level])
    x = np.zeros((len(t), 2))
    if level == 0:
        # a bright fifth, and a few coins
        x += pan(1.0 * drum(t, 0.0, 64), 0)
        x += pan(0.45 * bell(t, note(0, D5 / 4)), 0)          # D3, under it all
        x += pan(0.55 * bell(t, note(5)), -0.2)
        x += pan(0.45 * bell(t, note(8), start=0.03), 0.2)
        for i in range(4): x += pan(lu.coin(t, note(8 + i), 0.05 + i * 0.05, 0.3), rng.uniform(-0.5, 0.5))
        return fade(hall(x, 1.4, 0.3, seed=11), 0.6)

    if level == 1:
        # bells climbing the scale over a singing bowl that swells in under them
        for i, k in enumerate([3, 5, 6, 7, 8, 10]):
            x += pan(0.4 * bell(t, note(k), start=0.02 + i * 0.06), -0.6 + i * 0.24)
        f = 293.66
        bowl = (np.sin(2 * np.pi * f * t) + 0.4 * np.sin(2 * np.pi * f * 2.71 * t) + 0.2 * np.sin(2 * np.pi * f * 5.1 * t))
        bowl *= (0.6 + 0.4 * np.sin(2 * np.pi * 4.5 * t)) * np.clip(t / 0.15, 0, 1) * np.exp(-t / 1.1)
        x += pan(0.3 * bowl, 0)
        x += pan(0.6 * sat(glide(t, 120, 50, 0.04) * env(t, 0.2, 0.002), 1.6), 0)
        x += pan(1.1 * drum(t, 0.0, 60, decay=0.55), 0)
        x += pan(0.7 * drum(t, 0.19, 64, decay=0.35), 0)
        x += pan(0.5 * bell(t, note(0, D5 / 4)), 0)
        x += pan(0.3 * np.sin(2 * np.pi * 73.4 * t) * np.clip(t / 0.1, 0, 1) * np.exp(-t / 0.9), 0)
        return fade(hall(x, 2.0, 0.38, seed=13), 0.8)

    # legendary: the opera gong and a sub boom, then a string of firecrackers going off and bells
    # cascading down the scale and back up over them
    base = 146.8 / 1.5
    ratios = np.sort(np.concatenate([[1, 1.52, 2.05, 2.61, 3.14], rng.uniform(3.3, 34, 30)]))
    gong = modal(t, base * ratios, 3.0 / (1 + ratios * 0.09), 1 / np.sqrt(ratios) * rng.uniform(0.6, 1, len(ratios)),
                 bend=lambda tt: 1 - 0.035 * (1 - np.exp(-tt / 0.35)), attacks=0.002 + 0.006 * ratios)
    x += pan(0.5 * gong, 0)
    x += pan(1.0 * sat(glide(t, 110, 30, 0.1) * env(t, 0.5, 0.002) * 1.2, 2.2), 0)
    # the drums: a roll under the firecrackers, landing on a last big hit
    x += pan(1.2 * drum(t, 0.0, 56, decay=0.7), 0)
    for i, at in enumerate([0.55, 0.8, 1.0, 1.15, 1.27, 1.37, 1.46]):
        x += pan(0.55 * drum(t, at, 66 + i * 1.5, decay=0.2), (-0.3, 0.3)[i % 2])
    x += pan(1.2 * drum(t, 1.62, 54, decay=0.8), 0)
    x += pan(0.8 * sub(t, 1.62, 95, 30, decay=0.7), 0)
    x += pan(0.5 * filt(noise(len(t)), "highpass", 1800) * env(t, 0.008, 0.0002), 0)
    # the firecrackers: a quick irregular string, left to right and back
    at = 0.18
    i = 0
    while at < 1.9:
        x += pan(firecracker(t, at, rng.uniform(0.35, 0.8)), np.sin(i * 0.7) * 0.8)
        at += rng.uniform(0.018, 0.075)
        i += 1
    for i, k in enumerate([10, 9, 8, 7, 6, 5, 6, 7, 8, 10, 12]):
        x += pan(0.3 * bell(t, note(k), start=0.25 + i * 0.09), -0.7 + (i % 5) * 0.35)
    return fade(hall(x, 2.6, 0.42, seed=17, tone=5500), 1.0)


def reward():
    t = times(0.8)
    x = lu.coin(t, note(10), 0.0, 0.8) + 0.45 * bell(t, note(8), start=0.004)
    # the paper under it, tapped
    x += 0.4 * filt(noise(len(t)), "lowpass", 2600) * env(t, 0.018, 0.0006)
    x += 0.35 * glide(t, 220, 90, 0.01) * env(t, 0.05, 0.001)
    x += 0.6 * drum(t, 0.0, 82, decay=0.14)
    return fade(hall(pan(x, 0), 1.0, 0.22, seed=19), 0.3)


# ---------------------------------------------------------------- before it opens

CHARGE = {0: 1.4, 1: 2.0, 2: 2.8}          # EnvelopeOpening's charge per rarity, seconds


def idle():
    """a loop: a low bowl drone breathing, air moving, a far chime now and then. drawn long and
    crossfaded into itself so it loops without a seam"""
    loop, xf = 3.2, 0.8
    t = times(loop + xf)
    x = np.zeros((len(t), 2))
    breathe = 0.75 + 0.25 * np.sin(2 * np.pi * t / 1.6)
    for f, p, a in [(146.83, -0.3, 1.0), (147.33, 0.3, 1.0), (220.0, 0.0, 0.45), (293.66, 0.1, 0.18)]:
        x += pan(a * np.sin(2 * np.pi * f * t + rng.uniform(0, 6.28)) * breathe, p)
    air = swept(noise(len(t)), lambda s_: 900 + 500 * np.sin(2 * np.pi * s_ / 1.6), q=0.7)
    x += pan(0.12 * air, 0)
    for at, k in [(0.5, 12), (1.9, 10), (2.7, 13)]:
        x += pan(0.12 * bell(t, note(k), start=at), rng.uniform(-0.6, 0.6))
    x = hall(x, 2.2, 0.35, seed=31, tone=5000)
    # the tail folded back over the head
    n, m = int(loop * SR), int(xf * SR)
    w = np.linspace(0, 1, m)[:, None]
    out = x[:n].copy()
    out[:m] = out[:m] * np.sqrt(w) + x[n:n + m] * np.sqrt(1 - w)
    return out


def click():
    """opening it: the seal knocked with a knuckle of stone, the paper snapping taut, a bright ping"""
    t = times(0.9)
    x = np.zeros((len(t), 2))
    knock = modal(t, np.array([520, 1310, 2150, 3400]), [0.05, 0.03, 0.018, 0.01], [1, 0.7, 0.45, 0.25])
    x += pan(0.8 * knock, 0)
    x += pan(0.9 * drum(t, 0.0, 74, decay=0.16), 0)
    x += pan(0.5 * filt(noise(len(t)), "highpass", 3500) * env(t, 0.01, 0.0003), 0)
    x += pan(0.35 * bell(t, note(10), start=0.01), 0.2)
    return fade(hall(x, 1.0, 0.25, seed=33), 0.3)


def charge(level):
    """the build before the burst, exactly as long as the charge: everything rising together and
    faster, then a breath drawn in and a dead cut, so the burst lands out of silence"""
    L = CHARGE[level]
    t = times(L)
    u = t / L
    x = np.zeros((len(t), 2))
    cut = L - 0.07                        # the silence before the hit
    live = (t < cut).astype(float)

    # the drum roll: two hands, far apart at first, then a flurry, louder and a touch higher
    at, i = 0.0, 0
    while at < cut - 0.03:
        k = at / L
        x += pan(drum(t, at, 56 + 16 * k, level=0.3 + 0.95 * k ** 1.2, decay=0.1 + 0.14 * (1 - k)), (-0.25, 0.25)[i % 2])
        at += 0.3 * (1 - k) ** 1.7 + 0.038
        i += 1

    # the envelope rattling in the hands: paper crinkling faster and coins knocking inside
    grains = np.zeros_like(t)
    k = 0.0
    while k < cut:
        grains += env(t, rng.uniform(0.005, 0.015), 0.0004, start=k) * rng.uniform(0.3, 1.0) * (0.3 + 0.7 * k / L)
        k += rng.exponential(1 / (10 + 70 * (k / L) ** 1.5))
    x += pan(0.45 * filt(noise(len(t)), "bandpass", [1400, 6500]) * grains, -0.2)
    for _ in range(int(8 + 10 * level)):
        c = rng.uniform(0, 1) ** 0.6 * (cut - 0.05)
        x += pan(lu.coin(t, note(int(rng.integers(7, 12))), c, 0.12 + 0.25 * c / L), rng.uniform(-0.6, 0.6))

    # a reversed cymbal: a band of noise opening upward, swelling to the cut
    swell = swept(noise(len(t)), lambda s_: 300 + (4500 + 2500 * level) * (s_ / L) ** 1.6, q=0.8)
    swell += 0.5 * filt(noise(len(t)), "highpass", 5000)
    x += pan(0.34 * swell * u ** 2.6, 0)

    # a reversed gong under it: struck at the end and played backwards, so it blooms into the cut
    base = 146.8 / 1.5
    ratios = np.sort(np.concatenate([[1, 1.52, 2.05, 2.61, 3.14], rng.uniform(3.3, 26, 20)]))
    gong = modal(t, base * ratios, 1.6 / (1 + ratios * 0.09), 1 / np.sqrt(ratios) * rng.uniform(0.6, 1, len(ratios)))
    gong = gong[::-1] * np.clip(u / 0.3, 0, 1)
    x += pan((0.25 + 0.15 * level) * gong, 0)

    # the singing bowl climbing (an octave, more for rarer ones), its tremble quickening
    f = 146.83 * 2 ** (u ** 1.5 * (1 + 0.5 * level))
    trem = 0.55 + 0.45 * np.sin(2 * np.pi * np.cumsum(4 + 18 * u ** 2) / SR)
    bowl = (np.sin(2 * np.pi * np.cumsum(f) / SR) + 0.35 * np.sin(2 * np.pi * np.cumsum(f * 2.71) / SR)) * trem * u ** 1.4
    x += pan(0.22 * bowl, 0.1)

    # pressure: a sub on D and A, bending up, growing
    pressure = (np.sin(2 * np.pi * np.cumsum(36.7 * (1 + 0.12 * u)) / SR) + 0.6 * np.sin(2 * np.pi * np.cumsum(55 * (1 + 0.12 * u)) / SR))
    x += pan(0.6 * sat(pressure * u ** 2, 1.4), 0)

    # a fuse sizzling down to the firecrackers the burst sets off
    if level >= 1:
        start = 0.45 * L if level == 1 else 0.3 * L
        fuse = filt(noise(len(t)), "bandpass", [2500, 9000])
        crackle = np.zeros_like(t)
        for _ in range(int(120 * (L - start))):
            c = int(rng.uniform(start, cut) * SR)
            crackle[c] += rng.uniform(0.3, 1.0)
        crackle = sg.fftconvolve(crackle, np.exp(-np.arange(int(0.003 * SR)) / (0.0006 * SR)))[:len(t)]
        fuse *= np.clip((t - start) / 0.2, 0, 1) * (0.35 + 1.4 * crackle) * (0.4 + 0.6 * u)
        x += pan(0.16 * fuse, 0.3)

    # legendary: a low brass drone, D and A, its mouth opening as it rises
    if level == 2:
        saw = lambda fr: 2 * ((np.cumsum(fr) / SR) % 1) - 1
        brass = saw(73.4 * (1 + 0.06 * u)) + 0.8 * saw(110 * (1 + 0.06 * u)) + 0.5 * saw(146.8 * (1 + 0.06 * u))
        brass = swept(brass, lambda s_: 250 + 2400 * (s_ / L) ** 2, q=0.9, kind="low")
        x += pan(0.28 * brass * np.clip((u - 0.25) / 0.5, 0, 1) ** 1.5, 0)

    # the breath before the burst: air rushing in, sucked up to the cut
    inhale = swept(noise(len(t)), lambda s_: 400 + 6000 * np.clip((s_ - (cut - 0.28)) / 0.28, 0, 1) ** 2, q=0.7)
    x += pan(0.55 * inhale * np.clip((t - (cut - 0.28)) / 0.28, 0, 1) ** 2.2, 0)

    x = hall(x, 1.4, 0.25, seed=41 + level, tone=6000)[:len(t)]
    # dead: everything gone for the last beat, a 4 ms fade so it doesn't click
    g = np.clip((cut - t) / 0.004, 0, 1)
    return x * g[:, None]


# ---------------------------------------------------------------- the Wuchang

def chain():
    t = times(1.3)
    x = np.zeros((len(t), 2))
    # thrown: links rattling out, faster then slower, across the field
    at = 0.0
    for i in range(22):
        x += pan(metal_link(t, at, rng.uniform(0.3, 0.8)), -0.7 + 1.4 * i / 21)
        at += 0.012 + 0.02 * (i / 21) ** 2
    # the drag of it through the air
    x += pan(0.35 * swept(noise(len(t)), lambda s: 2500 + 3000 * np.exp(-s / 0.1), q=2.0) * env(t, 0.18, 0.01), 0)
    # the shackle catching: a heavy clank, low and long, and the chain snapping taut
    hit = 0.42
    clank = modal(t, np.array([310, 690, 1180, 1920, 2750]) * 1.0, [0.35, 0.22, 0.14, 0.08, 0.05], [1, 0.7, 0.5, 0.35, 0.2])
    clank = np.roll(clank, int(hit * SR)); clank[:int(hit * SR)] = 0
    x += pan(0.9 * clank, 0.2)
    x += pan(0.8 * sat(glide(np.maximum(t - hit, 0), 130, 45, 0.02) * env(t, 0.12, 0.002, start=hit), 1.8), 0.2)
    for i in range(6):
        x += pan(metal_link(t, hit + 0.02 + i * 0.03, 0.5, low=0.8), 0.2 + rng.uniform(-0.2, 0.2))
    return fade(hall(x, 1.6, 0.35, seed=23, tone=5000), 0.4)


def swallow():
    t = times(3.4)
    x = np.zeros((len(t), 2))
    # a breath drawn in: noise swelling up and closing in to a dark whoosh
    u = np.clip(t / 0.9, 0, 1)
    breath = swept(noise(len(t)), lambda s: 300 + 2200 * np.clip(s / 0.9, 0, 1) ** 2, q=0.8) * (u ** 2) * (t < 0.95)
    x += pan(0.7 * breath, 0)
    # a moan under it: a buzzing voice through two formants, sinking
    f = 110 * (1 - 0.25 * np.clip(t / 1.4, 0, 1))
    saw = 2 * ((np.cumsum(f) / SR) % 1) - 1
    moan = swept(saw, lambda s: 700 - 250 * np.clip(s / 1.4, 0, 1), q=5) + 0.6 * swept(saw, lambda s: 1150 - 350 * np.clip(s / 1.4, 0, 1), q=6)
    moan *= np.clip(t / 0.4, 0, 1) * np.exp(-np.maximum(t - 0.6, 0) / 0.45) * (0.7 + 0.3 * np.sin(2 * np.pi * 5 * t))
    x += pan(0.25 * moan, -0.15)
    # the dark closing: a gulp, a sub drop
    gulp = 0.95
    x += pan(1.0 * sat(glide(np.maximum(t - gulp, 0), 90, 28, 0.08) * env(t, 0.5, 0.003, start=gulp) * 1.3, 2.4), 0)
    x += pan(0.5 * filt(brown(len(t)), "lowpass", 140, order=4) * env(t, 0.6, 0.05, start=gulp), 0)
    # a temple bell tolled once under the ink, low and long
    toll = 1.35
    base = 98.0
    ratios = np.array([0.5, 1, 1.19, 1.5, 2.0, 2.52, 3.01, 4.1])
    b = modal(np.maximum(t - toll, 0), base * ratios, [4.0, 3.2, 2.2, 1.8, 1.2, 0.8, 0.6, 0.35], [0.8, 1, 0.5, 0.45, 0.35, 0.25, 0.18, 0.1])
    x += pan(0.55 * b * (t >= toll), 0)
    return fade(hall(x, 3.0, 0.45, seed=29, tone=4200, predelay=0.03), 1.0)


# ---------------------------------------------------------------- write

def write(path, x):
    x = np.clip(x, -1, 1)
    if x.ndim == 1: x = x[:, None]
    tpdf = (rng.uniform(-1, 1, x.shape) + rng.uniform(-1, 1, x.shape)) / 32768
    pcm = np.clip(np.round((x + tpdf) * 32767), -32768, 32767).astype("<i2")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(x.shape[1])
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


def level(x, offset_db=0.0):
    """to the game's reference loudness (plus an offset for the ones meant to sit under others),
    peaks limited"""
    y = x if x.ndim == 2 else x[:, None]
    y = y * 10 ** ((nz.REFERENCE + offset_db - nz.loudest(y)) / 20)
    y = nz.limit(y, nz.CEILING)
    return y if x.ndim == 2 else y[:, 0]


def main():
    clips = {
        "fe_pickup": level(pickup(), -1.0),
        "fe_idle": level(idle(), -9.0),
        "fe_click": level(click(), -2.0),
        "fe_charge_common": level(charge(0), -5.0),
        "fe_charge_rare": level(charge(1), -4.0),
        "fe_charge_legendary": level(charge(2), -3.0),
        "fe_shake": level(shake(), -3.0),
        "fe_tier": level(tier(), 0.0),
        "fe_open": level(open_flap(), 0.5),
        "fe_reveal_common": level(reveal(0), 0.5),
        "fe_reveal_rare": level(reveal(1), 1.5),
        "fe_reveal_legendary": level(reveal(2), 2.0),
        "fe_reward": level(reward(), -3.0),
        "wc_chain": level(chain(), 0.0),
        "wc_swallow": level(swallow(), 0.5),
    }
    for name, x in clips.items():
        write(os.path.join(DEST, name + ".wav"), x)
        print(f"{name}: {len(x) / SR:.2f}s, {'stereo' if x.ndim == 2 else 'mono'}, loudest 100ms {nz.loudest(x if x.ndim == 2 else x[:, None]):.1f} dB")

    # a legendary opening on EnvelopeOpening's timeline, to listen to
    def st(x): return x if x.ndim == 2 else np.stack([x, x], axis=1)
    timeline = [("fe_shake", 0.54, 1.0), ("fe_tier", 0.54 + 2.3 * 0.42, 1.22), ("fe_tier", 0.54 + 2.3 * 0.74 + 0.2, 1.34),
                ("fe_open", 3.24, 1.0), ("fe_reveal_legendary", 3.24, 1.0), ("fe_shake", 4.36, 1.3)]
    for i in range(5): timeline.append(("fe_reward", 4.96 + i * 0.32 + (0.23 if i > 0 else 0), 1 + i * 0.08))
    timeline.append(("fe_reveal_legendary", 4.96, 1.1))
    total = np.zeros((int(9.5 * SR), 2))
    for name, at, pitch in timeline:
        x = st(clips[name])
        if pitch != 1.0:
            n = int(len(x) / pitch)
            x = np.stack([np.interp(np.arange(n) * pitch, np.arange(len(x)), x[:, c]) for c in range(2)], axis=1)
        s = int(at * SR)
        total[s:s + len(x)] += x[:len(total) - s]
    total = nz.limit(total * 0.7, nz.CEILING)
    write(os.path.join(ct.OUT, "fortune_legendary.wav"), total)
    print("out/fortune_legendary.wav: a legendary opening on its timeline")


if __name__ == "__main__":
    main()
