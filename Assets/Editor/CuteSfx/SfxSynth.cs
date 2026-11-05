using System;
using System.IO;
using UnityEngine;

// Small offline synthesizer for the cute SFX set: tones with pitch sweeps, filtered noise, crackle,
// echo and seamless loops, written out as 16-bit mono WAV files.
public class SfxSynth
{
    public const int Rate = 44100;
    public enum Wave { Sine, Triangle, Square, Saw, Pulse }

    public float[] Data;
    private readonly System.Random rng;

    public SfxSynth(float seconds, int seed = 1)
    {
        Data = new float[Mathf.Max(1, (int)(seconds * Rate))];
        rng = new System.Random(seed);
    }

    public float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
    public static float Note(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

    private static float Osc(Wave wave, double phase)
    {
        double p = phase - Math.Floor(phase);
        switch (wave)
        {
            case Wave.Sine: return (float)Math.Sin(2 * Math.PI * p);
            case Wave.Triangle: return (float)(4 * Math.Abs(p - 0.5) - 1);
            case Wave.Square: return p < 0.5 ? 0.7f : -0.7f;
            case Wave.Saw: return (float)(2 * p - 1) * 0.7f;
            default: return p < 0.25 ? 0.7f : -0.7f;
        }
    }

    // A tone sweeping exponentially from f0 to f1, with attack and a power-curve decay.
    public SfxSynth Tone(float start, float dur, float f0, float f1, Wave wave, float vol,
        float attack = 0.004f, float decay = 1.5f, float vibHz = 0f, float vibDepth = 0f, float curve = 1f, float tremHz = 0f)
    {
        int s0 = (int)(start * Rate), n = (int)(dur * Rate), a = Mathf.Max(1, (int)(attack * Rate));
        double phase = 0;
        for (int i = 0; i < n && s0 + i < Data.Length; i++)
        {
            if (s0 + i < 0) continue;
            float t = i / (float)n;
            float f = f0 * Mathf.Pow(f1 / f0, Mathf.Pow(t, curve));
            if (vibHz > 0) f *= 1f + vibDepth * Mathf.Sin(2 * Mathf.PI * vibHz * i / Rate);
            phase += f / Rate;
            float env = Mathf.Min(1f, i / (float)a) * Mathf.Pow(1f - t, decay);
            if (tremHz > 0) env *= 0.55f + 0.45f * Mathf.Sin(2 * Mathf.PI * tremHz * i / Rate);
            Data[s0 + i] += Osc(wave, phase) * env * vol;
        }
        return this;
    }

    // Noise through a low-pass that sweeps from lp0 to lp1, plus an optional high-pass.
    public SfxSynth Noise(float start, float dur, float vol, float lp0, float lp1, float hp = 0f,
        float attack = 0.002f, float decay = 1.5f, float tremHz = 0f)
    {
        int s0 = (int)(start * Rate), n = (int)(dur * Rate), a = Mathf.Max(1, (int)(attack * Rate));
        float low = 0f, highLow = 0f;
        float hpCoef = hp > 0 ? 1f - Mathf.Exp(-2f * Mathf.PI * hp / Rate) : 0f;
        for (int i = 0; i < n && s0 + i < Data.Length; i++)
        {
            float t = i / (float)n;
            float lp = Mathf.Lerp(lp0, lp1, t);
            float lpCoef = 1f - Mathf.Exp(-2f * Mathf.PI * lp / Rate);
            low += lpCoef * ((float)rng.NextDouble() * 2f - 1f - low);
            float y = low;
            if (hp > 0) { highLow += hpCoef * (y - highLow); y -= highLow; }
            float env = Mathf.Min(1f, i / (float)a) * Mathf.Pow(1f - t, decay);
            if (tremHz > 0) env *= 0.5f + 0.5f * Mathf.Sin(2 * Mathf.PI * tremHz * i / Rate);
            if (s0 + i >= 0) Data[s0 + i] += y * env * vol * 2.2f;
        }
        return this;
    }

    // Random little pops, like fire crackle or crumbling debris.
    public SfxSynth Crackle(float start, float dur, float vol, float perSecond, float tone = 3000f)
    {
        int count = Mathf.Max(1, (int)(dur * perSecond));
        for (int c = 0; c < count; c++)
        {
            float at = start + R(0f, dur);
            float len = R(0.004f, 0.02f);
            Noise(at, len, vol * R(0.3f, 1f), tone * R(0.6f, 1.4f), tone * 0.4f, 300f, 0.0005f, 3f);
        }
        return this;
    }

    // Short glassy tinks spread over time, for sparkles and shatters.
    public SfxSynth Tinks(float start, float dur, int count, float fLow, float fHigh, float vol)
    {
        for (int c = 0; c < count; c++)
        {
            float f = R(fLow, fHigh);
            Tone(start + R(0f, dur), R(0.05f, 0.18f), f, f * R(0.98f, 1.03f), Wave.Sine, vol * R(0.4f, 1f), 0.001f, 2.5f);
        }
        return this;
    }

    public SfxSynth Echo(float delay, float feedback, float mix)
    {
        int d = (int)(delay * Rate);
        var wet = new float[Data.Length];
        for (int i = d; i < Data.Length; i++) wet[i] = Data[i - d] + wet[i - d] * feedback;
        for (int i = 0; i < Data.Length; i++) Data[i] += wet[i] * mix;
        return this;
    }

    // Normalizes to the target peak, soft-clips, and fades the edges so nothing clicks.
    public SfxSynth Finish(float peak = 0.85f, float fadeOut = 0.012f)
    {
        float max = 0.0001f;
        foreach (float v in Data) max = Mathf.Max(max, Mathf.Abs(v));
        float gain = peak / max;
        int fade = Mathf.Min(Data.Length / 2, (int)(fadeOut * Rate));
        int fadeIn = Mathf.Min(Data.Length / 2, 32);
        for (int i = 0; i < Data.Length; i++)
        {
            float v = (float)Math.Tanh(Data[i] * gain * 1.1f) / (float)Math.Tanh(1.1f);
            if (i < fadeIn) v *= i / (float)fadeIn;
            if (i > Data.Length - fade) v *= (Data.Length - i) / (float)fade;
            Data[i] = v;
        }
        return this;
    }

    // Folds the tail onto the start, so the clip loops without a seam.
    public SfxSynth MakeLoop(float crossfade)
    {
        int cf = (int)(crossfade * Rate);
        int len = Data.Length - cf;
        var loop = new float[len];
        Array.Copy(Data, loop, len);
        for (int i = 0; i < cf; i++)
        {
            float w = i / (float)cf;
            loop[i] = Data[len + i] * (1f - w) + Data[i] * w;
        }
        Data = loop;
        return this;
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using var stream = new FileStream(path, FileMode.Create);
        using var w = new BinaryWriter(stream);
        int bytes = Data.Length * 2;
        w.Write(new[] { 'R', 'I', 'F', 'F' });
        w.Write(36 + bytes);
        w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
        w.Write(16);
        w.Write((short)1);
        w.Write((short)1);
        w.Write(Rate);
        w.Write(Rate * 2);
        w.Write((short)2);
        w.Write((short)16);
        w.Write(new[] { 'd', 'a', 't', 'a' });
        w.Write(bytes);
        foreach (float v in Data) w.Write((short)Mathf.Clamp(v * 32767f, -32768f, 32767f));
    }
}
