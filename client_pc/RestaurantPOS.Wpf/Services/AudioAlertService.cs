using System;
using System.IO;
using System.Media;
using System.Threading.Tasks;

namespace RestaurantPOS.Wpf.Services;

/// <summary>
/// Professional synthesized audio alerts for restaurant order notifications
/// Zero external audio files required - generates clean, punchy PCM WAV chimes in memory
/// </summary>
public static class AudioAlertService
{
    private static byte[]? _orderAlertWav;
    private static int _cachedOrderVolume = -1;

    private static byte[]? _successChimeWav;
    private static int _cachedSuccessVolume = -1;

    public static void PlayOrderAlert(int volumePercent = 90)
    {
        if (volumePercent <= 0) return;
        volumePercent = Math.Clamp(volumePercent, 1, 100);

        Task.Run(() =>
        {
            try
            {
                if (_orderAlertWav == null || _cachedOrderVolume != volumePercent)
                {
                    _orderAlertWav = GenerateChimeWav(volumePercent, 
                        new[] { (880.0, 0.16), (1320.0, 0.22), (1760.0, 0.45) });
                    _cachedOrderVolume = volumePercent;
                }

                using var ms = new MemoryStream(_orderAlertWav);
                using var player = new SoundPlayer(ms);
                player.Play();
            }
            catch (Exception ex)
            {
                PosLogger.Warn("[Audio] Failed to play order alert chime: " + ex.Message);
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
        PlayOrderAlert(volumePercent);
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
