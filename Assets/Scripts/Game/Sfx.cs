using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tuntum
{
    /// <summary>
    /// Every sound is synthesised at start-up (no audio files): swishes, snaps, chimes that climb
    /// with the streak, a buzzer, fanfares and a crowd cheer with "woo!" voices and clapping.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        public static AudioClip Click, Deal, Flip, Wrong, Whoosh, Yay, Join, Win, CheerSmall;
        static readonly Dictionary<int, AudioClip> correct = new Dictionary<int, AudioClip>();
        static readonly Dictionary<int, AudioClip> fanfare = new Dictionary<int, AudioClip>();
        static readonly Dictionary<int, AudioClip> cheer = new Dictionary<int, AudioClip>();
        static Sfx inst;
        AudioSource src;

        public static bool Muted
        {
            get => PlayerPrefs.GetInt("muted", 0) == 1;
            set { PlayerPrefs.SetInt("muted", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static void Create(GameObject host)
        {
            inst = host.AddComponent<Sfx>();
            inst.src = host.AddComponent<AudioSource>();
            inst.src.playOnAwake = false;
            Click = Render("click", .1f, s => s.Tone(520, .08f, Wave.Square, .12f, 0, 880));
            Deal = Render("deal", .25f, s => { s.Noise(.16f, 900, .8f, .35f, 0, 4200); s.Tone(180, .06f, Wave.Triangle, .15f, .12f); });
            Flip = Render("flip", .15f, s => { s.Noise(.07f, 3000, .6f, .3f); s.Tone(320, .09f, Wave.Triangle, .18f, .02f, 520); });
            Wrong = Render("wrong", .5f, s => { s.Tone(240, .42f, Wave.Saw, .18f, 0, 90); s.Tone(251, .42f, Wave.Square, .08f, 0, 95); s.Noise(.2f, 300, .7f, .3f, 0, 0, Filter.Low); });
            Whoosh = Render("whoosh", .4f, s => s.Noise(.35f, 400, 1.2f, .3f, 0, 3000));
            Yay = Render("yay", .35f, s => { float[] f = { 784, 988, 1175 }; for (int i = 0; i < 3; i++) s.Tone(f[i], .16f, Wave.Square, .08f, i * .06f); });
            Join = Render("join", .6f, s => { float[] f = { 392, 523, 659, 784 }; for (int i = 0; i < 4; i++) s.Tone(f[i], .2f, Wave.Triangle, .15f, i * .08f); });
            Win = Render("win", 1.8f, s =>
            {
                float[] f = { 523, 659, 784, 1047, 784, 1047 };
                for (int i = 0; i < f.Length; i++) s.Tone(f[i], i == 5 ? .7f : .18f, Wave.Square, .1f, i * .13f);
                foreach (var g in new[] { 523f, 659f, 784f }) s.Tone(g, .9f, Wave.Triangle, .12f, .8f);
            });
            CheerSmall = CheerClip(1.5f, .7f);
        }

        public static void Play(AudioClip c, float volume = 1f)
        {
            if (inst == null || c == null || Muted) return;
            inst.src.PlayOneShot(c, volume);
        }

        /// <summary>Chime for a right guess; it climbs a semitone with every card in the streak.</summary>
        public static AudioClip Correct(int streak)
        {
            int n = Mathf.Clamp(streak, 1, 14);
            if (!correct.TryGetValue(n, out var c))
            {
                float b = 440 * Mathf.Pow(2, n / 12f);
                correct[n] = c = Render("correct" + n, .45f, s => { s.Tone(b, .18f, Wave.Triangle, .28f); s.Tone(b * 1.5f, .3f, Wave.Triangle, .24f, .08f); s.Tone(b * 3, .25f, Wave.Sine, .08f, .12f); });
            }
            return c;
        }

        public static AudioClip Fanfare(int level)
        {
            int l = Mathf.Clamp(level, 0, 3);
            if (!fanfare.TryGetValue(l, out var c))
            {
                float b = new[] { 523f, 587f, 659f, 784f }[l];
                fanfare[l] = c = Render("fanfare" + l, 1.1f, s =>
                {
                    float[] m = { 1, 1.26f, 1.5f, 2, 2.52f };
                    for (int i = 0; i < m.Length; i++) s.Tone(b * m[i], .22f, Wave.Square, .1f, i * .07f);
                    s.Tone(b * 2, .6f, Wave.Triangle, .2f, .35f);
                    s.Noise(.5f, 7000, .7f, .18f, .33f, 0, Filter.High);
                });
            }
            return c;
        }

        /// <summary>Crowd cheer, bigger for longer streaks. Level 1 = the 3-in-a-row cheer.</summary>
        public static AudioClip Cheer(int level)
        {
            if (level <= 1) return CheerSmall;
            if (!cheer.TryGetValue(level, out var c)) cheer[level] = c = CheerClip(2.2f + level * .2f, 1 + level * .15f);
            return c;
        }

        static AudioClip CheerClip(float dur, float size) => Render("cheer", dur + .2f, s =>
        {
            s.Noise(dur, 1100, .6f, .22f * size, 0, 0, Filter.Band, .25f);
            s.Noise(dur * .9f, 2600, .9f, .1f * size, 0, 0, Filter.Band, .3f);
            int voices = Mathf.RoundToInt(9 * size + 3);
            for (int v = 0; v < voices; v++)
            {
                float st = s.Rand() * .35f, len = .5f + s.Rand() * dur * .6f, f = 230 + s.Rand() * 260;
                s.Voice(st, len, f, f * (1.35f + s.Rand() * .5f), 700 + s.Rand() * 500, .05f * size, 5 + s.Rand() * 3);
            }
            int claps = Mathf.RoundToInt(26 * size);
            for (int c = 0; c < claps; c++) s.Noise(.04f, 1500 + s.Rand() * 1200, 1.4f, .18f + s.Rand() * .2f, .1f + s.Rand() * (dur - .2f));
        });

        static AudioClip Render(string name, float seconds, Action<Synth> draw)
        {
            var s = new Synth(seconds);
            draw(s);
            var data = s.Finish();
            var clip = AudioClip.Create(name, data.Length, 1, Synth.Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public enum Wave { Sine, Triangle, Square, Saw }
        public enum Filter { Band, Low, High }

        /// <summary>A small offline mixer: oscillators, filtered noise and envelopes, like Web Audio.</summary>
        sealed class Synth
        {
            public const int Rate = 44100;
            readonly float[] buf;
            readonly System.Random rng = new System.Random(1234);
            public Synth(float seconds) { buf = new float[Mathf.CeilToInt(seconds * Rate)]; }
            public float Rand() => (float)rng.NextDouble();

            // linear attack to vol, then exponential fall to silence at dur
            static float Env(float t, float dur, float vol, float attack)
            {
                if (t < attack) return vol * t / attack;
                float k = (t - attack) / Mathf.Max(.0001f, dur - attack);
                return vol * Mathf.Pow(.0001f / Mathf.Max(vol, .0001f), k);
            }

            static float Osc(Wave w, float phase)
            {
                float p = phase - Mathf.Floor(phase);
                switch (w)
                {
                    case Wave.Sine: return Mathf.Sin(p * 2 * Mathf.PI);
                    case Wave.Square: return p < .5f ? .8f : -.8f;
                    case Wave.Saw: return (2 * p - 1) * .8f;
                    default: return 1 - 4 * Mathf.Abs(p - .5f);
                }
            }

            public void Tone(float f, float dur, Wave w, float vol, float at = 0, float to = 0, float attack = .005f)
            {
                int start = (int)(at * Rate), n = (int)(dur * Rate);
                double phase = 0;
                for (int i = 0; i < n && start + i < buf.Length; i++)
                {
                    float t = i / (float)Rate;
                    float freq = to > 0 ? f * Mathf.Pow(to / f, t / dur) : f;
                    phase += freq / Rate;
                    buf[start + i] += Osc(w, (float)phase) * Env(t, dur, vol, attack);
                }
            }

            public void Noise(float dur, float f, float q, float vol, float at = 0, float to = 0, Filter kind = Filter.Band, float attack = 0)
            {
                int start = (int)(at * Rate), n = (int)(dur * Rate);
                var bq = new Biquad();
                for (int i = 0; i < n && start + i < buf.Length; i++)
                {
                    float t = i / (float)Rate;
                    if ((i & 31) == 0) bq.Set(kind, to > 0 ? f * Mathf.Pow(to / f, t / dur) : f, q);
                    float env = attack > 0 && t < attack ? vol * Mathf.Pow(t / attack, 2) : Env(t, dur, vol, 0);
                    buf[start + i] += bq.Run(Rand() * 2 - 1) * env;
                }
            }

            /// <summary>One crowd voice: a buzzy "woo" that rises and falls, with vibrato, through a vowel filter.</summary>
            public void Voice(float at, float len, float f0, float peak, float formant, float vol, float vibrato)
            {
                int start = (int)(at * Rate), n = (int)(len * Rate);
                var bq = new Biquad(); bq.Set(Filter.Band, formant, 3);
                double phase = 0;
                for (int i = 0; i < n && start + i < buf.Length; i++)
                {
                    float t = i / (float)Rate, k = t / len;
                    float freq = k < .35f ? f0 * Mathf.Pow(peak / f0, k / .35f) : peak * Mathf.Pow(f0 * 1.1f / peak, (k - .35f) / .65f);
                    freq += Mathf.Sin(t * vibrato * 2 * Mathf.PI) * freq * .03f;
                    phase += freq / Rate;
                    float env = t < .08f ? vol * t / .08f : Env(t - .08f, len - .08f, vol, 0);
                    buf[start + i] += bq.Run(Osc(Wave.Saw, (float)phase)) * env * 4;
                }
            }

            public float[] Finish()
            {
                for (int i = 0; i < buf.Length; i++) buf[i] = (float)Math.Tanh(buf[i] * .9f);
                return buf;
            }
        }

        struct Biquad
        {
            float b0, b1, b2, a1, a2, x1, x2, y1, y2;
            public void Set(Filter kind, float f, float q)
            {
                float w = 2 * Mathf.PI * Mathf.Clamp(f, 20, 20000) / Synth.Rate, cs = Mathf.Cos(w), al = Mathf.Sin(w) / (2 * q), a0 = 1 + al;
                switch (kind)
                {
                    case Filter.Low: b0 = (1 - cs) / 2; b1 = 1 - cs; b2 = b0; break;
                    case Filter.High: b0 = (1 + cs) / 2; b1 = -(1 + cs); b2 = b0; break;
                    default: b0 = al; b1 = 0; b2 = -al; break;
                }
                b0 /= a0; b1 /= a0; b2 /= a0; a1 = -2 * cs / a0; a2 = (1 - al) / a0;
            }
            public float Run(float x)
            {
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return y;
            }
        }
    }
}
