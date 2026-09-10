"""Generate local WAV/MP3 regression fixtures from the project's original score.

Uses an existing LAME DLL only as a test encoder; no encoder ships with the game.
Usage: python scripts/Prepare-LabFixtures.py --lame /path/to/libmp3lame.dll
"""
import argparse
import ctypes as c
from pathlib import Path
import shutil
import wave

parser = argparse.ArgumentParser()
parser.add_argument("--lame", required=True)
parser.add_argument("--output", default="TestResults/LabSongs")
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
output = (root / args.output).resolve()
output.mkdir(parents=True, exist_ok=True)
source = root / "Assets/Pulse/Resources/Afterlight.wav"
shutil.copyfile(source, output / "Afterlight - original.wav")
lame = c.CDLL(args.lame)
lame.lame_init.restype = c.c_void_p
for name in ["lame_set_in_samplerate", "lame_set_num_channels", "lame_set_brate", "lame_set_quality"]:
    getattr(lame, name).argtypes = [c.c_void_p, c.c_int]
lame.lame_init_params.argtypes = [c.c_void_p]
lame.lame_encode_buffer_interleaved.argtypes = [c.c_void_p, c.POINTER(c.c_short), c.c_int, c.POINTER(c.c_ubyte), c.c_int]
lame.lame_encode_flush.argtypes = [c.c_void_p, c.POINTER(c.c_ubyte), c.c_int]
lame.lame_get_lametag_frame.argtypes = [c.c_void_p, c.POINTER(c.c_ubyte), c.c_size_t]
lame.lame_get_lametag_frame.restype = c.c_size_t
lame.lame_close.argtypes = [c.c_void_p]
encoder = lame.lame_init()
try:
    with wave.open(str(source), "rb") as wav, (output / "Afterlight - MP3 export.mp3").open("wb") as mp3:
        assert wav.getnchannels() == 2 and wav.getsampwidth() == 2, "Fixture expects stereo PCM16"
        lame.lame_set_in_samplerate(encoder, wav.getframerate())
        lame.lame_set_num_channels(encoder, 2)
        lame.lame_set_brate(encoder, 192)
        lame.lame_set_quality(encoder, 2)
        assert lame.lame_init_params(encoder) == 0
        buffer = (c.c_ubyte * 65536)()
        while pcm := wav.readframes(8192):
            samples = (c.c_short * (len(pcm) // 2)).from_buffer_copy(pcm)
            count = lame.lame_encode_buffer_interleaved(encoder, samples, len(pcm) // 4, buffer, len(buffer))
            assert count >= 0, f"LAME error {count}"
            mp3.write(bytes(buffer[:count]))
        count = lame.lame_encode_flush(encoder, buffer, len(buffer))
        assert count >= 0
        mp3.write(bytes(buffer[:count]))
        count = lame.lame_get_lametag_frame(encoder, buffer, len(buffer))
        if 0 < count <= len(buffer):
            mp3.seek(0)
            mp3.write(bytes(buffer[:count]))
finally:
    lame.lame_close(encoder)
print(f"Original-score WAV and MP3 fixtures: {output}")
