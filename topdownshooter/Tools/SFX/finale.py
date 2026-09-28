"""The end of a Final Rush, the coins swept in after it, the Electrical Aura's pulse and the Seven
Star Swords' evolved stars, sound designed and synthesised from scratch (no samples) with the
Command Token's, the envelope's and the boss's instruments.

  rush_clear     the spirit seal's wave rolling out and sealing the horde (2.4 s): a temple drum and
                 a sub drop, the opera gong, a whoosh opening out across the field, and bells
                 climbing the scale over it
  rush_seal      one enemy sealed as the front passes it: a paper stamp slapped down and a small
                 bell (played for each, faster and higher as the wave goes, see SealWave)
  rush_bell      a Final Rush formation coming: the priest's hand bell that leads the dead
  coins_total    the last of the swept coins in: a cascade of coins and a bright bell (cha-ching)
  aura_pulse_1   the Electrical Aura's pulse: a snap of static, a low electric thump and a short
  aura_pulse_2   buzz, three takes so a pulse every half second never repeats itself exactly
  aura_pulse_3
  star_shoot_1   the Seven Star Swords' evolved stars bursting off the swords: soft, since it
  star_shoot_2   plays every burst all run; a breath of air, a gentle glint, a faint falling tone,
  star_shoot_3   three takes
  star_hit       a star striking: a small glassy twinkle

python finale.py writes them into Assets/### Different Engine/Resources/Sfx/ (the game loads them
from there), each at the game's reference loudness (normalize.py), plus out/finale.wav to listen to.
"""
import os

import numpy as np

import command_token as ct
from command_token import SR, times, env, glide, modal, noise, brown, filt, swept, sat, reverb, fade_out, bell
import fortune as fo
from fortune import pan, hall, fade, drum, sub, write, level, firecracker
import level_up as lu
from boss import whoosh, crackle
import normalize as nz

rng = np.random.default_rng(5150)
DEST = os.path.join(ct.HERE, "..", "..", "Assets", "### Different Engine", "Resources", "Sfx")


def gong(t, at, f0=98.0, level=1.0, decay=2.4):
    """the opera gong: inharmonic modes that bloom and shimmer"""
    u = np.maximum(t - at, 0)
    ratios = np.array([1, 1.48, 2.02, 2.76, 3.52, 4.61])
    x = modal(u, f0 * ratios, [decay, decay * 0.8, decay * 0.55, decay * 0.4, decay * 0.3, decay * 0.2], [1, 0.7, 0.55, 0.4, 0.3, 0.18])
    bloom = 1 + 0.35 * np.clip(u / 0.25, 0, 1) * np.exp(-u / 1.2)        # it swells a moment after the hit
    return level * x * bloom * (t >= at)


def rush_clear():
    t = times(2.8)
    x = np.zeros((len(t), 2))
    # the hit: drum, sub, gong, a crack of light
    x += pan(1.0 * drum(t, 0.0, 48, decay=0.5) + 0.9 * sub(t, 0.0, 110, 30, decay=0.7), 0)
    x += pan(0.55 * gong(t, 0.02, 98.0), -0.15) + pan(0.35 * gong(t, 0.025, 147.0, decay=1.8), 0.15)
    x += pan(0.6 * filt(noise(len(t)), "highpass", 2500) * env(t, 0.012, 0.0003), 0)
    # the wave opening out across the field: a whoosh sweeping down and spreading wide
    for side in (-1, 1):
        w = whoosh(t, 0.02, 0.9, 3200, 400, q=0.8)
        spread = np.clip(t / 0.8, 0, 1)
        x += np.stack([w * (0.5 + 0.5 * spread * (-side)), w * (0.5 + 0.5 * spread * side)], axis=1) * 0.35
    # the horde sealed as it passes: paper and fire crackling outward
    x += pan(0.3 * crackle(t, 0.1, 0.8, rate=70), -0.5) + pan(0.3 * crackle(t, 0.12, 0.8, rate=70), 0.5)
    # bells climbing the scale over it, and a last high one
    for i, at in enumerate([0.06, 0.16, 0.26, 0.36, 0.46, 0.56]):
        x += pan(0.2 * bell(t, fo.note(5 + i), start=at), -0.6 + 0.24 * i)
    x += pan(0.22 * bell(t, fo.note(12), start=0.7), 0)
    return fade(hall(x, 2.4, 0.3, seed=111, tone=5000), 0.8)


def rush_seal():
    t = times(0.45)
    x = np.zeros((len(t), 2))
    # a paper seal slapped down: a dull knock, the paper's snap
    x += pan(0.9 * glide(t, 420, 170, 0.01) * env(t, 0.03, 0.0008), 0)
    x += pan(0.5 * filt(noise(len(t)), "bandpass", [900, 4500]) * env(t, 0.018, 0.0004), 0)
    x += pan(0.3 * filt(noise(len(t)), "lowpass", 600) * env(t, 0.04, 0.001), 0)
    # and a small bell over it
    x += pan(0.3 * bell(t, fo.note(9), start=0.004), 0.1)
    return fade(hall(x, 0.5, 0.12, seed=113), 0.15)


def rush_bell():
    """the Taoist priest's hand bell that leads the dead: brass, rung three times in a quick shake,
    each a little softer, its clapper ticking against the rim, then ringing on"""
    t = times(1.6)
    x = np.zeros((len(t), 2))
    ratios = np.array([1.0, 2.32, 4.25, 6.63, 9.4])
    for k, (at, lv) in enumerate([(0.0, 1.0), (0.11, 0.75), (0.21, 0.6)]):
        u = np.maximum(t - at, 0)
        f0 = 1180.0 * (1 + 0.004 * k)
        ring = modal(u, f0 * ratios, [0.9, 0.55, 0.3, 0.18, 0.1], [1.0, 0.55, 0.35, 0.2, 0.1]) * (t >= at)
        tick = filt(noise(len(t)), "bandpass", [3000, 9000]) * env(t, 0.004, 0.0002, start=at)
        x += pan(0.45 * lv * ring + 0.25 * lv * tick, -0.15 + 0.15 * k)
    return fade(hall(x, 1.2, 0.3, seed=131, tone=7000), 0.4)


def coins_total():
    t = times(1.4)
    x = np.zeros((len(t), 2))
    at = 0.0
    for i in range(12):                                   # the cascade, faster and higher
        x += pan(lu.coin(t, fo.note(4 + i // 2, 587.33) * 2, at, 0.28), rng.uniform(-0.6, 0.6))
        at += 0.045 * (1 - i / 16)
    x += pan(0.5 * bell(t, fo.note(10), start=at + 0.02) + 0.35 * bell(t, fo.note(12), start=at + 0.05), 0)   # cha-ching
    x += pan(0.3 * drum(t, at + 0.02, 90, decay=0.12), 0)
    return fade(hall(x, 1.2, 0.2, seed=117), 0.4)


def aura_pulse(seed):
    r = np.random.default_rng(seed)
    t = times(0.4)
    x = np.zeros((len(t), 2))
    # a snap of static, a low electric thump, a short buzz dying
    x += pan(0.7 * filt(noise(len(t)), "highpass", 3000) * env(t, 0.006, 0.0002), 0)
    x += pan(0.6 * sat(glide(t, 180 + r.uniform(-20, 20), 70, 0.02) * env(t, 0.07, 0.001), 1.8), 0)
    f = 110 * r.uniform(0.95, 1.05)
    buzz = 2 * ((np.cumsum(np.full(len(t), f)) / SR) % 1) - 1
    buzz = filt(buzz, "bandpass", [600, 3200]) * env(t, 0.09, 0.002) * (0.6 + 0.4 * np.sign(np.sin(2 * np.pi * r.uniform(28, 40) * t)))
    x += pan(0.35 * buzz, r.uniform(-0.3, 0.3))
    # crackles off the ring
    for k in range(4):
        at = r.uniform(0.0, 0.12)
        x += pan(0.25 * filt(noise(len(t)), "bandpass", [2500, 8000]) * env(t, r.uniform(0.004, 0.01), 0.0002, start=at), r.uniform(-0.7, 0.7))
    return fade(hall(x, 0.5, 0.1, seed=seed), 0.12)


def star_shoot(seed):
    """soft: this plays every burst, every second and a bit, all run. a breath of air swelling and
    gone, one gentle glint, and a faint falling tone under it. no bright bells, no whistle"""
    r = np.random.default_rng(seed)
    t = times(0.6)
    x = np.zeros((len(t), 2))
    air = filt(noise(len(t)), "bandpass", [900, 3800]) * env(t, 0.16, 0.035)
    x += pan(0.5 * air, r.uniform(-0.3, 0.3))
    x += pan(0.22 * bell(t, fo.note(int(r.integers(5, 9))), start=0.02), r.uniform(-0.4, 0.4))
    fall = 520 + 700 * np.exp(-t / 0.08)
    x += pan(0.18 * np.sin(2 * np.pi * np.cumsum(fall) / SR) * env(t, 0.14, 0.01), 0)
    return fade(hall(x, 0.7, 0.2, seed=121 + seed, tone=4500), 0.25)


def star_hit():
    t = times(0.4)
    x = np.zeros((len(t), 2))
    x += pan(0.5 * bell(t, fo.note(14), start=0.0) + 0.25 * bell(t, fo.note(16), start=0.008), 0)
    x += pan(0.3 * filt(noise(len(t)), "highpass", 5000) * env(t, 0.012, 0.0003), 0)
    return fade(hall(x, 0.5, 0.15, seed=123, tone=7000), 0.12)


def main():
    clips = {
        "rush_clear": level(rush_clear(), 1.5),
        "rush_seal": level(rush_seal(), -4.0),
        "coins_total": level(coins_total(), -1.0),
        "rush_bell": level(rush_bell(), -4.0),
        "aura_pulse_1": level(aura_pulse(1), -5.0),
        "aura_pulse_2": level(aura_pulse(2), -5.0),
        "aura_pulse_3": level(aura_pulse(3), -5.0),
        "star_shoot_1": level(star_shoot(1), -10.0),
        "star_shoot_2": level(star_shoot(2), -10.0),
        "star_shoot_3": level(star_shoot(3), -10.0),
        "star_hit": level(star_hit(), -7.0),
    }
    for name, x in clips.items():
        write(os.path.join(DEST, name + ".wav"), x)
        print(f"{name}: {len(x) / SR:.2f}s, loudest 100ms {nz.loudest(x):.1f} dB")

    # the wave, the seals going off as it passes (faster and higher), then the coins
    total = np.zeros((int(6.5 * SR), 2))
    def put(name, at, pitch=1.0, gain=1.0):
        x = clips[name]
        if pitch != 1.0:
            n = int(len(x) / pitch)
            x = np.stack([np.interp(np.arange(n) * pitch, np.arange(len(x)), x[:, c]) for c in range(2)], axis=1)
        s = int(at * SR)
        if s >= len(total):
            return
        x = x[:len(total) - s]
        total[s:s + len(x)] += x * gain
    put("rush_clear", 0.0)
    for i in range(24):
        put("rush_seal", 0.05 + 0.9 * (i / 24) ** 1.4, 0.9 + 0.6 * i / 24, 0.6)
    put("coins_total", 2.3)
    for i in range(6): put("aura_pulse_%d" % (1 + i % 3), 3.2 + i * 0.4 / 1, 1.0, 0.8)
    total = nz.limit(total * 0.7, nz.CEILING)
    write(os.path.join(ct.OUT, "finale.wav"), total)
    s2 = np.zeros((int(3 * SR), 2))
    for i in range(3):
        x = clips["star_shoot_%d" % (1 + i % 3)]; st = int(i * 0.9 * SR); s2[st:st + len(x)] += x[:len(s2) - st]
        for k in range(3):
            y = clips["star_hit"]; st2 = st + int((0.35 + k * 0.08) * SR); s2[st2:st2 + len(y)] += y[:len(s2) - st2] * 0.8
    write(os.path.join(ct.OUT, "stars.wav"), nz.limit(s2 * 0.8, nz.CEILING))
    print("out/finale.wav, out/stars.wav")


if __name__ == "__main__":
    main()
