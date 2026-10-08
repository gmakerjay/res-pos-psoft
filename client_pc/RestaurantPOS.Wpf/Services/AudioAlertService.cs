using System;
using System.Collections.Concurrent;
using System.IO;
using System.Media;
using System.Threading.Tasks;

namespace RestaurantPOS.Wpf.Services;

/// <summary>
/// Professional synthesized audio alerts for restaurant order notifications
/// Zero external audio files required - generates clean, punchy PCM WAV chimes in memory
/// Supports escalating alert volume and rapid pulse frequency for unaccepted orders (+30s cycles)
/// </summary>
public static class AudioAlertService
{
    private static readonly ConcurrentDictionary<string, byte[]> _audioCache = new();

    private static byte[]? _successChimeWav;
    private static int _cachedSuccessVolume = -1;

    public static void PlayOrderAlert(int volumePercent = 90)
    {
        PlayEscalatingOrderAlert(1, volumePercent, false);
    }

    /// <summary>
    /// Play escalating audio alert for unaccepted orders.
    /// Level 1 (0s): Standard pleasant chime
    /// Level 2 (+30s): Louder, double-burst chime (more frequent)
    /// Level 3 (+60s): Even louder, triple-burst urgent chime
    /// Level 4+ (+90s+): Max volume (100%), rapid 4-pulse high-frequency urgent alarm
    /// </summary>
    public static void PlayEscalatingOrderAlert(int escalationLevel, int baseVolumePercent = 90, bool escalate = true)
    {
        if (baseVolumePercent <= 0) return;

        var level = Math.Clamp(escalationLevel, 1, 4);
        var volume = baseVolumePercent;
        if (escalate)
        {
            volume = level switch
            {
                1 => baseVolumePercent,
                2 => Math.Min(100, (int)(baseVolumePercent * 1.15) + 5),
                3 => Math.Min(100, (int)(baseVolumePercent * 1.30) + 10),
                _ => 100
            };
        }
        volume = Math.Clamp(volume, 1, 100);

        var cacheKey = $"lvl_{level}_vol_{volume}";
        var wav = _audioCache.GetOrAdd(cacheKey, _ =>
        {
            var notes = GetNotesForEscalationLevel(level);
            return GenerateChimeWav(volume, notes);
        });

        Task.Run(() =>
        {
            try
            {
                using var ms = new MemoryStream(wav);
                using var player = new SoundPlayer(ms);
                player.Play();
            }
            catch (Exception ex)
            {
                PosLogger.Warn("[Audio] Failed to play escalating order alert: " + ex.Message);
                try { SystemSounds.Exclamation.Play(); } catch { }
            }
        });
    }

    public static void PlaySuccessChime(int volumePercent = 90)
    {
        if (volumePercent <= 0) return;
        volumePercent = Math.Clamp(volumePercent, 1, 100);

        Task.Run(() =>
        {
            try
            {
                if (_successChimeWav == null || _cachedSuccessVolume != volumePercent)
                {
                    _successChimeWav = GenerateChimeWav(volumePercent, 
                        new[] { (523.25, 0.12), (659.25, 0.12), (783.99, 0.35) });
                    _cachedSuccessVolume = volumePercent;
                }

                using var ms = new MemoryStream(_successChimeWav);
                using var player = new SoundPlayer(ms);
                player.Play();
            }
            catch (Exception ex)
            {
                PosLogger.Warn("[Audio] Failed to play success chime: " + ex.Message);
            }
        });
    }

    public static void TestAlert(int volumePercent = 90)
    {
        PlayEscalatingOrderAlert(1, volumePercent, false);
    }

    public static void TestEscalatedAlert(int level, int volumePercent = 90)
    {
        PlayEscalatingOrderAlert(level, volumePercent, true);
    }

    private static (double freq, double duration)[] GetNotesForEscalationLevel(int level)
    {
        return level switch
        {
            1 => new[]
            {
                // Level 1 (Initial / 0s): Standard pleasant 3-note ascending chime
                (880.00, 0.16),
                (1320.00, 0.22),
                (1760.00, 0.45)
            },
            2 => new[]
            {
                // Level 2 (+30s): 2 rapid ascending bursts, higher pitch & tempo
                (987.77, 0.12), (1318.51, 0.12), (1975.53, 0.20),
                (0.0, 0.08), // silence gap
                (1174.66, 0.12), (1567.98, 0.12), (2349.32, 0.28)
            },
            3 => new[]
            {
                // Level 3 (+60s): 3 rapid ascending bursts, urgent and piercing
                (1174.66, 0.09), (1567.98, 0.09), (2349.32, 0.15),
                (0.0, 0.06), // silence gap
                (1318.51, 0.09), (1760.00, 0.09), (2637.02, 0.15),
                (0.0, 0.06), // silence gap
                (1567.98, 0.09), (2093.00, 0.09), (3135.96, 0.22)
            },
            _ => new[]
            {
                // Level 4+ (+90s+): 4 rapid staccato urgent alarm pulses (maximum urgency)
                (1760.00, 0.08), (2637.02, 0.12),
                (0.0, 0.05),
                (1760.00, 0.08), (2637.02, 0.12),
                (0.0, 0.05),
                (1975.53, 0.08), (2959.96, 0.12),
                (0.0, 0.05),
                (2349.32, 0.09), (3520.00, 0.24)
            }
        };
    }

    private static byte[] GenerateChimeWav(int volumePercent, (double freq, double duration)[] notes)
    {
        const int sampleRate = 44100;
        double totalSeconds = 0;
        foreach (var note in notes) totalSeconds += note.duration;
        totalSeconds += 0.05; // tail room

        int totalSamples = (int)(sampleRate * totalSeconds);
        short[] samples = new short[totalSamples];

        double maxAmp = (32760.0 * (volumePercent / 100.0));
        int currentSampleIdx = 0;

        foreach (var (freq, duration) in notes)
        {
            int noteSamples = (int)(sampleRate * duration);
            for (int i = 0; i < noteSamples && currentSampleIdx < totalSamples; i++, currentSampleIdx++)
            {
                if (freq <= 0)
                {
                    samples[currentSampleIdx] = 0;
                    continue;
                }

                double t = (double)i / sampleRate;
                // Decay envelope: exp decay + subtle harmonic
                double envelope = Math.Exp(-3.2 * (t / duration));
                double fundamental = Math.Sin(2.0 * Math.PI * freq * t);
                double overtone = 0.25 * Math.Sin(2.0 * Math.PI * (freq * 2.0) * t);
                double val = (fundamental + overtone) * envelope * maxAmp;
                samples[currentSampleIdx] = (short)Math.Clamp(val, -32767, 32767);
            }
        }

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // RIFF header
        bw.Write(new[] { 'R', 'I', 'F', 'F' });
        bw.Write(36 + totalSamples * 2);
        bw.Write(new[] { 'W', 'A', 'V', 'E' });

        // fmt subchunk
        bw.Write(new[] { 'f', 'm', 't', ' ' });
        bw.Write(16); // subchunk size
        bw.Write((short)1); // PCM format
        bw.Write((short)1); // mono channel
        bw.Write(sampleRate); // sample rate
        bw.Write(sampleRate * 2); // byte rate (sampleRate * numChannels * bitsPerSample/8)
        bw.Write((short)2); // block align (numChannels * bitsPerSample/8)
        bw.Write((short)16); // bits per sample

        // data subchunk
        bw.Write(new[] { 'd', 'a', 't', 'a' });
        bw.Write(totalSamples * 2);

        foreach (var s in samples)
        {
            bw.Write(s);
        }

        bw.Flush();
        return ms.ToArray();
    }
}

