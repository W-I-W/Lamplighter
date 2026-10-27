using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Главное меню, пауза, настройки, слоты, управление, титры и достижения.</summary>
public partial class Hud
{
    static string BarStr(float v) { int n = Mathf.RoundToInt(v * 10); return new string('█', n) + new string('░', 10 - n); }

    void MenuRow(float cx, float y, float w, string text, bool sel, string value = null, bool disabled = false)
    {
        var r = new Rect2(cx - w / 2, y - 18, w, 26);
        if (sel) { DrawRect(r, new Color(.2f, .38f, .55f, .55f)); DrawRect(new Rect2(r.Position, new Vector2(3, r.Size.Y)), new Color("#ffe27a")); }
        var col = disabled ? new Color(.5f, .5f, .55f) : sel ? Colors.White : new Color(.85f, .88f, .95f);
        Txt(text, new Vector2(r.Position.X + 14, y), 16, col);
        if (value != null) Txt(value, new Vector2(r.Position.X, y), 15, sel ? Gold : Dim, HorizontalAlignment.Right, w - 14);
    }

    void DrawMenu(Vector2 vs)
    {
        if (G.Menu == Mn.None) return;
        DrawRect(new Rect2(Vector2.Zero, vs), new Color(.015f, .02f, .05f, G.Menu == Mn.Main ? .9f : .8f));
        float cx = vs.X / 2, w = Mathf.Min(560, vs.X - 40);
        if (G.Menu == Mn.Main || G.Menu == Mn.Pause)
        {
            Txt("ФОНАРЩИК", new Vector2(0, 108), 40, new Color("#ffe27a"), HorizontalAlignment.Center, vs.X);
            Txt("Lanternreach", new Vector2(0, 136), 16, new Color(.7f, .85f, 1f), HorizontalAlignment.Center, vs.X);
            // мерцающий огонёк
            float fl = .8f + .2f * Mathf.Sin(G.UiTime * 9f); DrawCircle(new Vector2(cx, 70), 14 * fl, new Color(1f, .8f, .3f, .15f)); DrawCircle(new Vector2(cx, 70), 6 * fl, new Color(1f, .9f, .5f, .8f));
            var items = G.Menu == Mn.Main ? Game.MainItems : Game.PauseItems;
            for (int i = 0; i < items.Length; i++) MenuRow(cx, 190 + i * 34, 360, items[i], G.MenuCur == i, null, G.Menu == Mn.Main && i == 0 && !G.HasSave);
            Txt($"v{Game.VersionStr} · {Game.BuildStr}", new Vector2(0, vs.Y - 14), 11, Dim, HorizontalAlignment.Right, vs.X - 12);
            Txt("↑/↓ выбор · Enter — подтвердить" + (G.Menu == Mn.Pause ? " · Esc — продолжить" : ""), new Vector2(0, vs.Y - 14), 11, Dim, HorizontalAlignment.Center, vs.X);
            return;
        }
        float top = 70;
        switch (G.Menu)
        {
            case Mn.Settings:
                {
                    Txt("Настройки", new Vector2(0, top), 26, Gold, HorizontalAlignment.Center, vs.X);
                    string[] v = { BarStr(Settings.Music), BarStr(Settings.Sfx), $"{Settings.UiScale * 100:0}%", $"{Settings.Shake * 100:0}%", Settings.Fullscreen ? "да" : "нет", Settings.Fullscreen ? "—" : $"{Settings.Resolutions[Settings.Res].X}×{Settings.Resolutions[Settings.Res].Y}", Settings.Lang == "ru" ? "Русский" : "English", Settings.Diag ? "вкл" : "выкл", "", "" };
                    for (int i = 0; i < Game.SettingsItems.Length; i++) MenuRow(cx, top + 60 + i * 34, w, Game.SettingsItems[i], G.MenuCur == i, v[i] == "" ? null : v[i]);
                    Txt("←/→ изменить · Enter · Esc — назад", new Vector2(0, vs.Y - 14), 11, Dim, HorizontalAlignment.Center, vs.X);
                    break;
                }
            case Mn.Keys:
                {
                    Txt("Управление", new Vector2(0, top - 30), 26, Gold, HorizontalAlignment.Center, vs.X);
                    int n = KeyMap.Actions.Length; float rh = 26; float y0 = top + 10;
                    for (int i = 0; i < n + 2; i++)
                    {
                        string name = i < n ? KeyMap.Labels[i] : i == n ? "Сбросить по умолчанию" : "Назад";
                        string val = i < n ? (G.RebindAction == KeyMap.Actions[i] ? "нажмите клавишу…" : KeyMap.Name(KeyMap.Actions[i])) : null;
                        var r = new Rect2(cx - w / 2, y0 + i * rh - 16, w, rh - 2);
                        if (G.MenuCur == i) { DrawRect(r, new Color(.2f, .38f, .55f, .55f)); DrawRect(new Rect2(r.Position, new Vector2(3, r.Size.Y)), new Color("#ffe27a")); }
                        Txt(name, new Vector2(r.Position.X + 12, y0 + i * rh), 14, G.MenuCur == i ? Colors.White : new Color(.85f, .88f, .95f));
                        if (val != null) Txt(val, new Vector2(r.Position.X, y0 + i * rh), 14, G.RebindAction == KeyMap.Actions[i] ? Warn : Gold, HorizontalAlignment.Right, w - 12);
                    }
                    Txt("Enter — назначить · остальные клавиши (1–0, Z, X, F5) фиксированы · Esc — назад", new Vector2(0, vs.Y - 14), 11, Dim, HorizontalAlignment.Center, vs.X);
                    break;
                }
            case Mn.Slots:
                {
                    Txt(G.SlotSaveMode ? "Сохранить игру" : "Загрузить игру", new Vector2(0, top), 26, Gold, HorizontalAlignment.Center, vs.X);
                    for (int i = 0; i < G.Slots.Count; i++)
                    {
                        var s = G.Slots[i]; string name = i == 0 ? "Автосохранение" : $"Слот {i}";
                        string info = s.Exists ? $"ур. {s.Level} · маяк {s.Beacon}/6 · {(int)(s.Secs / 3600)}ч {(int)(s.Secs % 3600 / 60)}м" + (s.Finished ? " · пройдена" : "") : "пусто";
                        float y = top + 70 + i * 54; var r = new Rect2(cx - w / 2, y - 22, w, 46);
                        if (G.MenuCur == i) { DrawRect(r, new Color(.2f, .38f, .55f, .55f)); DrawRect(new Rect2(r.Position, new Vector2(3, r.Size.Y)), new Color("#ffe27a")); }
                        Txt(name, new Vector2(r.Position.X + 12, y - 2), 16, G.SlotSaveMode && i == 0 ? Dim : Colors.White);
                        Txt(info, new Vector2(r.Position.X + 12, y + 16), 12, Dim);
                        if (s.Stamp != "") Txt(s.Stamp, new Vector2(r.Position.X, y - 2), 12, Dim, HorizontalAlignment.Right, w - 12);
                    }
                    Txt("Enter — выбрать · Esc — назад", new Vector2(0, vs.Y - 14), 11, Dim, HorizontalAlignment.Center, vs.X);
                    break;
                }
            case Mn.Confirm:
                {
                    var r = new Rect2(cx - 280, vs.Y / 2 - 90, 560, 180); Box(r, Warn);
                    DrawMultilineString(font, new Vector2(r.Position.X + 20, r.Position.Y + 42), Loc.T(G.ConfirmText), HorizontalAlignment.Center, r.Size.X - 40, 16, -1, Colors.White);
                    MenuRow(cx - 100, r.End.Y - 36, 160, "Да", G.MenuCur == 0); MenuRow(cx + 100, r.End.Y - 36, 160, "Нет", G.MenuCur == 1);
                    break;
                }
            case Mn.Credits:
                {
                    float scroll = (G.UiTime * 30f) % (Story.Credits.Length * 34f + vs.Y);
                    for (int i = 0; i < Story.Credits.Length; i++)
                    {
                        float yy = vs.Y - scroll + i * 34; if (yy < -30 || yy > vs.Y + 30) continue;
                        bool head = i == 0 || (i > 0 && Story.Credits[i - 1] == "");
                        Txt(Story.Credits[i], new Vector2(0, yy), head ? 22 : 16, head ? Gold : new Color(.9f, .92f, 1f), HorizontalAlignment.Center, vs.X);
                    }
                    Txt($"Версия {Game.VersionStr} · {Game.BuildStr}", new Vector2(0, vs.Y - 30), 12, Dim, HorizontalAlignment.Center, vs.X);
                    Txt("Esc — назад", new Vector2(0, vs.Y - 12), 11, Dim, HorizontalAlignment.Center, vs.X);
                    break;
                }
            case Mn.Achv:
                {
                    Txt($"Достижения: {Ach.Got.Count}/{Ach.All.Length}", new Vector2(0, top - 30), 26, Gold, HorizontalAlignment.Center, vs.X);
                    float rh = 30; int vis = (int)((vs.Y - 140) / rh); int sc = Mathf.Max(0, G.MenuCur - vis + 1);
                    for (int i = sc; i < Ach.All.Length && i < sc + vis; i++)
                    {
                        var a = Ach.All[i]; bool got = Ach.Got.Contains(a.Id); float y = top + 20 + (i - sc) * rh;
                        var r = new Rect2(cx - w / 2, y - 18, w, rh - 2);
                        if (G.MenuCur == i) DrawRect(r, new Color(.2f, .38f, .55f, .4f));
                        Txt((got ? "★ " : "☆ ") + a.Name, new Vector2(r.Position.X + 10, y), 14, got ? Gold : new Color(.6f, .62f, .68f));
                        Txt(a.Desc, new Vector2(r.Position.X, y), 12, got ? Dim : new Color(.45f, .47f, .52f), HorizontalAlignment.Right, w - 10);
                    }
                    Txt("↑/↓ · Esc — назад", new Vector2(0, vs.Y - 14), 11, Dim, HorizontalAlignment.Center, vs.X);
                    break;
                }
        }
    }
}
