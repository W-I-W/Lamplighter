using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter;

/// <summary>Могила героя: при смерти все ресурсы, инструменты и оружие исчезают из рюкзака и ложатся сюда. Сам тайл могилы живёт в мире (виден всем и синхронизируется),
/// а содержимое хранится у владельца в сохранении героя: разбить могилу и забрать вещи может только он.</summary>
public sealed class GraveRec
{
    public int X, Y, Seed, Tier, AxeLv; public int[] Inv; public string[] Tools; public string[] WId; public int[] WLv; public int[][] WMods;          // WId/WLv/WMods: оружие с уровнем и вставленными модификаторами
}

public partial class Game
{
    public readonly List<GraveRec> Graves = new();

    /// <summary>Могила этого героя в этой клетке этого мира (чужие могилы записи не имеют).</summary>
    public GraveRec GraveAt(Vector2I tile) => Graves.Find(g => g.Seed == W.Seed && g.X == tile.X && g.Y == tile.Y);
    public bool OwnGrave(Vector2I tile) => GraveAt(tile) != null;

    /// <summary>Центр могилы, которую нужно вскрыть первой (для указателя на экране): указатель горит только у одной могилы, в порядке смертей — сначала самая ранняя (Graves ведётся по порядку добавления). Null, если могил нет.</summary>
    public Vector2? NextGravePos()
    {
        foreach (var g in Graves) if (g.Seed == W.Seed) return TileCenter(g.X, g.Y);
        return null;
    }

    /// <summary>Ближайшая свободная клетка для могилы: пустая, не вода, не на пороге построек.</summary>
    Vector2I? GraveSpot(Vector2I from)
    {
        for (int r = 0; r <= 6; r++)
            for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                int x = from.X + dx, y = from.Y + dy; if (!World.In(x, y) || W.Get(x, y) != Tl.None) continue;
                if (W.Ground[World.Key(x, y)] == Art.WaterG || W.Ground[World.Key(x, y)] == Art.HotAsh || W.Ground[World.Key(x, y)] == Art.VoidG) continue;
                if (IsBushAt(x, y)) continue; return new Vector2I(x, y);
            }
        return null;
    }

    /// <summary>Смерть: ресурсы и инструменты (с уровнем кирки и топора) уходят в могилу. Возвращает число потерянных предметов; без могилы (негде поставить) вещи остаются у героя.</summary>
    int BuryPlayer()
    {
        int total = 0; foreach (var n in Inv) total += n; total += OwnedTools.Count + Owned.Count;
        if (total == 0) return 0;
        var spot = GraveSpot(ToTile(PPos)); if (spot == null) return 0;
        var g = new GraveRec { X = spot.Value.X, Y = spot.Value.Y, Seed = W.Seed, Tier = Tier, AxeLv = AxeLv, Inv = (int[])Inv.Clone(), Tools = OwnedTools.ToArray(), WId = Owned.ConvertAll(o => o.Id).ToArray(), WLv = Owned.ConvertAll(o => o.Lv).ToArray(), WMods = Owned.ConvertAll(o => o.Mods.ToArray()).ToArray() };
        Graves.Add(g);
        BareChosen = false; Array.Clear(Inv); OwnedTools.Clear(); Owned.Clear(); CurW = -1; Tier = 0; AxeLv = 0; CurTool = "";
        for (int i = 0; i < HotCount; i++) if (IsToolRef(Hot[i]) || IsWeaponRef(Hot[i])) Hot[i] = null;
        SetObj(g.X, g.Y, Tl.Grave);
        HotFollow(); HandFallback(); RecomputeEffects(); Diag.Count("graves");
        return total;
    }

    /// <summary>Могила разбита владельцем: всё потерянное возвращается. Инструменты и их уровни не понижают уже имеющиеся (кирку успели сделать заново).</summary>
    void OpenGrave(Vector2I tile)
    {
        var g = GraveAt(tile); if (g == null) return; Graves.Remove(g);
        int total = 0; for (int i = 0; i < It.Count && i < g.Inv.Length; i++) if (g.Inv[i] > 0) { Inv[i] += g.Inv[i]; total += g.Inv[i]; }
        foreach (var t in g.Tools) if (Array.IndexOf(ToolDef.Ids, t) >= 0 && OwnedTools.Add(t)) AutoAssign(RefTool(t));
        if (g.WId != null) for (int i = 0; i < g.WId.Length; i++)
        {
            var wdef = Db.Weapon(g.WId[i]); if (wdef == null) continue;
            int glv = g.WLv != null && i < g.WLv.Length ? g.WLv[i] : 0; int[] gm = g.WMods != null && i < g.WMods.Length ? g.WMods[i] : null;
            var have = Owned.Find(o => o.Id == g.WId[i]);
            if (have == null) { var ow = new OwnedWeapon { Id = g.WId[i], Lv = glv }; if (gm != null) ow.Mods.AddRange(gm); Owned.Add(ow); total++; AutoAssign(RefWeapon(ow.Id)); continue; }
            have.Lv = Math.Max(have.Lv, glv);                                                       // такое оружие уже сделали заново: уровень берём лучший, а вставки не пропадают
            if (gm != null) foreach (int m in gm) { if (have.Mods.Count < wdef.Slots) have.Mods.Add(m); else if (m >= 0 && m < ModInv.Length) ModInv[m]++; }
            total++;
        }
        Tier = Math.Max(Tier, g.Tier); AxeLv = Math.Max(AxeLv, g.AxeLv);
        if (!OwnedTools.Contains(CurTool)) CurTool = OwnedTools.Contains("pickaxe") ? "pickaxe" : OwnedTools.FirstOrDefault() ?? "";
        RecomputeEffects(); Sfx.Play("pickup", -2, .8f);
        Announce("Могила разбита", total > 0 ? string.Format(Loc.T("Возвращено вещей: {0}"), total) : "Инструменты вернулись", new Color("#c8ccdc"), "chest");
        Save();
    }
}
