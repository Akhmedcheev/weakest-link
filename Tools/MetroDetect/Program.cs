using System;
using NAudio.Wave;

// Анализ Round_bed_(220).mp3 для определения ROUND2_METRONOME_OFFSET_MS
string baseDir = @"I:\WEAKEST LINK SOPFTWARE AI TESTERING FINALE\Assets\Audio";
string[] files = {
    System.IO.Path.Combine(baseDir, "Round_bed_(230).mp3"),  // Round 1 (для сравнения)
    System.IO.Path.Combine(baseDir, "Round_bed_(220).mp3"),  // Round 2 (нужно найти)
    System.IO.Path.Combine(baseDir, "Round_bed_(210).mp3"),  // Round 3
    System.IO.Path.Combine(baseDir, "Round_bed_(200).mp3"),  // Round 4
    System.IO.Path.Combine(baseDir, "Round_bed_(150).mp3"),  // Round 5
    System.IO.Path.Combine(baseDir, "Round_bed_(140).mp3"),  // Round 6
    System.IO.Path.Combine(baseDir, "Round_bed_(130).mp3"),  // Round 7
};

foreach (var input in files)
{
    if (!System.IO.File.Exists(input))
    {
        Console.WriteLine($"SKIP: {System.IO.Path.GetFileName(input)} — файл не найден");
        continue;
    }

    Console.WriteLine($"\n{"=",-60}");
    Console.WriteLine($"FILE: {System.IO.Path.GetFileName(input)}");
    Console.WriteLine($"  Size: {new System.IO.FileInfo(input).Length:N0} bytes");
    Console.WriteLine($"{"=",-60}");
    
    using var reader = new AudioFileReader(input);
    int sr = reader.WaveFormat.SampleRate;
    int ch = reader.WaveFormat.Channels;
    Console.WriteLine($"  {ch}ch, {sr}Hz, {reader.TotalTime.TotalSeconds:F2}s ({reader.TotalTime:m\\:ss\\.ff})");

    int wMs = 5;
    int wSamp = sr * wMs / 1000;
    float[] buf = new float[wSamp * ch];
    var energies = new System.Collections.Generic.List<double>();
    int read;
    while ((read = reader.Read(buf, 0, buf.Length)) > 0)
    {
        double sum = 0;
        for (int i = 0; i < read; i++) sum += buf[i] * buf[i];
        energies.Add(Math.Sqrt(sum / read));
    }

    double maxE = 0;
    foreach (var e in energies) if (e > maxE) maxE = e;

    // Noise floor first 20ms
    double noise = 0;
    int nw = Math.Min(4, energies.Count);
    for (int i = 0; i < nw; i++) noise += energies[i];
    noise /= nw;

    double thresh = Math.Max(noise * 5, maxE * 0.02);

    // First sound
    for (int i = 0; i < energies.Count; i++)
    {
        if (energies[i] > thresh)
        {
            Console.WriteLine($"  Первый звук: {i * wMs / 1000.0:F3}с ({i * wMs}мс)");
            break;
        }
    }

    // Detect peaks (ticks) in first 15s
    Console.WriteLine("  Тики (первые 15с):");
    int tc = 0; bool inP = false; double lastT = -1;
    for (int i = 0; i < energies.Count && tc < 15; i++)
    {
        double t = i * wMs / 1000.0;
        if (t > 15) break;
        if (energies[i] > thresh * 2 && !inP)
        {
            inP = true; tc++;
            double iv = lastT >= 0 ? (t - lastT) * 1000 : 0;
            Console.WriteLine($"    #{tc}: {t:F3}с ({t*1000:F0}мс)" + (iv > 0 ? $"  [Δ{iv:F0}мс]" : ""));
            lastT = t;
        }
        else if (energies[i] < thresh * 0.5) inP = false;
    }

    // Profile first 3s at 50ms
    Console.WriteLine("  Профиль 0-3с:");
    int pb = 50 / wMs;
    for (int b = 0; b < 60 && b * pb < energies.Count; b++)
    {
        double avg = 0; int c = 0;
        for (int j = 0; j < pb && b * pb + j < energies.Count; j++) { avg += energies[b * pb + j]; c++; }
        avg /= c;
        Console.WriteLine($"    {b*0.05,5:F2}s | {new string('#', Math.Max(0, (int)(avg / maxE * 50)))}");
    }
}
