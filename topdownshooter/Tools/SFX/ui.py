"""menu sounds, synthesised from scratch (no samples)

  ui_locked   a locked thing clicked (a map not earned yet): a dull wooden knock and a low buzz,
              twice, quick, the second a little lower. a no, not an error

python ui.py writes them into Assets/### Different Engine/Resources/Sfx/ at the game's reference
loudness (normalize.py), plus out/ui.wav to listen to them
"""
import os

import numpy as np

import command_token as ct
from command_token import SR, times, env, noise, filt, sat
from fortune import pan, hall, fade, write, level
import normalize as nz

DEST = os.path.join(ct.HERE, "..", "..", "Assets", "### Different Engine", "Resources", "Sfx")


def buzz(t, at, f, length):
    """a low buzz: two detuned saw-ish tones through a saturator, boxed in, clipped short"""
    k = np.clip((t - at) / length, 0, 1)
    gate = np.where((t >= at) & (t < at + length), 1.0, 0.0) * (1 - k) ** 0.6
    ph = 2 * np.pi * f * np.clip(t - at, 0, None)
    tone = sum(np.sin(ph * d * n) / n for d in (1.0, 1.012) for n in range(1, 7))
    tone = filt(sat(tone * 0.6, 2.2), "lowpass", 1400)
    return tone * gate * env(t, 0.5, 0.004, at)


def knock(t, at, f):
    """a dull wooden knock: a short damped tone and a tick of noise"""
    body = np.sin(2 * np.pi * f * np.clip(t - at, 0, None)) * env(t, 0.045, 0.001, at)
    tick = filt(noise(len(t)), "bandpass", [1200, 2600]) * env(t, 0.012, 0.0005, at) * 0.6
    return body + tick


def ui_locked():
    t = times(0.45)
    x = knock(t, 0.0, 190) + buzz(t, 0.0, 92, 0.085) * 0.8
    x += knock(t, 0.11, 160) + buzz(t, 0.11, 82, 0.11) * 0.8
    return hall(fade(pan(x, 0.0), 0.12), 0.35, 0.12, 131)


SOUNDS = {"ui_locked": (ui_locked, -6.0)}


def main():
    reel = []
    for name, (make, offset) in SOUNDS.items():
        x = level(make(), offset)
        write(os.path.join(DEST, name + ".wav"), x)
        print(f"{name:12s} {len(x) / SR:.2f}s, loudest 100ms {nz.loudest(x):.1f} dB")
        reel.append(x if x.ndim == 2 else np.stack([x, x], axis=1))
    write(os.path.join(ct.OUT, "ui.wav"), nz.limit(np.concatenate(reel) * 0.8, nz.CEILING))


if __name__ == "__main__":
    main()
