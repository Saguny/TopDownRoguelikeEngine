"""An enemy dying, sound designed and synthesised from scratch. it plays hundreds of times a run,
often a dozen at once, so it's short, punchy and soft at the top, never shrill: the body pops, it
bursts into a puff, and the spirit slips out as a faint breath rising. it suits the bloody death
and the bloodless one (motes of qi) alike. the game varies its pitch a little every time
(EnemyHealth), so a crowd dying doesn't sound like one sound repeating

python enemy_death.py writes Assets/### Different Engine/Sounds/Enemies/enemy_death.wav (mono,
48 kHz, 16 bit), its loudest 100 ms at the game's reference loudness.
"""
import os

import numpy as np

import command_token as ct
from command_token import SR, times, env, glide, noise, filt, swept, sat, reverb, fade_out

rng = np.random.default_rng(1618)
DEST = os.path.join(ct.HERE, "..", "..", "Assets", "### Different Engine", "Sounds", "Enemies")


def enemy_death():
    t = times(0.42)

    # the pop: a blip of body dropping in pitch, a little saturated so it's felt
    pop = sat(glide(t, 420, 120, 0.012) * env(t, 0.05, 0.0012) * 1.3, 1.8)

    # the burst: a puff of air, and a wet smack whose brightness falls away at once
    puff = filt(noise(len(t)), "bandpass", [280, 2400]) * env(t, 0.06, 0.002)
    smack = swept(noise(len(t)), lambda s: 4200 * np.exp(-s / 0.025) + 500, q=0.8, kind="low") * env(t, 0.045, 0.001)

    # the spirit leaving: a breath rising, with the faintest tone in it
    u = np.clip((t - 0.03) / 0.3, 0, 1)
    breath = swept(noise(len(t)), lambda s: 1400 * 3.2 ** np.clip((s - 0.03) / 0.3, 0, 1), q=2.2)
    breath *= (t >= 0.03) * np.sin(np.pi * u) ** 1.5
    tone = np.sin(2 * np.pi * np.cumsum(880 * 1.9 ** u) / SR) * (t >= 0.03) * np.sin(np.pi * u) ** 2

    mix = 1.0 * pop + 0.55 * puff + 0.45 * smack + 0.2 * breath + 0.035 * tone
    mix = reverb(mix, 0.45, 0.14, predelay=0.006, tone=5000, seed=51)[:len(t)]
    return fade_out(mix, 0.12)


def main():
    import normalize as nz
    x = enemy_death()[:, None]
    x *= 10 ** ((nz.REFERENCE - nz.loudest(x)) / 20)
    x = nz.limit(x, nz.CEILING)
    ct.write(os.path.join(DEST, "enemy_death.wav"), x[:, 0])
    np.save(os.path.join(ct.OUT, "enemy_death.npy"), x[:, 0])
    print(f"enemy_death.wav: {len(x) / SR:.2f}s, loudest 100ms {nz.loudest(x):.1f} dB, peak {20 * np.log10(nz.true_peak(x)):.1f} dBTP")


if __name__ == "__main__":
    main()
