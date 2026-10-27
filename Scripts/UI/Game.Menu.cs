using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Lamplighter;

public enum Mn { None, Main, Pause, Settings, Slots, Keys, Credits, Confirm, Achv }

public sealed class SlotInfo { public int Slot; public bool Exists; public int Level, Beacon; public bool Finished; public string Stamp = ""; public float Secs; }

/// <summary>Главное меню, пауза, настройки, слоты сохранений, титры и достижения.</summary>
public partial class Game
{
    public const string VersionStr = "1.0.0";
    public const string BuildStr = "Windows · Godot 4.7 .NET";

    public Mn Menu; public Mn MenuBack; public int MenuCur;
    public bool SlotSaveMode; public string ConfirmText = ""; Action confirmYes;
    public string RebindAction;                      // ждём клавишу для действия
    public List<SlotInfo> Slots = new();
    public float UiTime, PlaySecs;
    static bool skipMenu;

    public static readonly string[] MainItems = { "Продолжить", "Новая игра", "Загрузить игру", "Настройки", "Достижения", "Титры", "Выход" };
    public static readonly string[] PauseItems = { "Продолжить", "Сохранить игру", "Загрузить игру", "Настройки", "Достижения", "Титры", "В главное меню", "Выход" };
    public static readonly string[] SettingsItems = { "Громкость музыки", "Громкость звуков", "Масштаб интерфейса", "Тряска экрана", "Полный экран", "Разрешение окна", "Язык", "Диагностика (локальная)", "Управление", "Назад" };

    public bool HasSave => File.Exists(SavePath) || File.Exists(BakPath);
    string SaveDir => testing ? testDir : ProjectSettings.GlobalizePath("user://");
    string SlotPath(int s) => Path.Combine(SaveDir, s == 0 ? "save.json" : $"slot{s}.json");
    string BakPath => Path.Combine(SaveDir, "save.bak");

    // ───────────────────────── открытие/закрытие ─────────────────────────
    public void OpenMenu(Mn m) { Menu = m; MenuCur = 0; if (m == Mn.Slots) RefreshSlots(); if (m == Mn.Main && !HasSave) MenuCur = 1; Sfx.Play("tick", -4, 1.1f); }
    void CloseMenu() { Menu = Mn.None; RebindAction = null; Settings.Save(); }

    public void StartMenuOnLaunch()
    {
        if (testing || skipMenu) { skipMenu = false; return; }
        Menu = Mn.Main; MenuCur = HasSave ? 0 : 1;
    }

    public int MenuLen => Menu switch
    {
        Mn.Main => MainItems.Length, Mn.Pause => PauseItems.Length, Mn.Settings => SettingsItems.Length, Mn.Slots => 4,
        Mn.Keys => KeyMap.Actions.Length + 2, Mn.Confirm => 2, Mn.Achv => Ach.All.Length, _ => 1
    };

    // ───────────────────────── ввод ─────────────────────────
    void MenuKey(Key k)
    {
        if (RebindAction != null)
        {
            if (k != Key.Escape && k != Key.None) { KeyMap.Rebind(RebindAction, k); Settings.Save(); Sfx.Play("upgrade", -4, 1.4f); }
            RebindAction = null; return;
        }
        bool enter = k == Key.Enter || k == Key.KpEnter || k == Key.Space;
        if (k == Key.Up || k == Key.W) { MenuCur = (MenuCur + MenuLen - 1) % MenuLen; Sfx.Play("tick", -6, 1.4f); return; }
        if (k == Key.Down || k == Key.S) { MenuCur = (MenuCur + 1) % MenuLen; Sfx.Play("tick", -6, 1.4f); return; }
        if (k == Key.Escape) { MenuBackKey(); return; }
        switch (Menu)
        {
            case Mn.Main: if (enter) MainAct(MenuCur); break;
            case Mn.Pause: if (enter) PauseAct(MenuCur); break;
            case Mn.Settings: SettingsKey(k, enter); break;
            case Mn.Slots: if (enter) SlotAct(MenuCur); break;
            case Mn.Keys: KeysKey(k, enter); break;
            case Mn.Confirm: if (k == Key.Left || k == Key.Right || k == Key.A || k == Key.D) MenuCur = 1 - MenuCur; else if (enter) { if (MenuCur == 0) confirmYes?.Invoke(); else OpenMenu(MenuBack); } break;
            case Mn.Credits: case Mn.Achv: if (enter && Menu == Mn.Credits) MenuBackKey(); break;
        }
    }

    /// <summary>Мышь в меню: наведение выбирает пункт, ЛКМ — действие, ПКМ — «влево» в настройках или назад, колесо — вверх/вниз.</summary>
    void MenuMouse(InputEventMouse e)
    {
        if (hud == null) return;
        int hit = hud.MenuHit(e.Position / UiScaleVp);
        if (e is InputEventMouseMotion) { if (hit >= 0 && hit != MenuCur && RebindAction == null) { MenuCur = hit; Sfx.Play("tick", -6, 1.4f); } return; }
        if (e is not InputEventMouseButton mb || !mb.Pressed) return;
        switch (mb.ButtonIndex)
        {
            case MouseButton.WheelUp: if (RebindAction == null) MenuKey(Key.Up); break;
            case MouseButton.WheelDown: if (RebindAction == null) MenuKey(Key.Down); break;
            case MouseButton.Left:
                if (RebindAction != null) { MenuKey(Key.Escape); break; }
                if (Menu == Mn.Credits) { MenuBackKey(); break; }
                if (hit >= 0) { MenuCur = hit; MenuKey(Key.Enter); }
                break;
            case MouseButton.Right:
                if (RebindAction != null) { MenuKey(Key.Escape); break; }
                if (Menu == Mn.Settings && hit >= 0 && hit < 8) { MenuCur = hit; MenuKey(Key.Left); }
                else MenuBackKey();
                break;
        }
    }

    void MenuBackKey()
    {
        switch (Menu)
        {
            case Mn.Main: break;
            case Mn.Pause: CloseMenu(); break;
            case Mn.Settings: Settings.Save(); OpenMenu(MenuBack); break;
            case Mn.Keys: OpenMenu(Mn.Settings); MenuCur = 8; break;
            default: OpenMenu(MenuBack); break;
        }
    }

    void MainAct(int i)
    {
        switch (i)
        {
            case 0: if (HasSave) CloseMenu(); else Sfx.Play("error"); break;
            case 1: if (HasSave) AskConfirm("Начать новую игру? Текущее сохранение будет заменено (копия останется в save_prev.json).", NewGame, Mn.Main); else NewGame(); break;
            case 2: MenuBack = Mn.Main; SlotSaveMode = false; OpenMenu(Mn.Slots); break;
            case 3: MenuBack = Mn.Main; OpenMenu(Mn.Settings); break;
            case 4: MenuBack = Mn.Main; OpenMenu(Mn.Achv); break;
            case 5: MenuBack = Mn.Main; OpenMenu(Mn.Credits); break;
            case 6: Save(); GetTree().Quit(); break;
        }
    }

    void PauseAct(int i)
    {
        switch (i)
        {
            case 0: CloseMenu(); break;
            case 1: MenuBack = Mn.Pause; SlotSaveMode = true; OpenMenu(Mn.Slots); break;
            case 2: MenuBack = Mn.Pause; SlotSaveMode = false; OpenMenu(Mn.Slots); break;
            case 3: MenuBack = Mn.Pause; OpenMenu(Mn.Settings); break;
            case 4: MenuBack = Mn.Pause; OpenMenu(Mn.Achv); break;
            case 5: MenuBack = Mn.Pause; OpenMenu(Mn.Credits); break;
            case 6: Save(); skipMenu = false; GetTree().ReloadCurrentScene(); break;
            case 7: Save(); GetTree().Quit(); break;
        }
    }

    void AskConfirm(string text, Action yes, Mn back) { ConfirmText = text; confirmYes = yes; MenuBack = back; OpenMenu(Mn.Confirm); MenuCur = 1; }

    void SettingsKey(Key k, bool enter)
    {
        int d = k == Key.Left || k == Key.A ? -1 : k == Key.Right || k == Key.D ? 1 : 0;
        if (d == 0 && !enter) return;
        if (d == 0) d = 1;
        switch (MenuCur)
        {
            case 0: Settings.Music = Mathf.Clamp(Settings.Music + .1f * d, 0, 1); Settings.ApplyAudio(); break;
            case 1: Settings.Sfx = Mathf.Clamp(Settings.Sfx + .1f * d, 0, 1); Settings.ApplyAudio(); Sfx.Play("pickup"); break;
            case 2: Settings.UiMode = (Settings.UiMode + d + 5) % 5; break;
            case 3: Settings.Shake = Mathf.Clamp(Settings.Shake + .25f * d, 0, 1.5f); break;
            case 4: Settings.Fullscreen = !Settings.Fullscreen; Settings.ApplyWindow(testing); break;
            case 5: Settings.Res = (Settings.Res + d + Settings.Resolutions.Length) % Settings.Resolutions.Length; Settings.ApplyWindow(testing); break;
            case 6: Settings.Lang = Settings.Lang == "ru" ? "en" : "ru"; Loc.SetLang(Settings.Lang); break;
            case 7: Settings.Diag = !Settings.Diag; Diag.Enabled = Settings.Diag; if (Settings.Diag) Diag.Reset(); break;
            case 8: if (enter) { OpenMenu(Mn.Keys); } break;
            case 9: if (enter) { Settings.Save(); OpenMenu(MenuBack); } break;
        }
        Sfx.Play("tick", -6, 1.2f);
    }

    void KeysKey(Key k, bool enter)
    {
        if (!enter) return;
        if (MenuCur < KeyMap.Actions.Length) { RebindAction = KeyMap.Actions[MenuCur]; return; }
        if (MenuCur == KeyMap.Actions.Length) { KeyMap.Reset(); Settings.Save(); Toast("Управление сброшено"); return; }
        OpenMenu(Mn.Settings); MenuCur = 8;
    }

    // ───────────────────────── слоты ─────────────────────────
    public void RefreshSlots()
    {
        Slots.Clear();
        for (int s = 0; s <= 3; s++)
        {
            var si = new SlotInfo { Slot = s }; var p = SlotPath(s);
            if (File.Exists(p))
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(p)); var r = doc.RootElement; si.Exists = true;
                    if (r.TryGetProperty("Level", out var l)) si.Level = l.GetInt32();
                    if (r.TryGetProperty("Beacon", out var b)) si.Beacon = b.GetInt32();
                    if (r.TryGetProperty("Finished", out var f)) si.Finished = f.GetBoolean();
                    if (r.TryGetProperty("Stamp", out var st)) si.Stamp = st.GetString() ?? "";
                    if (r.TryGetProperty("PlaySecs", out var ps)) si.Secs = (float)ps.GetDouble();
                }
                catch { si.Exists = false; }
            Slots.Add(si);
        }
    }

    void SlotAct(int i)
    {
        if (SlotSaveMode)
        {
            if (i == 0) { Fail("Автосохранение обновляется само"); return; }
            Save(); try { File.Copy(SavePath, SlotPath(i), true); Toast($"Игра сохранена в слот {i}"); Sfx.Play("upgrade", -3, 1.3f); RefreshSlots(); } catch (Exception e) { Fail("Не удалось сохранить: " + e.Message); }
            return;
        }
        if (!Slots[i].Exists) { Sfx.Play("error"); return; }
        AskConfirm("Загрузить это сохранение? Несохранённый прогресс будет потерян.", () => LoadSlot(i), Mn.Slots);
    }

    void LoadSlot(int i)
    {
        try
        {
            if (i != 0) { if (File.Exists(SavePath)) File.Copy(SavePath, Path.Combine(SaveDir, "save_prev.json"), true); File.Copy(SlotPath(i), SavePath, true); }
            skipMenu = true; GetTree().ReloadCurrentScene();
        }
        catch (Exception e) { Fail("Не удалось загрузить: " + e.Message); }
    }

    // ───────────────────────── музыка ─────────────────────────
    float musicT;
    void UpdateMusic(float dt)
    {
        musicT -= dt; if (musicT > 0) return; musicT = .5f;
        string t = Menu == Mn.Main ? "menu" : FinStage == 1 || FinStage == 2 || FinStage == 3 ? "finale" : (Boss != null || RiftOn) ? "boss" : RegionAt(PPos) switch { 1 => "mines", 2 => "ash", 3 => "ice", 4 => "heart", _ => "forest" };
        Music.Play(t);
    }

    // ───────────────────────── тест меню ─────────────────────────
    void BuildMenuScript()
    {
        S(.3f, () => { OpenMenu(Mn.Main); });
        S(.4f, () => Shot("m1_main"));
        S(.1f, () => { MenuBack = Mn.Main; OpenMenu(Mn.Settings); });
        S(.4f, () => Shot("m2_settings"));
        S(.1f, () => { OpenMenu(Mn.Keys); });
        S(.4f, () => Shot("m3_keys"));
        S(.1f, () => { MenuBack = Mn.Pause; SlotSaveMode = true; OpenMenu(Mn.Slots); });
        S(.4f, () => Shot("m4_slots"));
        S(.1f, () => { MenuBack = Mn.Main; OpenMenu(Mn.Achv); Ach.Unlock("first_light", this); });
        S(.4f, () => Shot("m5_achv"));
        S(.1f, () => { OpenMenu(Mn.Credits); });
        S(.6f, () => Shot("m6_credits"));
        S(.1f, () => { Menu = Mn.None; OpenMenu(Mn.Pause); });
        S(.4f, () => Shot("m7_pause"));
        S(.1f, () => { MenuKey(Key.Down); MenuKey(Key.Enter); Log($"slots menu={Menu} save={SlotSaveMode}"); MenuKey(Key.Down); MenuKey(Key.Enter); Log($"save to slot1 exists={File.Exists(SlotPath(1))} valid={ValidSave(SlotPath(1))}"); });
        S(.2f, () => { Save(); Save(); Log($"bak exists={File.Exists(BakPath)} valid={ValidSave(BakPath)}"); File.WriteAllText(SavePath, "{ corrupted"); Log($"main valid={ValidSave(SavePath)} -> would load backup={ValidSave(BakPath)}"); Save(); Log($"after resave main valid={ValidSave(SavePath)}"); });
        S(.1f, () => { Menu = Mn.None; var before = KeyMap.Map["Interact"]; KeyMap.Rebind("Interact", Key.G); Log($"rebind: translate(G)={KeyMap.Translate(Key.G)} translate(E)={KeyMap.Translate(Key.E)}"); KeyMap.Reset(); Log($"reset translate(E)={KeyMap.Translate(Key.E)}"); Settings.Lang = "en"; });
    }
}
