using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Пользовательские настройки (user://settings.cfg): звук, интерфейс, окно, язык, управление.</summary>
public static class Settings
{
    public static float Music = .55f, Sfx = .8f, Shake = 1f;
    public static float Wind = 1f, Particles = 1f, Flash = 1f, Fx = 1f;   // интенсивность ветра, частиц, вспышек и дополнительных эффектов (0..1.5)
    public static int UiMode;                                  // 0 — авто (целое по высоте окна), 1–4 — фиксированный множитель
    public static int UiInt(float windowH) => UiMode > 0 ? UiMode : Math.Max(1, (int)Math.Floor(windowH / 360f));
    public static bool Fullscreen, Diag;
    public static int Res = 0;
    public static string Lang = "ru";
    public static readonly Vector2I[] Resolutions = { new(1280, 720), new(1600, 900), new(1920, 1080), new(2560, 1440) };
    const string Path = "user://settings.cfg";

    public static void Load()
    {
        var c = new ConfigFile();
        if (c.Load(Path) != Error.Ok) { Lang = DetectLang(); KeyMap.Reset(); return; }
        Music = (float)c.GetValue("audio", "music", Music); Sfx = (float)c.GetValue("audio", "sfx", Sfx);
        UiMode = Math.Clamp((int)c.GetValue("ui", "mode", 0), 0, 4); Shake = (float)c.GetValue("ui", "shake", Shake); Wind = (float)c.GetValue("fx", "wind", 1f); Particles = (float)c.GetValue("fx", "particles", 1f); Flash = (float)c.GetValue("fx", "flash", 1f); Fx = (float)c.GetValue("fx", "extra", 1f); Lang = (string)c.GetValue("ui", "lang", DetectLang());
        Diag = (bool)c.GetValue("dev", "diag", false); Fullscreen = (bool)c.GetValue("window", "fullscreen", false); Res = Math.Clamp((int)c.GetValue("window", "res", 0), 0, Resolutions.Length - 1);
        KeyMap.Reset();
        foreach (var a in KeyMap.Actions) { int k = (int)c.GetValue("keys", a, (int)KeyMap.Defaults[a]); if (k > 0 && !string.IsNullOrEmpty(OS.GetKeycodeString((Key)k))) KeyMap.Map[a] = (Key)k; }
    }

    static string DetectLang() { var l = OS.GetLocale(); return l.StartsWith("ru") ? "ru" : "en"; }

    public static void Save()
    {
        var c = new ConfigFile();
        c.SetValue("audio", "music", Music); c.SetValue("audio", "sfx", Sfx);
        c.SetValue("ui", "mode", UiMode); c.SetValue("ui", "shake", Shake); c.SetValue("fx", "wind", Wind); c.SetValue("fx", "particles", Particles); c.SetValue("fx", "flash", Flash); c.SetValue("fx", "extra", Fx); c.SetValue("ui", "lang", Lang);
        c.SetValue("dev", "diag", Diag); c.SetValue("window", "fullscreen", Fullscreen); c.SetValue("window", "res", Res);
        foreach (var a in KeyMap.Actions) c.SetValue("keys", a, (int)KeyMap.Map[a]);
        c.Save(Path);
    }

    static int Bus(string name)
    {
        int i = AudioServer.GetBusIndex(name);
        if (i >= 0) return i;
        AudioServer.AddBus(); i = AudioServer.BusCount - 1; AudioServer.SetBusName(i, name); AudioServer.SetBusSend(i, "Master"); return i;
    }

    public static void ApplyAudio()
    {
        AudioServer.SetBusVolumeDb(Bus("Music"), Music <= .001f ? -80f : Mathf.LinearToDb(Music));
        AudioServer.SetBusVolumeDb(Bus("SFX"), Sfx <= .001f ? -80f : Mathf.LinearToDb(Sfx));
    }

    static bool windowPlaced;

    public static void ApplyWindow(bool testing)
    {
        if (testing) return;
        // Перезагрузка сцены (выход в меню, смена сетевого мира) вызывает ApplyWindow снова: если окно уже в нужном режиме и размере, ничего не трогаем — иначе оно дёргалось
        if (Fullscreen) { if (DisplayServer.WindowGetMode() != DisplayServer.WindowMode.ExclusiveFullscreen) DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen); windowPlaced = true; }
        else
        {
            bool wasWindowed = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Windowed;
            if (windowPlaced && wasWindowed && DisplayServer.WindowGetSize() == Resolutions[Math.Min(Res, Resolutions.Length - 1)]) return;
            windowPlaced = true;
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            int scr = DisplayServer.WindowGetCurrentScreen(); var area = DisplayServer.ScreenGetUsableRect(scr);        // рабочая область именно того экрана, где окно (без панели задач)
            var r = Resolutions[Res]; if (r.X > area.Size.X || r.Y > area.Size.Y) { r = Resolutions[0]; for (int i = Resolutions.Length - 1; i >= 0; i--) if (Resolutions[i].X <= area.Size.X && Resolutions[i].Y <= area.Size.Y) { r = Resolutions[i]; break; } }
            DisplayServer.WindowSetSize(r); CenterWindow(scr);
            if (Engine.GetMainLoop() is SceneTree st) st.CreateTimer(.15).Timeout += () => CenterWindow(DisplayServer.WindowGetCurrentScreen());     // рамка окна известна не сразу после смены режима
        }
    }

    /// <summary>Центр окна (с рамкой и заголовком) в рабочей области выбранного экрана, с учётом его смещения на рабочем столе.</summary>
    static void CenterWindow(int scr)
    {
        if (Fullscreen || DisplayServer.WindowGetMode() != DisplayServer.WindowMode.Windowed) return;
        var area = DisplayServer.ScreenGetUsableRect(scr); var size = DisplayServer.WindowGetSizeWithDecorations();
        var pos = area.Position + (area.Size - size) / 2; pos.X = Math.Max(area.Position.X, pos.X); pos.Y = Math.Max(area.Position.Y, pos.Y);
        DisplayServer.WindowSetPosition(pos);
    }

    public static void Apply(bool testing) { ApplyAudio(); ApplyWindow(testing); Loc.SetLang(Lang); }
}

/// <summary>Привязка клавиш: игра обрабатывает «стандартные» клавиши, а нажатая клавиша переводится в стандартную по привязке.</summary>
public static class KeyMap
{
    public static readonly string[] Actions = { "Up", "Down", "Left", "Right", "Interact", "Upgrade", "Dash", "Flare", "Weapon", "Equip", "Journal", "Prod", "Build", "Pulse", "Tech", "Map", "Beam", "Attack" };
    public static readonly string[] Labels = { "Вверх", "Вниз", "Влево", "Вправо", "Взаимодействие", "Улучшить", "Рывок", "Вспышка", "Смена оружия", "Экипировка", "Журнал", "Производство", "Постройки", "Импульс света", "Улучшения", "Карта", "Луч", "Атака / добыча" };
    public static readonly Dictionary<string, Key> Defaults = new()
    {
        ["Up"] = Key.W, ["Down"] = Key.S, ["Left"] = Key.A, ["Right"] = Key.D, ["Interact"] = Key.E, ["Upgrade"] = Key.U, ["Dash"] = Key.Shift, ["Flare"] = Key.Q, ["Weapon"] = Key.V,
        ["Equip"] = Key.C, ["Journal"] = Key.J, ["Prod"] = Key.P, ["Build"] = Key.B, ["Pulse"] = Key.F, ["Tech"] = Key.T, ["Map"] = Key.M, ["Beam"] = Key.R, ["Attack"] = Key.Space,
    };
    public static readonly Dictionary<string, Key> Map = new();

    public static void Reset() { Map.Clear(); foreach (var kv in Defaults) Map[kv.Key] = kv.Value; }
    public static bool Held(string action) => Input.IsKeyPressed(Map[action]);

    /// <summary>Нажатая клавиша → стандартная клавиша действия (или None, если клавиша переназначена на другое действие).</summary>
    public static Key Translate(Key pressed)
    {
        foreach (var a in Actions) if (Map[a] == pressed) return Defaults[a];
        foreach (var a in Actions) if (Defaults[a] == pressed && Map[a] != pressed) return Key.None;   // стандартная клавиша ушла на другое действие
        return pressed;
    }

    public static string Name(string action) => OS.GetKeycodeString(Map[action]);

    public static void Rebind(string action, Key k)
    {
        foreach (var a in Actions) if (a != action && Map[a] == k) { Map[a] = Map[action]; break; }   // обмен, чтобы не было дублей
        Map[action] = k;
    }
}
