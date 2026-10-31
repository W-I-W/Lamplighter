using Godot;
using System;
using System.Collections.Generic;
using System.IO;

namespace Lamplighter;

public enum Mn { None, Main, Pause, Settings, Keys, Credits, Confirm, Achv, Multi, NetJoin, NetInfo, Flow, ChatCfg }

/// <summary>Главное меню, пауза, настройки, слоты сохранений, титры и достижения.</summary>
public partial class Game
{
    public const string VersionStr = "1.0.0";
    public const string BuildStr = "Windows · Godot 4.7 .NET";

    public Mn Menu; public Mn MenuBack; public int MenuCur;
    public string ConfirmText = ""; Action confirmYes;
    public string RebindAction;                      // ждём клавишу для действия
    public float UiTime, PlaySecs;

    public static readonly string[] MainItems = { "Играть", "Мультиплеер", "Настройки", "Достижения", "Титры", "Выход" };
    public static readonly string[] PauseItems = { "Продолжить", "Сохранить игру", "Настройки", "Достижения", "Сеть", "Титры", "В главное меню", "Выход" };
    public static readonly string[] SettingsItems = { "Громкость музыки", "Громкость звуков", "Масштаб интерфейса", "Тряска экрана", "Ветер", "Частицы", "Вспышки", "Доп. эффекты", "Полный экран", "Разрешение окна", "Язык", "Диагностика (локальная)", "Чат и уведомления", "Управление", "Назад" };
    public static readonly string[] ChatItems = { "Показывать уведомления игры", "Показывать сообщения чата", "Размер текста чата", "Прозрачность подложки", "Время показа сообщений", "Назад" };

    // ───────────────────────── открытие/закрытие ─────────────────────────
    public void OpenMenu(Mn m) { Menu = m; MenuCur = 0; if (m == Mn.Multi) RefreshNetMenu(); Sfx.Play("tick", -4, 1.1f); }
    void CloseMenu() { Menu = Mn.None; RebindAction = null; Settings.Save(); }

    public void StartMenuOnLaunch()
    {
        if (testing || GameSession.Active) return;
        Menu = Mn.Main; MenuCur = 0;
        if (GameSession.PendingTarget != null) { var t = GameSession.PendingTransport; var a = GameSession.PendingTarget; GameSession.PendingTarget = null; OpenGuestFlow(t, a); }
    }

    public int MenuLen => Menu switch
    {
        Mn.Main => MainItems.Length, Mn.Pause => PauseItems.Length, Mn.Settings => SettingsItems.Length,
        Mn.Keys => KeyMap.Actions.Length + 2, Mn.ChatCfg => ChatItems.Length, Mn.Confirm => 2, Mn.Achv => Ach.All.Length, Mn.Multi => 5, Mn.NetInfo => 3, _ => 1
    };

    // ───────────────────────── ввод ─────────────────────────
    void MenuKey(Key k)
    {
        if (RebindAction != null)
        {
            if (k != Key.Escape && k != Key.None) { KeyMap.Rebind(RebindAction, k); Settings.Save(); Sfx.Play("upgrade", -4, 1.4f); }
            RebindAction = null; return;
        }
        if (Menu == Mn.Flow) return;
        bool enter = k == Key.Enter || k == Key.KpEnter || k == Key.Space;
        if (k == Key.Up || k == Key.W) { MenuCur = (MenuCur + MenuLen - 1) % MenuLen; Sfx.Play("tick", -6, 1.4f); return; }
        if (k == Key.Down || k == Key.S) { MenuCur = (MenuCur + 1) % MenuLen; Sfx.Play("tick", -6, 1.4f); return; }
        if (k == Key.Escape) { MenuBackKey(); return; }
        switch (Menu)
        {
            case Mn.Main: if (enter) MainAct(MenuCur); break;
            case Mn.Pause: if (enter) PauseAct(MenuCur); break;
            case Mn.Settings: SettingsKey(k, enter); break;
            case Mn.ChatCfg: ChatCfgKey(k, enter); break;
            case Mn.Multi: if (enter) MultiAct(MenuCur); break;
            case Mn.NetInfo: if (enter) NetInfoAct(MenuCur); break;
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
        // громкость: клик или перетаскивание по полосе ставит значение, клик рядом с полосой прибавляет 10% (после максимума — с нуля)
        if (Menu == Mn.Settings && RebindAction == null && (hit == 0 || hit == 1) && (e is InputEventMouseButton vb ? vb.Pressed && vb.ButtonIndex == MouseButton.Left : e is InputEventMouseMotion && Input.IsMouseButtonPressed(MouseButton.Left)))
        {
            bool onBar = hud.SettingFrac(e.Position / UiScaleVp, hit, out float fr) && fr >= -.05f && fr <= 1.05f;
            if (onBar) { float v = Mathf.Round(Mathf.Clamp(fr, 0, 1) * 10f) / 10f; MenuCur = hit; SetVolume(hit, v); return; }
            if (e is InputEventMouseButton) { MenuCur = hit; float cur = hit == 0 ? Settings.Music : Settings.Sfx; SetVolume(hit, cur >= .95f ? 0f : cur + .1f); return; }
        }
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
                if ((Menu == Mn.Settings && hit >= 0 && hit < 12) || (Menu == Mn.ChatCfg && hit >= 0 && hit < 5)) { MenuCur = hit; MenuKey(Key.Left); }
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
            case Mn.Keys: OpenMenu(Mn.Settings); MenuCur = 13; break;
            case Mn.ChatCfg: Settings.Save(); OpenMenu(Mn.Settings); MenuCur = 12; break;
            default: OpenMenu(MenuBack); break;
        }
    }

    void MainAct(int i)
    {
        switch (i)
        {
            case 0: OpenFlow(FlowKind.Solo); break;
            case 1: MenuBack = Mn.Main; OpenMenu(Mn.Multi); break;
            case 2: MenuBack = Mn.Main; OpenMenu(Mn.Settings); break;
            case 3: MenuBack = Mn.Main; OpenMenu(Mn.Achv); break;
            case 4: MenuBack = Mn.Main; OpenMenu(Mn.Credits); break;
            case 5: Save(); NetService.Leave(false); GetTree().Quit(); break;
        }
    }

    void PauseAct(int i)
    {
        switch (i)
        {
            case 0: CloseMenu(); break;
            case 1: SaveAll(true); Toast("Герой и мир сохранены"); Sfx.Play("upgrade", -3, 1.3f); break;
            case 2: MenuBack = Mn.Pause; OpenMenu(Mn.Settings); break;
            case 3: MenuBack = Mn.Pause; OpenMenu(Mn.Achv); break;
            case 4: MenuBack = Mn.Pause; OpenMenu(Mn.NetInfo); break;
            case 5: MenuBack = Mn.Pause; OpenMenu(Mn.Credits); break;
            case 6: SaveAll(true); LeaveToMenu(); break;
            case 7: SaveAll(true); NetService.Leave(false); GetTree().Quit(); break;
        }
    }

    void AskConfirm(string text, Action yes, Mn back) { ConfirmText = text; confirmYes = yes; MenuBack = back; OpenMenu(Mn.Confirm); MenuCur = 1; }

    void SetVolume(int which, float v)
    {
        v = Mathf.Clamp(v, 0, 1); if (which == 0) { if (Mathf.IsEqualApprox(Settings.Music, v)) return; Settings.Music = v; } else { if (Mathf.IsEqualApprox(Settings.Sfx, v)) return; Settings.Sfx = v; Sfx.Play("pickup"); }
        Settings.ApplyAudio(); Settings.Save();
    }

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
            case 4: Settings.Wind = Mathf.Clamp(Settings.Wind + .25f * d, 0, 1.5f); break;
            case 5: Settings.Particles = Mathf.Clamp(Settings.Particles + .25f * d, 0, 1.5f); break;
            case 6: Settings.Flash = Mathf.Clamp(Settings.Flash + .25f * d, 0, 1.5f); break;
            case 7: Settings.Fx = Mathf.Clamp(Settings.Fx + .25f * d, 0, 1.5f); break;
            case 8: Settings.Fullscreen = !Settings.Fullscreen; Settings.ApplyWindow(testing); break;
            case 9: Settings.Res = (Settings.Res + d + Settings.Resolutions.Length) % Settings.Resolutions.Length; Settings.ApplyWindow(testing); break;
            case 10: Settings.Lang = Settings.Lang == "ru" ? "en" : "ru"; Loc.SetLang(Settings.Lang); break;
            case 11: Settings.Diag = !Settings.Diag; Diag.Enabled = Settings.Diag; if (Settings.Diag) Diag.Reset(); break;
            case 12: if (enter) { OpenMenu(Mn.ChatCfg); } break;
            case 13: if (enter) { OpenMenu(Mn.Keys); } break;
            case 14: if (enter) { Settings.Save(); OpenMenu(MenuBack); } break;
        }
        Sfx.Play("tick", -6, 1.2f);
    }

    void KeysKey(Key k, bool enter)
    {
        if (!enter) return;
        if (MenuCur < KeyMap.Actions.Length) { RebindAction = KeyMap.Actions[MenuCur]; return; }
        if (MenuCur == KeyMap.Actions.Length) { KeyMap.Reset(); Settings.Save(); Toast("Управление сброшено"); return; }
        OpenMenu(Mn.Settings); MenuCur = 13;
    }

    /// <summary>Настройки чата и журнала: два переключателя, размер текста, прозрачность подложки и время показа.</summary>
    void ChatCfgKey(Key k, bool enter)
    {
        int d = k == Key.Left || k == Key.A ? -1 : k == Key.Right || k == Key.D ? 1 : 0;
        if (d == 0 && !enter) return;
        if (d == 0) d = 1;
        switch (MenuCur)
        {
            case 0: Settings.ShowNotes = !Settings.ShowNotes; break;
            case 1: Settings.ShowChat = !Settings.ShowChat; break;
            case 2: Settings.ChatSize = 1 - Settings.ChatSize; break;
            case 3: Settings.ChatBack = Mathf.Clamp(Mathf.Round((Settings.ChatBack + .15f * d) * 100f) / 100f, 0, 1); break;
            case 4: Settings.ChatSecs = Mathf.Clamp(Settings.ChatSecs + 2f * d, 6f, 14f); break;
            case 5: if (enter) { Settings.Save(); OpenMenu(Mn.Settings); MenuCur = 12; } break;
        }
        Sfx.Play("tick", -6, 1.2f);
    }

    // ───────────────────────── музыка ─────────────────────────
    float musicT, musicAcc, combatT, prevHp = -1, hurtT, moodT; int mood, moodWant;     // mood: 0 — свет ламп/биом, 1 — темнота, 2 — у зажжённого маяка
    /// <summary>Музыка следует за игроком: меню → финал → босс → бой → маяк → темнота → биом. Бой включается сразу и держится 4 с после боя;
    /// у зажжённого маяка сразу играет спокойный трек; в темноту музыка уходит медленно (~5 с), чтобы не дёргаться на краю света. При низком HP темп падает.</summary>
    void UpdateMusic(float dt)
    {
        if (prevHp < 0) prevHp = Hp;
        if (Hp < prevHp - .5f) hurtT = 6f;
        prevHp = Hp; hurtT = Mathf.Max(0, hurtT - dt);
        musicAcc += dt; musicT -= dt; if (musicT > 0) return; float step = musicAcc; musicAcc = 0; musicT = .1f;
        int near = 0, close = 0; bool elite = false;
        foreach (var e in Enemies)
        {
            if (e.Hidden || e.Hp <= 0 || e.Kind == EK.Decoy || e.Kind == EK.Mirage) continue;
            float d = e.Pos.DistanceTo(PPos); if (d < 150f) { near++; elite |= e.Elite; if (d < 100f) close++; }
        }
        bool atBeacon = false; foreach (var a in W.Ancients) if (a.Lit && PPos.DistanceTo(a.Pos) < AncientR(a) * .85f) { atBeacon = true; break; }
        if (atBeacon) { combatT = 0; mood = 2; moodT = 0; }          // у маяка — сразу спокойный трек
        else
        {
            if (near >= 2 || elite || close >= 1 || (near >= 1 && hurtT > 0)) combatT = 4f; else combatT = Mathf.Max(0, combatT - step);
            moodWant = IsLit(PPos) ? 0 : 1;
            if (moodWant == mood) moodT = 0; else { moodT += step; if (moodT >= (moodWant == 1 ? 5f : 2.5f)) { mood = moodWant; moodT = 0; } }
        }
        Music.Health(MaxHp > 0 ? Hp / MaxHp : 1f);
        string zone = RegionAt(PPos) switch { 1 => "mines", 2 => "ash", 3 => "ice", 4 => "heart", _ => "forest" };
        string t = Menu == Mn.Main || Menu == Mn.Flow ? "menu" : FinStage == 1 || FinStage == 2 || FinStage == 3 ? "finale" : (Boss != null || RiftOn) ? "boss" : combatT > 0 ? "battle_" + zone : mood == 2 ? "beacon" : mood == 1 ? "dark_" + zone : zone;
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
        S(.1f, () => { MenuBack = Mn.Main; OpenMenu(Mn.Achv); Ach.Unlock("first_light", this); });
        S(.4f, () => Shot("m5_achv"));
        S(.1f, () => { OpenMenu(Mn.Credits); });
        S(.6f, () => Shot("m6_credits"));
        S(.1f, () => { Menu = Mn.None; OpenMenu(Mn.Pause); });
        S(.4f, () => Shot("m7_pause"));
        S(.1f, () => { Save(); Save(); Log($"world exists={File.Exists(ProjectSettings.GlobalizePath(SavePath))} valid={ValidSave(SavePath)} bak={File.Exists(ProjectSettings.GlobalizePath(SavePath) + ".bak")}"); File.WriteAllText(ProjectSettings.GlobalizePath(SavePath), "{ corrupted"); Log($"main valid={ValidSave(SavePath)} -> would load backup={SaveManager.LoadWorld(GameSession.WorldId) != null}"); Save(); Log($"after resave main valid={ValidSave(SavePath)}"); });
        S(.1f, () => { Menu = Mn.None; var before = KeyMap.Map["Interact"]; KeyMap.Rebind("Interact", Key.G); Log($"rebind: translate(G)={KeyMap.Translate(Key.G)} translate(E)={KeyMap.Translate(Key.E)}"); KeyMap.Reset(); Log($"reset translate(E)={KeyMap.Translate(Key.E)}"); Settings.Lang = "en"; });
    }
}
