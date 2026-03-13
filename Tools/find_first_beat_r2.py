"""
Find the first metronome click in the Round 2 (220 BPM) metronome file
"""
import librosa
import numpy as np

# The metronome WAV for round 2
metro_path = r"I:\WLINK ADIO\The Weakest Link question bed soundtracks\FOR ANTIGRAVITY\The weakest link question bed (220) FIXED-metronome-G minor-120bpm-441hz.wav"

# Also check the actual round bed file that will be used in the game
round_path = r"I:\WLINK ADIO\The Weakest Link question bed soundtracks\RUS\Round_bed_(220).mp3"

# Also check the Assets version
assets_path = r"I:\WEAKEST LINK SOPFTWARE AI TESTERING FINALE\Assets\Audio\Round_bed_(220).mp3"

for label, path in [
    ("Metronome WAV (220)", metro_path),
    ("Round_bed_(220) from RUS folder", round_path),
    ("Round_bed_(220) from Assets", assets_path),
]:
    print(f"\n{'='*60}")
    print(f"  {label}")
    print(f"  {path}")
    print(f"{'='*60}")
    
    try:
        y, sr = librosa.load(path, sr=44100, mono=True)
        duration = len(y) / sr
        print(f"  Duration: {duration:.2f}s | SR: {sr}")
        
        # Onset detection
        onset_env = librosa.onset.onset_strength(y=y, sr=sr, hop_length=256)
        onsets = librosa.onset.onset_detect(
            y=y, sr=sr, hop_length=256,
            onset_envelope=onset_env,
            backtrack=False,
            units='time'
        )
        print(f"  First 10 onsets: {[f'{t:.4f}' for t in onsets[:10]]}")
        if len(onsets) > 0:
            print(f"  >>> FIRST ONSET: {onsets[0]*1000:.1f}ms")
        
        # Peak amplitude
        abs_y = np.abs(y)
        max_amp = np.max(abs_y)
        threshold = max_amp * 0.1
        above = np.where(abs_y > threshold)[0]
        if len(above) > 0:
            first_time = above[0] / sr
            print(f"  >>> FIRST PEAK (10%): {first_time*1000:.1f}ms")
        
        # IOI
        if len(onsets) > 1:
            iois = np.diff(onsets[:15])
            median_ioi = np.median(iois)
            print(f"  Median IOI: {median_ioi*1000:.1f}ms -> BPM: {60/median_ioi:.1f}")
    except Exception as ex:
        print(f"  ERROR: {ex}")

print("\nDone.")
