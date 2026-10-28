using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public sealed class OwnedWeapon { public string Id; public List<int> Mods = new(); }
public sealed class PProj
{
    public Vector2 Pos, Vel; public float Dmg, Life, Blast; public int Pierce, Kind; public Color C; public HashSet<Enemy> Hit = new();
}
public sealed class BoltFx { public Vector2 A, B; public float T = .18f; public Color C = new("#d0b0ff"); }

/// <summary>Оружие и универсальная система эффектов: любой источник (оружие, вставка, реликвия, синергия) даёт силу эффектов Fxk,
/// а удар любого класса проходит один и тот же конвейер OnHit.</summary>
public partial class Game
{
    public List<OwnedWeapon> Owned = new();
    public int CurW = -1;
    public int[] ModInv = new int[7];
    public List<PProj> PProjs = new();
    public List<BoltFx> Bolts = new();
    public float[] EffP = new float[(int)Fxk.Count];
    public Dictionary<string, float> Num = new();
    public HashSet<string> Flags = new(), ActiveSyn = new();
    public Dictionary<string, int> TagCount = new();
    float swArcT, swArcDir, swArcHalf, swArcReach; bool inBlast;

    public WeaponDef CurDef { get { if (CurW < 0 || CurW >= Owned.Count) return null; return Db.Weapon(Owned[CurW].Id); } }
    public OwnedWeapon CurOw => CurDef == null ? null : Owned[CurW];
    public float E(Fxk k) => EffP[(int)k];
    public float N(string k) => Num.TryGetValue(k, out var v) ? v : 0f;
    public float WeaponDmg(WeaponDef w) => (w.Dmg + .35f * (Level - 1)) * (1f + N("Dmg"));
    public bool HasWeapon(string id) { foreach (var o in Owned) if (o.Id == id) return true; return false; }

    public void GiveWeapon(string id, bool announce = true)
    {
        if (HasWeapon(id)) return;
        var d = Db.Weapon(id); if (d == null) return;
        Owned.Add(new OwnedWeapon { Id = id });
        if (CurW < 0) CurW = Owned.Count - 1;
        AutoAssign(RefWeapon(id)); HotFollow();
        RecomputeEffects();
        if (announce) Announce($"Оружие: {d.Name}", $"{Db.ClassNames[(int)d.Cls]} · {Db.RarName[(int)d.Rar]} · {d.Desc}", Db.RarCol[(int)d.Rar], "chest");
    }

    public void EquipWeapon(int i)
    {
        if (i < -1 || i >= Owned.Count) return;
        CurW = i; MineProg = 0; RecomputeEffects(); HotFollow(); Sfx.Play("tick", 0, 1.4f);
    }

    public void CycleWeapon() => HotCycle(1);

    bool NearForge() => FurnaceNear(56, out _) || (BuildingNear(PPos, 56) is Building b && b.Type == Tl.Forge);
    public bool BpOk(string bp) => bp == null || BpDone.Contains(bp);

    public bool CraftWeapon(WeaponDef w)
    {
        if (w.Cost == null) return false;
        if (HasWeapon(w.Id)) { Toast("Это оружие уже есть"); return false; }
        if (!BpOk(w.Bp)) { Fail("Нужен чертёж: " + Db.Bp(w.Bp).Name + " (журнал [J])"); return false; }
        if (!NearForge()) { Fail("Оружие создаётся рядом с печью или кузницей"); return false; }
        if (!Can(w.Cost)) { Fail("Не хватает: " + NeedStr(w.Cost)); return false; }
        Pay(w.Cost); GiveWeapon(w.Id); GiveXp(25); Sfx.Play("build"); Save(); return true;
    }

    public bool CraftMod(int id)
    {
        var m = Db.Mods[id];
        if (!BpOk(m.Bp)) { Fail("Нужен чертёж: " + Db.Bp(m.Bp).Name + " (журнал [J])"); return false; }
        if (!NearForge()) { Fail("Модификаторы создаются рядом с печью или кузницей"); return false; }
        if (!Can(m.Cost)) { Fail("Не хватает: " + NeedStr(m.Cost)); return false; }
        Pay(m.Cost); ModInv[id]++; GiveXp(10); Sfx.Play("upgrade"); Toast($"Создан модификатор: {m.Name}"); return true;
    }

    public bool InstallMod(int id)
    {
        var ow = CurOw; var d = CurDef;
        if (ow == null) { Fail("Сначала возьмите оружие в руки"); return false; }
        if (ModInv[id] <= 0) { Fail("Нет такого модификатора"); return false; }
        if (ow.Mods.Count >= d.Slots) { Fail("Все слоты заняты (уберите вставку)"); return false; }
        ModInv[id]--; ow.Mods.Add(id); RecomputeEffects(); Sfx.Play("upgrade", -2, 1.3f);
        Toast($"{d.Name}: вставка «{Db.Mods[id].Name}»"); return true;
    }

    public void RemoveMod(int slot)
    {
        var ow = CurOw; if (ow == null || slot < 0 || slot >= ow.Mods.Count) return;
        ModInv[ow.Mods[slot]]++; ow.Mods.RemoveAt(slot); RecomputeEffects(); Sfx.Play("tick", 0, .8f);
    }

    // ───────────────────────── атаки ─────────────────────────
    public Color ElemColor()
    {
        if (E(Fxk.Burn) > 0) return new Color("#ff8a2a");
        if (E(Fxk.Freeze) > 0 || E(Fxk.Slow) > 0) return new Color("#8fd8ff");
        if (E(Fxk.Chain) > 0) return new Color("#d0b0ff");
        if (E(Fxk.Arc) > 0 || E(Fxk.Siphon) > 0) return new Color("#ffe27a");
        return new Color("#ffffff");
    }

    void Act(float dt)
    {
        var w = CurDef;
        if (w == null) { ActPick(dt, true); return; }
        var toAim = Aim - PPos; var dir = toAim.LengthSquared() > 1 ? toAim.Normalized() : new Vector2(Facing, 0);
        var tile = ToTile(Aim); var t = W.Get(tile.X, tile.Y);
        bool mineable = PPos.DistanceTo(TileCenter(tile.X, tile.Y)) <= 40f && Mineable(t);
        if (w.Cls == WClass.Sword)
        {
            if (SwordAttack(w, dir)) { MineProg = 0; MineTile = new Vector2I(-1, -1); return; }
            ActPick(dt, false); return;
        }
        bool enemyClose = false;
        foreach (var e in Enemies) if (!e.Hidden && e.Pos.DistanceTo(PPos) < 22) { enemyClose = true; break; }
        if (mineable && !enemyClose) { ActPick(dt, false); return; }
        MineProg = 0; MineTile = new Vector2I(-1, -1);
        FireRanged(w, dir);
    }

    public void HitEnemy(Enemy e, float dmg, Vector2 dir, bool melee, int depth = 0, bool light = false)
    {
        if (e.Hidden) return;
        Hurt(e, dmg, dir, melee, light);
        OnHit(e, dmg, dir, depth);
    }

    /// <summary>Единый конвейер эффектов попадания: не зависит от класса оружия.</summary>
    public void OnHit(Enemy e, float dmg, Vector2 dir, int depth)
    {
        float b = E(Fxk.Burn);
        if (b > 0 && rng.Randf() < .45f + .2f * b) e.Burn = Mathf.Max(e.Burn, 2f + b);
        float s = E(Fxk.Slow);
        if (s > 0) e.Slow = Mathf.Max(e.Slow, 1.6f + s);
        float f = E(Fxk.Freeze);
        if (f > 0 && !e.IsBoss && rng.Randf() < .18f * f + (s > 0 ? .05f : 0)) { e.Freeze = 1.1f; Burst(e.Pos, new Color("#bfe9ff"), 6, 40); }
        float c = E(Fxk.Chain);
        if (c > 0 && depth < 1)
        {
            int left = 1 + (int)c; var from = e;
            for (int i = 0; i < left; i++)
            {
                Enemy best = null; float bd = 80f;
                foreach (var o in Enemies) if (o != from && !o.Hidden && !o.IsBoss && o.Hp > 0 && !chainHit.Contains(o)) { float d = o.Pos.DistanceTo(from.Pos); if (d < bd) { bd = d; best = o; } }
                if (best == null) break;
                chainHit.Add(best); Bolts.Add(new BoltFx { A = from.Pos, B = best.Pos });
                HitEnemy(best, dmg * .45f, (best.Pos - from.Pos).Normalized(), false, depth + 1); from = best;
            }
            chainHit.Clear(); Sfx.Play("tick", -4, 2f);
        }
        float sp = E(Fxk.Siphon);
        if (sp > 0)
        {
            BeamE = Mathf.Min(100, BeamE + 4f * sp); FlareCd = Mathf.Max(0, FlareCd - .3f * sp); PulseCd = Mathf.Max(0, PulseCd - .3f * sp); DashCd = Mathf.Max(0, DashCd - .15f * sp);
        }
    }
    readonly HashSet<Enemy> chainHit = new();

    void BlastAt(Vector2 pos, float dmg)
    {
        if (inBlast) return; inBlast = true;
        float rad = 34f + 10f * E(Fxk.Blast);
        foreach (var o in new List<Enemy>(Enemies)) if (!o.Hidden && o.Pos.DistanceTo(pos) < rad + o.Radius) { Hurt(o, dmg * (.4f + .15f * E(Fxk.Blast)), (o.Pos - pos).Normalized(), false); if (E(Fxk.Burn) > 0) o.Burn = 2.5f; }
        var col = E(Fxk.Burn) > 0 || TagCount.GetValueOrDefault("fire") > 0 ? new Color("#ff8a2a") : new Color("#bfe9ff");
        Burst(pos, col, 22, 120); Burst(pos, new Color("#ffe27a"), 8, 70); Sfx.Play("boom", -6, 1.3f); Shake = Mathf.Max(Shake, 1.5f);
        for (int i = 0; i < 18; i++) { float a = i * Mathf.Tau / 18; Parts.Add(new Particle { P = pos, V = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 110, Life = .3f, Max = .3f, C = col, Size = 2 }); }
        inBlast = false;
    }

    bool SwordAttack(WeaponDef w, Vector2 dir)
    {
        float reach = w.Reach, half = w.Tier == 1 ? .9f : w.Tier == 2 ? 1.2f : 1.6f;
        var hits = new List<Enemy>();
        foreach (var e in Enemies)
        {
            if (e.Hidden) continue;
            var d = e.Pos - PPos; float dist = d.Length() - e.Radius;
            if (dist > reach) continue;
            if (dist > 10 && Mathf.Abs(Mathf.Wrap(d.Angle() - dir.Angle(), -Mathf.Pi, Mathf.Pi)) > half) continue;
            hits.Add(e);
        }
        if (hits.Count == 0) return false;
        if (AtkCd > 0) return true;
        AtkCd = w.Cd; swArcT = .2f; swArcDir = dir.Angle(); swArcHalf = half; swArcReach = reach;
        float dmg = WeaponDmg(w);
        foreach (var e in hits) HitEnemy(e, dmg, (e.Pos - PPos).Normalized(), true);
        Sfx.Play("hit");
        if (E(Fxk.Arc) > 0) { for (int i = 0; i < (int)Mathf.Ceil(E(Fxk.Arc)); i++) FireProj(2, dir, dmg * .55f, 150f, 1 + (int)E(Fxk.Pierce), 70f); }
        if (E(Fxk.Multi) > 0) for (int i = 0; i < (int)E(Fxk.Multi); i++) FireProj(2, dir.Rotated((i % 2 == 0 ? 1 : -1) * (.35f + .2f * (i / 2))), dmg * .4f, 150f, 1 + (int)E(Fxk.Pierce), 60f);
        return true;
    }

    void FireProj(int kind, Vector2 dir, float dmg, float speed, int pierce, float life, float blast = 0)
    {
        var col = kind == 2 ? (E(Fxk.Burn) > 0 ? new Color("#ff8a2a") : new Color("#fff2b0")) : ElemColor();
        PProjs.Add(new PProj { Pos = PPos + dir * 8, Vel = dir * speed, Dmg = dmg, Life = kind == 2 ? .35f : life, Pierce = pierce, Kind = kind, C = col, Blast = blast });
        OnWeaponFired(kind, PPos + dir * 8, dir, col);
    }

    void FireRanged(WeaponDef w, Vector2 dir)
    {
        if (AtkCd > 0) return;
        AtkCd = w.Cd; Swing = .1f;
        int n = 1 + (int)E(Fxk.Multi); float spread = .2f;
        for (int i = 0; i < n; i++)
        {
            var d = dir.Rotated((i - (n - 1) / 2f) * spread);
            float dmg = WeaponDmg(w) * (n > 1 ? .75f : 1f);
            if (w.Cls == WClass.Bow) FireProj(0, d, dmg, w.Speed, w.Pierce + (int)E(Fxk.Pierce), .75f);
            else FireProj(1, d, dmg, w.Speed, (int)E(Fxk.Pierce), .9f, 26f + 4f * E(Fxk.Blast));
        }
        if (E(Fxk.Arc) > 0) FireProj(2, dir, WeaponDmg(w) * .5f, 150f, 3, 70f);
        Sfx.Play(w.Cls == WClass.Bow ? "shoot" : "flare", -4, w.Cls == WClass.Bow ? 1.4f : 1.1f);
    }

    void UpdateWeaponFx(float dt)
    {
        swArcT = Mathf.Max(0, swArcT - dt);
        for (int i = Bolts.Count - 1; i >= 0; i--) { Bolts[i].T -= dt; if (Bolts[i].T <= 0) Bolts.RemoveAt(i); }
        for (int i = PProjs.Count - 1; i >= 0; i--)
        {
            var p = PProjs[i]; p.Life -= dt; p.Pos += p.Vel * dt;
            var t = ToTile(p.Pos); var tl = W.Get(t.X, t.Y);
            bool wall = World.Blocks(tl) && tl != Tl.Deep && p.Kind != 2;
            if (wall || p.Life <= 0)
            {
                if (p.Kind == 1) OrbBurst(p, null);
                PProjs.RemoveAt(i); continue;
            }
            var dir = p.Vel.Normalized(); bool dead = false;
            foreach (var e in new List<Enemy>(Enemies))
            {
                if (e.Hidden || p.Hit.Contains(e) || e.Pos.DistanceTo(p.Pos) > e.Radius + 3) continue;
                p.Hit.Add(e);
                if (p.Kind == 2 && e.Shield && (e.Kind == EK.WinterWarden || e.Kind == EK.Source)) { ReflectLight(e); p.Pierce = -1; dead = true; break; }
                HitEnemy(e, p.Dmg, dir, false, 0, p.Kind == 2);
                if (p.Kind == 1) OrbBurst(p, e);
                if (p.Pierce-- <= 0) { dead = true; break; }
            }
            if (dead) PProjs.RemoveAt(i);
            else if (rng.Randf() < dt * 30) Parts.Add(new Particle { P = p.Pos, V = Vector2.Zero, Life = .25f, Max = .25f, C = p.C, Size = 1.2f });
        }
    }

    void OrbBurst(PProj p, Enemy skip)
    {
        float r = p.Blast > 0 ? p.Blast : 22f;
        foreach (var o in new List<Enemy>(Enemies)) if (o != skip && !o.Hidden && o.Pos.DistanceTo(p.Pos) < r + o.Radius) HitEnemy(o, p.Dmg * .6f, (o.Pos - p.Pos).Normalized(), false, 1);
        Burst(p.Pos, p.C, 10, 70); Sfx.Play("tick", -3, .7f);
    }

}
