using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Отрисовка мира: сущности, игрок, здания, события, лучи, зоны и эффекты поверх тайлов.</summary>
public partial class Game
{
    void DrawHazards(EntityLayer c)
    {
        var pt = ToTile(PPos);
        if (RegionAt(PPos) == 2)
            for (int y = pt.Y - 9; y <= pt.Y + 9; y++)
                for (int x = pt.X - 14; x <= pt.X + 14; x++)
                {
                    if (!World.In(x, y) || W.Ground[World.Key(x, y)] != Art.HotAsh) continue;
                    bool act = HotActive(Time, x, y, out bool warn);
                    float a = act ? .5f + .15f * Mathf.Sin(Time * 14f + x) : warn ? .2f + .25f * (.5f + .5f * Mathf.Sin(Time * 18f)) : .07f + .03f * Mathf.Sin(Time * 2f + x * .7f);
                    Gfx.Rect(c, new Rect2(x * TS, y * TS, TS, TS), new Color(1f, act ? .45f : .35f, .1f, a));
                    if (act && rng.Randf() < .04f) Parts.Add(new Particle { P = new Vector2(x * TS + rng.RandfRange(2, 14), y * TS + 10), V = new Vector2(0, -rng.RandfRange(15, 35)), Life = .5f, Max = .5f, C = new Color("#ffb04a"), Size = 1.6f });
                }
        foreach (var z in Zones)
        {
            bool pending = z.T < z.Warn;
            if (pending)
            {
                float k = z.T / Mathf.Max(z.Warn, .01f);
                if (z.Smoke) { if ((int)(Time * 6) % 3 == 0) Gfx.Rect(c, new Rect2(z.Pos + new Vector2(rng.RandfRange(-z.R, z.R) * .6f, rng.RandfRange(-z.R, z.R) * .6f), new Vector2(1.5f, 1.5f)), new Color(1f, .5f, .2f, .5f)); }
                else
                {
                    Gfx.Disc(c, z.Pos, z.R, z.Dark ? new Color(.45f, .15f, .9f, .14f + .1f * Mathf.Sin(Time * 16f)) : z.Cold ? new Color(.5f, .85f, 1f, .12f + .1f * Mathf.Sin(Time * 16f)) : new Color(1f, .4f, .1f, .12f + .1f * Mathf.Sin(Time * 16f)));
                    Gfx.Ring(c, z.Pos, z.R, 28, z.Dark ? new Color(.75f, .45f, 1f, .85f) : z.Cold ? new Color(.7f, .95f, 1f, .8f) : new Color(1f, .6f, .2f, .8f), 1.5f);
                    Gfx.Ring(c, z.Pos, z.R * (1 - k), 24, new Color(1f, .9f, .5f, .6f), 1f);
                }
            }
            else if (z.Dps > 0)
            {
                float fade = Mathf.Min(1f, (z.Warn + z.Dur - z.T) * 2f);
                Gfx.Disc(c, z.Pos, z.R, z.Dark ? new Color(.3f, .08f, .6f, .5f * fade) : z.Cold ? new Color(.6f, .9f, 1f, .38f * fade) : z.Friendly ? new Color(1f, .8f, .3f, .3f * fade) : new Color(1f, .3f, .08f, .38f * fade));
                Gfx.Disc(c, z.Pos, z.R * .6f, new Color(1f, .75f, .2f, .3f * fade));
                Gfx.Ring(c, z.Pos, z.R, 24, new Color(1f, .6f, .2f, .7f * fade), 1.2f);
            }
        }
        // жерла: тлеющие огоньки и выбросы дыма
        foreach (var v in W.Vents)
        {
            var p = TileCenter(v.X, v.Y); if (p.DistanceTo(PPos) > 260) continue;
            if (rng.Randf() < .08f) Parts.Add(new Particle { P = p + new Vector2(rng.RandfRange(-3, 3), -4), V = new Vector2(rng.RandfRange(-6, 6), -rng.RandfRange(14, 30)), Life = 1.1f, Max = 1.1f, C = rng.Randf() < .3f ? new Color("#ff9a2e") : new Color(.35f, .33f, .32f), Size = 2f });
        }
    }

    /// <summary>Область ломания (слой между стволами и кронами): рамка наведённой клетки, подсветка и трещины ломаемого тайла.</summary>
    public void DrawBreakArea(EntityLayer c) => DrawBreakFx(c, true);

    /// <summary>Клетку закрывает крона другого дерева, если прямо под ней тоже стоит дерево: его крона лежит в этой клетке и должна лежать поверх подсветки.</summary>
    bool CoveredByCrown(Vector2I t) => World.In(t.X, t.Y + 1) && IsPlantTile(W.Get(t.X, t.Y + 1));

    /// <summary>Рамка и подсветка. Закрытая кроной другого дерева клетка рисуется в нижнем слое (под кроной), остальные — поверх всего, в том числе поверх копии ствола,
    /// когда игрок стоит за деревом: выделение никогда не оказывается «за» рубимым деревом.</summary>
    void DrawBreakFx(EntityLayer c, bool covered)
    {
        var at = ToTile(Aim);
        if (CoveredByCrown(at) == covered && PPos.DistanceTo(TileCenter(at.X, at.Y)) <= 40f && W.Get(at.X, at.Y) != Tl.None && !IsWall(W.Get(at.X, at.Y)))
            Gfx.Outline(c, new Rect2(at.X * TS, at.Y * TS, TS, TS), new Color(1, 1, 1, .25f), 1f);
        if (MineTile.X >= 0 && CoveredByCrown(MineTile) == covered) DrawMineFx(c);
    }

    /// <summary>Подсветка и трещины не закрытых кроной клеток и полоса прогресса ломания поверх деревьев.</summary>
    public void DrawBreakOverlay(EntityLayer c)
    {
        DrawBreakFx(c, false);
        if (MineTile.X >= 0 && MineProg > 0)
        {
            var o = new Vector2(MineTile.X * TS, MineTile.Y * TS + TS + 1);
            Gfx.Bar(c, o, TS, 2, MineProg / MineNeed, new Color("#ffe27a"));
        }
    }

    // ───────────────────────── отрисовка сущностей ─────────────────────────
    public void DrawEntities(EntityLayer c)
    {
        DrawWaterShimmer(c);
        if (SmeltQ.Count > 0)
        {
            var o = SmeltAt + new Vector2(-8, 9);
            Gfx.Bar(c, o, 16, 2, SmeltProg, new Color("#ff9a2e"));
        }
        DrawBuildings(c); DrawVillageDecor(c);
        DrawStatusMarkers(c);
        DrawSelection(c);
        DrawBuildGhost(c);
        DrawHazards(c);
        DrawHeartWorld(c);
        DrawBeams(c);
        DrawRings(c); DrawSparks(c); DrawPlayerGhosts(c);
        DrawWeaponFx(c);
        DrawBarriers(c);
        DrawEvents(c);

        var list = new List<(float y, Action a)>();
        DrawGiantTrees(list, c);
        foreach (var e in Enemies) { var ee = e; list.Add((e.Pos.Y, () => DrawEnemy(c, ee))); }
        foreach (var d in deaths) { var dd = d; list.Add((d.Pos.Y, () => DrawDeath(c, dd))); }
        foreach (var v in Villagers) { var vv = v; list.Add((v.Pos.Y, () => DrawVillager(c, vv))); }
        list.Add((PPos.Y, () => { DrawCosmetic(c); DrawPlayer(c); }));
        AddRemoteDraws(list, c);
        list.Sort((a, b) => a.y.CompareTo(b.y));
        foreach (var l in list) l.a();

        foreach (var p in Projs) { Gfx.Disc(c, p.Pos, 2.5f, new Color(p.C.R, p.C.G, p.C.B, .5f)); Gfx.Disc(c, p.Pos, 1.5f, p.C); }
        foreach (var p in Parts)
        {
            var col = p.C; col.A = Mathf.Clamp(p.Life / p.Max * 1.5f, 0, 1);
            Gfx.Rect(c, new Rect2(p.P - new Vector2(p.Size, p.Size) * .5f, new Vector2(p.Size, p.Size)), col);
        }
    }

    void DrawWaterShimmer(EntityLayer c)
    {
        var pt = ToTile(PPos);
        for (int y = pt.Y - 8; y <= pt.Y + 8; y++)
            for (int x = pt.X - 13; x <= pt.X + 13; x++)
            {
                if (!World.In(x, y) || W.Ground[World.Key(x, y)] != Art.WaterG) continue;
                float ph = (x * 7 + y * 13) % 10 * .6f;
                float a = .5f + .5f * Mathf.Sin(Time * 1.8f + ph);
                if (a < .55f) continue;
                Gfx.Rect(c, new Rect2(x * TS + 3 + (x * 5 + y * 3) % 7, y * TS + 4 + (x * 3 + y * 7) % 8, 4, 1), new Color(.75f, .92f, 1f, (a - .55f) * 1.2f));
            }
    }

    void Shadow(EntityLayer c, Vector2 p, float r)
    {
        c.DrawSetTransform(p + new Vector2(0, 6), 0, new Vector2(1, .4f));
        Gfx.Disc(c, Vector2.Zero, r, new Color(0, 0, 0, .35f));
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    void DrawPlayer(EntityLayer c)
    {
        bool water = WaterAt(PPos);
        Shadow(c, PPos, 5);
        if (Invuln > 0 && DashT <= 0 && ((int)(Time * 20) % 2 == 0)) return;
        int f = pVel.LengthSquared() > 100 ? (int)Walk % 2 : 0;
        float bob = pVel.LengthSquared() > 100 ? -Mathf.Abs(Mathf.Sin(Walk)) : 0;
        c.DrawSetTransform(PPos + new Vector2(0, bob + (water ? 2 : 0)), 0, new Vector2(Facing, 1));
        var mod = DashT > 0 ? new Color(.6f, 1f, 1f, .8f) : Colors.White;
        LookArt.Draw(c, PlayerLook, f, Def("player").Offset, mod, water ? 13 : 16, 1, HasHandLamp, pVel.LengthSquared() > 100);
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        if (water) Gfx.Rect(c, new Rect2(PPos.X - 7, PPos.Y + 1, 14, 2), new Color(.5f, .8f, 1f, .45f));
        if (Swing > 0)
        {
            var d = (Aim - PPos); d = d.LengthSquared() > 1 ? d.Normalized() : new Vector2(Facing, 0);
            bool mining = MineTile.X >= 0 && MineProg > 0 && AtkCd <= 0;
            float t = 1 - Swing / .22f, ang, sg = d.X < 0 ? -1 : 1;   // зеркалим замах при прицеле влево
            if (mining)
            {
                // удар с возвратом: замах назад (быстро замедляется), резкий удар вниз, короткая задержка на ударе и плавный возврат в исходное положение
                float ph = (Time * MineHz) % 1f; if (mineLastPh < .5f && ph >= .5f) MineStrikeFx(); mineLastPh = ph;
                float rest = .2f, back = -1.35f, hit = .72f, off;      // положительный угол — вниз, отрицательный — вверх: замах поднимает инструмент, удар опускает не ниже ног (≈ 41° под горизонталью)
                if (ph < .30f) { float u = ph / .30f; off = Mathf.Lerp(rest, back, 1f - (1f - u) * (1f - u)); }                     // замах: сначала быстро, к концу замедляется
                else if (ph < .50f) { float u = (ph - .30f) / .20f; off = Mathf.Lerp(back, hit, u * u * u); }                        // удар: разгон до самого конца
                else if (ph < .62f) { float u = (ph - .50f) / .12f; off = Mathf.Lerp(hit, hit - .1f, u); }                          // отдача на ударе
                else { float u = (ph - .62f) / .38f; off = Mathf.Lerp(hit - .1f, rest, u * u * (3f - 2f * u)); }                     // возврат
                ang = d.Angle() + sg * off;
            }
            else ang = d.Angle() + sg * (-1.0f + t * 2.0f);
            DrawHeldItem(c, PPos + new Vector2(0, -3), ang);
        }
        if (SpinT > 0)
        {
            float k = 1 - SpinT / .25f;
            Gfx.Ring(c, PPos, 10 + 20 * k, 32, new Color(1, .95f, .7f, 1 - k), 2f);
        }
    }

    void DrawStatusMarkers(EntityLayer c)
    {
        foreach (var b in Buildings)
        {
            if (!IsProducer(b.Type) || b.Status == 0 || b.Pos.DistanceTo(PPos) > 190) continue;
            var col = b.Status == 4 ? new Color(.7f, .72f, .8f) : b.Status == 2 ? new Color("#ffb04a") : new Color("#ff6a5a");
            var p = b.Pos + new Vector2(0, -15 - 1.5f * Mathf.Sin(Time * 5f));
            Gfx.Rect(c, new Rect2(p - new Vector2(3, 3), new Vector2(6, 6)), new Color(0, 0, 0, .7f)); Gfx.Rect(c, new Rect2(p - new Vector2(2, 2), new Vector2(4, 4)), col);
        }
    }

    float mineLastPh;
    void MineStrikeFx()
    {
        if (MineTile.X < 0) return;
        var p = TileCenter(MineTile.X, MineTile.Y); var col = new Color(1f, .9f, .6f);
        for (int i = 0; i < 4; i++)
            Parts.Add(new Particle { P = p + new Vector2(rng.RandfRange(-4, 4), rng.RandfRange(-4, 4)), V = Vector2.FromAngle(rng.Randf() * Mathf.Tau) * rng.RandfRange(18, 40), Life = .3f, Max = .3f, C = col, Size = 1.4f });
    }

    void DrawMineFx(EntityLayer c)
    {
        if (MineTile.X < 0 || MineNeed <= 0) return;
        var p = TileCenter(MineTile.X, MineTile.Y); float k = Mathf.Clamp(MineProg / MineNeed, 0, 1);
        float j = Swing > 0 ? Mathf.Sin(Time * 60f) * .8f : 0;
        Gfx.Rect(c, new Rect2(p - new Vector2(8 - j, 8), new Vector2(16, 16)), new Color(1, 1, 1, .12f * k + hitFlash * .35f));
    }

    // ───────────────────────── облики ─────────────────────────
    void DrawCosmetic(EntityLayer c)
    {
        if (CosmSel <= 0 || CosmSel >= CosmCol.Length) return;
        var col = CosmCol[CosmSel]; float p = .6f + .4f * Mathf.Sin(Time * 3f);
        Gfx.Ring(c, PPos + new Vector2(0, 2), 11, 24, new Color(col.R, col.G, col.B, .55f * p), 1.2f);
        if (rng.Randf() < .08f) Parts.Add(new Particle { P = PPos + Vector2.FromAngle(rng.Randf() * Mathf.Tau) * 10, V = new Vector2(0, -rng.RandfRange(8, 18)), Life = .7f, Max = .7f, C = col, Size = 1.4f });
    }

    void DrawEvents(EntityLayer c)
    {
        foreach (var ev in Events)
        {
            switch (ev.Kind)
            {
                case "lamp":
                    c.DrawTextureRectRegion(Art.TileTex, new Rect2(ev.Pos - new Vector2(8, 8), new Vector2(16, 16)), new Rect2(Art.Lantern * 16, 0, 16, 16));
                    Gfx.Rect(c, new Rect2(ev.Pos + new Vector2(-8, 9), new Vector2(16, 2)), new Color(0, 0, 0, .7f)); Gfx.Rect(c, new Rect2(ev.Pos + new Vector2(-8, 9), new Vector2(16 * Mathf.Clamp(ev.V / 60f, 0, 1), 2)), new Color("#ffe27a")); break;
                case "trader":
                    if (Mathf.Abs(PPos.X - ev.Pos.X) > 10) ev.Face = PPos.X < ev.Pos.X ? -1 : 1;
                    Shadow(c, ev.Pos, 5); c.DrawSetTransform(ev.Pos, 0, new Vector2(ev.Face, 1));
                    DrawFrame(c, Def("trader"), (int)(Time * 2) % 2, Colors.White); c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
                    Gfx.Disc(c, ev.Pos + new Vector2(0, -16), 2.2f, new Color("#ffd86b")); break;
                case "traveler":
                    if (Mathf.Abs(PPos.X - ev.Npc.X) > 10) ev.Face = PPos.X < ev.Npc.X ? -1 : 1;
                    Shadow(c, ev.Npc, 5); c.DrawSetTransform(ev.Npc, 0, new Vector2(ev.Face, 1));
                    DrawFrame(c, Def("traveler"), (int)(Time * (ev.State == 1 ? 6 : 2)) % 2, Colors.White); c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
                    if (ev.State == 0) Gfx.Disc(c, ev.Npc + new Vector2(0, -16), 2.2f, new Color("#8ff0ff")); break;
                case "meteor":
                    if (ev.State == 0) { Gfx.Ring(c, ev.Pos, 10 + 8 * (1 - ev.V / 3.2f), 16, new Color(1f, .9f, .5f, .7f), 1.5f); Gfx.Line(c, ev.Pos + new Vector2(-ev.V * 30, -ev.V * 90), ev.Pos, new Color(1f, .95f, .7f, .7f), 3f); }
                    else Gfx.Ring(c, ev.Pos, 12 + 2 * Mathf.Sin(Time * 4), 18, new Color(1f, .9f, .5f, .5f), 1f);
                    break;
                case "awaken":
                    Gfx.Ring(c, ev.Pos, 52 + 2 * Mathf.Sin(Time * 3), 28, new Color(.6f, .38f, .85f, .35f + .15f * Mathf.Sin(Time * 5)), 1f); break;
                case "rift":
                    for (int i = 0; i < 3; i++) Gfx.Arc(c, ev.Pos, 8 + i * 5 + 2 * Mathf.Sin(Time * 3 + i), Time * (i % 2 == 0 ? 1 : -1.3f), Time * (i % 2 == 0 ? 1 : -1.3f) + 4.2f, 16, new Color(.75f, .45f, 1f, .9f - i * .2f), 2f);
                    Gfx.Disc(c, ev.Pos, 5, new Color(.15f, .05f, .3f, .9f)); break;
            }
        }
    }

    void DrawBeams(EntityLayer c)
    {
        foreach (var (t, d) in W.Emitters)
        {
            var p = TileCenter(t.X, t.Y); var dv = new Vector2(d.X, d.Y); var perp = new Vector2(-dv.Y, dv.X);
            Gfx.Triangle(c, p + dv * 8, dv, 6f, new Color(1f, .9f, .4f, .9f));
        }
        foreach (var bp in Beams)
        {
            float w = bp.C == Colors.White ? 3f : 2f;
            float pulse = .75f + .25f * Mathf.Sin(Time * 18f);
            for (int i = 0; i + 1 < bp.Pts.Count; i++)
            {
                Gfx.Line(c, bp.Pts[i], bp.Pts[i + 1], new Color(bp.C.R, bp.C.G, bp.C.B, .22f), w * 3.4f);
                Gfx.Line(c, bp.Pts[i], bp.Pts[i + 1], new Color(bp.C.R, bp.C.G, bp.C.B, .9f * pulse), w);
                Gfx.Line(c, bp.Pts[i], bp.Pts[i + 1], new Color(1, 1, 1, .8f), 1f);
            }
        }
    }

    void DrawBarriers(EntityLayer c)
    {
        foreach (var s in Barriers)
        {
            float k = .6f + .4f * Mathf.Sin(Time * 6f);
            Gfx.Line(c, s.a, s.b, new Color(.55f, .9f, 1f, .25f * k), 7f);
            Gfx.Line(c, s.a, s.b, new Color(.7f, .95f, 1f, .8f * k), 1.5f);
            if (rng.Randf() < .1f) Parts.Add(new Particle { P = s.a.Lerp(s.b, rng.Randf()), V = new Vector2(0, -rng.RandfRange(6, 14)), Life = .5f, Max = .5f, C = new Color("#bff9ff"), Size = 1.4f });
        }
    }

    void DrawRings(EntityLayer c)
    {
        foreach (var r in rings)
        {
            float k = r.T / r.Dur, e = 1 - Mathf.Pow(1 - k, 3);
            Gfx.Ring(c, r.P, r.R * e, 48, new Color(r.C.R, r.C.G, r.C.B, (1 - k) * .8f), 2f + 2f * (1 - k));
            Gfx.Ring(c, r.P, r.R * e * .8f, 40, new Color(1, 1, 1, (1 - k) * .35f), 1f);
        }
    }

    void DrawVillager(EntityLayer c, Villager v)
    {
        Shadow(c, v.Pos, 4);
        var pd = ProfDefs.Of(v.Prof); var def = Def(pd != null && pd.Sprite != "" ? pd.Sprite : "villager");
        int f = v.State == VState.Working && def.Frames >= 4 ? 2 + (int)(v.Anim / .35f) % 2 : v.State == VState.Walking ? (int)v.Walk % 2 : v.Working ? (int)v.Walk % 2 : 0;
        float bob = v.State == VState.Walking ? -Mathf.Abs(Mathf.Sin(v.Walk)) * 1f : 0;
        c.DrawSetTransform(v.Pos + new Vector2(0, bob), 0, new Vector2(v.Face, 1));
        DrawFrame(c, def, f, Colors.White);
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    /// <summary>Портрет для окна разговора: первый кадр листа профессии.</summary>
    readonly Dictionary<string, AtlasTexture> portraits = new();
    public Texture2D VillagerPortrait(Villager v)
    {
        var pd = ProfDefs.Of(v.Prof); string id = pd != null && pd.Sprite != "" ? pd.Sprite : "villager";
        if (portraits.TryGetValue(id, out var t)) return t; var d = Def(id); if (d.Sheet == null) return null;
        return portraits[id] = new AtlasTexture { Atlas = d.Sheet, Region = new Rect2(0, 0, d.FrameSize, d.FrameSize) };
    }

    // ───────────────────────── отрисовка ─────────────────────────
    void DrawBuildings(EntityLayer c)
    {
        foreach (var b in Buildings)
        {
            bool linked = b.Linked || b.Forced;
            if (b.Type == Tl.Warehouse)
            {
                float f = Mathf.Clamp(StockTotal / (float)(StockCap * 3), 0, 1);
                var o = b.Pos + new Vector2(-7, 9);
                Gfx.Rect(c, new Rect2(o, new Vector2(14, 2)), new Color(0, 0, 0, .6f));
                Gfx.Rect(c, new Rect2(o, new Vector2(14 * f, 2)), new Color("#d9b27c"));
                if (b.Level >= 5) Gfx.Disc(c, b.Pos + new Vector2(0, -11), 1.6f, new Color("#ffe27a"));
                continue;
            }
            if (b.Type == Tl.Hub || b.Type == Tl.Stall || b.Type == Tl.House)
            {
                if (b.Type == Tl.Hub) Gfx.Ring(c, b.Pos, NodeRange(b), 48, new Color(.55f, .9f, 1f, .08f + .04f * Mathf.Sin(Time * 2f)), 1f);
                if (!linked && b.Type == Tl.Hub) Gfx.Disc(c, b.Pos + new Vector2(0, -11), 2.2f, new Color("#ff5050"));
                continue;
            }
            int bk = BIndex(b.Type); float p = Mathf.Clamp(b.Prog / BInterval[bk][b.Level - 1], 0, 1);
            var o2 = b.Pos + new Vector2(-7, 9);
            Gfx.Rect(c, new Rect2(o2, new Vector2(14, 2)), new Color(0, 0, 0, .6f));
            Gfx.Rect(c, new Rect2(o2, new Vector2(14 * p, 2)), b.Type == Tl.Forge || b.Type == Tl.Collector || b.Type == Tl.Refinery ? new Color("#ff9a2e") : b.Type == Tl.Sawmill ? new Color("#7ad45a") : new Color("#8ff0ff"));
            if (b.Status == 1) { Gfx.Disc(c, b.Pos + new Vector2(0, -11), 2.2f, new Color("#ff5050")); Gfx.Rect(c, new Rect2(b.Pos + new Vector2(-.5f, -13.5f), new Vector2(1, 2.5f)), Colors.White); }
            else if (b.Status == 2) Gfx.Disc(c, b.Pos + new Vector2(0, -11), 2.2f, new Color("#ffd86b"));
            else if (b.Status == 3) { Gfx.Disc(c, b.Pos + new Vector2(0, -11), 2.2f, new Color("#ff9a3a")); Gfx.Line(c, b.Pos + new Vector2(-3, -11), b.Pos + new Vector2(3, -11), new Color(0, 0, 0, .8f), 1f); }
            else if (b.Status == 4) Gfx.Rect(c, new Rect2(b.Pos + new Vector2(-2.5f, -13), new Vector2(5, 4)), new Color("#8a909c"));
            if (b.Level >= 5) Gfx.Disc(c, b.Pos + new Vector2(6, -9), 1.6f, new Color("#ffe27a"));
        }
    }

    void DrawWeaponFx(EntityLayer c)
    {
        foreach (var p in PProjs)
        {
            var d = p.Vel.Normalized();
            if (p.Kind == 0) { Gfx.Line(c, p.Pos - d * 5, p.Pos + d * 3, new Color("#d9b27c"), 1.5f); Gfx.Line(c, p.Pos, p.Pos + d * 3, p.C, 2f); }
            else if (p.Kind == 1) { Gfx.Disc(c, p.Pos, 4.5f, new Color(p.C.R, p.C.G, p.C.B, .35f)); Gfx.Disc(c, p.Pos, 2.8f, p.C); Gfx.Disc(c, p.Pos, 1.3f, Colors.White); }
            else { var pp = new Vector2(-d.Y, d.X); Gfx.Line(c, p.Pos - pp * 7, p.Pos + d * 3, new Color(p.C.R, p.C.G, p.C.B, p.Life * 3f), 2f); Gfx.Line(c, p.Pos + pp * 7, p.Pos + d * 3, new Color(p.C.R, p.C.G, p.C.B, p.Life * 3f), 2f); }
        }
        foreach (var b in Bolts)
        {
            var prev = b.A; int segs = 5;
            for (int i = 1; i <= segs; i++)
            {
                var pt = b.A.Lerp(b.B, i / (float)segs) + (i == segs ? Vector2.Zero : new Vector2(rng.RandfRange(-4, 4), rng.RandfRange(-4, 4)));
                Gfx.Line(c, prev, pt, new Color(b.C.R, b.C.G, b.C.B, b.T * 5f), 3f); Gfx.Line(c, prev, pt, new Color(1, 1, 1, b.T * 5f), 1f); prev = pt;
            }
        }
        if (swArcT > 0)
        {
            float k = swArcT / .2f; var col = ElemColor(); col.A = k;
            // дуга удара «проносится» слева направо: яркая голова и затухающий хвост из нескольких отрезков
            float sweep = Mathf.Clamp((1f - k) * 1.6f, 0f, 1f), a0 = swArcDir - swArcHalf, span = swArcHalf * 2f, reach = swArcReach * (1.05f - .1f * k);
            for (int i = 0; i < 6; i++)
            {
                float t0 = Mathf.Max(0, sweep - (i + 1) * .09f), t1 = Mathf.Max(0, sweep - i * .09f); if (t1 <= t0) continue;
                Gfx.Arc(c, PPos, reach, a0 + span * t0, a0 + span * t1, 4, new Color(col.R, col.G, col.B, k * (1f - i * .16f)), i == 0 ? 3f : 2f);
            }
            Gfx.Arc(c, PPos, reach * .86f, a0, a0 + span * sweep, 14, new Color(1, 1, 1, k * .45f), 1f);
        }
    }
}
