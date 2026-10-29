using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Звуки синтезируются кодом при запуске — внешних файлов нет.</summary>
public partial class Sfx : Node
{
    const int SR = 22050;
    static Sfx inst;
    readonly Dictionary<string, AudioStream> bank = new();
    AudioStreamPlayer[] pool;
    int next;
    readonly Random rnd = new(7);
    readonly RandomNumberGenerator prng = new();

    public static Action<string, float, float> Hook;                // сеть: пересылает игровые звуки остальным игрокам
    public static void Play(string name, float db = 0f, float pitch = 1f) { inst?.P(name, db, pitch); Hook?.Invoke(name, db, pitch); }
    /// <summary>Звук, пришедший по сети: играется без повторной пересылки.</summary>
    public static void PlayRemote(string name, float db = 0f, float pitch = 1f) => inst?.P(name, db, pitch);

    public override void _Ready()
    {
        inst = this; prng.Randomize();
        pool = new AudioStreamPlayer[14];
        for (int i = 0; i < pool.Length; i++) { pool[i] = new AudioStreamPlayer { VolumeDb = -6, Bus = AudioServer.GetBusIndex("SFX") >= 0 ? "SFX" : "Master" }; AddChild(pool[i]); }

        bank["mine"] = Wav(Tone(900, 500, .06f, 1, .4f, 6));
        bank["break"] = Wav(Tone(220, 60, .16f, 2, .8f, 5));
        bank["hit"] = Wav(Tone(300, 100, .09f, 2, .6f, 7));
        bank["hurt"] = Wav(Tone(260, 70, .2f, 2, .3f, 4));
        bank["pickup"] = Wav(Tone(700, 1200, .09f, 0, 0, 4));
        bank["tick"] = Wav(Tone(1400, 1400, .025f, 0, .3f, 10));
        bank["build"] = Wav(Cat(Tone(150, 80, .1f, 3, .5f, 7), Tone(660, 660, .14f, 0, 0, 6)));
        bank["upgrade"] = Wav(Seq(new float[] { 392, 523, 659, 784 }, .09f, 1, .6f));
        bank["levelup"] = Wav(Seq(new float[] { 523, 659, 784, 1047 }, .11f, 3));
        bank["ancient"] = Wav(Add(Tone(110, 440, 1.8f, 3, 0, 1.1f), Tone(220, 880, 1.8f, 0, 0, 1.2f), Tone(0, 0, 1.8f, 0, .5f, 3)));
        bank["error"] = Wav(Tone(150, 120, .1f, 1, 0, 6));
        bank["dash"] = Wav(Tone(900, 200, .16f, 2, 1f, 6));
        bank["spin"] = Wav(Tone(500, 1000, .2f, 2, .5f, 5));
        bank["shoot"] = Wav(Tone(700, 200, .12f, 1, .2f, 6));
        bank["boom"] = Wav(Add(Tone(90, 30, .5f, 3, 0, 4), Tone(0, 0, .5f, 0, 1f, 5)));
        bank["chest"] = Wav(Seq(new float[] { 659, 784, 988, 1319 }, .09f, 0));
        bank["rescue"] = Wav(Seq(new float[] { 392, 523, 659, 784, 1047 }, .1f, 3));
        bank["tech"] = Wav(Seq(new float[] { 440, 554, 659, 880, 1109 }, .07f, 0));
        bank["boss"] = Wav(Add(Tone(70, 50, 1.0f, 2, .3f, 1.5f), Tone(55, 45, 1.0f, 1, 0, 1.5f)));
        bank["flare"] = Wav(Tone(300, 1200, .25f, 0, .4f, 4));
        bank["pulse"] = Wav(Add(Tone(80, 300, .6f, 3, 0, 2), Tone(0, 0, .6f, 0, 1f, 4)));
        bank["discover"] = Wav(Seq(new float[] { 587, 740, 880 }, .12f, 0));
        // замена файлами: res://Assets/Audio/Sfx/NAME.ogg|wav (или user://sfx/NAME.*) — синтезированные звуки остаются запасными
        foreach (var name in new List<string>(bank.Keys))
            foreach (var ext in new[] { "ogg", "wav" })
            {
                var path = $"res://Assets/Audio/Sfx/{name}.{ext}";
                if (ResourceLoader.Exists(path)) { var st = ResourceLoader.Load<AudioStream>(path); if (st != null) { bank[name] = st; break; } }
            }
    }

    void P(string name, float db, float pitch)
    {
        if (pool == null || !bank.TryGetValue(name, out var s)) return;
        var p = pool[next++ % pool.Length];
        p.Stream = s; p.VolumeDb = -7 + db; p.PitchScale = pitch * prng.RandfRange(.95f, 1.05f); p.Play();
    }

    float Osc(double ph, int kind)
    {
        double f = ph - Math.Floor(ph);
        return kind switch { 0 => (float)Math.Sin(ph * Math.PI * 2), 1 => f < .5 ? .6f : -.6f, 2 => (float)(f * 2 - 1), _ => (float)(Math.Abs(f * 4 - 2) - 1) };
    }

    float[] Tone(float f0, float f1, float dur, int kind, float noise, float decay)
    {
        int n = (int)(dur * SR); var s = new float[n]; double ph = 0;
        for (int i = 0; i < n; i++)
        {
            float p = (float)i / n, f = f0 + (f1 - f0) * p;
            ph += f / SR;
            float env = Mathf.Min(1, i / 110f) * Mathf.Exp(-decay * p);
            float v = (f0 > 0 ? Osc(ph, kind) * (1 - noise * .5f) : 0) + (float)(rnd.NextDouble() * 2 - 1) * noise;
            s[i] = v * env * .55f;
        }
        return s;
    }

    float[] Seq(float[] fs, float nd, int kind, float noise = 0)
    {
        var all = new List<float>();
        foreach (var f in fs) all.AddRange(Tone(f, f, nd, kind, noise, 3f));
        return all.ToArray();
    }

    static float[] Cat(params float[][] a) { var l = new List<float>(); foreach (var x in a) l.AddRange(x); return l.ToArray(); }
    static float[] Add(params float[][] a)
    {
        int n = 0; foreach (var x in a) n = Math.Max(n, x.Length);
        var r = new float[n]; foreach (var x in a) for (int i = 0; i < x.Length; i++) r[i] += x[i];
        return r;
    }

    static AudioStreamWav Wav(float[] s)
    {
        var d = new byte[s.Length * 2];
        for (int i = 0; i < s.Length; i++)
        {
            short v = (short)(Math.Clamp(s[i], -1f, 1f) * 30000);
            d[i * 2] = (byte)(v & 0xff); d[i * 2 + 1] = (byte)((v >> 8) & 0xff);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = SR, Stereo = false, Data = d };
    }
}
