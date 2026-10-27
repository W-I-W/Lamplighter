using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Отрисовка подсказки над выбранным объектом мира. Рисует только готовую модель (TipModel) по стилю (TooltipStyle):
/// pixel-панель из темы, пиксельный шрифт, координаты округляются до целых пикселей.</summary>
public partial class Hud
{
    TooltipStyle tipStyle; readonly TooltipAnimator tip = new(); float tipLast;

    /// <summary>Одна визуальная строка: фрагменты, клавиша действия (если есть) и признак строки с вниманием.</summary>
    sealed class TipLine { public List<TipSpan> Spans = new(); public string Key; public bool Ok = true, Alert; public float W; }

    /// <summary>Раскладка строк: переносит по словам (после перевода), чтобы подсказка не превышала MaxWidth.</summary>
    List<TipLine> LayoutTip(TipModel m, TooltipStyle st)
    {
        var lines = new List<TipLine>(); float maxW = st.MaxWidth - st.PadX * 2;
        if (!m.HasAction && m.Title != "") { var t = new TipLine(); t.Spans.Add(new(Loc.T(m.Title), TipRole.Text)); t.W = TextW(m.Title); lines.Add(t); }
        foreach (var r in m.Rows)
        {
            bool action = r.Kind == TipKind.Action; float indent = action ? KeyCapW(r.Key) + 3 : 0;
            var l = new TipLine { Key = action ? r.Key : null, Ok = r.Ok, Alert = !r.Ok, W = indent }; lines.Add(l);
            foreach (var sp in r.Spans)
            {
                string txt = Loc.T(sp.Text); var words = txt.Split(' ');
                for (int k = 0; k < words.Length; k++)
                {
                    string wd = words[k] + (k < words.Length - 1 ? " " : ""); float ww = TextW(wd);
                    if (l.W + TextW(wd.TrimEnd()) > maxW && l.Spans.Count > 0 && wd.Trim() != "")
                    {
                        l = new TipLine { Ok = r.Ok, Alert = !r.Ok, W = indent }; lines.Add(l);
                    }
                    if (l.Spans.Count > 0 && l.Spans[^1].Role == sp.Role) l.Spans[^1] = new TipSpan(l.Spans[^1].Text + wd, sp.Role); else l.Spans.Add(new TipSpan(wd, sp.Role));
                    l.W += ww;
                }
            }
        }
        return lines;
    }

    float KeyCapW(string key) => Mathf.Max(11, TextW(key) + 6);

    void DrawTooltip(Vector2 vs)
    {
        tipStyle ??= GD.Load<TooltipStyle>(TooltipStyle.Path) ?? new TooltipStyle(); var st = tipStyle;
        float now = Time.GetTicksMsec() / 1000f, dt = Mathf.Clamp(now - tipLast, 0, .1f); tipLast = now;
        TipModel m = null; string key = null;
        if (G.Window == Win.None && G.Menu == Mn.None && !G.TechOpen && !G.MapOpen && G.Interact.Selected != null && G.selA > .02f)
        {
            m = TipModel.From(G.Cur); key = G.Cur.Key; if (m.Rows.Count == 0 && m.Title == "") m = null;
        }
        tip.Update(dt, key, m, G.selRect, st);
        if (tip.Shown == null || tip.Alpha <= .01f) return;

        var lines = LayoutTip(tip.Shown, st); if (lines.Count == 0) return;
        float w = st.MinWidth; foreach (var l in lines) w = Mathf.Max(w, l.W + st.PadX * 2);
        w = Mathf.Min(Mathf.Round(w), st.MaxWidth + 8); float h = 3 + lines.Count * st.LineHeight;
        bool ok = tip.Shown.PrimaryOk, hasAction = tip.Shown.HasAction;

        // позиция: над объектом, по центру; в пределах свободного места между панелями HUD; при нехватке места — под объектом
        var top = ToScreen(new Vector2(tip.Anchor.GetCenter().X, tip.Anchor.Position.Y));
        float x = top.X - w / 2, y = top.Y - st.Gap - h;
        if (y < st.TopMargin) y = ToScreen(new Vector2(0, tip.Anchor.End.Y)).Y + st.Gap;
        if (G.GameHudNode != null) { var (sl, sr) = G.GameHudNode.FreeSpan(y, y + h, vs.X, G.UiScaleVp); x = Mathf.Clamp(x, sl + 4, Mathf.Max(sl + 4, sr - 4 - w)); }
        x = Mathf.Clamp(x, 2, Mathf.Max(2, vs.X - w - 2));
        y = Mathf.Clamp(y, 2, Mathf.Max(2, vs.Y - h - 2));
        y += (1 - tip.Rise(st)) * st.SlidePx;
        var r = new Rect2(Mathf.Round(x), Mathf.Round(y), w, h);

        var old = curMod; curMod = new Color(1, 1, 1, tip.Alpha);
        Sty(r, hasAction && ok ? st.PanelAvailable : st.PanelLocked);
        if (hasAction)
        {
            var acc = ok ? st.OkColor : st.BadColor; float pa = ok ? .55f + .45f * tip.Pulse(st.KeyPulseSpeed) : .5f + .5f * tip.Pulse(st.AlertPulseSpeed);
            Rect(new Rect2(r.Position.X + 3, r.Position.Y + 4, 1, h - 8), new Color(acc.R, acc.G, acc.B, pa));
        }
        for (int i = 0; i < lines.Count; i++)
        {
            var l = lines[i]; float bx = r.Position.X + st.PadX, by = r.Position.Y + 10 + st.LineHeight * i;
            if (l.Key != null) { DrawKeyCap(l.Key, new Vector2(bx, by), l.Ok, st); bx += KeyCapW(l.Key) + 3; }
            foreach (var s in l.Spans)
            {
                var c = st.Color(s.Role);
                if (!l.Ok && l.Key != null) c = st.DimColor;                                              // недоступное действие приглушено
                else if (s.Role == TipRole.Bad || s.Role == TipRole.Ok && l.Alert) c.A = .72f + .28f * tip.Pulse(st.AlertPulseSpeed);
                Txt(s.Text, new Vector2(bx, by), 9, c); bx += TextW(s.Text);
            }
        }
        if (tip.Flash > 0) Rect(new Rect2(r.Position + Vector2.One, r.Size - new Vector2(2, 2)), new Color(1, 1, 1, .35f * tip.Flash / st.FlashTime));
        curMod = old;
    }

    /// <summary>Клавиша-«кнопка» [E]: золотая и мягко пульсирует, когда действие доступно; серая, когда нет.</summary>
    void DrawKeyCap(string key, Vector2 baseline, bool ok, TooltipStyle st)
    {
        float w = KeyCapW(key); var cap = new Rect2(baseline.X, baseline.Y - 8, w, 11);
        Sty(cap, st.KeycapStyle);
        var c = ok ? st.KeyColor.Lerp(Colors.White, .55f * tip.Pulse(st.KeyPulseSpeed)) : st.DimColor;
        Txt(key, new Vector2(Mathf.Round(cap.Position.X + (w - TextW(key)) / 2), baseline.Y), 9, c);
    }
}
