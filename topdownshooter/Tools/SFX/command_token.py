"""The Command Token's cast, sound designed and synthesised from scratch (no samples).

The cast animation (Animations/VFX/CommandTokenCast.anim) plays five sounds:
  0.317s  ct_stamp_1   the first seal slams down
  0.567s  ct_stamp_2   the second
  0.817s  ct_stamp_3   the third, carrying a riser that builds through the charge (the camera's
                       small shakes at 1.1-1.5s get a sub pulse each) and cuts dead at 1.55s
  1.55s   ct_blast     the seals explode: an opera gong crash, a sub boom, a fire burst
  1.6s    ct_shockwave the shockwave crosses the screen: an air whoosh, a rumble, talisman sparks

A stamp is a seal of stone on paper: a pitched-down thump, a knock of stone and wood, the slap of
paper, and a small bronze bell. The bells climb D-F-A (a D minor chord) so the tension rises seal
by seal, and the gong resolves it an octave and a half under the first.

python command_token.py writes the five clips (mono, 48 kHz, 16 bit, like the project's other
sounds) into Assets/### Different Engine/Sounds/CommandToken/, plus out/command_token_cast.wav:
the whole cast mixed on its timeline, to listen to outside Unity.
"""
import os
import wave

import numpy as np
import scipy.signal as sg

SR = 48000
HERE = os.path.dirname(os.path.abspath(__file__))
DEST = os.path.join(HERE, "..", "..", "Assets", "### Different Engine", "Sounds", "CommandToken")
OUT = os.path.join(HERE, "out")

rng = np.random.default_rng(1609)

# the cast's timeline (see the animation's events)
STAMPS = [0.317, 0.567, 0.817]
BLAST, SHOCK = 1.55, 1.6
SHAKES = [1.1, 1.2, 1.3, 1.4, 1.5]
BELLS = [587.33, 698.46, 880.0]            # D5 F5 A5


# ---------------------------------------------------------------- building blocks

def times(seconds):
    return np.arange(int(seconds * SR)) / SR


def env(t, decay, attack=0.001, start=0.0):
    """an attack ramp, then an exponential decay, starting at `start`"""
    u = t - start
    a = np.clip(u / attack, 0, 1) if attack > 0 else (u >= 0).astype(float)
    return np.where(u >= 0, a * np.exp(-np.maximum(u, 0) / decay), 0.0)


def glide(t, f0, f1, tau):
    """a sine whose pitch falls (or rises) from f0 to f1 with time constant tau"""
    f = f1 + (f0 - f1) * np.exp(-t / tau)
    return np.sin(2 * np.pi * np.cumsum(f) / SR)


def modal(t, freqs, decays, amps, bend=None, attacks=None):
    """struck metal, stone or wood: damped sines at the object's modes"""
    out = np.zeros_like(t)
    for i, (f, d, a) in enumerate(zip(freqs, decays, amps)):
        fr = f * (bend(t) if bend is not None else 1.0)
        phase = 2 * np.pi * np.cumsum(np.broadcast_to(fr, t.shape)) / SR + rng.uniform(0, 2 * np.pi)
        att = attacks[i] if attacks is not None else 0.0005
        out += a * np.sin(phase) * env(t, d, att)
    return out


def noise(n):
    return rng.standard_normal(n)


def brown(n):
    b = np.cumsum(noise(n))
    b = sg.sosfilt(sg.butter(1, 20, "hp", fs=SR, output="sos"), b)
    return b / (np.abs(b).max() + 1e-9)


def filt(x, kind, f, order=2):
    return sg.sosfilt(sg.butter(order, f, kind, fs=SR, output="sos"), x)


def swept(x, fc, q=1.4, kind="band", block=64):
    """a biquad whose centre (or cutoff) follows fc(t), block by block, keeping its state"""
    y = np.zeros_like(x)
    zi = np.zeros(2)
    for s in range(0, len(x), block):
        f = float(np.clip(fc((s + block / 2) / SR), 20, SR * 0.45))
        w0 = 2 * np.pi * f / SR
        alpha = np.sin(w0) / (2 * q)
        cw = np.cos(w0)
        if kind == "band":
            b = np.array([alpha, 0, -alpha])
        else:  # lowpass
            b = np.array([(1 - cw) / 2, 1 - cw, (1 - cw) / 2])
        a = np.array([1 + alpha, -2 * cw, 1 - alpha])
        y[s:s + block], zi = sg.lfilter(b / a[0], a / a[0], x[s:s + block], zi=zi)
    return y


def sat(x, drive):
    """tape-ish saturation: weight without clipping"""
    return np.tanh(drive * x) / np.tanh(drive)


def reverb(x, rt, wet, predelay=0.012, tone=5500, seed=0):
    """a hall: a few early reflections and a dense tail that darkens as it decays"""
    r = np.random.default_rng(seed)
    n = int(rt * SR)
    t = np.arange(n) / SR
    tail = r.standard_normal(n) * np.exp(-6.9 * t / rt)
    tail = filt(tail, "lowpass", tone)
    tail = filt(tail, "highpass", 90)
    ir = np.zeros(n + int(predelay * SR))
    ir[int(predelay * SR):] = tail
    for k in range(6):   # early reflections
        ir[int((0.004 + 0.007 * k + r.uniform(0, 0.004)) * SR)] += r.uniform(0.15, 0.35) * (0.8 ** k)
    ir /= np.sqrt((ir ** 2).sum())
    w = sg.fftconvolve(x, ir)[:len(x) + len(ir) - 1]
    dry = np.concatenate([x, np.zeros(len(w) - len(x))])
    return dry + wet * w


def fade_out(x, seconds):
    n = min(len(x), int(seconds * SR))
    x = x.copy()
    x[-n:] *= np.linspace(1, 0, n) ** 2
    return x


def trim(x, seconds):
    return x[:int(seconds * SR)]


def peak_to(x, db):
    return x / (np.abs(x).max() + 1e-12) * 10 ** (db / 20)


def bell(t, f0, start=0.0, level=1.0):
    """a small bronze bell: inharmonic partials, two copies a hair apart so it beats"""
    ratios, decays, amps = [1, 2.32, 4.25, 6.63, 9.4], [1.1, 0.6, 0.32, 0.18, 0.1], [1, 0.5, 0.3, 0.18, 0.1]
    u = np.maximum(t - start, 0)
    out = np.zeros_like(t)
    for detune in (0.0, 0.9):
        for r_, d, a in zip(ratios, decays, amps):
            out += a * np.sin(2 * np.pi * (f0 * r_ + detune) * u + rng.uniform(0, 6.28)) * np.exp(-u / d)
    return level * out * np.clip(u / 0.0015, 0, 1) * (t >= start)


# ---------------------------------------------------------------- the seal stamps

def stamp(k):
    last = k == 2
    length = (BLAST - STAMPS[2]) if last else 1.3
    t = times(length + (0 if last else 0.0))

    # the seal's weight: a thump that drops in pitch, a little higher each seal
    thump = glide(t, 200 + 25 * k, 52 + 7 * k, 0.016) * env(t, 0.11, 0.0015)
    thump = sat(thump * 1.4, 1.9)
    low = filt(noise(len(t)), "lowpass", 160) * env(t, 0.07, 0.002)

    # stone and wood knocking together
    kf = np.array([410, 1080, 1790, 2860, 4030]) * (1 + 0.07 * k)
    knock = modal(t, kf, [0.05, 0.03, 0.017, 0.01, 0.006], [1, 0.72, 0.46, 0.3, 0.18])
    knock += filt(noise(len(t)), "bandpass", [1600, 7000]) * env(t, 0.004, 0.0003) * 0.9

    # the slap of the paper under it
    paper = filt(noise(len(t)), "lowpass", 2400) * env(t, 0.03, 0.0008)
    paper += filt(noise(len(t)), "highpass", 5000) * env(t, 0.012, 0.0005) * 0.35

    # and the bell, a hair late: the seal's power answering
    ring = bell(t, BELLS[k], start=0.006, level=1.0)

    mix = 1.0 * thump + 0.35 * low + 0.55 * knock + 0.32 * paper + 0.16 * ring
    mix = reverb(mix, 1.2, 0.3, seed=k)[:len(t)]

    if last:
        mix += riser(t)
        # the dead cut before the blast: 4ms of fade, then nothing
        n = int(0.004 * SR)
        mix[-n:] *= np.linspace(1, 0, n)
    else:
        mix = fade_out(mix, 0.35)
    return mix


def riser(t):
    """from the third seal to the blast: a reversed gong swelling up, a bowl tone climbing an
    octave and trembling faster, and a sub pulse under each of the camera's small shakes"""
    start, end = 0.12, BLAST - STAMPS[2]
    u = np.clip((t - start) / (end - start), 0, 1)
    live = (t >= start).astype(float)

    swell = swept(noise(len(t)), lambda s: 350 * (12 ** np.clip((s - start) / (end - start), 0, 1)), q=0.9)
    swell += 0.5 * swept(noise(len(t)), lambda s: 1800 * (4 ** np.clip((s - start) / (end - start), 0, 1)), q=2.5)
    swell *= live * u ** 2.6

    # the bowl: D4 gliding up to D5, trembling from 6 to 20 times a second
    f = 293.66 * (2 ** (u ** 1.6))
    trem = 0.6 + 0.4 * np.sin(2 * np.pi * np.cumsum(6 + 14 * u ** 2) / SR)
    bowl = (np.sin(2 * np.pi * np.cumsum(f) / SR) + 0.35 * np.sin(2 * np.pi * np.cumsum(f * 2.71) / SR)) * trem
    bowl *= live * u ** 1.8

    pulses = np.zeros_like(t)
    for i, s in enumerate(SHAKES):
        at = s - STAMPS[2]
        pulses += glide(np.maximum(t - at, 0), 95, 42, 0.02) * env(t, 0.07, 0.002, start=at) * (0.5 + 0.12 * i)

    return 0.55 * swell + 0.2 * bowl + 0.55 * pulses


# ---------------------------------------------------------------- the blast

def blast():
    t = times(3.4)

    crack = filt(noise(len(t)), "highpass", 1800) * env(t, 0.007, 0.0002)
    crack += filt(noise(len(t)), "bandpass", [300, 3000]) * env(t, 0.02, 0.0005) * 0.6

    sub = sat(glide(t, 110, 30, 0.1) * env(t, 0.6, 0.002) * 1.2, 2.4)
    body = filt(noise(len(t)), "lowpass", 220, order=4) * env(t, 0.28, 0.003)

    # a fire burst: crackling noise, its brightness falling away
    crackle = np.zeros(len(t))
    for _ in range(220):
        at = int(rng.uniform(0, 0.9) ** 1.8 * SR)
        if at < len(t):
            crackle[at] += rng.uniform(0.4, 1.0)
    crackle = sg.fftconvolve(crackle, np.exp(-np.arange(int(0.004 * SR)) / (0.0008 * SR)))[:len(t)]
    fire = swept(noise(len(t)) * (0.6 + 1.6 * crackle), lambda s: 7500 * np.exp(-s / 0.35) + 450, q=0.8, kind="low")
    fire *= env(t, 0.5, 0.004)

    # the opera gong (da luo): a dense, dark spectrum whose high partials bloom in a moment after
    # the strike, the whole pitch sinking a little ("wahhh")
    base = 146.8 / 1.5    # an octave and a half under the first bell's D, near G2
    ratios = np.sort(np.concatenate([[1, 1.52, 2.05, 2.61, 3.14], rng.uniform(3.3, 38, 36)]))
    decays = 2.8 / (1 + ratios * 0.09)
    amps = 1 / np.sqrt(ratios) * rng.uniform(0.6, 1.0, len(ratios))
    attacks = 0.002 + 0.006 * ratios
    gong = modal(t, base * ratios, decays, amps, bend=lambda tt: 1 - 0.035 * (1 - np.exp(-tt / 0.35)), attacks=attacks)
    gong += filt(noise(len(t)), "bandpass", [700, 9000]) * env(t, 0.18, 0.002) * 0.35   # the gong's hiss

    mix = 0.55 * crack + 1.0 * sub + 0.55 * body + 0.45 * fire + 0.42 * gong
    mix = reverb(mix, 2.6, 0.45, predelay=0.02, tone=4500, seed=11)[:len(t)]
    return fade_out(mix, 0.9)


# ---------------------------------------------------------------- the shockwave

def shockwave():
    t = times(2.2)

    # air pushed across the screen: a band of noise sweeping up as the wave leaves and down as it
    # passes, the same an octave up a moment later for width
    def centre(s):
        return 260 + 3000 * np.sin(np.clip(s / 0.6, 0, 1) * np.pi) ** 1.5 + 500 * np.exp(-s / 0.1)
    whoosh = swept(noise(len(t)), centre, q=1.1)
    whoosh += 0.45 * swept(noise(len(t)), lambda s: 2 * centre(max(0.0, s - 0.03)), q=1.6)
    shape = np.clip(t / 0.05, 0, 1) * np.where(t < 0.3, 1, np.exp(-(t - 0.3) / 0.28))
    whoosh *= shape

    rumble = filt(brown(len(t)), "lowpass", 110, order=4) * env(t, 0.9, 0.02)

    # sparks of the talismans' fire, scattered through the wave and thinning out
    sparks = np.zeros_like(t)
    for _ in range(46):
        at = 0.06 + rng.uniform(0, 1) ** 1.7 * 1.0
        f = rng.uniform(2600, 7200)
        d = rng.uniform(0.05, 0.16)
        sparks += rng.uniform(0.3, 1.0) * np.sin(2 * np.pi * f * np.maximum(t - at, 0)) * env(t, d, 0.0008, start=at)

    mix = 0.75 * whoosh + 0.9 * rumble + 0.08 * sparks
    mix = reverb(mix, 1.9, 0.45, predelay=0.015, seed=21)[:len(t)]
    return fade_out(mix, 0.6)


# ---------------------------------------------------------------- write

def write(path, x):
    x = np.clip(x, -1, 1)
    tpdf = (rng.uniform(-1, 1, len(x)) + rng.uniform(-1, 1, len(x))) / 32768
    pcm = np.clip(np.round((x + tpdf) * 32767), -32768, 32767).astype("<i2")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


def main():
    clips = {
        # levels relative to each other (the stamps climbing, the blast on top), the blast's
        # loudest 100 ms at the game's reference loudness (normalize.py)
        "ct_stamp_1": peak_to(stamp(0), -7.7),
        "ct_stamp_2": peak_to(stamp(1), -7.2),
        "ct_stamp_3": peak_to(stamp(2), -6.7),
        "ct_blast": peak_to(blast(), -5.2),
        "ct_shockwave": peak_to(shockwave(), -8.2),
    }
    for name, x in clips.items():
        write(os.path.join(DEST, name + ".wav"), x)
        rms = 20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-12)
        print(f"{name}: {len(x) / SR:.2f}s, peak {20 * np.log10(np.abs(x).max()):.1f} dB, rms {rms:.1f} dB")

    # the whole cast on its timeline, to listen to
    at = dict(zip(clips, STAMPS + [BLAST, SHOCK]))
    total = np.zeros(int((SHOCK + 2.4) * SR))
    for name, x in clips.items():
        s = int(at[name] * SR)
        total[s:s + len(x)] += x[:len(total) - s]
    write(os.path.join(OUT, "command_token_cast.wav"), peak_to(total, -1.0))
    np.save(os.path.join(OUT, "command_token_cast.npy"), total)
    print("out/command_token_cast.wav: the whole cast")


if __name__ == "__main__":
    main()
