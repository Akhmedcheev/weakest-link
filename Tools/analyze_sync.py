"""
Analyze synchronization between:
1. Round_bed_(230).mp3 — the main round track
2. 230 METRONOME FIXED.mp3 — isolated metronome clicks
3. Combined TRACK+METRONOM.mp3 — both mixed together

We detect onsets (beat hits / clicks) in each track and compare timings.
"""
import librosa
import numpy as np

BASE = r"I:\WLINK ADIO\The Weakest Link question bed soundtracks\FOR ANTIGRAVITY"

def detect_onsets(path, label, sr=22050, hop=512):
    """Load audio and detect onset times."""
    print(f"\n{'='*60}")
    print(f"  Analyzing: {label}")
    print(f"  File: {path}")
    print(f"{'='*60}")
    
    y, sr_actual = librosa.load(path, sr=sr, mono=True)
    duration = len(y) / sr_actual
    print(f"  Duration: {duration:.2f}s | Sample rate: {sr_actual}")
    
    # Onset detection
    onset_env = librosa.onset.onset_strength(y=y, sr=sr_actual, hop_length=hop)
    onsets = librosa.onset.onset_detect(
        y=y, sr=sr_actual, hop_length=hop,
        onset_envelope=onset_env,
        backtrack=False,
        units='time'
    )
    
    print(f"  Total onsets detected: {len(onsets)}")
    if len(onsets) > 0:
        print(f"  First 20 onsets (seconds): {[f'{t:.3f}' for t in onsets[:20]]}")
    
    # Compute inter-onset intervals (IOI) for first 30 onsets
    if len(onsets) > 1:
        iois = np.diff(onsets[:30])
        print(f"  First IOIs (gaps between beats): {[f'{d:.3f}' for d in iois[:20]]}")
        median_ioi = np.median(iois)
        estimated_bpm = 60.0 / median_ioi if median_ioi > 0 else 0
        print(f"  Median IOI: {median_ioi:.4f}s -> Estimated BPM: {estimated_bpm:.1f}")
    
    return onsets, duration

def detect_percussive_onsets(path, label, sr=22050, hop=512):
    """Detect onsets specifically in the percussive component (for metronome clicks)."""
    print(f"\n{'='*60}")
    print(f"  Percussive onset analysis: {label}")
    print(f"{'='*60}")
    
    y, sr_actual = librosa.load(path, sr=sr, mono=True)
    
    # Separate harmonic and percussive
    y_harm, y_perc = librosa.effects.hpss(y)
    
    onset_env = librosa.onset.onset_strength(y=y_perc, sr=sr_actual, hop_length=hop)
    onsets = librosa.onset.onset_detect(
        y=y_perc, sr=sr_actual, hop_length=hop,
        onset_envelope=onset_env,
        backtrack=False,
        units='time'
    )
    
    print(f"  Percussive onsets: {len(onsets)}")
    if len(onsets) > 0:
        print(f"  First 20: {[f'{t:.3f}' for t in onsets[:20]]}")
    
    if len(onsets) > 1:
        iois = np.diff(onsets[:30])
        median_ioi = np.median(iois)
        estimated_bpm = 60.0 / median_ioi if median_ioi > 0 else 0
        print(f"  Median IOI: {median_ioi:.4f}s -> BPM: {estimated_bpm:.1f}")
    
    return onsets

def compare_onsets(onsets_a, onsets_b, label_a, label_b, tolerance=0.05):
    """Compare two onset arrays and report sync quality."""
    print(f"\n{'='*60}")
    print(f"  SYNC COMPARISON: {label_a} vs {label_b}")
    print(f"{'='*60}")
    
    if len(onsets_a) == 0 or len(onsets_b) == 0:
        print("  Cannot compare - one set is empty.")
        return
    
    # For each onset in B, find nearest onset in A
    diffs = []
    for t_b in onsets_b[:50]:
        closest_idx = np.argmin(np.abs(onsets_a - t_b))
        diff = t_b - onsets_a[closest_idx]
        diffs.append(diff)
    
    diffs = np.array(diffs)
    abs_diffs = np.abs(diffs)
    
    matched = np.sum(abs_diffs < tolerance)
    total = len(diffs)
    
    print(f"  Tolerance: {tolerance*1000:.0f}ms")
    print(f"  Matched: {matched}/{total} ({100*matched/total:.1f}%)")
    print(f"  Mean offset: {np.mean(diffs)*1000:.1f}ms")
    print(f"  Median offset: {np.median(diffs)*1000:.1f}ms")
    print(f"  Std dev: {np.std(diffs)*1000:.1f}ms")
    print(f"  Max absolute drift: {np.max(abs_diffs)*1000:.1f}ms")
    
    # Show first 15 individual offsets
    print(f"\n  Per-beat offsets (first 15):")
    for i, d in enumerate(diffs[:15]):
        status = "OK" if abs(d) < tolerance else "MISS"
        print(f"    Beat {i+1}: {d*1000:+.1f}ms {status}")
    
    # Verdict
    print(f"\n  -- VERDICT --")
    if np.mean(abs_diffs) < 0.020:
        print(f"  EXCELLENT sync (<20ms mean drift)")
    elif np.mean(abs_diffs) < 0.040:
        print(f"  GOOD sync (20-40ms mean drift)")
    elif np.mean(abs_diffs) < 0.070:
        print(f"  ACCEPTABLE sync (40-70ms mean drift, audible but tolerable)")
    else:
        print(f"  POOR sync (>70ms mean drift, clearly audible desync)")
    
    if abs(np.mean(diffs)) > 0.015:
        direction = "LATE" if np.mean(diffs) > 0 else "EARLY"
        print(f"  WARNING: Consistent offset: metronome is {abs(np.mean(diffs))*1000:.1f}ms {direction}")

# ==============================================================
# 1. Analyze individual tracks
# ==============================================================

track_onsets, track_dur = detect_onsets(
    f"{BASE}\\Round_bed_(230).mp3", "Round_bed_(230) - Main Track")

metro_onsets, metro_dur = detect_onsets(
    f"{BASE}\\230 METRONOME FIXED.mp3", "230 METRONOME FIXED - Metronome")

combined_onsets, combined_dur = detect_onsets(
    f"{BASE}\\Combined TRACK+METRONOM.mp3", "Combined TRACK+METRONOM")

# ==============================================================
# 2. Percussive separation for better click detection
# ==============================================================

track_perc = detect_percussive_onsets(
    f"{BASE}\\Round_bed_(230).mp3", "Main Track (percussive)")

metro_perc = detect_percussive_onsets(
    f"{BASE}\\230 METRONOME FIXED.mp3", "Metronome (percussive)")

# ==============================================================
# 3. Compare sync
# ==============================================================

# Main comparison: metronome clicks vs main track beats
compare_onsets(track_onsets, metro_onsets, "Main Track", "Metronome", tolerance=0.050)

# Percussive comparison
compare_onsets(track_perc, metro_perc, "Main Track (perc)", "Metronome (perc)", tolerance=0.050)

# Also compare metronome with the combined mix
compare_onsets(combined_onsets, metro_onsets, "Combined Mix", "Metronome", tolerance=0.050)

print("\n\nDone.")
