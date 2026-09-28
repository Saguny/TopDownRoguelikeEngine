"""Yama, King of Hell: his fight, sound designed and synthesised from scratch (no samples) with the
Command Token's, the envelope's and the Magistrate's instruments (command_token, fortune, boss,
finale). the danmaku's small sounds are Touhou's in spirit: a dry tick for a shot, a hiss for a
graze, a falling chirp for a hit.

  his entrance
    yama_toll        a great temple bell, struck three times as he comes (the game drops its pitch each time)
    yama_rumble      the earth shuddering under the horde as it sinks away
    yama_gate        the ground cracking open into the gate of hell, fire roaring up out of it
    yama_roar        his voice: a chorus of low, buzzing throats with a gong and a drum under it
    yama_bar         his health bar filling: a riser climbing into a bell
  his fight
    yama_declare     a spell card declared: power drawn in, then a bright ring and the gong
    yama_page        the ledger's pages thrown: paper fluttering on a gust
    yama_court       a court's verdict: a gavel on wood and a bronze bowl
    yama_dash        him gliding to a new place
    yama_shot        a volley of small bullets: a dry tick
    yama_shot_big    a big orb: a soft low thump
    yama_graze       a bullet grazed: a quick hiss and glint
    yama_hit         him being hit: a light tick, over and over as the player's fire lands
    yama_player_hit  the player hit: a chirp falling away, and a crunch
    yama_cancel      the bullets cancelled: glitter sweeping up
    yama_break       a phase broken: a blast, the gong, glass shattering
    yama_bonus       a spell card cleared unhurt: a fanfare of bells and coins
  his end
    yama_death_cry   his last roar, falling away
    yama_blast       one of the blasts going off in him
    yama_death       the end: a blast of light, the gong, the sub dropping out, a choir
    yama_victory     the run won: bells climbing into a bright chord

python yama.py writes them into Assets/### Different Engine/Resources/Sfx/ (the game loads them
from there), each at the game's reference loudness (normalize.py), plus out/yama.wav, the fight
in brief, to listen to.
"""
import os

import numpy as np

import command_token as ct
from command_token import SR, times, env, glide, modal, noise, brown, filt, swept, sat, bell
import fortune as fo
from fortune import pan, hall, fade, drum, sub, write, level, rustle
import level_up as lu
from boss import whoosh, crackle, roar, voice, bowl, stone, grit, thunder
from finale import gong
import normalize as nz

rng = np.random.default_rng(6660)
DEST = os.path.join(ct.HERE, "..", "..", "Assets", "### Different Engine", "Resources", "Sfx")


def square(t, f):
    ph = np.cumsum(np.broadcast_to(f, t.shape)) / SR
    return np.sign(np.sin(2 * np.pi * ph))


# ---------------------------------------------------------------- his entrance

def toll():
    t = times(4.5)
    x = np.zeros((len(t), 2))
    # the strike: a wooden beam on bronze, then the bell's long hum and its beating partials
    x += pan(0.5 * filt(noise(len(t)), "bandpass", [200, 1800]) * env(t, 0.03, 0.001), 0)
    x += pan(0.9 * bowl(t, 58.0, decay=4.0) + 0.5 * bowl(t, 58.0 * 1.19, decay=3.0), -0.1)
    x += pan(0.45 * modal(t, 58.0 * np.array([2.0, 2.76, 3.9, 5.4, 6.8]), [3.0, 2.2, 1.4, 0.9, 0.6], [0.6, 0.45, 0.3, 0.2, 0.12]), 0.15)
    x += pan(0.9 * sub(t, 0.0, 70, 40, decay=1.2), 0)
    return fade(hall(x, 4.0, 0.4, seed=601, tone=3500), 1.2)


def rumble():
    t = times(3.2)
    x = np.zeros((len(t), 2))
    x += pan(1.0 * thunder(t, 0.0, 2.8), 0)
    x += pan(0.35 * grit(t, 0.2, 2.2), -0.4) + pan(0.35 * grit(t, 0.3, 2.2), 0.4)
    x += pan(0.5 * filt(brown(len(t)), "lowpass", 90) * 6 * env(t, 1.4, 0.4), 0)
    return fade(hall(x, 2.0, 0.25, seed=603, tone=2500), 0.8)


def gate():
    t = times(3.4)
    x = np.zeros((len(t), 2))
    for k, at in enumerate([0.0, 0.12, 0.3, 0.5, 0.75]):
        x += pan(0.6 * stone(t, at, 1.0 - k * 0.12), -0.6 + k * 0.3)
    x += pan(0.9 * sub(t, 0.05, 120, 28, decay=0.9) + 0.8 * drum(t, 0.0, 44, decay=0.6), 0)
    # the pit opening: air dragged down into it, then fire roaring up
    x += pan(0.5 * whoosh(t, 0.1, 1.2, 2400, 180, q=0.9), 0)
    fire = roar(t, 0.6, 2.6, f=180) * np.clip((t - 0.6) / 0.8, 0, 1) * np.exp(-np.maximum(t - 1.8, 0) / 1.0)
    x += pan(0.6 * fire, -0.2) + pan(0.35 * crackle(t, 0.7, 2.2, rate=55), 0.3)
    return fade(hall(x, 2.6, 0.3, seed=605, tone=3000), 0.8)


def roar_voice(t, start, length, f0, bend, level=1.0):
    """a chorus of throats, a little apart in pitch and time, growling through an 'ah'"""
    x = np.zeros_like(t)
    for k, (m, d) in enumerate([(1.0, 0.0), (1.012, 0.02), (0.5, 0.01), (1.5, 0.03), (0.995, 0.015)]):
        x += voice(t, start + d, length, f0 * m, formants=(650, 1080), bend=bend, level=0.5 if m != 1.0 else 0.7)
    grain = filt(noise(len(t)), "bandpass", [300, 2400]) * (0.6 + 0.4 * np.sin(2 * np.pi * 37 * t))
    u = np.clip((t - start) / length, 0, 1)
    x += 0.35 * grain * np.sin(np.pi * u) ** 0.6 * ((t >= start) & (t < start + length))
    return level * sat(x * 1.6, 2.2)


def roar_():
    t = times(3.4)
    x = np.zeros((len(t), 2))
    x += pan(1.0 * roar_voice(t, 0.05, 2.1, 62.0, bend=-0.25), 0)
    x += pan(0.6 * roar(t, 0.05, 2.0, f=140), 0)
    x += pan(0.8 * drum(t, 0.0, 46, decay=0.7) + 0.8 * sub(t, 0.0, 100, 30, decay=0.9), 0)
    x += pan(0.5 * gong(t, 0.02, 82.0, decay=2.8), -0.2) + pan(0.35 * gong(t, 0.03, 123.0, decay=2.0), 0.2)
    return fade(hall(x, 3.0, 0.35, seed=607, tone=4000), 1.0)


def bar_fill():
    t = times(1.8)
    x = np.zeros((len(t), 2))
    rise = swept(noise(len(t)), lambda s: 300 + 5000 * np.clip(s / 1.1, 0, 1) ** 2, q=2.5) * np.clip(t / 1.1, 0, 1) ** 2 * (t < 1.15)
    x += pan(0.35 * rise, 0)
    for i, at in enumerate(np.linspace(0.1, 1.0, 8)):
        x += pan(0.14 * bell(t, fo.note(i, 293.66), start=at), -0.7 + i * 0.2)
    x += pan(0.4 * bell(t, fo.note(10, 293.66), start=1.12) + 0.25 * bell(t, fo.note(12, 293.66), start=1.14), 0)
    x += pan(0.5 * drum(t, 1.12, 70, decay=0.2), 0)
    return fade(hall(x, 1.6, 0.3, seed=609, tone=6000), 0.5)


# ---------------------------------------------------------------- his fight

def declare():
    t = times(2.6)
    x = np.zeros((len(t), 2))
    # power drawn in: a reversed swell of noise and a rising whine
    pull = swept(noise(len(t)), lambda s: 400 + 6000 * np.clip(s / 0.9, 0, 1) ** 1.5, q=3) * np.clip(t / 0.9, 0, 1) ** 3 * (t < 0.92)
    x += pan(0.5 * pull, 0)
    whine = np.sin(2 * np.pi * np.cumsum(300 + 1500 * np.clip(t / 0.9, 0, 1) ** 2) / SR) * np.clip(t / 0.9, 0, 1) ** 2 * (t < 0.92)
    x += pan(0.2 * whine, 0.2)
    # the card: a bright ring, the gong, a crack of light
    at = 0.92
    x += pan(0.5 * bell(t, 1760.0, start=at) + 0.35 * bell(t, 2637.0, start=at + 0.01) + 0.25 * bell(t, 3520.0, start=at + 0.02), 0)
    x += pan(0.6 * gong(t, at, 98.0, decay=2.2), -0.2) + pan(0.4 * gong(t, at + 0.01, 147.0, decay=1.6), 0.2)
    x += pan(0.5 * filt(noise(len(t)), "highpass", 3000) * env(t, 0.02, 0.0003, start=at), 0)
    x += pan(0.7 * drum(t, at, 55, decay=0.4), 0)
    return fade(hall(x, 2.4, 0.35, seed=611, tone=6500), 0.7)


def page():
    t = times(0.8)
    x = np.zeros((len(t), 2))
    x += pan(0.8 * rustle(t, 0.0, 0.45, rate=40, bright=1.2), -0.2)
    x += pan(0.5 * whoosh(t, 0.0, 0.5, 1800, 600, q=1.0), 0.2)
    x += pan(0.3 * filt(noise(len(t)), "bandpass", [2000, 7000]) * env(t, 0.015, 0.0005), 0)
    return fade(hall(x, 0.8, 0.2, seed=613), 0.25)


def court():
    t = times(1.2)
    x = np.zeros((len(t), 2))
    # a gavel on hard wood, then a bronze bowl ringing
    x += pan(0.9 * modal(t, np.array([820.0, 1640.0, 2390.0]), [0.05, 0.03, 0.02], [1.0, 0.4, 0.2]), 0)
    x += pan(0.5 * filt(noise(len(t)), "bandpass", [500, 3000]) * env(t, 0.01, 0.0002), 0)
    x += pan(0.45 * bowl(t, 311.0, start=0.005, decay=0.9), 0.2)
    x += pan(0.4 * drum(t, 0.0, 90, decay=0.12), 0)
    return fade(hall(x, 1.0, 0.25, seed=615), 0.35)


def dash():
    t = times(0.6)
    x = np.zeros((len(t), 2))
    for side in (-1, 1):
        w = whoosh(t, 0.0, 0.45, 1400, 3200, q=1.4)
        x += np.stack([w * (0.5 - 0.4 * side * np.clip(t / 0.4, 0, 1)), w * (0.5 + 0.4 * side * np.clip(t / 0.4, 0, 1))], axis=1) * 0.4
    x += pan(0.25 * roar(t, 0.0, 0.4, f=260) * env(t, 0.2, 0.05), 0)
    return fade(hall(x, 0.6, 0.15, seed=617), 0.2)


def shot():
    t = times(0.18)
    x = np.zeros((len(t), 2))
    # Touhou's tan: a dry tick with a pitched blip in it
    x += pan(0.7 * filt(noise(len(t)), "bandpass", [1200, 6000]) * env(t, 0.012, 0.0002), 0)
    x += pan(0.5 * glide(t, 1100, 520, 0.01) * env(t, 0.025, 0.0005), 0)
    return fade(x, 0.05)


def shot_big():
    t = times(0.45)
    x = np.zeros((len(t), 2))
    x += pan(0.9 * glide(t, 220, 60, 0.05) * env(t, 0.12, 0.002), 0)
    x += pan(0.4 * filt(noise(len(t)), "lowpass", 1400) * env(t, 0.06, 0.002), 0)
    x += pan(0.2 * filt(noise(len(t)), "highpass", 4000) * env(t, 0.01, 0.0003), 0)
    return fade(hall(x, 0.4, 0.1, seed=619), 0.12)


def graze():
    t = times(0.14)
    x = np.zeros((len(t), 2))
    x += pan(0.6 * filt(noise(len(t)), "highpass", 5000) * env(t, 0.03, 0.001), 0)
    x += pan(0.3 * np.sin(2 * np.pi * 3700 * t) * env(t, 0.02, 0.0005), 0)
    return fade(x, 0.04)


def hit():
    t = times(0.09)
    x = np.zeros((len(t), 2))
    x += pan(0.6 * filt(noise(len(t)), "bandpass", [1800, 5200]) * env(t, 0.008, 0.0002), 0)
    x += pan(0.35 * square(t, 1250.0) * env(t, 0.012, 0.0003), 0)
    return fade(x, 0.03)


def player_hit():
    t = times(0.7)
    x = np.zeros((len(t), 2))
    # the falling chirp, square and bright, and the crunch under it
    f = 160 + 1340 * np.exp(-t / 0.12)
    x += pan(0.35 * filt(square(t, f), "lowpass", 5000) * env(t, 0.3, 0.001), 0)
    x += pan(0.6 * sat(filt(noise(len(t)), "bandpass", [200, 2500]) * env(t, 0.06, 0.001), 2.0), 0)
    x += pan(0.6 * sub(t, 0.0, 120, 45, decay=0.25), 0)
    return fade(hall(x, 0.6, 0.15, seed=621), 0.2)


def cancel():
    t = times(1.2)
    x = np.zeros((len(t), 2))
    for k in range(18):
        at = k * 0.03
        x += pan(0.1 * bell(t, fo.note(4 + k, 587.33), start=at), rng.uniform(-0.9, 0.9))
    x += pan(0.3 * swept(noise(len(t)), lambda s: 2000 + 8000 * np.clip(s / 0.6, 0, 1), q=2) * env(t, 0.6, 0.01), 0)
    return fade(hall(x, 1.2, 0.3, seed=623, tone=8000), 0.4)


def break_():
    t = times(2.8)
    x = np.zeros((len(t), 2))
    x += pan(1.0 * drum(t, 0.0, 40, decay=0.6) + 0.9 * sub(t, 0.0, 110, 26, decay=0.9), 0)
    blast = filt(noise(len(t)), "lowpass", 2600) * env(t, 0.35, 0.002)
    x += pan(0.8 * sat(blast, 1.8), 0)
    x += pan(0.5 * gong(t, 0.02, 92.0, decay=2.4), -0.2) + pan(0.35 * gong(t, 0.03, 138.0, decay=1.8), 0.2)
    # glass shattering, bright shards scattering either side
    for k in range(22):
        at = 0.02 + rng.exponential(0.08)
        f0 = rng.uniform(2500, 7000)
        x += pan(0.12 * modal(np.maximum(t - at, 0), f0 * np.array([1, 1.47, 2.3]), [0.12, 0.08, 0.05], [1, 0.5, 0.3]) * (t >= at), rng.uniform(-0.9, 0.9))
    x += pan(0.35 * crackle(t, 0.05, 0.8, rate=80), 0)
    return fade(hall(x, 2.6, 0.35, seed=625, tone=6000), 0.8)


def bonus():
    t = times(2.2)
    x = np.zeros((len(t), 2))
    seq = [0, 2, 4, 5, 7, 9, 10]
    for i, n in enumerate(seq):
        at = i * 0.075
        x += pan(0.3 * bell(t, fo.note(n, 587.33), start=at), -0.6 + i * 0.2)
    at = len(seq) * 0.075 + 0.05
    for n, lv in ((10, 0.45), (12, 0.35), (14, 0.3)):
        x += pan(lv * bell(t, fo.note(n, 587.33), start=at), 0)
    for k in range(10):
        x += pan(lu.coin(t, fo.note(6 + k % 5, 587.33) * 2, at + k * 0.04, 0.2), rng.uniform(-0.7, 0.7))
    x += pan(0.4 * drum(t, at, 80, decay=0.2), 0)
    return fade(hall(x, 1.8, 0.3, seed=627, tone=7000), 0.6)


# ---------------------------------------------------------------- his end

def death_cry():
    t = times(4.0)
    x = np.zeros((len(t), 2))
    x += pan(1.0 * roar_voice(t, 0.0, 3.2, 70.0, bend=-0.55), 0)
    x += pan(0.5 * roar(t, 0.0, 3.0, f=120) * np.exp(-t / 1.6), 0)
    x += pan(0.4 * thunder(t, 0.2, 3.0), 0)
    return fade(hall(x, 3.2, 0.4, seed=629, tone=3000), 1.2)


def blast():
    t = times(1.0)
    x = np.zeros((len(t), 2))
    x += pan(0.8 * sat(filt(noise(len(t)), "lowpass", 3000) * env(t, 0.18, 0.002), 1.6), 0)
    x += pan(0.7 * sub(t, 0.0, 140, 40, decay=0.3) + 0.5 * drum(t, 0.0, 60, decay=0.25), 0)
    x += pan(0.3 * crackle(t, 0.02, 0.5, rate=70), 0)
    return fade(hall(x, 0.9, 0.2, seed=631), 0.3)


def death():
    t = times(6.0)
    x = np.zeros((len(t), 2))
    # a breath of silence drawn in, then everything at once
    at = 0.35
    suck = swept(noise(len(t)), lambda s: 5000 - 4600 * np.clip(s / at, 0, 1), q=2) * np.clip(t / at, 0, 1) ** 2 * (t < at)
    x += pan(0.4 * suck, 0)
    x += pan(1.0 * drum(t, at, 36, decay=1.0) + 1.0 * sub(t, at, 120, 22, decay=1.6), 0)
    x += pan(0.9 * sat(filt(noise(len(t)), "lowpass", 3500) * env(t, 0.8, 0.003, start=at), 1.5), 0)
    x += pan(0.6 * gong(t, at, 73.0, decay=4.0), -0.25) + pan(0.45 * gong(t, at + 0.02, 110.0, decay=3.2), 0.25)
    x += pan(0.4 * crackle(t, at, 1.5, rate=60), -0.4) + pan(0.4 * crackle(t, at + 0.05, 1.5, rate=60), 0.4)
    # the choir after it: an open chord of voices swelling and fading
    for f0, p in ((146.8, -0.4), (220.0, 0.0), (293.7, 0.4), (440.0, 0.1)):
        x += pan(0.18 * voice(t, at + 0.8, 4.2, f0, formants=(700, 1150)), p)
    for i, n in enumerate([0, 4, 7, 9]):
        x += pan(0.2 * bell(t, fo.note(n, 293.66), start=at + 1.0 + i * 0.25), -0.5 + i * 0.33)
    return fade(hall(x, 4.5, 0.45, seed=633, tone=5000), 1.8)


def victory():
    t = times(3.4)
    x = np.zeros((len(t), 2))
    for i, n in enumerate([0, 2, 4, 7, 9, 12]):
        x += pan(0.28 * bell(t, fo.note(n, 440.0), start=i * 0.11), -0.7 + i * 0.28)
    at = 0.8
    for n, lv in ((12, 0.4), (14, 0.35), (16, 0.3), (19, 0.25)):
        x += pan(lv * bell(t, fo.note(n, 440.0), start=at), 0)
    x += pan(0.5 * bowl(t, 220.0, start=at, decay=2.4), 0) + pan(0.35 * gong(t, at, 110.0, decay=2.5), 0)
    return fade(hall(x, 3.0, 0.4, seed=635, tone=7000), 1.0)


def main():
    clips = {
        "yama_toll": level(toll(), 1.5),
        "yama_rumble": level(rumble(), -2.0),
        "yama_gate": level(gate(), 0.5),
        "yama_roar": level(roar_(), 2.0),
        "yama_bar": level(bar_fill(), -3.0),
        "yama_declare": level(declare(), 0.5),
        "yama_page": level(page(), -5.0),
        "yama_court": level(court(), -4.0),
        "yama_dash": level(dash(), -5.0),
        "yama_shot": level(shot(), -9.0),
        "yama_shot_big": level(shot_big(), -6.0),
        "yama_graze": level(graze(), -9.0),
        "yama_hit": level(hit(), -10.0),
        "yama_player_hit": level(player_hit(), -1.0),
        "yama_cancel": level(cancel(), -3.0),
        "yama_break": level(break_(), 1.0),
        "yama_bonus": level(bonus(), -1.0),
        "yama_death_cry": level(death_cry(), 1.5),
        "yama_blast": level(blast(), -2.0),
        "yama_death": level(death(), 2.0),
        "yama_victory": level(victory(), 0.0),
    }
    for name, x in clips.items():
        write(os.path.join(DEST, name + ".wav"), x)
        print(f"{name}: {len(x) / SR:.2f}s, loudest 100ms {nz.loudest(x):.1f} dB")

    # the fight in brief: tolls, the gate, the roar, a card declared, a few volleys and grazes,
    # a hit, the break and the bonus, then his end and the win
    total = np.zeros((int(26 * SR), 2))

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

    put("yama_toll", 0.0); put("yama_rumble", 0.0)
    put("yama_toll", 1.2, 0.94); put("yama_gate", 1.2)
    put("yama_toll", 2.4, 0.88)
    put("yama_roar", 4.0)
    put("yama_bar", 7.0)
    put("yama_declare", 8.6)
    for i in range(24):
        put("yama_shot", 10.2 + i * 0.12, 1 + 0.05 * (i % 3), 0.8)
        if i % 4 == 1: put("yama_graze", 10.25 + i * 0.12, 1.0, 0.8)
        if i % 2 == 0: put("yama_hit", 10.3 + i * 0.12, 1.0, 0.8)
    put("yama_page", 10.2); put("yama_page", 11.9)
    put("yama_shot_big", 12.4)
    put("yama_player_hit", 13.0)
    put("yama_break", 14.2); put("yama_cancel", 14.3)
    put("yama_bonus", 15.0)
    put("yama_death_cry", 17.0)
    for k in range(10): put("yama_blast", 17.3 + k * (0.3 - k * 0.02), 0.9 + 0.03 * k, 0.8)
    put("yama_death", 19.6)
    put("yama_victory", 23.0)
    total = nz.limit(total * 0.8, nz.CEILING)
    write(os.path.join(ct.OUT, "yama.wav"), total)
    print("out/yama.wav")


if __name__ == "__main__":
    main()
