using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Эффекты оружия и способностей игрока: следы снарядов, дульные вспышки, силуэты рывка, волны света.
/// Логика боя лишь сообщает события (OnWeaponFired, OnDash, OnPulse); внешний вид и интенсивность (Settings.Particles/Fx) — здесь.</summary>
public partial class Game
{
    sealed class Ghost { public Vector2 P; public int Face; public float T; }
    readonly List<Ghost> ghosts = new(); float ghostT, projTrailT;

    /// <summary>Событие: игрок выстрелил (лук, посох, дуга).</summary>
    public void OnWeaponFired(int kind, Vector2 pos, Vector2 dir, Color col)
    {
        Impact(pos + dir * 4f, dir, col, kind == 1 ? .7f : .5f, kind == 1 ? 4 : 2);
        if (kind == 1) AddRing(pos + dir * 6f, 9f, .22f, new Color(col.R, col.G, col.B, .8f));          // зарядка и выпуск магического шара
    }

    /// <summary>Событие: игрок начал рывок.</summary>
    public void OnDash(Vector2 pos, Vector2 dir)
    {
        ghostT = 0; Burst(pos + new Vector2(0, 5), new Color("#c8e8ff"), Mathf.RoundToInt(6 * Settings.Particles), 45f);
        AddRing(pos + new Vector2(0, 4), 12f, .25f, new Color(.7f, .9f, 1f, .7f));
    }

    /// <summary>Событие: волна света (способность F).</summary>
    public void OnPulse(Vector2 pos)
    {
        AddRing(pos, 170f, .55f, new Color("#fff2b0")); AddRing(pos, 120f, .45f, new Color("#ffe27a")); AddRing(pos, 70f, .35f, Colors.White);
        for (int i = 0; i < Mathf.RoundToInt(40 * Settings.Particles); i++)
        {
            float a = rng.Randf() * Mathf.Tau, s = rng.RandfRange(60f, 170f);
            Parts.Add(new Particle { P = pos, V = Vector2.FromAngle(a) * s, Life = .5f, Max = .5f, C = new Color("#fff2b0"), Size = 1.5f });
        }
    }

    void UpdatePlayerFx(float dt)
    {
        if (DashT > 0) { ghostT -= dt; if (ghostT <= 0) { ghostT = .03f; ghosts.Add(new Ghost { P = PPos, Face = DashDir.X < 0 ? -1 : 1 }); if (ghosts.Count > 12) ghosts.RemoveAt(0); } }
        for (int i = ghosts.Count - 1; i >= 0; i--) { ghosts[i].T += dt; if (ghosts[i].T > .22f) ghosts.RemoveAt(i); }
        // след снарядов: пиксельные частицы позади стрелы и мерцание шара посоха
        projTrailT -= dt;
        if (projTrailT <= 0 && Settings.Particles > .05f)
        {
            projTrailT = .02f;
            foreach (var p in PProjs)
            {
                if (Parts.Count > VisualFxConfig.Get().MaxParticles) break;
                var d = p.Vel.Normalized();
                if (p.Kind == 0) Parts.Add(new Particle { P = p.Pos - d * 6f, V = Vector2.Zero, Life = .16f, Max = .16f, C = new Color(1f, .95f, .75f), Size = 1.2f });
                else if (p.Kind == 1) { var o = new Vector2(-d.Y, d.X) * Mathf.Sin(Time * 40f + p.Pos.X) * 3f; Parts.Add(new Particle { P = p.Pos - d * 3f + o, V = -d * 10f, Life = .3f, Max = .3f, C = p.C, Size = 1.3f }); }
            }
        }
    }

    /// <summary>Затухающие силуэты за игроком во время рывка.</summary>
    void DrawPlayerGhosts(EntityLayer c)
    {
        if (ghosts.Count == 0) return; var d = Def("player"); if (d.Sheet == null) return; float fy = d.Offset.Y + d.FrameSize;
        foreach (var g in ghosts)
        {
            float a = (1f - g.T / .22f) * .45f;
            c.DrawSetTransform(g.P + new Vector2(0, fy), 0, new Vector2(g.Face, 1));
            c.DrawTextureRectRegion(d.Sheet, new Rect2(d.Offset - new Vector2(0, fy), new Vector2(d.FrameSize, d.FrameSize)), new Rect2(0, 0, d.FrameSize, d.FrameSize), new Color(.6f, .85f, 1f, a));
        }
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}
