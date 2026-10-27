using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Главное меню, пауза, настройки, слоты, управление, титры и достижения — пиксельные рамки и кнопки из темы.</summary>
public partial class Hud
{
    static string BarStr(float v) { int n = Mathf.RoundToInt(v * 10); return new string('█', n) + new string('░', 10 - n); }

    /// <summary>Кнопка меню: состояния обычная / выбранная / недоступная берутся из темы (Button, ButtonSelected).</summary>
    void MenuButton(float cx, float y, float w, string text, bool sel, string value = null, bool disabled = false)
    {
        var r = new Rect2(Mathf.Round(cx - w / 2), Mathf.Round(y), w, 15); panelRects.Add(r);
        theme.GetStylebox(disabled ? "disabled" : "normal", disabled ? "Button" : sel ? "ButtonSelected" : "Button").Draw(GetCanvasItem(), r);
        var col = disabled ? new Color("#5a6278") : sel ? Gold : Text;
        Txt(text, new Vector2(r.Position.X + 6, y + 11), 9, col, HorizontalAlignment.Left, w - 12);
        if (value != null) Txt(value, new Vector2(r.Position.X, y + 11), 9, sel ? Gold : Dim, HorizontalAlignment.Right, w - 6);
    }

    void DrawMenu(Vector2 vs)
    {
        if (G.Menu == Mn.None) return;
        DrawRect(new Rect2(Vector2.Zero, vs), new Color(.02f, .03f, .06f, G.Menu == Mn.Main ? .92f : .82f));
        float cx = vs.X / 2, w = Mathf.Min(260, vs.X - 20);
        if (G.Menu == Mn.Main || G.Menu == Mn.Pause)
        {
            Txt("ФОНАРЩИК", new Vector2(0, 62), 14, Gold, HorizontalAlignment.Center, vs.X);
            Txt("Lanternreach", new Vector2(0, 76), 9, new Color("#9fd8ff"), HorizontalAlignment.Center, vs.X);
            float fl = .8f + .2f * Mathf.Sin(G.UiTime * 9f); DrawRect(new Rect2(cx - 3, 24, 6, 6), new Color(1f, .8f, .3f, .35f * fl)); DrawRect(new Rect2(cx - 2, 25, 4, 4), new Color(1f, .9f, .5f)); DrawRect(new Rect2(cx - 1, 26, 2, 2), Colors.White);
            var items = G.Menu == Mn.Main ? Game.MainItems : Game.PauseItems; float y0 = 92;
            Panel(new Rect2(cx - w / 2 - 8, y0 - 8, w + 16, items.Length * 17 + 10), "PanelContainer");
            for (int i = 0; i < items.Length; i++) MenuButton(cx, y0 + i * 17, w, items[i], G.MenuCur == i, null, G.Menu == Mn.Main && i == 0 && !G.HasSave);
            Txt($"v{Game.VersionStr} · {Game.BuildStr}", new Vector2(0, vs.Y - 5), 9, Dim, HorizontalAlignment.Right, vs.X - 6);
            Txt("↑/↓ выбор · Enter" + (G.Menu == Mn.Pause ? " · Esc — продолжить" : ""), new Vector2(0, vs.Y - 5), 9, Dim, HorizontalAlignment.Center, vs.X);
            return;
        }
        switch (G.Menu)
        {
            case Mn.Settings:
                {
                    var r = WinRect(vs, 300, 226); var a = Frame(r, "Настройки", "←/→ изменить · Enter · Esc — назад");
                    string[] modes = { "Авто", "×1", "×2", "×3", "×4" };
                    string[] v = { BarStr(Settings.Music), BarStr(Settings.Sfx), modes[Settings.UiMode], $"{Settings.Shake * 100:0}%", Settings.Fullscreen ? "да" : "нет", Settings.Fullscreen ? "—" : $"{Settings.Resolutions[Settings.Res].X}×{Settings.Resolutions[Settings.Res].Y}", Settings.Lang == "ru" ? "Русский" : "English", Settings.Diag ? "вкл" : "выкл", "", "" };
                    for (int i = 0; i < Game.SettingsItems.Length; i++) MenuButton(a.Position.X + a.Size.X / 2, a.Position.Y + 6 + i * 16, a.Size.X - 4, Game.SettingsItems[i], G.MenuCur == i, v[i] == "" ? null : v[i]);
                    break;
                }
            case Mn.Keys:
                {
                    var r = WinRect(vs, 300, 280); var a = Frame(r, "Управление", "Enter — назначить · Esc — назад");
                    int n = KeyMap.Actions.Length;
                    for (int i = 0; i < n + 2; i++)
                    {
                        string name = i < n ? KeyMap.Labels[i] : i == n ? "Сбросить" : "Назад"; string val = i < n ? (G.RebindAction == KeyMap.Actions[i] ? "нажмите…" : KeyMap.Name(KeyMap.Actions[i])) : null;
                        MenuButton(a.Position.X + a.Size.X / 2, a.Position.Y + 4 + i * 12, a.Size.X - 4, name, G.MenuCur == i, val);
                    }
                    break;
                }
            case Mn.Slots:
                {
                    var r = WinRect(vs, 300, 190); var a = Frame(r, G.SlotSaveMode ? "Сохранить игру" : "Загрузить игру", "Enter — выбрать · Esc — назад");
                    for (int i = 0; i < G.Slots.Count; i++)
                    {
                        var s = G.Slots[i]; string name = i == 0 ? "Автосохранение" : $"Слот {i}";
                        string info = s.Exists ? $"ур.{s.Level} · маяк {s.Beacon}/6 · {(int)(s.Secs / 3600)}ч {(int)(s.Secs % 3600 / 60)}м" + (s.Finished ? " · пройдена" : "") : "пусто";
                        float y = a.Position.Y + 4 + i * 36; var rr = new Rect2(a.Position.X, y, a.Size.X, 32); panelRects.Add(rr);
                        theme.GetStylebox("normal", G.MenuCur == i ? "ButtonSelected" : "Button").Draw(GetCanvasItem(), rr);
                        Txt(name, new Vector2(rr.Position.X + 6, y + 11), 9, G.SlotSaveMode && i == 0 ? Dim : Text); Txt(info, new Vector2(rr.Position.X + 6, y + 23), 9, Dim, HorizontalAlignment.Left, rr.Size.X - 12);
                        if (s.Stamp != "") Txt(s.Stamp, new Vector2(rr.Position.X, y + 11), 9, Dim, HorizontalAlignment.Right, rr.Size.X - 6);
                    }
                    break;
                }
            case Mn.Confirm:
                {
                    var r = WinRect(vs, 300, 100); var a = Frame(r, "Подтверждение", null, Warn);
                    Para(G.ConfirmText, a.Position.X + 4, a.Position.Y + 10, a.Size.X - 8, Text);
                    MenuButton(cx - 50, r.End.Y - 24, 80, "Да", G.MenuCur == 0); MenuButton(cx + 50, r.End.Y - 24, 80, "Нет", G.MenuCur == 1);
                    break;
                }
            case Mn.Credits:
                {
                    float scroll = (G.UiTime * 20f) % (Story.Credits.Length * 18f + vs.Y);
                    for (int i = 0; i < Story.Credits.Length; i++)
                    {
                        float yy = vs.Y - scroll + i * 18; if (yy < -20 || yy > vs.Y + 20) continue; bool head = i == 0 || (i > 0 && Story.Credits[i - 1] == "");
                        Txt(Story.Credits[i], new Vector2(0, yy), head ? 14 : 9, head ? Gold : Text, HorizontalAlignment.Center, vs.X);
                    }
                    Txt($"Версия {Game.VersionStr} · {Game.BuildStr}", new Vector2(0, vs.Y - 16), 9, Dim, HorizontalAlignment.Center, vs.X); Txt("Esc — назад", new Vector2(0, vs.Y - 5), 9, Dim, HorizontalAlignment.Center, vs.X);
                    break;
                }
            case Mn.Achv:
                {
                    var r = WinRect(vs, 360, 300); var a = Frame(r, $"Достижения {Ach.Got.Count}/{Ach.All.Length}", "↑/↓ · Esc — назад");
                    int vis = (int)((a.Size.Y - 4) / 22), sc = Mathf.Max(0, G.MenuCur - vis + 1);
                    for (int i = sc; i < Ach.All.Length && i < sc + vis; i++)
                    {
                        var d = Ach.All[i]; bool got = Ach.Got.Contains(d.Id); float y = a.Position.Y + 6 + (i - sc) * 22;
                        if (G.MenuCur == i) theme.GetStylebox("normal", "ButtonSelected").Draw(GetCanvasItem(), new Rect2(a.Position.X, y - 2, a.Size.X, 21));
                        Txt((got ? "★ " : "☆ ") + d.Name, new Vector2(a.Position.X + 4, y + 8), 9, got ? Gold : Dim, HorizontalAlignment.Left, a.Size.X - 8);
                        Txt(d.Desc, new Vector2(a.Position.X + 14, y + 17), 9, got ? Text : new Color("#6a7288"), HorizontalAlignment.Left, a.Size.X - 18);
                    }
                    break;
                }
        }
    }
}
