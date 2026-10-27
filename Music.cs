using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Фоновая музыка: заменяется файлами res://audio/music/NAME.ogg|mp3|wav, иначе синтезируется бесшовная петля (16 с).</summary>
public partial class Music : Node
{
    public static Music Inst;
    const int SR = 22050; const float T = 16f;
    AudioStreamPlayer a, b, cur;
    string curName = "";
    readonly Dictionary<string, AudioStream> cache = new();
    float fade = 1f;
    public static readonly string[] Tracks = { "menu", "forest", "mines", "ash", "ice", "heart", "boss", "finale" };

    public override void _Ready()
    {
        Inst = this;
        a = new AudioStreamPlayer { Bus = "Music", VolumeDb = -80 }; b = new AudioStreamPlayer { Bus = "Music", VolumeDb = -80 };
        AddChild(a); AddChild(b);
    }

    public static void Play(string name) => Inst?.Switch(name);

    void Switch(string name)
    {
        if (name == curName) return;
        curName = name; fade = 0;
        var next = cur == a ? b : a; var stream = Get(name);
        if (stream == null) return;
        next.Stream = stream; next.VolumeDb = -80; next.Play(); old = cur; cur = next;
    }
    AudioStreamPlayer old;

    public override void _Process(double delta)
    {
        if (cur == null) return;
        fade = Mathf.Min(1f, fade + (float)delta / 2.5f);
        cur.VolumeDb = Mathf.LinearToDb(Mathf.Max(.0001f, fade)) - 4f;
        if (old != null && old != cur) { old.VolumeDb = Mathf.LinearToDb(Mathf.Max(.0001f, 1f - fade)) - 4f; if (fade >= 1f) { old.Stop(); old = null; } }
    }

    AudioStream Get(string name)
    {
        if (cache.TryGetValue(name, out var s)) return s;
        foreach (var ext in new[] { "ogg", "mp3", "wav" })
            foreach (var root in new[] { "res://audio/music/", "user://music/" })
            {
                var p = $"{root}{name}.{ext}";
                if (!(root.StartsWith("res") ? ResourceLoader.Exists(p) : FileAccess.FileExists(p))) continue;
                AudioStream st = null;
                if (ext == "ogg") { st = AudioStreamOggVorbis.LoadFromFile(ProjectSettings.GlobalizePath(p)); if (st is AudioStreamOggVorbis o) o.Loop = true; }
                else if (ext == "mp3") { var f = FileAccess.Open(p, FileAccess.ModeFlags.Read); if (f != null) { var m = new AudioStreamMP3 { Data = f.GetBuffer((long)f.GetLength()), Loop = true }; st = m; } }
                else { var f = FileAccess.Open(p, FileAccess.ModeFlags.Read); if (f != null) st = AudioStreamWav.LoadFromBuffer(f.GetBuffer((long)f.GetLength())); }
                if (st != null) { cache[name] = st; return st; }
            }
        return cache[name] = Synth(name);
    }

    // ───────────────────────── синтез ─────────────────────────
    sealed class Spec { public float Root; public float[] Scale; public int Bells; public float BellTau, Bright, Pulse, Wind; public float[] Chord; }

    static readonly Dictionary<string, Spec> Specs = new()
    {
        ["menu"] = new Spec { Root = 110f, Scale = new float[] { 0, 3, 5, 7, 10 }, Bells = 9, BellTau = 2.2f, Bright = .4f, Chord = new float[] { 1, 1.5f, 2 } },
        ["forest"] = new Spec { Root = 146.8f, Scale = new float[] { 0, 2, 4, 7, 9 }, Bells = 12, BellTau = 1.8f, Bright = .35f, Wind = .5f, Chord = new float[] { 1, 1.5f, 2.25f } },
        ["mines"] = new Spec { Root = 82.4f, Scale = new float[] { 0, 2, 3, 5, 7, 9, 10 }, Bells = 14, BellTau = 2.6f, Bright = .5f, Chord = new float[] { 1, 1.5f, 2 } },
        ["ash"] = new Spec { Root = 65.4f, Scale = new float[] { 0, 1, 5, 7, 8 }, Bells = 8, BellTau = 1.4f, Bright = .25f, Wind = 1f, Chord = new float[] { 1, 1.335f, 2 } },
        ["ice"] = new Spec { Root = 174.6f, Scale = new float[] { 0, 2, 4, 6, 7, 11 }, Bells = 16, BellTau = 3.2f, Bright = .8f, Wind = .35f, Chord = new float[] { 1, 1.5f, 2, 3 } },
        ["heart"] = new Spec { Root = 61.7f, Scale = new float[] { 0, 1, 3, 6, 7 }, Bells = 7, BellTau = 3.5f, Bright = .3f, Wind = .6f, Chord = new float[] { 1, 1.06f, 1.5f } },
        ["boss"] = new Spec { Root = 110f, Scale = new float[] { 0, 3, 5, 6, 7, 10 }, Bells = 10, BellTau = 1.2f, Bright = .5f, Pulse = 1f, Chord = new float[] { 1, 1.5f, 2 } },
        ["finale"] = new Spec { Root = 130.8f, Scale = new float[] { 0, 2, 4, 5, 7, 9, 11 }, Bells = 18, BellTau = 2.4f, Bright = .7f, Chord = new float[] { 1, 1.25f, 1.5f, 2 } },
    };

    /// <summary>Частота, округлённая до кратной 1/T — тогда синус целое число раз укладывается в петлю.</summary>
    static double Snap(double f) => Math.Max(1, Math.Round(f * T)) / T;

    static AudioStream Synth(string name)
    {
        var sp = Specs.TryGetValue(name, out var s0) ? s0 : Specs["menu"];
        int n = (int)(T * SR); var buf = new float[n]; var rnd = new Random(name.GetHashCode() & 0x7fffffff);
        // дрон
        for (int v = 0; v < sp.Chord.Length; v++)
        {
            double f = Snap(sp.Root * sp.Chord[v]); double lfo = Snap(.1 + v * .05); double ph0 = rnd.NextDouble() * 6.28; double amp = .16 / (1 + v * .5);
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / SR; double env = .6 + .4 * Math.Sin(2 * Math.PI * lfo * t + ph0);
                buf[i] += (float)(amp * env * (Math.Sin(2 * Math.PI * f * t) + .25 * Math.Sin(4 * Math.PI * f * t) * sp.Bright));
            }
        }
        // шум-ветер: сумма периодических синусов
        if (sp.Wind > 0)
            for (int k = 0; k < 24; k++)
            {
                double f = Snap(180 + rnd.NextDouble() * 700), ph = rnd.NextDouble() * 6.28, lfo = Snap(.06 + rnd.NextDouble() * .2), lp = rnd.NextDouble() * 6.28; double amp = .012 * sp.Wind;
                for (int i = 0; i < n; i++) { double t = (double)i / SR; buf[i] += (float)(amp * (.5 + .5 * Math.Sin(2 * Math.PI * lfo * t + lp)) * Math.Sin(2 * Math.PI * f * t + ph)); }
            }
        // колокольчики с переносом хвоста через границу петли
        for (int bI = 0; bI < sp.Bells; bI++)
        {
            int start = (int)((bI + rnd.NextDouble() * .8) / sp.Bells * n); int deg = rnd.Next(sp.Scale.Length), oct = rnd.Next(2, 5);
            double f = Snap(sp.Root * Math.Pow(2, sp.Scale[deg] / 12.0) * Math.Pow(2, oct - 2)); double tau = sp.BellTau * (.6 + rnd.NextDouble() * .8); double amp = .09 * (.5 + sp.Bright * .5);
            int len = (int)(tau * 4 * SR);
            for (int i = 0; i < len; i++)
            {
                double t = (double)i / SR; double env = Math.Min(1, i / 200.0) * Math.Exp(-t / tau);
                double v = Math.Sin(2 * Math.PI * f * t) + .35 * Math.Sin(2 * Math.PI * f * 2.76 * t) * sp.Bright + .15 * Math.Sin(2 * Math.PI * f * 5.4 * t) * sp.Bright;
                buf[(start + i) % n] += (float)(amp * env * v);
            }
        }
        // ритмический пульс боевой темы: 4 удара на фразу
        if (sp.Pulse > 0)
            for (int beat = 0; beat < 32; beat++)
            {
                int start = (int)(beat / 32.0 * n); double f0 = Snap(sp.Root * .5); int len = (int)(.45 * SR);
                for (int i = 0; i < len; i++) { double t = (double)i / SR; double env = Math.Exp(-t * 7); buf[(start + i) % n] += (float)(.16 * env * Math.Sin(2 * Math.PI * (f0 + 40 * Math.Exp(-t * 20)) * t) * (beat % 4 == 0 ? 1 : .55)); }
            }
        float peak = 0; foreach (var x in buf) peak = Math.Max(peak, Math.Abs(x));
        var data = new byte[n * 2]; float g = peak > 0 ? .55f / peak : 1f;
        for (int i = 0; i < n; i++) { short v = (short)(Math.Clamp(buf[i] * g, -1f, 1f) * 30000); data[i * 2] = (byte)(v & 255); data[i * 2 + 1] = (byte)((v >> 8) & 255); }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = SR, Stereo = false, Data = data, LoopMode = AudioStreamWav.LoopModeEnum.Forward, LoopBegin = 0, LoopEnd = n };
    }
}
