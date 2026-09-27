"""Brings sound effects to one reference loudness, so their levels in the game are set by the
volume on whatever plays them (an enemy's Hit Volume, a pickup's Pickup Volume...) and not by how
loud each file happened to be exported.

Loudness is the loudest 100 ms of the clip (short sounds are too short for LUFS gating), brought to
REFERENCE; a look-ahead limiter then keeps the peaks, checked 4x oversampled so no decoder can
clip them, at or under CEILING.

    python normalize.py <in> <out> [--mono] [<in> <out> [--mono] ...]

ffmpeg reads anything and writes the output by its extension (.ogg is Vorbis quality 7, .wav 16
bit). the game's sounds are 48 kHz.
"""
import os
import subprocess
import sys

import numpy as np
import scipy.signal as sg

SR = 48000
REFERENCE = -14.0      # dBFS, the loudest 100 ms
CEILING = -1.0         # dBFS, true peak
FFMPEG = os.environ.get("FFMPEG", r"C:\Users\saguny\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-8.0.1-full_build\bin\ffmpeg.exe")


def decode(path, channels):
    raw = subprocess.run([FFMPEG, "-v", "error", "-i", path, "-f", "f32le", "-ac", str(channels), "-ar", str(SR), "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32).astype(np.float64).reshape(-1, channels)


def channels_of(path):
    out = subprocess.run([FFMPEG.replace("ffmpeg.exe", "ffprobe.exe"), "-v", "error", "-select_streams", "a:0",
                          "-show_entries", "stream=channels", "-of", "csv=p=0", path], capture_output=True, text=True).stdout
    return max(1, min(2, int(out.strip() or 1)))


def loudest(x, window=0.1):
    power = (x ** 2).mean(axis=1)
    n = max(1, min(len(power), int(window * SR)))
    c = np.concatenate([[0], np.cumsum(power)])
    return 10 * np.log10(((c[n:] - c[:-n]) / n).max() + 1e-12)


def true_peak(x):
    return np.abs(sg.resample_poly(x, 4, 1, axis=0)).max()


def limit(x, ceiling_db, lookahead=0.0015, release=0.04):
    """a look-ahead peak limiter: the gain comes down just before a peak and eases back up"""
    ceiling = 10 ** (ceiling_db / 20)
    for _ in range(4):    # the oversampled check can find what the sample peaks missed
        peak = np.abs(x).max(axis=1)
        need = np.minimum(1.0, ceiling / np.maximum(peak, 1e-12))
        la = max(1, int(lookahead * SR))
        # each sample takes the deepest reduction of the look-ahead ahead of it, so the gain is
        # already down when the peak arrives
        held = np.lib.stride_tricks.sliding_window_view(np.pad(need, (0, la), constant_values=1.0), la + 1).min(axis=1)
        g = np.empty_like(held)
        coef = np.exp(-1 / (release * SR))
        cur = 1.0
        for i, h in enumerate(held):
            cur = h if h < cur else h + (cur - h) * coef
            g[i] = cur
        x = x * g[:, None]
        tp = true_peak(x)
        if tp <= ceiling * 1.0001:
            break
        x = x * (ceiling / tp)
    return x


def encode(x, path):
    os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
    ch = x.shape[1]
    codec = ["-c:a", "libvorbis", "-q:a", "7"] if path.lower().endswith(".ogg") else ["-c:a", "pcm_s16le"]
    subprocess.run([FFMPEG, "-v", "error", "-y", "-f", "f32le", "-ac", str(ch), "-ar", str(SR), "-i", "-", *codec, path],
                   input=x.astype(np.float32).tobytes(), check=True)


def normalize(src, dst, mono=False):
    x = decode(src, 1 if mono else channels_of(src))
    before, peak_before = loudest(x), 20 * np.log10(true_peak(x) + 1e-12)
    x = x * 10 ** ((REFERENCE - before) / 20)
    x = limit(x, CEILING)
    encode(x, dst)
    y = decode(dst, x.shape[1])
    print(f"{os.path.basename(dst):22s} {before:6.1f} -> {loudest(y):6.1f} dB loudest 100ms, "
          f"peak {peak_before:5.1f} -> {20 * np.log10(true_peak(y) + 1e-12):5.1f} dBTP, {'mono' if x.shape[1] == 1 else 'stereo'}")


if __name__ == "__main__":
    args, i = sys.argv[1:], 0
    while i < len(args):
        mono = i + 2 < len(args) and args[i + 2] == "--mono"
        normalize(args[i], args[i + 1], mono)
        i += 3 if mono else 2
