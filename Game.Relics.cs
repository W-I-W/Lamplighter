using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Lamplighter;

/// <summary>Реликвии, теги и синергии: всё считается из Resource-данных (RelicDef/SynergyDef).</summary>
public partial class Game
{
    public List<string> OwnedRelics = new(), EquippedRelics = new(), TempRelics = new();
    public int BossCount { get { int n = 0, m = BossMask; while (m != 0) { n += m & 1; m >>= 1; } return n; } }
    public int RelicSlots => 3 + Math.Min(3, BossCount);
    public float Surge; float fx1, surgeT = 20, freezeT = 6, trailT2;
    public float Luck => N("Luck");
    public bool SurgeOn => Surge > 0;

    void AddEffect(string eff)
    {
        var p = eff.Split(':'); if (p.Length < 2) return;
        if (p[0] == "Flag") { Flags.Add(p[1]); return; }
        if (!float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return;
        if (Enum.TryParse<Fxk>(p[0], out var k)) EffP[(int)k] += v; else Num[p[0]] = N(p[0]) + v;
    }

    bool SynReq(string cond)
    {
        if (cond.StartsWith("tag:"))
        {
            var s = cond.Substring(4); int ge = s.IndexOf(">="); string tag = ge >= 0 ? s.Substring(0, ge) : s; int need = ge >= 0 ? int.Parse(s.Substring(ge + 2)) : 1;
            return TagCount.GetValueOrDefault(tag) >= need;
        }
        if (cond.StartsWith("class:")) { var w = CurDef; return w != null && Db.ClassNames[(int)w.Cls].Length > 0 && ClassKey(w.Cls) == cond.Substring(6); }
        if (cond.StartsWith("ability:"))
            return cond.Substring(8) switch
            {
                "pulse" => Has(Tech.Pulse), "dash" => Has(Tech.Dash), "spin" => Has(Tech.Spin), "flare" => Has(Tech.Flare), "beam" => Has(Tech.Beam),
                "lamp" => Lamps.Count > 0 || Has(Tech.HeroLamp) || HeroLampLv > 0, _ => false
            };
        return false;
    }
    static string ClassKey(WClass c) => c == WClass.Sword ? "sword" : c == WClass.Bow ? "bow" : c == WClass.Staff ? "staff" : "pick";

    public void RecomputeEffects()
    {
        Array.Clear(EffP); Num.Clear(); Flags.Clear(); ActiveSyn.Clear(); TagCount.Clear();
        void Tag(string t) { if (!string.IsNullOrEmpty(t)) TagCount[t] = TagCount.GetValueOrDefault(t) + 1; }
        var w = CurDef;
        if (w != null)
        {
            foreach (var (k, p) in w.Inherent) EffP[(int)k] += p;
            foreach (var t in w.Tags) Tag(t);
            foreach (var m in CurOw.Mods) { var md = Db.Mods[m]; foreach (var (k, p) in md.Eff) EffP[(int)k] += p; Tag(md.Tag); }
        }
        foreach (var id in EquippedRelics) ApplyRelic(id, Tag);
        foreach (var id in TempRelics) ApplyRelic(id, Tag);
        foreach (var s in Db.Synergies)
        {
            bool ok = true; foreach (var r in s.Requires) if (!SynReq(r)) { ok = false; break; }
            if (!ok) continue;
            ActiveSyn.Add(s.Id); foreach (var g in s.Grants) AddEffect(g);
        }
    }

    void ApplyRelic(string id, Action<string> tag)
    {
        var d = Db.Relic(id); if (d == null) return;
        foreach (var t in d.Tags) tag(t);
        foreach (var e in d.Effects) AddEffect(e);
    }

    public void GiveRelic(string id, bool quiet = false)
    {
        var d = Db.Relic(id); if (d == null) return;
        if (OwnedRelics.Contains(id))
        {
            int coins = 20 * (d.Rarity + 1); Inv[It.Coins] += coins; Toast($"Дубликат «{d.Title}» обменян на {coins} монет"); return;
        }
        OwnedRelics.Add(id);
        if (d.Permanent && EquippedRelics.Count < RelicSlots) EquippedRelics.Add(id);
        RecomputeEffects();
        Found.Add($"{Db.RarName[d.Rarity]}: реликвия «{d.Title}»");
        if (!quiet) Announce($"Реликвия: {d.Title}", $"{d.Desc} · [C] экипировка", Db.RarCol[d.Rarity], "chest");
    }

    public bool ToggleRelic(string id)
    {
        if (EquippedRelics.Contains(id)) { EquippedRelics.Remove(id); RecomputeEffects(); Sfx.Play("tick", 0, .8f); return true; }
        if (EquippedRelics.Count >= RelicSlots) { Fail($"Все слоты реликвий заняты ({RelicSlots}) — снимите другую"); return false; }
        var d = Db.Relic(id); if (d == null || !d.Permanent) return false;
        EquippedRelics.Add(id); RecomputeEffects(); Sfx.Play("upgrade", -3, 1.4f);
        foreach (var s in ActiveSyn) { }
        return true;
    }

    public void GrantTemp(string id) { if (!TempRelics.Contains(id)) TempRelics.Add(id); RecomputeEffects(); }
    public void ClearTemp() { if (TempRelics.Count == 0) return; TempRelics.Clear(); RecomputeEffects(); }

    // ───────────────────────── триггеры синергий ─────────────────────────
    void FreezeNearby(float r, float dur)
    {
        foreach (var e in Enemies) if (!e.Hidden && e.Pos.DistanceTo(PPos) < r + e.Radius) { if (e.IsBoss) e.Slow = Mathf.Max(e.Slow, dur); else e.Freeze = Mathf.Max(e.Freeze, dur); }
        for (int i = 0; i < 24; i++) { float a = i * Mathf.Tau / 24; Parts.Add(new Particle { P = PPos, V = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r * 2.2f), Life = .45f, Max = .45f, C = new Color("#bfe9ff"), Size = 2 }); }
        Sfx.Play("flare", -4, 1.6f);
    }

    void RelicOnDash()
    {
        if (!Flags.Contains("FrostDash")) return;
        FreezeNearby(55, 1.0f);
        foreach (var e in Enemies) if (!e.Hidden && e.Pos.DistanceTo(PPos) < 60) Hurt(e, 4f, (e.Pos - PPos).Normalized(), false);
    }

    void RelicOnPulse()
    {
        if (!Flags.Contains("FirePulse")) return;
        for (int i = 0; i < 8; i++) AddZone(PPos + Vector2.FromAngle(i * Mathf.Tau / 8) * 55f, 22, 0, 4f, 14f, false, true);
        Burst(PPos, new Color("#ff8a2a"), 40, 170); Sfx.Play("boom", -2, .9f);
    }

    void UpdateRelics(float dt)
    {
        fx1 -= dt; if (fx1 <= 0) { fx1 = 1f; RecomputeEffects(); }
        if (Flags.Contains("FireTrail") && pVel.LengthSquared() > 100) { trailT2 -= dt; if (trailT2 <= 0) { trailT2 = .22f; AddZone(PPos, 9, 0, 1.8f, 9f, false, true); } }
        if (Flags.Contains("FreezePulse")) { freezeT -= dt; if (freezeT <= 0) { freezeT = 7f; FreezeNearby(80, 1.3f); } }
        if (Flags.Contains("RadiantSurge"))
        {
            if (Surge > 0)
            {
                Surge -= dt; BeamE = 100; DashCd = Mathf.Max(0, DashCd - dt); FlareCd = Mathf.Max(0, FlareCd - dt); PulseCd = Mathf.Max(0, PulseCd - dt);
                if (Surge <= 0) surgeT = 45f;
            }
            else { surgeT -= dt; if (surgeT <= 0) { Surge = 8f; Announce("Лучезарность!", "Все световые способности усилены на 8 секунд", new Color("#ffe27a"), "ancient"); Flash = .3f; } }
        }
        else Surge = 0;
    }
}
