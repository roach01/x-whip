using System;
using System.Text;
using XWhip;

internal static class WhipSoundTests
{
    private static int passed;

    private static int Main()
    {
        try
        {
            Run("Wave file has valid RIFF and chunk sizes", Headers);
            Run("Wave uses mono sixteen-bit PCM", PcmFormat);
            Run("Wave lasts between one hundred eighty and two hundred forty milliseconds", Duration);
            Run("Wave contains non-silent audio", NonSilent);
            Run("Samples retain volume headroom without clipping", Headroom);
            Run("Fixed seed produces identical bytes", Deterministic);
            Run("Snap decays to a quiet tail", QuietTail);
            Console.WriteLine("PASS: " + passed + " tests");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex.Message);
            return 1;
        }
    }

    private static void Run(string name, Action test)
    { test(); passed++; Console.WriteLine("PASS " + name); }
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); }
    private static string Tag(byte[] wave, int offset, int count)
    { return Encoding.ASCII.GetString(wave, offset, count); }

    private static void Headers()
    {
        byte[] wave = WhipSound.BuildWave();
        Check(wave.Length > 44, "audio data exists");
        Check(Tag(wave, 0, 4) == "RIFF", "RIFF tag");
        Check(BitConverter.ToInt32(wave, 4) == wave.Length - 8, "RIFF size");
        Check(Tag(wave, 8, 4) == "WAVE", "WAVE tag");
        Check(Tag(wave, 12, 4) == "fmt ", "format chunk");
        Check(BitConverter.ToInt32(wave, 16) == 16, "PCM format chunk length");
        Check(Tag(wave, 36, 4) == "data", "data chunk");
        Check(BitConverter.ToInt32(wave, 40) == wave.Length - 44, "data size");
    }

    private static void PcmFormat()
    {
        byte[] wave = WhipSound.BuildWave();
        Check(BitConverter.ToInt16(wave, 20) == 1, "PCM format");
        Check(BitConverter.ToInt16(wave, 22) == 1, "mono channel");
        int rate = BitConverter.ToInt32(wave, 24);
        Check(rate == 22050, "sample rate");
        Check(BitConverter.ToInt32(wave, 28) == rate * 2, "byte rate");
        Check(BitConverter.ToInt16(wave, 32) == 2, "sample alignment");
        Check(BitConverter.ToInt16(wave, 34) == 16, "sample bit depth");
        Check((wave.Length - 44) % 2 == 0, "whole PCM frames");
    }

    private static void Duration()
    {
        byte[] wave = WhipSound.BuildWave();
        double seconds = (double)BitConverter.ToInt32(wave, 40) / BitConverter.ToInt32(wave, 28);
        Check(seconds >= .18 && seconds <= .24, "short effect duration: " + seconds);
    }

    private static void NonSilent()
    {
        byte[] wave = WhipSound.BuildWave();
        Check(Rms(wave, 0, .13) > .005, "audible whoosh");
        Check(Rms(wave, .13, .16) > .025, "audible snap");
    }

    private static void Headroom()
    {
        byte[] wave = WhipSound.BuildWave();
        int peak = 0;
        for (int offset = 44; offset < wave.Length; offset += 2)
            peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(wave, offset)));
        Check(peak > 1000, "useful signal peak");
        Check(peak < 16384, "restrained peak and no clipping: " + peak);
    }

    private static void Deterministic()
    {
        byte[] first = WhipSound.BuildWave(), second = WhipSound.BuildWave();
        Check(first.Length == second.Length, "consistent data length");
        for (int i = 0; i < first.Length; i++) Check(first[i] == second[i], "consistent byte " + i);
    }

    private static void QuietTail()
    {
        byte[] wave = WhipSound.BuildWave();
        Check(Rms(wave, .195, .21) < Rms(wave, .13, .145) / 30, "one snap with a quiet tail");
        Check(Math.Abs((int)BitConverter.ToInt16(wave, wave.Length - 2)) < 20, "no abrupt last-sample edge");
    }

    private static double Rms(byte[] wave, double start, double end)
    {
        int rate = BitConverter.ToInt32(wave, 24), count = (wave.Length - 44) / 2;
        int first = (int)(start * rate), last = Math.Min(count, (int)(end * rate));
        double sum = 0;
        for (int i = first; i < last; i++)
        {
            double sample = BitConverter.ToInt16(wave, 44 + i * 2) / 32768.0;
            sum += sample * sample;
        }
        return Math.Sqrt(sum / (last - first));
    }
}
