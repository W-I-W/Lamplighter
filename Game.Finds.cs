using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Чертежи, редкость и награды за находки. Ключевые предметы выдаются фиксированными тегами, случайность — только бонус.</summary>
public partial class Game
{
    public HashSet<string> BpDone = new();
    public Dictionary<string, int> Frag = new();
    public List<string> Found = new();

    public void AddFragment(string id, int n = 1)
    {
        var d = Db.Bp(id); if (d == null || BpDone.Contains(id)) return;
        Frag[id] = Frag.GetValueOrDefault(id) + n;
        if (Frag[id] >= d.Need) UnlockBp(id);
        else { Toast($"Фрагмент чертежа «{d.Name}»: {Frag[id]}/{d.Need}"); Sfx.Play("tech", -4, 1.2f); }
    }

    public void UnlockBp(string id, bool announce = true)
    {
        var d = Db.Bp(id); if (d == null || !BpDone.Add(id)) return;
        Frag[id] = d.Need; Found.Add($"Чертёж: {d.Name}");
        if (announce) Announce($"Чертёж собран: {d.Name}", d.Desc + " · журнал [J]", new Color("#8ff0ff"), "tech");
    }

    public Rar RollRarity(Random r, int tier, float extra = 0)
    {
        float luck = Luck + extra;
        float[] w = { 55 - 8 * tier, 28, 12 + 4 * tier + 20 * luck, 4 + 2 * tier + 8 * luck, .5f + .6f * tier + 2 * luck };
        float sum = 0; foreach (var x in w) sum += Math.Max(0, x);
        float v = (float)r.NextDouble() * sum;
        for (int i = 0; i < w.Length; i++) { v -= Math.Max(0, w[i]); if (v <= 0) return (Rar)i; }
        return Rar.Common;
    }

    static readonly string[] BasicBps = { "bp_mod_multi", "bp_sword2", "bp_bow2", "bp_staff2", "bp_hub", "bp_refinery", "bp_stall", "bp_amp" };
    static readonly string[] AdvBps = { "bp_mod_ignite", "bp_mod_frost", "bp_mod_shock", "bp_mod_siphon", "bp_sword3", "bp_bow3", "bp_staff3", "bp_barrier", "bp_herolamp" };
    static readonly string[] CommonRelics = { "r_embersp", "r_frostshard", "r_beaconwick", "r_sparkcoil", "r_minercharm", "r_luckycoin", "r_merchant" };
    static readonly string[] RareRelics = { "r_ashcrown", "r_rimemirror", "r_firstlight", "r_stormclaw", "r_shadowblood" };

    string PickUnowned(Random r, string[] pool)
    {
        var l = new List<string>(); foreach (var p in pool) if (!OwnedRelics.Contains(p)) l.Add(p);
        if (l.Count == 0) return null; return l[r.Next(l.Count)];
    }
    string PickBp(Random r, string[] pool)
    {
        var l = new List<string>(); foreach (var p in pool) if (!BpDone.Contains(p)) l.Add(p);
        if (l.Count == 0) return null; return l[r.Next(l.Count)];
    }

    /// <summary>Случайная редкая награда нужной редкости (всегда что-то получаем).</summary>
    public string RollTag(Random r, int tier, float extra = 0)
    {
        var rar = RollRarity(r, tier, extra);
        string tag = null;
        switch (rar)
        {
            case Rar.Common: tag = r.Next(2) == 0 ? $"coins:{8 + 6 * tier + r.Next(10)}" : $"item:{(r.Next(2) == 0 ? It.Iron : It.Copper)}:{2 + r.Next(3)}"; break;
            case Rar.Uncommon:
                if (r.Next(10) < 6) { int m = r.Next(7); tag = $"mod:{m}"; } else { var b = PickBp(r, BasicBps); tag = b != null ? "frag:" + b : $"coins:{25 + 10 * tier}"; }
                break;
            case Rar.Rare:
                int q = r.Next(100);
                if (q < 40) { var b = PickBp(r, AdvBps); tag = b != null ? "frag:" + b : null; }
                else if (q < 75) { var rl = PickUnowned(r, CommonRelics) ?? PickUnowned(r, RareRelics); tag = rl != null ? "relic:" + rl : null; }
                tag ??= "item:" + It.AncCrystal + ":1"; break;
            case Rar.Epic:
                var er = PickUnowned(r, RareRelics);
                if (er != null && r.Next(2) == 0) tag = "relic:" + er;
                else { var ws = new[] { "sword3", "bow3", "staff3" }; var wl = new List<string>(); foreach (var x in ws) if (!HasWeapon(x)) wl.Add(x); tag = wl.Count > 0 ? "weapon:" + wl[r.Next(wl.Count)] : (er != null ? "relic:" + er : "item:" + It.AncCrystal + ":2"); }
                break;
            default:
                tag = !HasWeapon("storm_u") ? "weapon:storm_u" : (PickUnowned(r, RareRelics) is string lr ? "relic:" + lr : "item:" + It.AncCrystal + ":3"); break;
        }
        return tag + "|" + (int)rar;
    }

    /// <summary>Выдаёт награду по тегу: bp:ID, frag:ID, relic:ID, weapon:ID, mod:N, item:IDX:N, coins:N.</summary>
    public void GrantTag(string tagRar, Vector2 at)
    {
        if (string.IsNullOrEmpty(tagRar)) return;
        var parts0 = tagRar.Split('|'); var tag = parts0[0]; int rar = parts0.Length > 1 ? int.Parse(parts0[1]) : -1;
        var p = tag.Split(':');
        switch (p[0])
        {
            case "bp": UnlockBp(p[1]); break;
            case "frag": AddFragment(p[1]); break;
            case "relic": GiveRelic(p[1]); break;
            case "weapon": GiveWeapon(p[1]); Found.Add("Оружие: " + Db.Weapon(p[1])?.Name); break;
            case "mod": { int m = int.Parse(p[1]); ModInv[m]++; Announce($"Модификатор: {Db.Mods[m].Name}", Db.Mods[m].Desc + " · [C] установить в оружие", Db.RarCol[1], "chest"); Found.Add("Модификатор: " + Db.Mods[m].Name); break; }
            case "item": { int it = int.Parse(p[1]), n = int.Parse(p[2]); Give(it, n, at); if (it == It.AncCrystal) Found.Add("Древний кристалл"); break; }
            case "coins": { int n = int.Parse(p[1]); Inv[It.Coins] += n; Texts.Add(new FText { P = at, S = $"+{n} монет", Life = 1.2f, C = It.Col[It.Coins] }); break; }
        }
        if (Found.Count > 60) Found.RemoveRange(0, Found.Count - 60);
    }
}
