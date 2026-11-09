using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Содержимое подсказки предмета: заголовок, тип, строки характеристик и описание.</summary>
public sealed class ItemTipData
{
    public string Name = "", Kind = "", Desc = ""; public Color Accent = new("#b8bcc8"); public int Count;
    public readonly List<(string label, string value, Color color)> Stats = new();
    public void Stat(string label, string value, Color? color = null) => Stats.Add((label, value, color ?? new Color("#f2e6c0")));
    public string Signature => $"{Name}|{Kind}|{Desc}|{Count}|{string.Join(";", Stats)}";
}

/// <summary>Красивая подсказка предмета: цветная рамка по редкости, заголовок, тип, таблица характеристик, описание.
/// Создаётся кодом, чтобы окна инвентаря и сундука использовали одну и ту же панель.</summary>
public partial class ItemTipPanel : PanelContainer
{
    readonly Label nameL = new(), kindL = new(), descL = new(); readonly VBoxContainer stats = new(); readonly ColorRect sepStats = new(), sepDesc = new();
    readonly StyleBoxFlat box = new(); string sig = "";

    public static ItemTipPanel Create(Node parent)
    {
        var p = new ItemTipPanel { Theme = GD.Load<Theme>(Hud.ThemePath), Visible = false, MouseFilter = MouseFilterEnum.Ignore, ZIndex = 4095 };
        parent.AddChild(p); return p;
    }

    public override void _Ready()
    {
        box.BgColor = new Color(.08f, .065f, .1f, .96f);
        box.SetBorderWidthAll(1); box.SetContentMarginAll(6); box.ContentMarginTop = 4; box.ContentMarginBottom = 5;
        box.ShadowColor = new Color(0, 0, 0, .45f); box.ShadowSize = 3; box.ShadowOffset = new Vector2(1, 2);
        AddThemeStyleboxOverride("panel", box);
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 2); v.MouseFilter = MouseFilterEnum.Ignore; AddChild(v);
        nameL.AutowrapMode = TextServer.AutowrapMode.WordSmart; nameL.CustomMinimumSize = new Vector2(110, 0);
        kindL.AddThemeColorOverride("font_color", new Color("#9a93a8"));
        descL.AddThemeColorOverride("font_color", new Color("#c9c0d6"));
        descL.AutowrapMode = TextServer.AutowrapMode.WordSmart; descL.CustomMinimumSize = new Vector2(140, 0);
        stats.AddThemeConstantOverride("separation", 1); stats.MouseFilter = MouseFilterEnum.Ignore;
        foreach (var s in new[] { sepStats, sepDesc }) { s.CustomMinimumSize = new Vector2(0, 1); s.MouseFilter = MouseFilterEnum.Ignore; }
        foreach (Control c in new Control[] { nameL, kindL, sepStats, stats, sepDesc, descL }) { c.MouseFilter = MouseFilterEnum.Ignore; v.AddChild(c); }
    }

    public void Set(ItemTipData d)
    {
        string s = d.Signature; if (s == sig) return; sig = s;
        var nameText = Loc.T(d.Name) + (d.Count > 1 ? $"  ×{d.Count}" : "");
        nameL.Text = nameText; nameL.AddThemeColorOverride("font_color", d.Accent);
        kindL.Text = Loc.T(d.Kind); kindL.Visible = d.Kind != "";
        box.BorderColor = d.Accent.Darkened(.25f);
        var line = new Color(d.Accent.R, d.Accent.G, d.Accent.B, .35f); sepStats.Color = sepDesc.Color = line;
        foreach (var c in stats.GetChildren()) { stats.RemoveChild(c); c.QueueFree(); }
        foreach (var (label, value, color) in d.Stats)
        {
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            var l = new Label { Text = Loc.T(label), SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
            l.AddThemeColorOverride("font_color", new Color("#9a93a8"));
            var val = new Label { Text = Loc.T(value), HorizontalAlignment = HorizontalAlignment.Right, MouseFilter = MouseFilterEnum.Ignore };
            val.AddThemeColorOverride("font_color", color);
            row.AddThemeConstantOverride("separation", 8); row.AddChild(l); row.AddChild(val); stats.AddChild(row);
        }
        sepStats.Visible = stats.GetChildCount() > 0; stats.Visible = sepStats.Visible;
        descL.Text = d.Desc; descL.Visible = d.Desc != ""; sepDesc.Visible = descL.Visible;
    }

    /// <summary>Показывает подсказку у курсора, не выходя за пределы родителя.</summary>
    public void ShowAt(Control host, Vector2 mouse)
    {
        ResetSize(); Size = GetCombinedMinimumSize();
        var local = host.GetGlobalTransformWithCanvas().AffineInverse() * mouse;
        var at = local + new Vector2(14, 16);
        if (at.X + Size.X > host.Size.X) at.X = local.X - Size.X - 10;                       // у правого края — слева от курсора
        at.X = Mathf.Clamp(at.X, 0, Mathf.Max(0, host.Size.X - Size.X)); at.Y = Mathf.Clamp(at.Y, 0, Mathf.Max(0, host.Size.Y - Size.Y));
        Position = at.Round(); Visible = true;
    }
}

/// <summary>Общая подсказка для любых контролов интерфейса: Bind(control, поставщик данных) — и при наведении показывается красивая панель.
/// Один узел на интерфейс; поставщик вызывается каждый кадр, пока курсор над контролом, поэтому подсказка всегда актуальна.</summary>
public partial class ItemTipHost : Control
{
    public static ItemTipHost I;
    public Game G;
    ItemTipPanel panel; Control cur; readonly Dictionary<Control, Func<ItemTipData>> map = new();

    public static ItemTipHost Create(Game g, Control root)
    {
        var h = new ItemTipHost { G = g, MouseFilter = MouseFilterEnum.Ignore, Name = "ItemTips" };
        h.SetAnchorsPreset(LayoutPreset.FullRect); root.AddChild(h); I = h; return h;
    }

    public override void _Ready() { panel = ItemTipPanel.Create(this); }
    public override void _ExitTree() { if (I == this) I = null; }

    /// <summary>Привязывает подсказку к контролу; повторный вызов только обновляет поставщика (карточки в списках переиспользуются).</summary>
    public static void Bind(Control c, Func<ItemTipData> f)
    {
        if (I == null || c == null) return;
        bool first = !I.map.ContainsKey(c); I.map[c] = f;
        if (!first) return;
        if (c.MouseFilter == MouseFilterEnum.Ignore) c.MouseFilter = MouseFilterEnum.Pass;
        c.TooltipText = "";
        c.MouseEntered += () => { if (I != null) I.cur = c; };
        c.MouseExited += () => { if (I != null && I.cur == c) I.cur = null; };
        c.TreeExited += () => { if (I != null) { I.map.Remove(c); if (I.cur == c) I.cur = null; } };
    }

    public override void _Process(double delta)
    {
        var mouse = GetViewport().GetMousePosition();
        bool ok = cur != null && IsInstanceValid(cur) && cur.IsVisibleInTree() && G != null && !G.InventoryUi.CarryActive && G.Menu == Mn.None
            && new Rect2(Vector2.Zero, cur.Size).HasPoint(cur.GetGlobalTransformWithCanvas().AffineInverse() * mouse);
        var d = ok && map.TryGetValue(cur, out var f) ? f?.Invoke() : null;
        if (d == null) { panel.Visible = false; return; }
        panel.Set(d); panel.ShowAt(this, mouse);
    }
}
