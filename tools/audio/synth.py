"""Synthèse des sons de Golferoo (C20) : python -I tools/audio/synth.py
Bibliothèque standard seulement, graine fixe : la sortie est identique à chaque lancement.
Écrit les .wav (mono, 22,05 kHz, 16 bits) dans game/assets/audio/."""
import math
import random
import struct
import wave
from pathlib import Path

RATE = 22050
OUT = Path(__file__).resolve().parents[2] / "game" / "assets" / "audio"


def tone(freq, dur, decay=8.0, harmonics=(1.0,)):
    """Sinus (et harmoniques) à enveloppe exponentielle, attaque de 3 ms."""
    n = int(RATE * dur)
    out = []
    for i in range(n):
        t = i / RATE
        env = min(1.0, t / 0.003) * math.exp(-decay * t)
        out.append(env * sum(a * math.sin(2 * math.pi * freq * (k + 1) * t) for k, a in enumerate(harmonics)))
    return out


def noise(rng, dur, decay=6.0, smooth=0.0):
    """Bruit blanc, filtré passe-bas par un lissage d'ordre 1 (0 = brut, proche de 1 = grave)."""
    out, last = [], 0.0
    for i in range(int(RATE * dur)):
        last = smooth * last + (1 - smooth) * rng.uniform(-1, 1)
        out.append(last * math.exp(-decay * i / RATE))
    return out


def mix(*parts):
    """Somme les pistes ; chaque piste est (décalage en secondes, échantillons)."""
    n = max(int(RATE * d) + len(s) for d, s in parts)
    out = [0.0] * n
    for delay, samples in parts:
        start = int(RATE * delay)
        for i, v in enumerate(samples):
            out[start + i] += v
    return out


def write(name, samples, peak=0.8):
    top = max(abs(v) for v in samples) or 1.0
    data = b"".join(struct.pack("<h", int(32767 * peak * v / top)) for v in samples)
    with wave.open(str(OUT / name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(data)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    rng = random.Random(20)
    write("ui_click.wav", mix((0, tone(1800, 0.05, 70)), (0, noise(rng, 0.02, 200, 0.3))))
    write("ui_select.wav", mix((0, tone(660, 0.09, 35, (1, 0.3))), (0.04, tone(990, 0.09, 35, (1, 0.3)))))
    write("ui_refuse.wav", mix((0, tone(180, 0.18, 14, (1, 0.6, 0.3))), (0.09, tone(140, 0.2, 14, (1, 0.6, 0.3)))))
    write("cash.wav", mix((0, noise(rng, 0.03, 120)), (0.03, tone(2400, 0.45, 9, (1, 0.5, 0.25))), (0.09, tone(3200, 0.4, 10, (1, 0.4)))))
    write("water.wav", mix((0, noise(rng, 0.7, 5, 0.8)), (0.02, noise(rng, 0.25, 14, 0.5)), (0.05, tone(320, 0.2, 18))))
    write("objective.wav", mix(*[(0.11 * i, tone(f, 0.35, 7, (1, 0.4, 0.15))) for i, f in enumerate((523.25, 659.25, 783.99, 1046.5))]))
    write("advice.wav", mix((0, tone(1318.5, 0.6, 6, (1, 0.3))), (0.0, tone(1318.5 * 2.756, 0.3, 14, (0.2,)))))
    write("quit.wav", mix(*[(0.16 * i, tone(440, 0.14, 12, (1, 0.7, 0.5, 0.3))) for i in range(3)]))


if __name__ == "__main__":
    main()
