using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Фоновая музыка: заменяется файлами res://Assets/Audio/Music/NAME[_2,_3].ogg|mp3|wav (несколько треков на тему чередуются), иначе синтезируется бесшовная петля (16 с).</summary>
public partial class Music : Node
{
    public static Music Inst;
    const int SR = 22050; const float T = 16f;
    AudioStreamPlayer a, b, cur;
    string curName = "";
    readonly Dictionary<string, AudioStream> cache = new();
    float fade = 1f, fadeDur = 2.5f, low, lowTarget;
    public static readonly string[] Tracks = { "menu", "forest", "mines", "ash", "ice", "heart", "boss", "finale" };

    public override void _Ready()
    {
        Inst = this;
        a = new AudioStreamPlayer { Bus = "Music", VolumeDb = -80 }; b = new AudioStreamPlayer { Bus = "Music", VolumeDb = -80 };
        AddChild(a); AddChild(b);
    }

    public static void Play(string name) => Inst?.Switch(name);
    /// <summary>Здоровье игрока: чем ниже (0..1), тем медленнее и тяжелее звучит музыка.</summary>
    public static void Health(float ratio) { if (Inst != null) Inst.lowTarget = Mathf.Clamp((.4f - ratio) / .4f, 0f, 1f); }

    void Switch(string name)
    {
        if (name == curName) return;
        curName = name; fadeDur = name.StartsWith("battle") ? .5f : name == "boss" ? 1f : name.StartsWith("dark_") ? 4f : 3f; Start(PickVariant(name));
    }

    string curKey = "";
    readonly Random pick = new();

    /// <summary>Файлы NAME.mp3, NAME_2.mp3, NAME_3.mp3… — несколько треков на одну тему; без файлов остаётся синтез.</summary>
    static bool HasFile(string key)
    {
        foreach (var ext in new[] { "ogg", "mp3", "wav" })
            if (ResourceLoader.Exists($"res://Assets/Audio/Music/{key}.{ext}") || FileAccess.FileExists($"user://music/{key}.{ext}")) return true;
        return false;
    }

    string PickVariant(string name)
    {
        var v = new List<string>();
        if (HasFile(name)) v.Add(name);
        for (int i = 2; HasFile($"{name}_{i}"); i++) v.Add($"{name}_{i}");
        if (v.Count == 0) return name;
        if (v.Count == 1) return v[0];
        string k; do k = v[pick.Next(v.Count)]; while (k == curKey && v.Count > 1);
        return k;
    }

    void Start(string key)
    {
        var stream = Get(key);
        if (stream == null) return;
        oldKey = curKey; curKey = key; fade = 0;
        var next = cur == a ? b : a;
        next.Stream = stream; next.VolumeDb = -80; next.Play(); old = cur; cur = next;
    }
    AudioStreamPlayer old; string oldKey = "";

    /// <summary>Поправка громкости на трек (дБ): выровнено по ~-13 LUFS с запасом по пику; новые файлы без записи играют как есть.</summary>
    static readonly Dictionary<string, float> Gain = new()
    {
        ["ash"] = -5.3f, ["ash_2"] = -2.0f, ["ash_3"] = -6.4f, ["ash_4"] = 2.0f,
        ["ash_5"] = -4.4f, ["battle_ash"] = -0.9f, ["battle_ash_2"] = -0.8f, ["battle_forest"] = -0.2f,
        ["battle_forest_2"] = -2.4f, ["battle_heart"] = -1.0f, ["battle_heart_2"] = 0.0f, ["battle_ice"] = -6.8f,
        ["battle_ice_2"] = -5.5f, ["battle_mines"] = -2.1f, ["battle_mines_2"] = -0.3f, ["boss"] = -1.4f,
        ["boss_2"] = 1.4f, ["boss_3"] = 1.6f, ["finale"] = -1.0f, ["finale_2"] = -5.6f,
        ["finale_3"] = -1.0f, ["finale_4"] = 1.1f, ["finale_5"] = 3.4f, ["forest"] = 14.7f,
        ["forest_2"] = -8.0f, ["forest_3"] = -0.6f, ["forest_4"] = 1.1f, ["forest_5"] = 0.5f,
        ["forest_6"] = 0.5f, ["heart"] = 6.5f, ["heart_2"] = -1.0f, ["heart_3"] = -1.1f,
        ["heart_4"] = -1.0f, ["heart_5"] = 3.4f, ["ice"] = -0.1f, ["ice_2"] = 3.6f,
        ["ice_3"] = -4.8f, ["ice_4"] = 1.6f, ["menu"] = -5.3f, ["menu_2"] = -4.3f,
        ["menu_3"] = -3.4f, ["menu_4"] = -6.9f, ["menu_5"] = -2.6f, ["menu_6"] = -4.6f,
        ["mines"] = 1.0f, ["mines_2"] = -5.1f, ["mines_3"] = -0.2f, ["mines_4"] = 0.4f,
        ["beacon"] = 0.7f, ["beacon_2"] = -0.1f, ["beacon_3"] = -3.3f, ["beacon_4"] = -0.9f, ["dark_ash"] = 4.2f, ["dark_ash_2"] = -0.8f, ["dark_forest"] = -0.3f, ["dark_forest_2"] = -1.5f, ["dark_heart"] = 2.0f, ["dark_heart_2"] = -0.1f, ["dark_ice"] = 0.3f, ["dark_ice_2"] = 3.8f, ["dark_mines"] = -3.5f, ["dark_mines_2"] = 1.5f
    };
    float GainOf(string key) => (Gain.TryGetValue(key, out var g) ? g : 0f) + (key.StartsWith("dark_") ? -3f : 0f);     // тёмные темы тише: фон, а не действие

    public override void _Process(double delta)
    {
        if (cur == null) return;
        if (!cur.Playing && fade >= 1f && curName != "") Start(PickVariant(curName));
        fade = Mathf.Min(1f, fade + (float)delta / fadeDur);
        low = Mathf.Lerp(low, lowTarget, Mathf.Min(1f, (float)delta * 1.5f)); a.PitchScale = b.PitchScale = 1f - .08f * low;
        cur.VolumeDb = Mathf.LinearToDb(Mathf.Max(.0001f, fade)) - 2f + GainOf(curKey);
        if (old != null && old != cur) { old.VolumeDb = Mathf.LinearToDb(Mathf.Max(.0001f, 1f - fade)) - 2f + GainOf(oldKey); if (fade >= 1f) { old.Stop(); old = null; } }
    }

    AudioStream Get(string name)
    {
        if (cache.TryGetValue(name, out var s)) return s;
        foreach (var ext in new[] { "ogg", "mp3", "wav" })
            foreach (var root in new[] { "res://Assets/Audio/Music/", "user://music/" })
            {
                var p = $"{root}{name}.{ext}";
                if (!(root.StartsWith("res") ? ResourceLoader.Exists(p) : FileAccess.FileExists(p))) continue;
                AudioStream st = null;
                if (ext == "ogg") { st = AudioStreamOggVorbis.LoadFromFile(ProjectSettings.GlobalizePath(p)); if (st is AudioStreamOggVorbis o) o.Loop = true; }
                else if (ext == "mp3") { var f = FileAccess.Open(p, FileAccess.ModeFlags.Read); if (f != null) { var m = new AudioStreamMP3 { Data = f.GetBuffer((long)f.GetLength()), Loop = false }; st = m; } }
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
