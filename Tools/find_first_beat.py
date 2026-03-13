"""
Find the exact time of the first metronome click in 230 METRONOME FIXED.mp3
"""
import librosa
import numpy as np

path = r"I:\WLINK ADIO\The Weakest Link question bed soundtracks\FOR ANTIGRAVITY\230 METRONOME FIXED.mp3"

print("Loading 230 METRONOME FIXED.mp3...")
y, sr = librosa.load(path, sr=44100, mono=True)
duration = len(y) / sr
print(f"Duration: {duration:.2f}s | SR: {sr}")

# Method 1: librosa onset detection (high resolution)
print("\n=== Method 1: librosa onset detection ===")
onset_env = librosa.onset.onset_strength(y=y, sr=sr, hop_length=256)
onsets = librosa.onset.onset_detect(
    y=y, sr=sr, hop_length=256,
    onset_envelope=onset_env,
    backtrack=False,
    units='time'
)
print(f"First 10 onsets: {[f'{t:.4f}' for t in onsets[:10]]}")
if len(onsets) > 0:
    print(f"FIRST ONSET: {onsets[0]:.4f}s = {onsets[0]*1000:.1f}ms")

# Method 2: Peak amplitude detection (find first sample above threshold)
print("\n=== Method 2: Peak amplitude detection ===")
abs_y = np.abs(y)
max_amp = np.max(abs_y)
threshold = max_amp * 0.1  # 10% of max amplitude

above = np.where(abs_y > threshold)[0]
if len(above) > 0:
    first_sample = above[0]
    first_time = first_sample / sr
    print(f"Threshold: {threshold:.4f} (10% of max {max_amp:.4f})")
    print(f"First sample above threshold: #{first_sample}")
    print(f"FIRST PEAK: {first_time:.4f}s = {first_time*1000:.1f}ms")

# Method 3: RMS energy envelope
print("\n=== Method 3: RMS energy (frame-level) ===")
frame_length = 512
hop = 256
rms = librosa.feature.rms(y=y, frame_length=frame_length, hop_length=hop)[0]
rms_threshold = np.max(rms) * 0.1
rms_above = np.where(rms > rms_threshold)[0]
if len(rms_above) > 0:
    first_frame = rms_above[0]
    first_time_rms = librosa.frames_to_time(first_frame, sr=sr, hop_length=hop)
    print(f"First RMS frame above 10%: frame #{first_frame}")
    print(f"FIRST RMS PEAK: {first_time_rms:.4f}s = {first_time_rms*1000:.1f}ms")

# Method 4: even more sensitive - 5% threshold
print("\n=== Method 4: Very sensitive (5% threshold) ===")
threshold_5 = max_amp * 0.05
above_5 = np.where(abs_y > threshold_5)[0]
if len(above_5) > 0:
    first_sample_5 = above_5[0]
    first_time_5 = first_sample_5 / sr
    print(f"FIRST PEAK (5%): {first_time_5:.4f}s = {first_time_5*1000:.1f}ms")

# IOI for BPM confirmation
print("\n=== IOI / BPM ===")
if len(onsets) > 1:
    iois = np.diff(onsets[:20])
    print(f"IOIs: {[f'{d:.4f}' for d in iois[:10]]}")
    median_ioi = np.median(iois)
    print(f"Median IOI: {median_ioi:.4f}s = {median_ioi*1000:.1f}ms")
    print(f"BPM: {60/median_ioi:.1f}")

print("\n" + "="*50)
print("RECOMMENDATION:")
if len(onsets) > 0:
    # Use onset detection result as the most musically accurate
    ms = round(onsets[0] * 1000)
    print(f"  Set TRACK_INTRO_DELAY_MS = {ms}")
    print(f"  (First metronome click at {onsets[0]*1000:.1f}ms)")
