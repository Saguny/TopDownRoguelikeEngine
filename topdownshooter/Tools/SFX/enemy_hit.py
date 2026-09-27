"""An enemy taking a hit, sound designed and synthesised from scratch as the small sibling of
enemy_death.py: the same pitched body and air, shorter and drier, with no burst and no spirit
leaving, so a hit reads as a lighter version of a kill. it plays constantly (every arrow, every
tick of the aura), so it's tiny: a knock of body dropping in pitch, a crisp tick on top, a breath
of air, gone in about 80 ms. the game varies its pitch a little every time (EnemyHealth)

python enemy_hit.py writes Assets/### Different Engine/Sounds/Enemies/enemy_hit.wav (mono, 48 kHz,
16 bit), its loudest 100 ms at the game's reference loudness.
"""
import os

import numpy as np

import command_token as ct
from command_token import SR, times, env, glide, noise, filt, swept, sat, fade_out

rng = np.random.default_rng(1414)
DEST = os.path.join(ct.HERE, "..", "..", "Assets", "### Different Engine", "Sounds", "Enemies")


def enemy_hit():
    t = times(0.16)
    # the knock: the death's pop, higher and shorter
    knock = sat(glide(t, 520, 210, 0.008) * env(t, 0.028, 0.0008) * 1.2, 1.6)
    # the tick of the blow landing
    tick = filt(noise(len(t)), "bandpass", [2200, 7500]) * env(t, 0.0035, 0.0002)
    # a breath of air knocked out, its brightness falling away
    air = swept(noise(len(t)), lambda s: 3200 * np.exp(-s / 0.02) + 700, q=0.9, kind="low") * env(t, 0.03, 0.001)
    mix = 1.0 * knock + 0.4 * tick + 0.35 * air
    return fade_out(mix, 0.06)


def main():
    import normalize as nz
    x = enemy_hit()[:, None]
    x *= 10 ** ((nz.REFERENCE - nz.loudest(x)) / 20)
    x = nz.limit(x, nz.CEILING)
    ct.write(os.path.join(DEST, "enemy_hit.wav"), x[:, 0])
    np.save(os.path.join(ct.OUT, "enemy_hit.npy"), x[:, 0])
    print(f"enemy_hit.wav: {len(x) / SR:.2f}s, loudest 100ms {nz.loudest(x):.1f} dB, peak {20 * np.log10(nz.true_peak(x)):.1f} dBTP")


if __name__ == "__main__":
    main()
