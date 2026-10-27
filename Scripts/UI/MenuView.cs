using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Полноэкранный слой (Scenes/UI/Menu.tscn): главное меню и пауза, титры, финальные сцены.</summary>
public partial class MenuView : Control
{
    public Game G; public bool Started;
    ColorRect dim; TextureRect flame; VBoxContainer box; Label title, sub, foot, version; PanelContainer panel; VBoxContainer items; Control lines; Control centerWrap;
    readonly List<WindowRow> rows = new(); readonly List<Label> labels = new(); readonly List<RichTextLabel> riches = new(); int nRich; PackedScene rowScene; int nRow, nLabel;

    public override void _Ready()
    {
        dim = GetNode<ColorRect>("%Dim"); flame = GetNode<TextureRect>("%Flame"); box = GetNode<VBoxContainer>("%Box"); title = GetNode<Label>("%Title"); sub = GetNode<Label>("%Sub");
        panel = GetNode<PanelContainer>("%Panel"); items = GetNode<VBoxContainer>("%Items"); lines = GetNode<Control>("%Lines"); foot = GetNode<Label>("%Foot"); version = GetNode<Label>("%Version");
        centerWrap = panel.GetParent<Control>(); rowScene = GD.Load<PackedScene>("res://Scenes/UI/Row.tscn");
    }

    /// <summary>Начало кадра. withMenu: показывать заголовок, пламя и панель с пунктами (главное меню / пауза).</summary>
    public void Begin(bool withMenu, float dimAlpha, string footer, bool showVersion)
    {
        nRow = nLabel = nRich = 0; Visible = true; Started = true; dim.Color = new Color(.02f, .03f, .06f, dimAlpha);
        flame.Visible = box.Visible = withMenu;
        foot.Text = footer == null ? "" : Loc.T(footer); version.Visible = showVersion; if (showVersion) version.Text = $"v{Game.VersionStr} · {Game.BuildStr}";
    }

    public void Titles(string t, string s, Color gold, Color blue)
    {
        title.Text = Loc.T(t); title.Modulate = gold; sub.Text = s; sub.Modulate = blue;
        float fl = .8f + .2f * Mathf.Sin(G.UiTime * 9f); flame.Modulate = new Color(1f, .85f, .4f, fl);
    }

    public void Item(int idx, bool sel, string text, bool disabled)
    {
        WindowRow r; if (nRow < rows.Count) r = rows[nRow]; else { r = rowScene.Instantiate<WindowRow>(); rows.Add(r); items.AddChild(r); }
        if (r.GetIndex() != nRow) items.MoveChild(r, nRow); nRow++; r.Visible = true;
        r.Set(idx, true, sel, disabled, Loc.T(text), null, disabled ? new Color("#5a6278") : sel ? new Color("#ffe08a") : new Color("#d8e0f0"));
    }

    /// <summary>Строка текста на заданном месте экрана (титры, финальные сцены). wrap — перенос по ширине.</summary>
    public void Line(string s, Vector2 pos, float width, bool big, Color c, HorizontalAlignment al = HorizontalAlignment.Center, bool wrap = false)
    {
        Label l; if (nLabel < labels.Count) l = labels[nLabel]; else { l = new Label { MouseFilter = MouseFilterEnum.Ignore }; labels.Add(l); lines.AddChild(l); }
        nLabel++; l.Visible = true; string v = big ? "BigLabel" : "PlainLabel"; if (l.ThemeTypeVariation != v) l.ThemeTypeVariation = v;
        s = Loc.T(s); if (l.Text != s) l.Text = s; l.HorizontalAlignment = al; l.Modulate = c;
        l.AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;
        l.Position = new Vector2(Mathf.Round(pos.X), Mathf.Round(pos.Y)); l.Size = new Vector2(width, big ? 22 : wrap ? 100 : 11);
    }

    /// <summary>Абзац с раскраской ключевых слов и «печатной машинкой» (visible — сколько символов показано, -1 — все).</summary>
    public void RichLine(string plain, Vector2 pos, float width, Color baseCol, int visible)
    {
        RichTextLabel l; if (nRich < riches.Count) l = riches[nRich]; else { l = new RichTextLabel { MouseFilter = MouseFilterEnum.Ignore, FitContent = true }; riches.Add(l); lines.AddChild(l); }
        nRich++; l.Visible = true; TextFx.Apply(l, plain, baseCol, 0); l.VisibleCharacters = visible;
        l.Position = new Vector2(Mathf.Round(pos.X), Mathf.Round(pos.Y)); l.Size = new Vector2(width, 100);
    }

    public void End()
    {
        for (int i = nRow; i < rows.Count; i++) rows[i].Visible = false;
        for (int i = nLabel; i < labels.Count; i++) labels[i].Visible = false;
        for (int i = nRich; i < riches.Count; i++) riches[i].Visible = false;
    }

    bool Inside(Control c, Vector2 p) => c.Visible && new Rect2(Vector2.Zero, c.Size).HasPoint(c.GetGlobalTransform().AffineInverse() * p);

    /// <summary>Пункт под точкой (логические координаты) или -1.</summary>
    public int ItemHit(Vector2 logical)
    {
        if (!Visible) return -1; var p = logical * G.UiScaleVp;
        for (int i = 0; i < nRow; i++) if (Inside(rows[i], p)) return rows[i].Index;
        return -1;
    }

    public bool IsOver(Vector2 logical) => Visible && box.Visible && Inside(panel, logical * G.UiScaleVp);
}
