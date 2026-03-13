"""Detect the first metronome tick onset in Round_bed_(220).mp3 for Round 2."""
import subprocess, struct, sys, os, tempfile

INPUT = r"I:\WEAKEST LINK SOPFTWARE AI TESTERING FINALE\Assets\Audio\Round_bed_(220).mp3"
wav_path = os.path.join(tempfile.gettempdir(), "round2_analysis.wav")

print(f"Analyzing: {INPUT}")
print(f"Converting MP3 to WAV...")
r = subprocess.run(["ffmpeg", "-y", "-i", INPUT, "-ac", "1", "-ar", "44100", wav_path],
               capture_output=True, text=True)
if r.returncode != 0:
    print(f"ffmpeg error: {r.stderr[:500]}")
    sys.exit(1)

import wave
with wave.open(wav_path, 'r') as wf:
    n_channels = wf.getnchannels()
    sample_width = wf.getsampwidth()
    framerate = wf.getframerate()
    n_frames = wf.getnframes()
    raw = wf.readframes(n_frames)

print(f"Audio: {n_channels}ch, {sample_width*8}bit, {framerate}Hz, {n_frames/framerate:.2f}s total")

if sample_width == 2:
    fmt = f"<{n_frames * n_channels}h"
    samples = list(struct.unpack(fmt, raw))
elif sample_width == 4:
    fmt = f"<{n_frames * n_channels}i"
    samples = list(struct.unpack(fmt, raw))

if n_channels == 2:
    samples = [(samples[i] + samples[i+1]) // 2 for i in range(0, len(samples), 2)]

max_val = max(abs(s) for s in samples)
print(f"Max sample value: {max_val}")

# RMS energy in 5ms windows (higher resolution)
window_ms = 5
window_samples = int(framerate * window_ms / 1000)
n_windows = len(samples) // window_samples

energies = []
for i in range(n_windows):
    chunk = samples[i * window_samples : (i + 1) * window_samples]
    rms = (sum(s*s for s in chunk) / len(chunk)) ** 0.5
    energies.append(rms)

# Noise floor from first 50ms
noise_windows = max(1, 50 // window_ms)
noise_floor = sum(energies[:noise_windows]) / noise_windows
print(f"Noise floor RMS: {noise_floor:.1f}")

# Threshold for detection
threshold = max(noise_floor * 5, max_val * 0.02)
print(f"Detection threshold: {threshold:.1f}")

# Find first window where energy exceeds threshold (first sound event)
first_sound_time = None
for i, e in enumerate(energies):
    if e > threshold:
        first_sound_time = i * window_ms / 1000.0
        print(f"\n>>> FIRST SOUND: {first_sound_time:.3f}s ({first_sound_time*1000:.0f}ms)")
        print(f"    Window #{i}, RMS energy: {e:.1f} (threshold: {threshold:.1f})")
        break

# Detect peaks (ticks) in first 15s
print(f"\nTicks (first 15s):")
tc = 0
in_peak = False
last_t = -1
for i in range(len(energies)):
    if tc >= 15:
        break
    t = i * window_ms / 1000.0
    if t > 15:
        break
    if energies[i] > threshold * 2 and not in_peak:
        in_peak = True
        tc += 1
        iv = (t - last_t) * 1000 if last_t >= 0 else 0
        delta = f"  [delta {iv:.0f}ms]" if iv > 0 else ""
        print(f"  #{tc}: {t:.3f}s ({t*1000:.0f}ms){delta}")
        last_t = t
    elif energies[i] < threshold * 0.5:
        in_peak = False

# Energy profile first 3s at 50ms
print(f"\nEnergy profile (50ms intervals, first 3s):")
pb = 50 // window_ms
for b in range(60):
    start = b * pb
    end = start + pb
    if end > len(energies):
        break
    avg_e = sum(energies[start:end]) / pb
    bar = "#" * int(avg_e / max(1, max_val) * 80)
    t = b * 0.05
    marker = ""
    if first_sound_time and abs(t - first_sound_time) < 0.05:
        marker = " <<< FIRST SOUND"
    print(f"  {t:5.2f}s | {avg_e:8.0f} | {bar}{marker}")

# Also analyze Round_bed_(230).mp3 for comparison
print(f"\n{'='*60}")
print(f"COMPARISON: Round_bed_(230).mp3 (Round 1)")
INPUT_R1 = r"I:\WEAKEST LINK SOPFTWARE AI TESTERING FINALE\Assets\Audio\Round_bed_(230).mp3"
wav_path_r1 = os.path.join(tempfile.gettempdir(), "round1_analysis.wav")
r = subprocess.run(["ffmpeg", "-y", "-i", INPUT_R1, "-ac", "1", "-ar", "44100", wav_path_r1],
               capture_output=True, text=True)
if r.returncode == 0:
    with wave.open(wav_path_r1, 'r') as wf:
        raw_r1 = wf.readframes(wf.getnframes())
        n_frames_r1 = wf.getnframes()
    samples_r1 = list(struct.unpack(f"<{n_frames_r1}h", raw_r1))
    max_val_r1 = max(abs(s) for s in samples_r1)
    
    energies_r1 = []
    for i in range(len(samples_r1) // window_samples):
        chunk = samples_r1[i * window_samples : (i + 1) * window_samples]
        rms = (sum(s*s for s in chunk) / len(chunk)) ** 0.5
        energies_r1.append(rms)
    
    noise_r1 = sum(energies_r1[:noise_windows]) / noise_windows
    thresh_r1 = max(noise_r1 * 5, max_val_r1 * 0.02)
    
    for i, e in enumerate(energies_r1):
        if e > thresh_r1:
            t = i * window_ms / 1000.0
            print(f"  Round 1 first sound: {t:.3f}s ({t*1000:.0f}ms) [current constant: 505ms]")
            break
    
    os.remove(wav_path_r1)

os.remove(wav_path)
print(f"\n{'='*60}")
print("RECOMMENDATION FOR ROUND2_METRONOME_OFFSET_MS:")
if first_sound_time is not None:
    recommended = int(first_sound_time * 1000)
    print(f"  Value: {recommended} ms")
