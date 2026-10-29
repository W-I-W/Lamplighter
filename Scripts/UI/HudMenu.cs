using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Главное меню, пауза, настройки, слоты, управление, титры и достижения: сцены Menu.tscn (меню, титры) и Window.tscn (остальное).</summary>
public partial class Hud
{
    static string BarStr(float v) { int n = Mathf.RoundToInt(v * 10); return new string('█', n) + new string('░', 10 - n); }

    /// <summary>Индекс пункта меню под точкой (логические координаты) или -1.</summary>
    public int MenuHit(Vector2 p)
    {
        int i = mv.ItemHit(p); return i >= 0 ? i : wv.RowHit(p);
    }

    public bool SettingFrac(Vector2 logical, int idx, out float frac) => wv.ValueFrac(logical, idx, out frac);

    void DrawMenu(Vector2 vs)
    {
        if (G.Menu == Mn.None) return;
        if (G.Menu == Mn.Main || G.Menu == Mn.Pause)
        {
            mv.Begin(true, G.Menu == Mn.Main ? .92f : .82f, "↑/↓ выбор · Enter" + (G.Menu == Mn.Pause ? " · Esc — продолжить" : ""), true);
            mv.Titles("LANTHER", G.Menu == Mn.Pause ? G.SessionLine : "", Gold, new Color("#9fd8ff"));
            var items = G.Menu == Mn.Main ? Game.MainItems : Game.PauseItems;
            for (int i = 0; i < items.Length; i++) mv.Item(i, G.MenuCur == i, items[i], false);
            return;
        }
        switch (G.Menu)
        {
            case Mn.Settings:
                {
                    wv.Begin(vs, "Настройки", Gold, "←/→ изменить · Enter · Esc — назад", new Vector2(300, 318), 0, .82f);
                    string[] modes = { "Авто", "×1", "×2", "×3", "×4" };
                    string[] v = { BarStr(Settings.Music), BarStr(Settings.Sfx), modes[Settings.UiMode], $"{Settings.Shake * 100:0}%", $"{Settings.Wind * 100:0}%", $"{Settings.Particles * 100:0}%", $"{Settings.Flash * 100:0}%", $"{Settings.Fx * 100:0}%", Settings.Fullscreen ? "да" : "нет", Settings.Fullscreen ? "—" : $"{Settings.Resolutions[Settings.Res].X}×{Settings.Resolutions[Settings.Res].Y}", Settings.Lang == "ru" ? "Русский" : "English", Settings.Diag ? "вкл" : "выкл", "", "", "" };
                    for (int i = 0; i < Game.SettingsItems.Length; i++) wv.Row(i, G.MenuCur == i, Game.SettingsItems[i], v[i] == "" ? null : v[i], G.MenuCur == i ? Gold : Text, null, null, true);
                    break;
                }
            case Mn.ChatCfg:
                {
                    wv.Begin(vs, "Чат и уведомления", Gold, "←/→ изменить · Enter · Esc — назад", new Vector2(320, 190), 0, .82f);
                    string[] cv = { Settings.ShowNotes ? "да" : "нет", Settings.ShowChat ? "да" : "нет", Settings.ChatSize == 0 ? "обычный" : "крупный", $"{Settings.ChatBack * 100:0}%", $"{Settings.ChatSecs:0} с", "" };
                    for (int i = 0; i < Game.ChatItems.Length; i++) wv.Row(i, G.MenuCur == i, Game.ChatItems[i], cv[i] == "" ? null : cv[i], G.MenuCur == i ? Gold : Text, null, null, true);
                    break;
                }
            case Mn.Keys:
                {
                    wv.Begin(vs, "Управление", Gold, "Enter — назначить · Esc — назад", new Vector2(300, 340), 0, .82f);
                    int n = KeyMap.Actions.Length;
                    for (int i = 0; i < n + 2; i++)
                    {
                        string name = i < n ? KeyMap.Labels[i] : i == n ? "Сбросить" : "Назад"; string val = i < n ? (G.RebindAction == KeyMap.Actions[i] ? "нажмите…" : KeyMap.Name(KeyMap.Actions[i])) : null;
                        wv.Row(i, G.MenuCur == i, name, val, G.MenuCur == i ? Gold : Text, null, null, true);
                    }
                    break;
                }
            case Mn.Confirm:
                {
                    wv.Begin(vs, "Подтверждение", Warn, null, new Vector2(300, 110), 0, .82f);
                    wv.Text(false, G.ConfirmText, Text);
                    wv.Row(0, G.MenuCur == 0, "Да", null, G.MenuCur == 0 ? Gold : Text, null, null, true);
                    wv.Row(1, G.MenuCur == 1, "Нет", null, G.MenuCur == 1 ? Gold : Text, null, null, true);
                    break;
                }
            case Mn.Credits:
                {
                    mv.Begin(false, .82f, "Esc — назад", false);
                    float scroll = (G.UiTime * 20f) % (Story.Credits.Length * 18f + vs.Y);
                    for (int i = 0; i < Story.Credits.Length; i++)
                    {
                        float yy = vs.Y - scroll + i * 18; if (yy < -20 || yy > vs.Y + 20) continue; bool head = i == 0 || (i > 0 && Story.Credits[i - 1] == "");
                        mv.Line(Story.Credits[i], new Vector2(0, yy - (head ? 14 : 7)), vs.X, head, head ? Gold : Text);
                    }
                    mv.Line($"{Loc.T("Версия")} {Game.VersionStr} · {Game.BuildStr}", new Vector2(0, vs.Y - 25), vs.X, false, Dim);
                    break;
                }
            case Mn.Multi:
                {
                    wv.Begin(vs, "Мультиплеер", Gold, "↑/↓ · Enter · Esc — назад", new Vector2(360, 230), 0, .82f);
                    string[] rows = { "Steam: создать игру", "Steam: войти по ID лобби", "Локальная сеть: создать игру", "Локальная сеть: подключиться по адресу", "Назад" };
                    bool[] ok = { G.NetSteamOk, G.NetSteamOk, true, true, true };
                    wv.Text(false, "Хост выбирает героя и мир, гость — только героя: он попадает в мир хоста.", Dim, true, 3);
                    for (int i = 0; i < rows.Length; i++) wv.Row(i, G.MenuCur == i, rows[i], null, !ok[i] ? Muted : G.MenuCur == i ? Gold : Text, null, null, true);
                    if (!G.NetSteamOk) wv.Text(false, Loc.T("Steam недоступен: ") + Loc.T(G.NetSteamWhy), Warn, true, 3);
                    if (NetService.State == NetState.Failed) wv.Text(false, NetService.Status, Bad, true, 2);
                    break;
                }
            case Mn.NetJoin:
                {
                    wv.Begin(vs, G.NetJoinSteam ? "Вход в лобби Steam" : "Подключение к игре", Gold, "Enter — выбрать героя и подключиться · Esc — назад", new Vector2(340, 150), 0, .82f);
                    if (GameSession.Mode == SessionMode.Guest) wv.Text(false, Loc.T("Герой") + ": " + NetService.LocalName, Cyan);
                    wv.Text(false, G.NetJoinSteam ? "ID лобби:" : "Адрес хоста (порт по умолчанию 24865):", Dim);
                    wv.Text(false, G.NetAddr + (((int)(G.UiTime * 2) % 2 == 0) ? "_" : " "), Text, false);
                    bool busy = NetService.State == NetState.Connecting || NetService.State == NetState.Handshaking;
                    if (NetService.Status != "") wv.Text(false, NetService.Status, NetService.State == NetState.Failed ? Bad : busy ? Cyan : Good, true, 3);
                    break;
                }
            case Mn.NetInfo:
                {
                    wv.Begin(vs, "Сеть", Gold, "↑/↓ · Enter · Esc — назад", new Vector2(360, 230), 0, .82f);
                    if (!NetService.Active) wv.Text(false, "Сетевая игра не запущена. Создать её можно в главном меню: «Мультиплеер».", Dim, true, 3);
                    else
                    {
                        wv.Text(false, $"{Loc.T(NetService.Role == NetRole.Host ? "Вы — хост" : "Вы — клиент")} · {Loc.T(NetService.Transport?.DisplayName ?? "")} · {NetService.Count}/{NetConst.MaxPlayers}", Cyan, true, 2);
                        foreach (var p in NetService.Players.Values) wv.Text(false, "● " + p.Name + (p.PeerId == 1 ? " " + Loc.T("(хост)") : "") + (p.PeerId == NetService.LocalId ? " " + Loc.T("(вы)") : ""), Good, false);
                        string inv = NetService.Transport?.InviteInfo ?? "";
                        if (inv != "") wv.Text(false, Loc.T(NetService.Transport is SteamTransport ? "ID лобби:" : "Порт:") + " " + inv, Gold, true, 2);
                        if (NetService.Status != "") wv.Text(false, NetService.Status, Dim, true, 2);
                    }
                    string[] rows = { "Пригласить друзей", "Отключиться", "Назад" };
                    for (int i = 0; i < rows.Length; i++) wv.Row(i, G.MenuCur == i, rows[i], null, G.MenuCur == i ? Gold : Text, null, null, true);
                    break;
                }
            case Mn.Achv:
                {
                    wv.Begin(vs, $"{Loc.T("Достижения")} {Ach.Got.Count}/{Ach.All.Length}", Gold, "↑/↓ · Esc — назад", new Vector2(360, 300), 0, .82f);
                    int vis = Math.Max(1, wv.Fit(22)), sc = Mathf.Max(0, G.MenuCur - vis + 1);
                    for (int i = sc; i < Ach.All.Length && i < sc + vis; i++)
                    {
                        var d = Ach.All[i]; bool got = Ach.Got.Contains(d.Id);
                        wv.Row(i, G.MenuCur == i, (got ? "★ " : "☆ ") + d.Name, null, got ? Gold : Dim, d.Desc, got ? Text : Muted);
                    }
                    break;
                }
        }
    }
}
