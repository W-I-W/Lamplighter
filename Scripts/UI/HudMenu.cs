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

    void DrawMenu(Vector2 vs)
    {
        if (G.Menu == Mn.None) return;
        if (G.Menu == Mn.Main || G.Menu == Mn.Pause)
        {
            mv.Begin(true, G.Menu == Mn.Main ? .92f : .82f, "↑/↓ выбор · Enter" + (G.Menu == Mn.Pause ? " · Esc — продолжить" : ""), true);
            mv.Titles("ФОНАРЩИК", "Lanternreach", Gold, new Color("#9fd8ff"));
            var items = G.Menu == Mn.Main ? Game.MainItems : Game.PauseItems;
            for (int i = 0; i < items.Length; i++) mv.Item(i, G.MenuCur == i, items[i], G.Menu == Mn.Main && i == 0 && !G.HasSave);
            return;
        }
        switch (G.Menu)
        {
            case Mn.Settings:
                {
                    wv.Begin(vs, "Настройки", Gold, "←/→ изменить · Enter · Esc — назад", new Vector2(300, 236), 0, .82f);
                    string[] modes = { "Авто", "×1", "×2", "×3", "×4" };
                    string[] v = { BarStr(Settings.Music), BarStr(Settings.Sfx), modes[Settings.UiMode], $"{Settings.Shake * 100:0}%", Settings.Fullscreen ? "да" : "нет", Settings.Fullscreen ? "—" : $"{Settings.Resolutions[Settings.Res].X}×{Settings.Resolutions[Settings.Res].Y}", Settings.Lang == "ru" ? "Русский" : "English", Settings.Diag ? "вкл" : "выкл", "", "" };
                    for (int i = 0; i < Game.SettingsItems.Length; i++) wv.Row(i, G.MenuCur == i, Game.SettingsItems[i], v[i] == "" ? null : v[i], G.MenuCur == i ? Gold : Text, null, null, true);
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
            case Mn.Slots:
                {
                    wv.Begin(vs, G.SlotSaveMode ? "Сохранить игру" : "Загрузить игру", Gold, "Enter — выбрать · Esc — назад", new Vector2(300, 210), 0, .82f);
                    for (int i = 0; i < G.Slots.Count; i++)
                    {
                        var s = G.Slots[i]; string name = i == 0 ? "Автосохранение" : $"Слот {i}";
                        string info = s.Exists ? string.Format(Loc.T("ур.{0} · маяк {1}/6 · {2}ч {3}м"), s.Level, s.Beacon, (int)(s.Secs / 3600), (int)(s.Secs % 3600 / 60)) + (s.Finished ? Loc.T(" · пройдена") : "") : "пусто";
                        wv.Row(i, G.MenuCur == i, name, s.Stamp != "" ? s.Stamp : null, G.SlotSaveMode && i == 0 ? Dim : Text, info, Dim, true);
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
