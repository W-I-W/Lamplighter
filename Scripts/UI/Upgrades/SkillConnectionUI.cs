using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Линии зависимостей дерева: ломаные под узлами. Золотая — требование выполнено, голубая — родитель развивается, серая — закрыто.
/// При выполнении требования золото «протекает» по линии от родителя к потомку.</summary>
public partial class SkillConnectionUI : Control
{
    public sealed class Edge { public int From, To; public Vector2[] Pts; public float Len; public int State; public float Lit; }
    public readonly List<Edge> Edges = new();
    static readonly Color Gold = new("#ffd23f"), Cyan = new("#4cb8d8"), Grey = new("#38415a");

    public void Rebuild(IEnumerable<(int from, int to, Rect2 a, Rect2 b)> links, Game g)
    {
        var old = new Dictionary<(int, int), float>(); foreach (var e in Edges) old[(e.From, e.To)] = e.Lit; Edges.Clear();
        foreach (var (f, t, a, b) in links)
        {
            var pts = new List<Vector2>(); var pc = a.Position + a.Size / 2; var cc = b.Position + b.Size / 2;
            if (Mathf.Abs(pc.Y - cc.Y) < 1f) { float y = pc.Y; pts.Add(new Vector2(cc.X > pc.X ? a.End.X : a.Position.X, y)); pts.Add(new Vector2(cc.X > pc.X ? b.Position.X : b.End.X, y)); }
            else
            {
                float y0 = pc.Y < cc.Y ? a.End.Y : a.Position.Y, y1 = pc.Y < cc.Y ? b.Position.Y : b.End.Y, mid = Mathf.Round((y0 + y1) / 2f);
                pts.Add(new Vector2(pc.X, y0)); pts.Add(new Vector2(pc.X, mid)); if (Mathf.Abs(pc.X - cc.X) > .5f) pts.Add(new Vector2(cc.X, mid)); pts.Add(new Vector2(cc.X, y1));
            }
            float len = 0; for (int i = 1; i < pts.Count; i++) len += pts[i - 1].DistanceTo(pts[i]);
            int st = g.LinkState(f, t); Edges.Add(new Edge { From = f, To = t, Pts = pts.ToArray(), Len = len, State = st, Lit = old.TryGetValue((f, t), out var l) ? l : st == 2 ? 1f : 0f });
        }
        QueueRedraw();
    }

    /// <summary>Обновляет состояния связей и плавно зажигает золото; true, если что-то изменилось.</summary>
    public bool Step(Game g, float dt)
    {
        bool ch = false;
        foreach (var e in Edges)
        {
            int st = g.LinkState(e.From, e.To); if (st != e.State) { e.State = st; ch = true; }
            float target = st == 2 ? 1f : 0f; if (!Mathf.IsEqualApprox(e.Lit, target)) { e.Lit = Mathf.MoveToward(e.Lit, target, dt * 3.2f); ch = true; }
        }
        if (ch) QueueRedraw(); return ch;
    }

    public override void _Draw()
    {
        foreach (var e in Edges)
        {
            var col = e.State == 1 ? Cyan : Grey;
            for (int i = 1; i < e.Pts.Length; i++) DrawLine(e.Pts[i - 1], e.Pts[i], col, 2f);
            if (e.Lit > 0f)
            {
                float left = e.Len * e.Lit;
                for (int i = 1; i < e.Pts.Length && left > 0; i++)
                {
                    float seg = e.Pts[i - 1].DistanceTo(e.Pts[i]); var to = seg <= left ? e.Pts[i] : e.Pts[i - 1].Lerp(e.Pts[i], left / seg);
                    DrawLine(e.Pts[i - 1], to.Round(), Gold, 2f); left -= seg;
                }
            }
        }
    }
}
