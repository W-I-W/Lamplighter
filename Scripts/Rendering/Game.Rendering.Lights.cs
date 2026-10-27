using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Свет и Тьма: подготовка данных для шейдеров (источники света, дым, силуэты). Какие территории открыты — решает игровая логика, шейдер лишь показывает.</summary>
public partial class Game
{
    Vector4[] SmokeUniform()
    {
        var a = new Vector4[16]; int n = 0; var cc = CamCenter;
        foreach (var z in Zones) if (z.Smoke && z.T < z.Warn + .8f && n < 16) a[n++] = new Vector4(z.Pos.X, z.Pos.Y, z.R * 1.6f, .95f);
        foreach (var c in Clouds) if (n < 16 && c.Pos.DistanceTo(cc) < 700) a[n++] = new Vector4(c.Pos.X, c.Pos.Y, c.R, .7f);
        return a;
    }

    public IEnumerable<Vector4> ZoneLights()
    {
        int n = 0;
        foreach (var z in Zones)
            if (z.T >= z.Warn && z.T < z.Warn + z.Dur && z.Dps > 0 && !z.Cold && !z.Dark) { yield return new Vector4(z.Pos.X, z.Pos.Y, z.R * 2.2f, 0); if (++n >= 12) yield break; }
    }

    void UpdateShaders(float dt)
    {
        var vs = ViewSize;
        darkRect.Size = vs; glowRect.Size = vs; darkRect.Position = glowRect.Position = Vector2.Zero;
        var arr = new Vector4[LightLimit]; int n = 0;
        float fl = 1f + .05f * Mathf.Sin(Time * 11f) + .03f * Mathf.Sin(Time * 23f + 1f);
        arr[n++] = new Vector4(PPos.X, PPos.Y, PlayerLightR * fl, 0);
        foreach (var a in W.Ancients) if (a.Lit && n < LightLimit) arr[n++] = new Vector4(a.Pos.X, a.Pos.Y, AncientR(a), Mathf.Max(a.Anim < 1 ? 1 - a.Anim : 0, a.Pulse));
        foreach (var l in Lamps) if (n < LightLimit) arr[n++] = new Vector4(l.Pos.X, l.Pos.Y, LampRad(l) * (1f + .02f * Mathf.Sin(Time * 9f + l.Pos.X)), 0);
        foreach (var b in Beacons) if (n < LightLimit) arr[n++] = new Vector4(b.Pos.X, b.Pos.Y, BeaconR(b), b.Ring * (1 - b.T / b.Dur));
        foreach (var p in BeamLights) if (n < LightLimit - 26) arr[n++] = new Vector4(p.X, p.Y, 34, 0);
        foreach (var z in ZoneLights()) if (n < LightLimit - 12) arr[n++] = z;
        foreach (var z in EventLights()) if (n < LightLimit - 6) arr[n++] = z;
        foreach (var z in HeartLights()) if (n < LightLimit - 6) arr[n++] = z;
        if (FinStage == 1 && n < LightLimit) arr[n++] = new Vector4(W.Ancients[0].Pos.X, W.Ancients[0].Pos.Y, Mathf.Max(1f, FinT * 900f), FinT < 10 ? 1f : 0f);
        // силуэты: слабая подсветка интересных объектов сквозь тьму
        foreach (var m in Marks()) if (n < LightLimit) arr[n++] = m;
        var target = RegionAt(PPos) switch { 1 => new Vector3(.0f, .03f, .075f), 2 => new Vector3(.075f, .03f, .016f), 3 => new Vector3(.03f, .05f, .09f), 4 => new Vector3(.05f, .012f, .07f), _ => new Vector3(.012f, .016f, .045f) };
        tint = tint.Lerp(target, Mathf.Min(1, dt * 1.5f));
        dawn = Mathf.Lerp(dawn, DawnTarget, Mathf.Min(1, dt * (FinStage == 1 ? 6f : 1.2f)));
        var sm = SmokeUniform();
        foreach (var m in new[] { darkMat, glowMat })
        {
            m.SetShaderParameter("cam", CamCenter);
            m.SetShaderParameter("view", vs);
            m.SetShaderParameter("zoom", Zoom);
            m.SetShaderParameter("time", Time);
            m.SetShaderParameter("count", n);
            m.SetShaderParameter("lights", arr);
            m.SetShaderParameter("flash", Flash);
            m.SetShaderParameter("tint", tint);
            m.SetShaderParameter("dawn", dawn);
            m.SetShaderParameter("smokes", sm);
        }
    }

    public IEnumerable<Vector4> EventLights()
    {
        foreach (var ev in Events)
        {
            float k = .85f + .15f * Mathf.Sin(Time * 5f);
            if (ev.Kind == "lamp") yield return new Vector4(ev.Pos.X, ev.Pos.Y, 78 * k * Mathf.Clamp(ev.V / 30f, .4f, 1f), 0);
            else if (ev.Kind == "trader") yield return new Vector4(ev.Pos.X, ev.Pos.Y, 56, 0);
            else if (ev.Kind == "meteor" && ev.State == 1) yield return new Vector4(ev.Pos.X, ev.Pos.Y, 46 * k, 0);
            else if (ev.Kind == "rift") yield return new Vector4(ev.Pos.X, ev.Pos.Y, 50 * k, 0);
            else if (ev.Kind == "traveler") yield return new Vector4(ev.Npc.X, ev.Npc.Y, 40, 0);
            else if (ev.Kind == "caravan" || ev.Kind == "elite") yield return new Vector4(ev.Pos.X, ev.Pos.Y, 30, -.45f * k);
        }
    }

    /// <summary>Источники света Сердца для шейдера: ложные огни мерцают, настоящий — горит ровно.</summary>
    IEnumerable<Vector4> HeartLights()
    {
        if (RegionAt(PPos) != 4) yield break;
        int n = 0; var cc = CamCenter;
        foreach (var rd in W.Riddles)
            for (int i = 0; i < rd.Lanterns.Count; i++)
            {
                var t = rd.Lanterns[i]; if (W.Get(t.X, t.Y) != Tl.FalseLantern) continue; var p = TileCenter(t.X, t.Y); if (p.DistanceTo(cc) > 520) continue;
                float f = rd.Poi.State >= 3 ? 1f : rd.Real == i ? (N("Reveal") > 0 ? 1.35f : 1f) : 1f + .45f * Mathf.Sin(Time * (13f + i * 3.1f) + i) * Mathf.Sin(Time * 5f + i * 2f);
                yield return new Vector4(p.X, p.Y, 58 * Mathf.Max(.3f, f), 0); if (++n > 10) yield break;
            }
        foreach (var t in W.FalseLights)
        {
            if (W.Get(t.X, t.Y) != Tl.FalseLantern) continue; var p = TileCenter(t.X, t.Y); if (p.DistanceTo(cc) > 520) continue;
            float f = 1f + .5f * Mathf.Sin(Time * 15f + t.X) * Mathf.Sin(Time * 6f + t.Y);
            yield return new Vector4(p.X, p.Y, 50 * Mathf.Max(.3f, f), 0); if (++n > 14) yield break;
        }
        foreach (var f in ArenaFalse) { var p = TileCenter(f.tile.X, f.tile.Y); float fl = 1f + .5f * Mathf.Sin(Time * 15f + f.tile.X); yield return new Vector4(p.X, p.Y, 46 * Mathf.Max(.3f, fl), 0); }
        foreach (var p0 in W.ArenaPylons) if (PylonT.GetValueOrDefault(World.Key(p0.X, p0.Y)) > 0) { var p = TileCenter(p0.X, p0.Y); yield return new Vector4(p.X, p.Y, 70, 0); }
    }

    // ───────────────────────── силуэты в темноте ─────────────────────────
    public IEnumerable<Vector4> Marks()
    {
        int n = 0;
        float pulse = .85f + .15f * Mathf.Sin(Time * 2.5f);
        foreach (var a in W.Ancients)
        {
            if (a.Lit) continue;
            float d = a.Pos.DistanceTo(PPos); if (d > (a.Boss ? 420 : 340)) continue;
            yield return new Vector4(a.Pos.X, a.Pos.Y, a.Boss ? 70 : 50, -(a.Boss ? .55f : .45f) * pulse); if (++n >= 12) yield break;
        }
        foreach (var p in W.Pois)
        {
            if (p.State >= 3 && p.Kind != PK.Chest) continue;
            if (!(p.Kind == PK.TechCache || p.Kind == PK.Spire || p.Kind == PK.Altar || p.Kind == PK.Workshop || p.Kind == PK.Puzzle || p.Kind == PK.Obelisk || p.Kind == PK.Shrine || p.Kind == PK.Camp || p.Kind == PK.Ruins || p.Kind == PK.House)) continue;
            if (p.Pos.DistanceTo(PPos) > 300) continue;
            yield return new Vector4(p.Pos.X, p.Pos.Y, 34, -.4f * pulse); if (++n >= 12) yield break;
        }
        foreach (var e in Enemies) if (e.IsBoss && !e.Hidden && e.Pos.DistanceTo(PPos) < 360) { yield return new Vector4(e.Pos.X, e.Pos.Y, 46, -.55f * pulse); if (++n >= 12) yield break; }
        foreach (var v in W.Vents)
        {
            var p = TileCenter(v.X, v.Y); if (p.DistanceTo(PPos) > 260) continue;
            yield return new Vector4(p.X, p.Y, 26, -.4f * pulse); if (++n >= 12) yield break;
        }
        foreach (var (t, _) in W.Emitters)
        {
            var p = TileCenter(t.X, t.Y); if (p.DistanceTo(PPos) > 300) continue;
            yield return new Vector4(p.X, p.Y, 28, -.5f * pulse); if (++n >= 12) yield break;
        }
        foreach (var s in new[] { W.Seal1, W.Seal2 })
            if (s.Count > 0 && W.Get(s[0].X, s[0].Y) == Tl.Seal)
            {
                var p = TileCenter(s[0].X, s[0].Y) + new Vector2(8, 56); if (p.DistanceTo(PPos) > 400) continue;
                yield return new Vector4(p.X, p.Y, 80, -.55f * pulse); if (++n >= 12) yield break;
            }
    }
}
