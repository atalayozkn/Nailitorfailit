using System;
using System.Collections.Generic;
using UnityEngine;
using W = SfxSynth.Wave;

// The cute SFX set: every sound is built from a few layered tones and noises.
// Rules of thumb: rising pitch = good, falling = bad, short and bouncy, a little echo on celebrations.
public static class CuteSfxRecipes
{
    public class Recipe
    {
        public string Name;
        public int Variations;
        public bool Loop;
        public Func<int, SfxSynth> Build; // variation index -> finished sound
    }

    static Recipe One(string name, Func<int, SfxSynth> build, int variations = 1, bool loop = false) =>
        new Recipe { Name = name, Build = build, Variations = variations, Loop = loop };

    static float N(int midi) => SfxSynth.Note(midi);

    public static readonly List<Recipe> All = new()
    {
        // ---------- Construction ----------
        One("Construction/TilePlace", v =>
        {
            var s = new SfxSynth(0.3f, 10 + v); float k = s.R(0.92f, 1.08f);
            s.Tone(0, 0.14f, 190 * k, 70 * k, W.Sine, 0.9f, 0.002f, 3f);
            s.Noise(0, 0.06f, 0.35f, 1400, 300, 80);
            s.Tone(0.012f, 0.06f, 520 * k, 760 * k, W.Triangle, 0.22f, 0.002f, 2f);
            return s.Finish(0.8f);
        }, 2),
        One("Construction/Nail", v =>
        {
            var s = new SfxSynth(0.28f, 20 + v); float k = s.R(0.9f, 1.12f);
            s.Noise(0, 0.014f, 0.6f, 9000, 6000, 2000);
            s.Tone(0, 0.16f, 1850 * k, 1840 * k, W.Sine, 0.32f, 0.001f, 4f);
            s.Tone(0, 0.11f, 2770 * k, 2760 * k, W.Sine, 0.18f, 0.001f, 5f);
            s.Tone(0.002f, 0.07f, 330 * k, 180 * k, W.Sine, 0.5f, 0.001f, 2.5f);
            return s.Finish(0.75f);
        }, 4),
        One("Construction/TileFinished", v =>
        {
            var s = new SfxSynth(0.7f, 30);
            s.Tone(0, 0.2f, N(88), N(88), W.Triangle, 0.5f, 0.003f, 1.8f);
            s.Tone(0.09f, 0.35f, N(95), N(95), W.Triangle, 0.5f, 0.003f, 1.6f);
            s.Tone(0.09f, 0.3f, N(107), N(107), W.Sine, 0.12f, 0.003f, 2f);
            return s.Echo(0.09f, 0.3f, 0.35f).Finish(0.75f);
        }),
        One("Construction/RoomFinished", v =>
        {
            var s = new SfxSynth(1.9f, 40);
            int[] arp = { 72, 76, 79, 84 };
            for (int i = 0; i < arp.Length; i++)
            {
                s.Tone(i * 0.1f, 0.16f, N(arp[i]), N(arp[i]), W.Pulse, 0.22f, 0.003f, 1.4f);
                s.Tone(i * 0.1f, 0.16f, N(arp[i]), N(arp[i]), W.Triangle, 0.3f, 0.003f, 1.4f);
            }
            foreach (int m in new[] { 84, 88, 91 })
                s.Tone(0.42f, 1.0f, N(m), N(m), W.Triangle, 0.3f, 0.01f, 1.3f, 5.5f, 0.006f);
            s.Tone(0.42f, 0.9f, N(48), N(48), W.Sine, 0.35f, 0.01f, 1.6f);
            s.Tinks(0.45f, 0.9f, 10, 2400, 4800, 0.12f);
            return s.Echo(0.11f, 0.3f, 0.3f).Finish(0.8f, 0.08f);
        }),
        One("Construction/PickUp", v =>
        {
            var s = new SfxSynth(0.16f, 50 + v); float k = s.R(0.9f, 1.15f);
            s.Tone(0, 0.08f, 380 * k, 900 * k, W.Sine, 0.7f, 0.003f, 1.8f);
            s.Tone(0.01f, 0.06f, 760 * k, 1500 * k, W.Triangle, 0.15f, 0.003f, 2f);
            return s.Finish(0.7f);
        }, 3),
        One("Construction/TakeWood", v =>
        {
            var s = new SfxSynth(0.25f, 61);
            s.Noise(0, 0.01f, 0.4f, 5000, 3000, 800);
            s.Tone(0, 0.13f, 260, 245, W.Sine, 0.6f, 0.001f, 3f);
            s.Tone(0, 0.09f, 540, 520, W.Triangle, 0.25f, 0.001f, 3f);
            s.Tone(0.09f, 0.1f, 300, 280, W.Sine, 0.4f, 0.001f, 3f);
            return s.Finish(0.75f);
        }),
        One("Construction/TakeBrick", v =>
        {
            var s = new SfxSynth(0.25f, 62);
            s.Noise(0, 0.11f, 0.45f, 2600, 1200, 400, 0.01f, 1.2f);
            s.Tone(0.1f, 0.07f, 720, 640, W.Triangle, 0.4f, 0.001f, 3f);
            s.Tone(0.1f, 0.09f, 200, 140, W.Sine, 0.45f, 0.001f, 2.5f);
            return s.Finish(0.75f);
        }),
        One("Construction/TakeCement", v =>
        {
            var s = new SfxSynth(0.3f, 63);
            s.Noise(0, 0.18f, 0.6f, 700, 250, 60, 0.01f, 1.4f);
            s.Tone(0, 0.2f, 130, 75, W.Sine, 0.6f, 0.005f, 2f);
            s.Tone(0.04f, 0.07f, 420, 620, W.Triangle, 0.12f, 0.003f, 2f);
            return s.Finish(0.75f);
        }),
        One("Construction/TakeGlass", v =>
        {
            var s = new SfxSynth(0.4f, 64);
            s.Tone(0, 0.3f, 3200, 3190, W.Sine, 0.35f, 0.001f, 3f);
            s.Tone(0.004f, 0.25f, 4700, 4690, W.Sine, 0.2f, 0.001f, 3.5f);
            s.Tone(0.07f, 0.22f, 3600, 3590, W.Sine, 0.25f, 0.001f, 3f);
            return s.Finish(0.6f);
        }),

        // ---------- Stations ----------
        One("Stations/CutterCut", v =>
        {
            var s = new SfxSynth(0.32f, 70 + v); float k = s.R(0.92f, 1.1f);
            s.Tone(0, 0.26f, 120 * k, 165 * k, W.Square, 0.16f, 0.01f, 1.2f, 34f, 0.08f);
            s.Noise(0, 0.26f, 0.3f, 3000, 2200, 800, 0.01f, 1.2f, 27f);
            s.Noise(0.2f, 0.09f, 0.14f, 4500, 3000, 1500, 0.002f, 2f);
            return s.Finish(0.62f);
        }, 3),
        One("Stations/ProductOut", v =>
        {
            var s = new SfxSynth(0.2f, 80);
            s.Tone(0, 0.08f, 300, 950, W.Sine, 0.8f, 0.002f, 2f);
            s.Tone(0.03f, 0.06f, 950, 1350, W.Triangle, 0.25f, 0.002f, 2f);
            return s.Finish(0.75f);
        }),
        One("Stations/MixerDone", v =>
        {
            var s = new SfxSynth(1.2f, 90);
            s.Tone(0, 0.55f, 85, 95, W.Sine, 0.45f, 0.03f, 0.6f, 0f, 0f, 1f, 7f);
            for (int i = 0; i < 4; i++) s.Tone(0.04f + i * 0.13f, 0.07f, 260 + i * 40, 560 + i * 60, W.Sine, 0.4f, 0.003f, 2f);
            s.Tone(0.62f, 0.4f, N(84), N(84), W.Triangle, 0.4f, 0.003f, 1.6f);
            s.Tone(0.7f, 0.45f, N(91), N(91), W.Triangle, 0.35f, 0.003f, 1.6f);
            return s.Echo(0.09f, 0.25f, 0.25f).Finish(0.78f, 0.05f);
        }),
        One("Stations/MixerFail", v =>
        {
            var s = new SfxSynth(1.1f, 100);
            s.Tone(0, 0.45f, 170, 150, W.Sine, 0.6f, 0.003f, 1.4f, 13f, 0.35f);
            s.Tone(0.42f, 0.22f, N(67), N(66), W.Triangle, 0.4f, 0.01f, 0.8f);
            s.Tone(0.66f, 0.4f, N(65), N(63), W.Triangle, 0.4f, 0.01f, 1.4f, 6f, 0.02f);
            return s.Finish(0.75f, 0.05f);
        }),
        One("Stations/Refuel", v =>
        {
            var s = new SfxSynth(0.6f, 110 + v);
            s.Noise(0, 0.5f, 0.12f, 600, 400, 80, 0.03f, 0.8f);
            for (int i = 0; i < 4; i++)
            {
                float f = s.R(210f, 300f);
                s.Tone(i * 0.11f + s.R(0f, 0.02f), 0.07f, f, f * 2.3f, W.Sine, 0.55f, 0.004f, 1.6f);
            }
            return s.Finish(0.72f);
        }, 3),
        One("Stations/OutOfPower", v =>
        {
            var s = new SfxSynth(1.0f, 120);
            s.Tone(0, 0.62f, 660, 95, W.Triangle, 0.5f, 0.005f, 0.9f, 0f, 0f, 0.6f);
            s.Tone(0, 0.62f, 330, 48, W.Square, 0.12f, 0.005f, 0.9f, 0f, 0f, 0.6f);
            foreach (float at in new[] { 0.66f, 0.76f, 0.84f }) s.Noise(at, 0.05f, 0.35f, 900, 300, 100, 0.002f, 2f);
            return s.Finish(0.75f, 0.05f);
        }),

        // ---------- Fire ----------
        One("Fire/Ignite", v =>
        {
            var s = new SfxSynth(0.6f, 130);
            s.Noise(0, 0.5f, 0.6f, 350, 2800, 90, 0.06f, 1.4f);
            s.Tone(0, 0.4f, 95, 55, W.Sine, 0.35f, 0.03f, 1.5f);
            s.Crackle(0.15f, 0.35f, 0.25f, 30f);
            return s.Finish(0.75f, 0.05f);
        }),
        One("Fire/BurnLoop", v =>
        {
            var s = new SfxSynth(3.4f, 140);
            s.Noise(0, 3.4f, 0.13f, 650, 650, 60, 0.3f, 0f, 0.6f);
            s.Crackle(0, 3.4f, 0.32f, 16f, 3200f);
            s.Crackle(0, 3.4f, 0.18f, 8f, 1500f);
            return s.Finish(0.6f, 0f).MakeLoop(0.35f);
        }, 1, true),
        One("Fire/Out", v =>
        {
            var s = new SfxSynth(0.8f, 150);
            s.Noise(0, 0.12f, 0.45f, 700, 300, 60, 0.002f, 2f);
            s.Noise(0.02f, 0.65f, 0.4f, 5500, 3200, 1600, 0.02f, 1.3f);
            s.Tone(0.05f, 0.3f, 1200, 480, W.Sine, 0.12f, 0.01f, 1.5f);
            return s.Finish(0.7f, 0.05f);
        }),

        // ---------- Tile damage ----------
        One("TileDamage/DestroyedWood", v =>
        {
            var s = new SfxSynth(0.6f, 160);
            s.Crackle(0, 0.28f, 0.7f, 120f, 2500f);
            s.Tone(0, 0.2f, 150, 60, W.Sine, 0.7f, 0.002f, 2f);
            s.Tone(0.03f, 0.12f, 420, 240, W.Triangle, 0.3f, 0.002f, 2f);
            s.Tone(0.25f, 0.22f, N(62), N(58), W.Triangle, 0.22f, 0.01f, 1.5f);
            return s.Finish(0.8f, 0.04f);
        }),
        One("TileDamage/DestroyedBrick", v =>
        {
            var s = new SfxSynth(0.7f, 170);
            s.Noise(0, 0.45f, 0.55f, 1600, 300, 80, 0.003f, 1.5f);
            s.Crackle(0, 0.5f, 0.45f, 60f, 1800f);
            s.Tone(0, 0.26f, 105, 48, W.Sine, 0.7f, 0.002f, 2f);
            return s.Finish(0.8f, 0.05f);
        }),
        One("TileDamage/DestroyedGlass", v =>
        {
            var s = new SfxSynth(0.8f, 180);
            s.Noise(0, 0.1f, 0.35f, 9000, 6000, 4000, 0.001f, 2f);
            s.Tinks(0, 0.55f, 16, 2500, 6200, 0.3f);
            return s.Echo(0.07f, 0.2f, 0.2f).Finish(0.72f, 0.05f);
        }),
        One("TileDamage/HitWood", v =>
        {
            var s = new SfxSynth(0.2f, 190 + v);
            s.Tone(0, 0.14f, 300, 250, W.Triangle, 0.35f, 0.004f, 1.6f, 22f, 0.05f);
            s.Noise(0, 0.03f, 0.25f, 3000, 1500, 500);
            return s.Finish(0.5f);
        }, 2),
        One("TileDamage/HitBrick", v =>
        {
            var s = new SfxSynth(0.15f, 195 + v);
            s.Noise(0, 0.04f, 0.5f, 6000, 2500, 1500, 0.001f, 2f);
            s.Tone(0, 0.04f, 900, 760, W.Triangle, 0.25f, 0.001f, 2.5f);
            return s.Finish(0.5f);
        }, 2),
        One("TileDamage/HitGlass", v =>
        {
            var s = new SfxSynth(0.2f, 198 + v); float f = s.R(3800f, 4600f);
            s.Tone(0, 0.08f, f, f, W.Sine, 0.4f, 0.001f, 3f);
            s.Tone(0.03f, 0.06f, f * 1.12f, f * 1.12f, W.Sine, 0.2f, 0.001f, 3f);
            return s.Finish(0.45f);
        }, 2),

        // ---------- Water and electric ----------
        One("Hazards/PipeBurst", v =>
        {
            var s = new SfxSynth(1.1f, 210);
            s.Tone(0, 0.08f, 700, 300, W.Triangle, 0.5f, 0.001f, 2f);
            s.Noise(0.02f, 0.95f, 0.5f, 3800, 2600, 500, 0.02f, 0.9f);
            for (int i = 0; i < 6; i++) { float f = s.R(380f, 600f); s.Tone(s.R(0.1f, 0.8f), 0.06f, f, f * 2f, W.Sine, 0.25f, 0.003f, 1.8f); }
            return s.Finish(0.72f, 0.06f);
        }),
        One("Hazards/SocketSpark", v =>
        {
            var s = new SfxSynth(0.25f, 220 + v); float k = s.R(0.9f, 1.15f);
            s.Noise(0, 0.06f, 0.6f, 9000, 6000, 2000, 0.001f, 2f);
            s.Tone(0, 0.1f, 1800 * k, 600 * k, W.Square, 0.2f, 0.001f, 1.5f);
            s.Crackle(0.02f, 0.12f, 0.4f, 40f, 5000f);
            return s.Finish(0.7f);
        }, 2),
        One("Hazards/ElectrifiedLoop", v =>
        {
            var s = new SfxSynth(2.3f, 230);
            s.Tone(0, 2.3f, 110, 110, W.Square, 0.12f, 0.05f, 0f, 0f, 0f, 1f, 8f);
            s.Tone(0, 2.3f, 220, 220, W.Saw, 0.06f, 0.05f, 0f);
            s.Crackle(0, 2.3f, 0.2f, 6f, 6000f);
            return s.Finish(0.5f, 0f).MakeLoop(0.25f);
        }, 1, true),
        One("Hazards/PlayerElectrocuted", v =>
        {
            var s = new SfxSynth(1.1f, 240);
            s.Tone(0, 0.7f, 220, 240, W.Square, 0.22f, 0.003f, 0.6f, 18f, 0.5f);
            s.Crackle(0, 0.7f, 0.3f, 40f, 6000f);
            s.Tone(0.74f, 0.18f, 700, 380, W.Triangle, 0.45f, 0.003f, 1.4f);
            s.Tone(0.9f, 0.1f, 300, 900, W.Sine, 0.3f, 0.003f, 2f);
            return s.Finish(0.75f, 0.04f);
        }),

        // ---------- Traffic ----------
        One("Traffic/CarEngineLoop", v =>
        {
            var s = new SfxSynth(1.7f, 250);
            s.Tone(0, 1.7f, 58, 58, W.Square, 0.25f, 0.05f, 0f, 0f, 0f, 1f, 9f);
            s.Tone(0, 1.7f, 116, 116, W.Triangle, 0.12f, 0.05f, 0f, 0.5f, 0.02f);
            s.Noise(0, 1.7f, 0.1f, 280, 280, 40, 0.05f, 0f);
            return s.Finish(0.45f, 0f).MakeLoop(0.2f);
        }, 1, true),
        One("Traffic/Horn", v =>
        {
            var s = new SfxSynth(0.45f, 260 + v); float f = v == 0 ? 700f : 560f;
            s.Tone(0, 0.12f, f, f, W.Pulse, 0.35f, 0.006f, 0.4f);
            s.Tone(0, 0.12f, f * 1.25f, f * 1.25f, W.Triangle, 0.25f, 0.006f, 0.4f);
            s.Tone(0.2f, 0.15f, f, f, W.Pulse, 0.35f, 0.006f, 0.4f);
            s.Tone(0.2f, 0.15f, f * 1.25f, f * 1.25f, W.Triangle, 0.25f, 0.006f, 0.4f);
            return s.Finish(0.7f);
        }, 2),

        // ---------- Player ----------
        One("Player/OutOfStamina", v =>
        {
            var s = new SfxSynth(0.9f, 270);
            s.Noise(0, 0.2f, 0.45f, 1100, 600, 300, 0.03f, 1.2f);
            s.Noise(0.32f, 0.22f, 0.4f, 1000, 500, 300, 0.03f, 1.2f);
            s.Tone(0.05f, 0.6f, 720, 280, W.Sine, 0.16f, 0.02f, 1.2f, 5f, 0.02f);
            return s.Finish(0.65f, 0.04f);
        }),

        // ---------- Dog ----------
        One("Dog/Petted", v =>
        {
            var s = new SfxSynth(0.45f, 280 + v); float k = v == 0 ? 1f : 1.12f;
            s.Tone(0, 0.09f, 900 * k, 1400 * k, W.Sine, 0.5f, 0.004f, 1.2f, 25f, 0.03f);
            s.Tone(0.12f, 0.12f, 1000 * k, 1650 * k, W.Sine, 0.5f, 0.004f, 1.2f, 25f, 0.03f);
            s.Tinks(0.15f, 0.2f, 3, 2600, 3600, 0.1f);
            return s.Finish(0.65f);
        }, 2),
        One("Dog/Gift", v =>
        {
            var s = new SfxSynth(0.7f, 290);
            s.Noise(0, 0.08f, 0.35f, 2600, 1200, 300);
            s.Tone(0, 0.12f, 520, 360, W.Square, 0.3f, 0.003f, 1.5f);
            s.Tone(0, 0.12f, 1040, 720, W.Triangle, 0.15f, 0.003f, 1.5f);
            s.Tone(0.26f, 0.09f, 420, 200, W.Sine, 0.5f, 0.002f, 2f);
            s.Tone(0.38f, 0.25f, N(88), N(88), W.Triangle, 0.25f, 0.003f, 1.6f);
            return s.Echo(0.08f, 0.2f, 0.2f).Finish(0.7f, 0.04f);
        }),
        One("Dog/Rebel", v =>
        {
            var s = new SfxSynth(0.8f, 300 + v);
            s.Tone(0, 0.5f, 90, 80, W.Saw, 0.35f, 0.04f, 0.8f, 0f, 0f, 1f, 30f);
            s.Noise(0, 0.5f, 0.2f, 500, 400, 80, 0.04f, 0.8f, 30f);
            s.Tone(0.52f, 0.07f, 620, 640, W.Triangle, 0.3f, 0.003f, 1.5f);
            s.Tone(0.63f, 0.08f, 720, 760, W.Triangle, 0.3f, 0.003f, 1.5f);
            return s.Finish(0.65f, 0.03f);
        }, 2),
        One("Dog/MoodWorse", v =>
        {
            var s = new SfxSynth(0.75f, 310);
            s.Tone(0, 0.26f, 300, 230, W.Triangle, 0.45f, 0.01f, 0.9f, 6f, 0.03f);
            s.Tone(0.28f, 0.4f, 255, 185, W.Triangle, 0.45f, 0.01f, 1.4f, 6f, 0.03f);
            return s.Finish(0.6f, 0.04f);
        }),
        One("Dog/BowlFill", v =>
        {
            var s = new SfxSynth(0.8f, 320);
            for (int i = 0; i < 34; i++)
            {
                float at = Mathf.Pow(s.R(0f, 1f), 1.4f) * 0.62f;
                float f = s.R(1700f, 3400f);
                s.Tone(at, 0.012f, f, f, W.Sine, s.R(0.15f, 0.35f), 0.0005f, 2f);
                s.Noise(at, 0.006f, 0.15f, 6000, 4000, 1500);
            }
            s.Tone(0, 0.6f, 880, 875, W.Sine, 0.05f, 0.01f, 2f);
            return s.Finish(0.6f, 0.05f);
        }),

        // ---------- Mail ----------
        One("Mail/Arrived", v =>
        {
            var s = new SfxSynth(0.8f, 330);
            s.Tone(0, 0.08f, 3000, 2950, W.Sine, 0.25f, 0.001f, 3f);
            s.Tone(0.04f, 0.25f, N(88), N(88), W.Triangle, 0.45f, 0.003f, 1.4f);
            s.Tone(0.24f, 0.45f, N(84), N(84), W.Triangle, 0.45f, 0.003f, 1.4f);
            return s.Echo(0.1f, 0.25f, 0.25f).Finish(0.72f, 0.05f);
        }),
        One("Mail/Collect", v =>
        {
            var s = new SfxSynth(0.9f, 340);
            s.Noise(0, 0.25f, 0.25f, 7000, 5000, 2000, 0.01f, 1f, 35f);
            int[] up = { 84, 88, 91, 96 };
            for (int i = 0; i < up.Length; i++) s.Tone(0.12f + i * 0.07f, 0.18f, N(up[i]), N(up[i]), W.Triangle, 0.35f, 0.003f, 1.5f);
            s.Tinks(0.38f, 0.3f, 4, 3000, 4500, 0.1f);
            return s.Echo(0.09f, 0.25f, 0.25f).Finish(0.72f, 0.05f);
        }),

        // ---------- UI and flow ----------
        One("UI/Hover", v =>
        {
            var s = new SfxSynth(0.06f, 350);
            s.Tone(0, 0.03f, 1500, 1600, W.Sine, 0.5f, 0.001f, 2f);
            return s.Finish(0.4f);
        }),
        One("UI/Click", v =>
        {
            var s = new SfxSynth(0.12f, 360);
            s.Tone(0, 0.06f, 600, 1150, W.Sine, 0.6f, 0.001f, 1.6f);
            s.Tone(0.01f, 0.04f, 1200, 1300, W.Triangle, 0.15f, 0.001f, 2f);
            return s.Finish(0.6f);
        }),
        One("UI/Warning", v =>
        {
            var s = new SfxSynth(0.32f, 370);
            s.Tone(0, 0.1f, 330, 300, W.Triangle, 0.45f, 0.004f, 1f);
            s.Tone(0.13f, 0.12f, 300, 265, W.Triangle, 0.45f, 0.004f, 1.2f);
            return s.Finish(0.5f);
        }),
        One("UI/Victory", v =>
        {
            var s = new SfxSynth(3.4f, 380);
            int[] melody = { 67, 72, 76, 79, 76, 79 };
            float[] times = { 0f, 0.14f, 0.28f, 0.42f, 0.7f, 0.84f };
            for (int i = 0; i < melody.Length; i++)
            {
                s.Tone(times[i], 0.22f, N(melody[i]), N(melody[i]), W.Pulse, 0.18f, 0.004f, 1.2f);
                s.Tone(times[i], 0.22f, N(melody[i]), N(melody[i]), W.Triangle, 0.3f, 0.004f, 1.2f);
            }
            foreach (int m in new[] { 72, 76, 79, 84 }) s.Tone(1.0f, 1.9f, N(m), N(m), W.Triangle, 0.22f, 0.02f, 1.4f, 5f, 0.006f);
            s.Tone(0, 0.4f, N(48), N(48), W.Sine, 0.4f, 0.005f, 1.2f);
            s.Tone(0.42f, 0.4f, N(55), N(55), W.Sine, 0.4f, 0.005f, 1.2f);
            s.Tone(1.0f, 1.6f, N(48), N(48), W.Sine, 0.45f, 0.01f, 1.4f);
            s.Tinks(1.0f, 1.6f, 14, 2400, 5200, 0.12f);
            return s.Echo(0.12f, 0.3f, 0.3f).Finish(0.8f, 0.15f);
        }),
        One("UI/Defeat", v =>
        {
            var s = new SfxSynth(3.0f, 390);
            int[] fall = { 67, 66, 65 };
            for (int i = 0; i < fall.Length; i++)
            {
                s.Tone(i * 0.42f, 0.38f, N(fall[i]), N(fall[i]) * 0.985f, W.Triangle, 0.4f, 0.02f, 0.7f);
                s.Tone(i * 0.42f, 0.38f, N(fall[i]), N(fall[i]) * 0.985f, W.Saw, 0.1f, 0.02f, 0.7f);
            }
            s.Tone(1.26f, 1.5f, N(64), N(62), W.Triangle, 0.45f, 0.02f, 1.3f, 5.5f, 0.03f, 1.6f);
            s.Tone(1.26f, 1.5f, N(64), N(62), W.Saw, 0.1f, 0.02f, 1.3f, 5.5f, 0.03f, 1.6f);
            s.Tone(2.6f, 0.15f, 500, 900, W.Sine, 0.2f, 0.003f, 2f);
            return s.Finish(0.72f, 0.1f);
        }),

        // ---------- Birds (style test ambience) ----------
        One("Birds/Chirp", v =>
        {
            var s = new SfxSynth(1.4f, 400 + v);
            int notes = v == 2 ? 8 : 2 + v;
            float baseF = s.R(2800f, 4200f), t = 0f;
            for (int i = 0; i < notes; i++)
            {
                float len = v == 2 ? 0.045f : s.R(0.06f, 0.13f);
                float f0 = baseF * s.R(0.85f, 1.2f), f1 = f0 * (v == 2 ? 0.8f : s.R(0.6f, 1.5f));
                s.Tone(t, len, f0, f1, W.Sine, 0.45f, 0.003f, 0.9f, 32f, 0.04f, 0.6f);
                s.Tone(t, len, f0 * 2f, f1 * 2f, W.Sine, 0.06f, 0.003f, 0.9f, 32f, 0.04f, 0.6f);
                t += len + (v == 2 ? 0.02f : s.R(0.04f, 0.1f));
            }
            return s.Echo(0.13f, 0.15f, 0.12f).Finish(0.5f, 0.05f);
        }, 5),
    };
}
