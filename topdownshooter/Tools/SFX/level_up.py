"""The level up, sound designed and synthesised from scratch: the wen raining behind the menu as it
flies in (LevelUpTransition: the panel lands 0.34s after it opens). kept light, since it comes
often: a few coins run ahead of the panel, a flurry as it lands with one soft bronze bell under it,
then the rest trickling down, lower as they fall, thinning out. the coins ring on the notes of the
D major pentatonic (the Chinese gong-shang-jue-zhi-yu), so a flurry is never a clash

python level_up.py writes Assets/### Different Engine/Sounds/LevelUp/level_up.wav (stereo, 48 kHz,
16 bit: the level up plays in 2D), its loudest 100 ms at the game's reference loudness.
"""
import os
import wave

import numpy as np

import command_token as ct
from command_token import SR, times, noise, filt, reverb, fade_out, bell

rng = np.random.default_rng(3141)
DEST = os.path.join(ct.HERE, "..", "..", "Assets", "### Different Engine", "Sounds", "LevelUp")
LAND = 0.33

D6 = 1174.66
PENTA = [1, 9 / 8, 5 / 4, 3 / 2, 5 / 3]            # D E F# A B


def note(i):
    """the i-th note of the D major pentatonic from D6 up"""
    return D6 * PENTA[i % 5] * 2 ** (i // 5)


def pan(x, p):
    """equal power, p from -1 (left) to 1 (right)"""
    a = (p + 1) * np.pi / 4
    return np.stack([x * np.cos(a), x * np.sin(a)], axis=1)


def coin(t, f, start, level):
    """a bronze cash coin landing: a click, then a few inharmonic modes ringing briefly, the bright
    ones gone first. a light second bounce a moment after, most of the time"""
    out = np.zeros_like(t)
    bounces = [(0.0, 1.0)] + ([(rng.uniform(0.05, 0.09), rng.uniform(0.25, 0.4))] if rng.random() < 0.7 else [])
    for delay, k in bounces:
        u = t - start - delay
        live = u >= 0
        u = np.maximum(u, 0)
        ring = sum(a * np.sin(2 * np.pi * f * r * u + rng.uniform(0, 6.28)) * np.exp(-u / d)
                   for r, d, a in [(1, 0.2, 1), (2.43, 0.11, 0.55), (3.92, 0.06, 0.32), (5.61, 0.035, 0.18)])
        click = filt(noise(len(t)), "bandpass", [3000, 11000]) * np.exp(-u / 0.0015) * 0.5
        out += k * (ring * np.clip(u / 0.0008, 0, 1) + click) * live
    return level * out


def level_up():
    t = times(2.4)
    mix = np.zeros((len(t), 2))

    # when each coin lands: a few ahead of the panel, most as it lands, the rest trickling down
    lands = list(LAND - rng.uniform(0.05, 0.22, 4))
    lands += list(LAND + rng.gamma(1.6, 0.06, 9))
    lands += list(LAND + 0.25 + rng.uniform(0, 1) ** 1.4 * 1.4 for _ in range(10))
    for at in sorted(lands):
        late = np.clip((at - LAND) / 1.6, 0, 1)
        # higher while the flurry is thick, lower as the last ones fall
        i = int(rng.integers(3, 9)) - int(round(4 * late))
        level = rng.uniform(0.5, 1.0) * (1 - 0.55 * late) * (0.6 if at < LAND else 1.0)
        mix += pan(coin(t, note(max(0, i)), at, level), rng.uniform(-0.75, 0.75))

    # the one soft bell under the landing, so it still reads as a reward
    mix += pan(bell(t, D6 / 2, start=LAND) * 0.35, 0.0)

    wet = np.stack([reverb(mix[:, 0], 1.6, 0.25, predelay=0.015, tone=7000, seed=41)[:len(t)],
                    reverb(mix[:, 1], 1.6, 0.25, predelay=0.018, tone=7000, seed=43)[:len(t)]], axis=1)
    return np.stack([fade_out(wet[:, 0], 0.5), fade_out(wet[:, 1], 0.5)], axis=1)


def write_stereo(path, x):
    x = np.clip(x, -1, 1)
    tpdf = (rng.uniform(-1, 1, x.shape) + rng.uniform(-1, 1, x.shape)) / 32768
    pcm = np.clip(np.round((x + tpdf) * 32767), -32768, 32767).astype("<i2")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


def main():
    import normalize as nz
    x = level_up()
    x *= 10 ** ((nz.REFERENCE - nz.loudest(x)) / 20)
    x = nz.limit(x, nz.CEILING)
    write_stereo(os.path.join(DEST, "level_up.wav"), x)
    np.save(os.path.join(ct.OUT, "level_up.npy"), x.mean(axis=1))
    print(f"level_up.wav: {len(x) / SR:.2f}s, loudest 100ms {nz.loudest(x):.1f} dB, peak {20 * np.log10(nz.true_peak(x)):.1f} dBTP")


if __name__ == "__main__":
    main()
