using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Холст дерева навыков: узлы в двумерном пространстве, линии под ними, перемещение (перетаскивание пустого места, средняя кнопка), масштаб колёсиком с целым пиксельным шагом
/// (узлы и иконки не размываются), «Центр» и «Всё дерево». Клик без перетаскивания выбирает узел; покупка — только кнопка «Улучшить» или Enter, поэтому случайное перетаскивание ничего не покупает.
/// Положение и масштаб запоминаются по категориям (Game.TechView).</summary>
public partial class SkillTreeCanvasUI : Control
{
    public Game G;
    public static readonly Vector2 Cell = new(54, 64);
    Control world; SkillConnectionUI conn; PackedScene nodeScene;
    readonly Dictionary<int, SkillNodeUI> nodes = new(); readonly List<int> order = new();
    int page = -1, zk = 2, hoverTech = -1; Vector2 off;
    bool pressL, pressM, dragged; Vector2 pressPos; string sig = "";
    bool needView;

    float Scale => Mathf.Max(1f, G?.UiScaleVp ?? 2f);
    int MaxK => (int)Mathf.Round(Scale * 2);
    public float Z => zk / Scale;
    public int ZoomStep => zk;

    public override void _Ready()
    {
        ClipContents = true; MouseFilter = MouseFilterEnum.Stop; FocusMode = FocusModeEnum.None; TextureFilter = TextureFilterEnum.Nearest;
        world = new Control { Name = "World", MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.Nearest }; AddChild(world);
        conn = new SkillConnectionUI { Name = "Links", MouseFilter = MouseFilterEnum.Ignore }; world.AddChild(conn);
        nodeScene = GD.Load<PackedScene>("res://Scenes/UI/SkillNode.tscn");
    }


    // ───────────────────────── для проверок ─────────────────────────
    public int NodeCount => nodes.Count;
    public int LinkCount => conn.Edges.Count;
    public Vector2 Offset => off;
    public bool HasNode(int tech) => nodes.ContainsKey(tech);
    public Rect2 NodeRect(int tech) => nodes.TryGetValue(tech, out var n) ? new Rect2(n.Position * Z + off, n.Size * Z) : default;
    public void ClickNode(int tech) { if (nodes.ContainsKey(tech)) Select(tech); }
    public void PanBy(Vector2 d) { off += d; Clamp(); Apply(); }
    public string LabelText(int tech) => nodes.TryGetValue(tech, out var n) ? n.GetNode<Label>("%Name").Text : "";

    // ───────────────────────── построение ─────────────────────────
    public void Refresh()
    {
        if (G == null || G.Upgrades == null) return;
        var cat = G.Upgrades.ByPage(G.TechPage); if (cat == null) return;
        if (cat.Page != page) { if (!needView) SaveView(); page = cat.Page; Rebuild(); needView = true; }
        bool showName = Z >= 1.45f;
        foreach (var i in order)
        {
            var n = nodes[i]; n.Bind(i, G.SkillLv[i], Game.SkillMax(i), G.StateOfTech(i), i == G.TechSel, UpgradeIcons.ForTech(i), Loc.T(Game.Techs[i].Name), showName);
        }
    }

    void Rebuild()
    {
        foreach (var n in nodes.Values) { world.RemoveChild(n); n.QueueFree(); } nodes.Clear(); order.Clear();
        var links = new List<(int, int, Rect2, Rect2)>();
        foreach (var i in Game.PageList(page))
        {
            var n = nodeScene.Instantiate<SkillNodeUI>(); world.AddChild(n); n.Grid = Game.SkillOf(i).Pos; n.Position = (n.Grid * Cell + new Vector2(2, 2)).Round(); n.Size = new Vector2(SkillNodeUI.W, SkillNodeUI.H);
            nodes[i] = n; order.Add(i);
        }
        foreach (var i in order) foreach (var (p, _) in Game.ParentReqs(i)) if (nodes.ContainsKey(p)) links.Add((p, i, nodes[p].Rect, nodes[i].Rect));
        conn.Rebuild(links, G); conn.Size = new Vector2(4000, 4000); conn.Position = Vector2.Zero;
        sig = "";
    }

    // ───────────────────────── вид ─────────────────────────
    void SaveView() { if (page >= 0 && !needView) G.TechView[page] = new Vector3(off.X, off.Y, zk); }

    void LoadView()
    {
        if (G.TechView.TryGetValue(page, out var v)) { zk = Mathf.Clamp((int)v.Z, 1, MaxK); off = new Vector2(v.X, v.Y); Clamp(); Apply(); return; }
        zk = Mathf.RoundToInt(Scale); var b = Bounds(); if (b.Size.X * Z > Size.X - 8 || b.Size.Y * Z > Size.Y - 8) { FitAll(); return; }
        off = Snap((Size - b.Size * Z) / 2f - b.Position * Z); Apply();
    }

    Rect2 Bounds() { Rect2 r = default; bool first = true; foreach (var n in nodes.Values) { var q = new Rect2(n.Position, n.Size); r = first ? q : r.Merge(q); first = false; } return r; }
    Vector2 Snap(Vector2 v) => new Vector2(Mathf.Round(v.X * Scale), Mathf.Round(v.Y * Scale)) / Scale;

    void Apply()
    {
        off = Snap(off); world.Scale = new Vector2(Z, Z); world.Position = off; SaveView();
    }

    void Clamp()
    {
        var b = Bounds(); if (b.Size == Vector2.Zero) return; float m = 40f;
        float x0 = b.Position.X * Z + off.X, x1 = b.End.X * Z + off.X, y0 = b.Position.Y * Z + off.Y, y1 = b.End.Y * Z + off.Y;
        if (x1 < m) off.X += m - x1; if (x0 > Size.X - m) off.X -= x0 - (Size.X - m);
        if (y1 < m) off.Y += m - y1; if (y0 > Size.Y - m) off.Y -= y0 - (Size.Y - m);
    }

    public void FitAll()
    {
        var b = Bounds(); if (b.Size == Vector2.Zero) return; int k = 1;
        for (int c = MaxK; c >= 1; c--) if (b.Size.X * c / Scale <= Size.X - 8 && b.Size.Y * c / Scale <= Size.Y - 8) { k = c; break; }
        zk = k; off = Snap((Size - b.Size * Z) / 2f - b.Position * Z); Apply();
    }

    public void CenterOnSelected()
    {
        if (!nodes.TryGetValue(G.TechSel, out var n)) { FitAll(); return; }
        off = Snap(Size / 2f - (n.Position + n.Size / 2f) * Z); Clamp(); Apply();
    }

    public void Zoom(int dir, Vector2? at = null)
    {
        int nk = Mathf.Clamp(zk + dir, 1, MaxK); if (nk == zk) return; var p = at ?? Size / 2f; var wp = (p - off) / Z; zk = nk; off = Snap(p - wp * Z); Clamp(); Apply();
    }

    int HitTest(Vector2 p) { foreach (var kv in nodes) if (NodeRect(kv.Key).HasPoint(p)) return kv.Key; return -1; }

    void Select(int tech) { if (G.TechSel != tech) { G.TechSel = tech; Sfx.Play("tick", -6, 1.4f); } }

    // ───────────────────────── ввод ─────────────────────────
    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed) { Zoom(1, mb.Position); AcceptEvent(); }
            else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed) { Zoom(-1, mb.Position); AcceptEvent(); }
            else if (mb.ButtonIndex == MouseButton.Left) { if (mb.Pressed) { pressL = true; dragged = false; pressPos = mb.Position; } else { if (pressL && !dragged) { int h = HitTest(mb.Position); if (h >= 0) Select(h); } pressL = false; } AcceptEvent(); }
            else if (mb.ButtonIndex == MouseButton.Middle) { pressM = mb.Pressed; dragged = true; AcceptEvent(); }
        }
        else if (e is InputEventMouseMotion mm)
        {
            if (pressM || pressL)
            {
                if (pressM || mm.Position.DistanceTo(pressPos) > 4f) { dragged = true; off += mm.Relative; Clamp(); Apply(); }
            }
            else
            {
                int h = HitTest(mm.Position);
                if (h != hoverTech) { if (hoverTech >= 0 && nodes.TryGetValue(hoverTech, out var o)) o.SetHover(false); hoverTech = h; if (h >= 0) nodes[h].SetHover(true); TooltipText = h >= 0 ? Loc.T(Game.Techs[h].Name) + $"  {G.SkillLv[h]}/{Game.SkillMax(h)}" : ""; }
            }
        }
    }

    public override void _Process(double delta)
    {
        if (G == null || !Visible) return;
        conn.Step(G, (float)delta);
        if (needView && Size.X > 20) { needView = false; LoadView(); }
        string s = page + "|" + zk + "|" + Size;
        if (s != sig) { sig = s; if (!needView) { Clamp(); Apply(); } }
    }

    /// <summary>Анимация открытия новых ступеней: узлы-потомки получают вспышку, когда родитель достиг требуемого уровня.</summary>
    public void FlashChildren(int parent)
    {
        foreach (var i in order) foreach (var (p, _) in Game.ParentReqs(i)) if (p == parent && nodes.TryGetValue(i, out var n)) n.Pulse(new Color(1.6f, 1.6f, 1.2f), .6f);
    }

    public void FlashNode(int tech) { if (nodes.TryGetValue(tech, out var n)) n.Pulse(new Color(2f, 1.8f, .8f), .5f); }
    public Vector2 NodeCentreLocal(int tech) => nodes.TryGetValue(tech, out var n) ? (n.Position + n.Size / 2f) * Z + off : Size / 2f;
}
