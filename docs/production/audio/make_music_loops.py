"""AI-206-MUSIC: turn picked ElevenLabs music takes into game-ready files.

Loops get a seamless wrap: the last XFADE seconds are equal-power crossfaded
with the first XFADE seconds, and the file then starts XFADE seconds in, so the
end flows straight into the start when Unity loops the clip. Stingers are only
trimmed of trailing silence. Everything is loudness-matched (EBU R128) and
written as 48 kHz stereo Ogg Vorbis.

    python3 docs/production/audio/make_music_loops.py <takes_dir> <out_dir>
"""
import subprocess
import sys
import pathlib
import re
import numpy as np

SR = 48000
XFADE = 2.0
PICKS = {
    # out name: (take file, kind, target LUFS)
    "music_menu_loop": ("menu_1.mp3", "loop", -20.0),
    "music_battle_zeus_loop": ("zeus_4.mp3", "loop", -20.0),
    "music_battle_poseidon_loop": ("poseidon_1.mp3", "loop", -20.0),
    "music_victory_stinger": ("victory_4.mp3", "stinger", -17.0),
    "music_defeat_stinger": ("defeat_3.mp3", "stinger", -19.0),
}


def read(path):
    r = subprocess.run(["ffmpeg", "-v", "error", "-i", str(path), "-ac", "2", "-ar", str(SR),
                        "-f", "f32le", "-"], capture_output=True, check=True)
    return np.frombuffer(r.stdout, dtype=np.float32).reshape(-1, 2).copy()


def measure_lufs(path):
    r = subprocess.run(["ffmpeg", "-hide_banner", "-i", str(path), "-af", "ebur128", "-f", "null", "-"],
                       capture_output=True, text=True)
    return float(re.findall(r"I:\s+(-?[\d.]+) LUFS", r.stderr)[-1])


def write(x, path, lufs):
    raw = path.with_suffix(".raw.wav")
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", "2", "-i", "-",
                    str(raw)], input=x.astype(np.float32).tobytes(), check=True)
    # One static gain, not loudnorm's dynamic mode, so the loop seam keeps its level.
    gain = lufs - measure_lufs(raw)
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", str(raw), "-af", f"volume={gain:.2f}dB",
                    "-ar", str(SR), "-c:a", "libvorbis", "-q:a", "6", str(path)], check=True)
    raw.unlink()


def make_loop(x):
    n = int(XFADE * SR)
    t = np.linspace(0, np.pi / 2, n)[:, None]
    head, tail = x[:n], x[-n:]
    blended = tail * np.cos(t) + head * np.sin(t)
    return np.concatenate([x[n:-n], blended])


def trim_tail(x, floor_db=-60.0):
    level = 20 * np.log10(np.abs(x).max(axis=1) + 1e-9)
    loud = np.nonzero(level > floor_db)[0]
    end = min(len(x), loud[-1] + int(0.25 * SR)) if len(loud) else len(x)
    y = x[:end].copy()
    fade = min(int(0.2 * SR), len(y))
    y[-fade:] *= np.linspace(1, 0, fade)[:, None]
    return y


def main(src, out):
    src, out = pathlib.Path(src), pathlib.Path(out)
    out.mkdir(parents=True, exist_ok=True)
    for name, (take, kind, lufs) in PICKS.items():
        x = read(src / take)
        y = make_loop(x) if kind == "loop" else trim_tail(x)
        write(y, out / f"{name}.ogg", lufs)
        print(f"{name}: {take} -> {len(y) / SR:.1f}s {kind} @ {lufs} LUFS")


if __name__ == "__main__":
    main(*sys.argv[1:3])
