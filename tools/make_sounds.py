#!/usr/bin/env python3
"""Synthesises AVAKong's sound effects and tunes into ../src/AVAKong/Sounds as 16-bit mono WAV.

Nothing is sampled from any arcade machine: every effect is built here from square, triangle and
noise voices, and the tunes are new compositions. Run it from anywhere:
    python3 tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "AVAKong", "Sounds")
RNG = random.Random(1981)


def write(name, samples, volume=0.7):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * volume * 32767)) for s in samples))


def voice(kind, phase):
    p = phase % 1.0
    if kind == "square":
        return 1.0 if p < 0.5 else -1.0
    if kind == "pulse":
        return 1.0 if p < 0.25 else -1.0
    if kind == "tri":
        return 4 * p - 1 if p < 0.5 else 3 - 4 * p
    if kind == "saw":
        return 2 * p - 1
    if kind == "sine":
        return math.sin(2 * math.pi * p)
    return RNG.uniform(-1, 1)


def tone(seconds, f0, f1=None, kind="square", attack=0.004, release=1.0, vibrato=0.0, vib_rate=8.0, gain=1.0):
    """A note gliding from f0 to f1 (exponentially), with a short attack and a shaped decay."""
    n = int(RATE * seconds)
    f1 = f1 or f0
    out, phase, held = [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f = f0 * (f1 / f0) ** (i / max(1, n - 1))
        if vibrato:
            f *= 1 + vibrato * math.sin(2 * math.pi * vib_rate * t)
        phase += f / RATE
        if kind == "noise":
            # Noise re-sampled at the voice's rate: a lower 'pitch' sounds rougher.
            if int(phase) != int(phase - f / RATE):
                held = RNG.uniform(-1, 1)
            s = held
        else:
            s = voice(kind, phase)
        env = min(1.0, t / attack) * (1 - i / n) ** release
        out.append(s * env * gain)
    return out


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def cat(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p):
            out[i] += v
    return out


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(note):
    n = NOTES[note[0]]
    rest = note[1:]
    while rest and rest[0] in "#b":
        n += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12)


def tune(bpm, lead, bass, kind="pulse"):
    """Two voices: a lead and a bass line, as (note, beats) lists; '-' is a rest."""
    beat = 60.0 / bpm

    def line(notes, k, gain, release):
        out = []
        for note, beats in notes:
            d = beats * beat
            out.extend(silence(d) if note == "-" else tone(d, hz(note), kind=k, release=release, gain=gain))
        return out

    return mix(line(lead, kind, 0.55, 0.6), line(bass, "tri", 0.6, 0.3))


def main():
    os.makedirs(OUT, exist_ok=True)
    write("Walk.wav", tone(0.03, 900, 500, "pulse", release=2), 0.25)
    write("Jump.wav", tone(0.22, 300, 900, "square", attack=0.004, release=0.7), 0.4)
    write("JumpOver.wav", cat(*[tone(0.05, hz(n), kind="pulse", release=0.6) for n in ["C6", "E6", "G6"]]), 0.4)
    write("Smash.wav", cat(tone(0.04, 1400, 600, "square"), tone(0.18, 900, 120, "noise", release=1.2)), 0.55)
    write("Item.wav", cat(*[tone(0.06, hz(n), kind="square", release=0.5) for n in ["G5", "C6", "E6", "G6", "C7"]]), 0.4)
    write("RivetPop.wav", cat(tone(0.03, 300, 1500, "square"), tone(0.1, 1500, 2200, "pulse", release=1)), 0.45)
    write("Bounce.wav", tone(0.12, 500, 1100, "tri", release=0.8, vibrato=0.05, vib_rate=30), 0.35)
    write("Pound.wav", cat(*[cat(tone(0.08, 90, 60, "square", release=1.2), silence(0.06)) for _ in range(4)]), 0.6)
    write("Throw.wav", tone(0.18, 220, 120, "noise", release=1), 0.35)
    write("TimeLow.wav", cat(*[cat(tone(0.08, 1800, kind="square", release=0.2), silence(0.08)) for _ in range(4)]), 0.35)
    write("ExtraLife.wav", cat(*[tone(0.07, hz(n), kind="square", release=0.4) for n in ["E6", "G#6", "B6", "E7"] * 3]), 0.45)

    # Caught: a short falling phrase (new).
    write("Death.wav", tune(150,
        [("E5", 0.5), ("D#5", 0.5), ("D5", 0.5), ("C#5", 0.5), ("C5", 1), ("-", 0.5), ("A4", 0.5), ("E4", 1)],
        [("A2", 2), ("F2", 1), ("E2", 2)], kind="square"), 0.6)
    # "How high can you climb?": a rising call, one step up for each stage (new).
    write("HowHigh.wav", tune(140,
        [("G4", 0.5), ("C5", 0.5), ("E5", 0.5), ("G5", 1), ("E5", 0.5), ("G5", 0.5), ("C6", 1.5), ("-", 0.5)],
        [("C3", 1), ("G3", 1), ("C3", 1), ("E3", 1), ("C3", 1)]), 0.6)
    write("StageClear.wav", tune(160,
        [("C5", 0.5), ("E5", 0.5), ("G5", 0.5), ("C6", 0.5), ("A5", 0.5), ("C6", 0.5), ("E6", 1), ("D6", 0.5), ("E6", 0.5), ("G6", 2)],
        [("C3", 1), ("G3", 1), ("F3", 1), ("A3", 1), ("G3", 1), ("C4", 2)]), 0.6)
    write("LevelClear.wav", tune(150,
        [("G5", 0.5), ("G5", 0.5), ("E5", 0.5), ("C5", 0.5), ("D5", 1), ("G4", 1),
         ("C5", 0.5), ("E5", 0.5), ("G5", 0.5), ("C6", 0.5), ("B5", 1), ("G5", 1),
         ("A5", 0.5), ("B5", 0.5), ("C6", 0.5), ("D6", 0.5), ("E6", 1), ("C6", 1), ("G6", 3), ("-", 1)],
        [(n, 1) for n in ["C3", "G2", "C3", "G2", "A2", "E2", "G2", "D3", "F2", "G2", "C3", "C3", "C3", "-", "-", "-"]]), 0.6)
    write("GameOver.wav", tune(92,
        [("G4", 1), ("E4", 1), ("C4", 1), ("D4", 1), ("C4", 2), ("-", 1)],
        [("C3", 2), ("A2", 2), ("G2", 2), ("C2", 1)], kind="tri"), 0.65)

    # Each stage's music: a short loop of its own (all new).
    write("MusicGirders.wav", tune(150,
        [("C5", 0.5), ("-", 0.5), ("E5", 0.5), ("-", 0.5), ("G5", 0.5), ("-", 0.5), ("E5", 0.5), ("-", 0.5),
         ("D5", 0.5), ("-", 0.5), ("F5", 0.5), ("-", 0.5), ("A5", 0.5), ("-", 0.5), ("F5", 0.5), ("-", 0.5)],
        [(n, 0.5) for n in ["C3", "G3"] * 4 + ["D3", "A3"] * 4]), 0.45)
    write("MusicConveyors.wav", tune(160,
        [("E5", 0.25), ("G5", 0.25), ("E5", 0.25), ("G5", 0.25), ("A5", 1), ("G5", 0.25), ("E5", 0.25), ("G5", 0.25), ("E5", 0.25), ("D5", 1),
         ("C5", 0.25), ("E5", 0.25), ("C5", 0.25), ("E5", 0.25), ("G5", 1), ("E5", 0.5), ("D5", 0.5), ("C5", 1)],
        [(n, 0.5) for n in ["A2", "E3", "A2", "E3", "G2", "D3", "G2", "D3", "C3", "G3", "C3", "G3", "G2", "D3", "C3", "C3"]]), 0.45)
    write("MusicElevators.wav", tune(132,
        [("A4", 1), ("C5", 1), ("E5", 1), ("D5", 1), ("C5", 1), ("B4", 1), ("A4", 2)],
        [(n, 0.5) for n in ["A2", "A3"] * 4 + ["F2", "F3", "E2", "E3"] * 2]), 0.4)
    write("MusicRivets.wav", tune(144,
        [("C5", 0.5), ("C5", 0.5), ("G5", 0.5), ("G5", 0.5), ("A5", 0.5), ("A5", 0.5), ("G5", 1),
         ("F5", 0.5), ("F5", 0.5), ("E5", 0.5), ("E5", 0.5), ("D5", 0.5), ("D5", 0.5), ("C5", 1)],
        [(n, 0.5) for n in ["C3", "G3"] * 2 + ["F3", "C4", "C3", "G3"] + ["F3", "C4", "C3", "G3", "G2", "D3", "C3", "C3"]]), 0.45)
    # The hammer: a fast, insistent beat.
    write("MusicHammer.wav", tune(200,
        [("C6", 0.5), ("G5", 0.5), ("E5", 0.5), ("G5", 0.5)] * 2 + [("D6", 0.5), ("A5", 0.5), ("F5", 0.5), ("A5", 0.5)] * 2,
        [(n, 0.5) for n in ["C3", "C3", "G2", "G2"] * 2 + ["D3", "D3", "A2", "A2"] * 2], kind="square"), 0.45)

    # The title tune: a bold, stomping march in C (new), 8 bars, loops.
    write("TitleTune.wav", tune(132,
        [("C5", 1), ("C5", 0.5), ("D5", 0.5), ("E5", 1), ("G5", 1), ("F5", 0.5), ("E5", 0.5), ("D5", 0.5), ("C5", 0.5), ("D5", 2),
         ("E5", 1), ("E5", 0.5), ("F5", 0.5), ("G5", 1), ("C6", 1), ("B5", 0.5), ("A5", 0.5), ("G5", 0.5), ("F5", 0.5), ("G5", 2),
         ("A5", 1), ("G5", 0.5), ("F5", 0.5), ("E5", 1), ("C5", 1), ("D5", 0.5), ("E5", 0.5), ("F5", 0.5), ("D5", 0.5), ("C5", 2)],
        [(n, 1) for n in ["C3", "G2", "C3", "G2", "F2", "C3", "G2", "D3", "C3", "G2", "C3", "E3", "F2", "G2", "C3", "C3",
                          "F2", "C3", "C3", "G2", "D3", "G2", "C3", "C3"]]), 0.6)
    print("sounds written to", os.path.normpath(OUT))


if __name__ == "__main__":
    main()
