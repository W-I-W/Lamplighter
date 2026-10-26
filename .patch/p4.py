def rw(p, f):
    s=open(p,encoding='utf-8').read(); s2=f(s); open(p,'w',encoding='utf-8').write(s2); return s!=s2

def game(s):
    s=s.replace('        Tl.DeadTree => Art.DeadTree, Tl.Obsidian => Art.Obsidian, Tl.Ember => Art.Ember, Tl.AshRuin => Art.AshRuin, Tl.Vent => Art.Vent,','        Tl.DeadTree => Art.DeadTree, Tl.Obsidian => Art.Obsidian, Tl.Ember => Art.Ember, Tl.AshRuin => Art.AshRuin, Tl.Vent => Art.Vent,\n        Tl.TechCache => Art.TechCache, Tl.Spire => Art.Spire, Tl.Altar => Art.Altar, Tl.AltarOn => Art.AltarOn, Tl.AmpCrystal => Art.AmpCrystal, Tl.BarrierPost => Art.BarrierPost, Tl.MeteorOre => Art.MeteorOre,\n        Tl.Stall => Art.Stall + Math.Min(lv, 3) - 1, Tl.Hub => Art.Hub + Math.Min(lv, 3) - 1, Tl.Refinery => Art.Refinery + Math.Min(lv, 3) - 1,')
    s=s.replace('                if (t != Tl.None) objL.SetCell(','                if (t != Tl.None && t != Tl.SecretCache) objL.SetCell(')
    s=s.replace('        if (t == Tl.None) objL.EraseCell(new Vector2I(x, y));','        if (t == Tl.None || t == Tl.SecretCache) objL.EraseCell(new Vector2I(x, y));')
    return s
print(rw('Game.cs',game))

def prog(s):
    s=s.replace('|| t == Tl.BridgeBroken || t == Tl.Seal || t == Tl.Receiver || t == Tl.ReceiverOn)','|| t == Tl.BridgeBroken || t == Tl.Seal || t == Tl.Receiver || t == Tl.ReceiverOn || t == Tl.TechCache || t == Tl.Spire || t == Tl.Altar || t == Tl.AltarOn)')
    s=s.replace('            case Tl.Chest: case Tl.Shrine: case Tl.Obelisk: case Tl.Cage: return p != null && PoiIx(p, ix);','            case Tl.Chest: case Tl.Shrine: case Tl.Obelisk: case Tl.Cage: case Tl.TechCache: case Tl.Spire: return p != null && PoiIx(p, ix);\n            case Tl.Altar: ix.Lines.Add("Алтарь лучей: направьте лучи на три приёмника вокруг (кристальные фонари, зеркала, Прожектор)"); return true;\n            case Tl.AltarOn: ix.Lines.Add("Алтарь пробуждён"); return true;')
    s=s.replace('if (p.Kind == PK.Workshop || p.Kind == PK.Puzzle ||','if (p.Kind == PK.TechCache || p.Kind == PK.Spire || p.Kind == PK.Altar || p.Kind == PK.Workshop || p.Kind == PK.Puzzle ||') if 'if (p.Kind == PK.Workshop || p.Kind == PK.Puzzle ||' in s else s
    s=s.replace('if (!(p.Kind == PK.Workshop ||','if (!(p.Kind == PK.TechCache || p.Kind == PK.Spire || p.Kind == PK.Altar || p.Kind == PK.Workshop ||')
    return s
print(rw('Game.Progress.cs',prog))

def poi(s):
    s=s.replace('PK.Workshop => "Заброшенная мастерская", PK.Puzzle => "Хранилище света", PK.BridgeSite => "Разрушенный мост", _ => "Эфирная роща"','PK.Workshop => "Заброшенная мастерская", PK.Puzzle => "Хранилище света", PK.BridgeSite => "Разрушенный мост",\n        PK.TechCache => "Забытая технология", PK.Spire => "Древний кристалл", PK.Altar => "Алтарь лучей", PK.Secret => "Скрытый тайник", _ => "Эфирная роща"')
    s=s.replace('PK.BridgeSite => "Почините мост [E] и пройдите на ту сторону", _ =>','PK.BridgeSite => "Почините мост [E] и пройдите на ту сторону",\n        PK.TechCache => "Постамент хранит чертёж — коснитесь [E]", PK.Spire => "Шпиль древнего света: возьмите кристалл [E]",\n        PK.Altar => "Направьте три луча на приёмники — откроется сокровищница", PK.Secret => "Тайник проявился!", _ =>')
    s=s.replace('PK.BridgeSite => new Color("#d9b27c"), _ =>','PK.BridgeSite => new Color("#d9b27c"), PK.TechCache => new Color("#8ff0ff"), PK.Spire => new Color("#ffb0ff"), PK.Altar => new Color("#ffe27a"), PK.Secret => new Color("#ffd86b"), _ =>')
    s=s.replace('            float d = p.Pos.DistanceTo(PPos);\n            if (!p.Seen && d < seeR)','            float d = p.Pos.DistanceTo(PPos);\n            if (p.Kind == PK.Secret && W.Get(p.Tile.X, p.Tile.Y) == Tl.SecretCache) continue;\n            if (!p.Seen && d < seeR)')
    # PoiIx new tiles + chest lock
    s=s.replace('            case Tl.Chest:\n                if (p.Kind == PK.Camp && p.State < 2) ix.Lines.Add("Сундук охраняется: победите стражу лагеря");','            case Tl.TechCache: ix.Lines.Add("[E] Взять чертёж с постамента"); ix.E = () => ClaimCache(p); return true;\n            case Tl.Spire: ix.Lines.Add("[E] Взять древний световой кристалл"); ix.E = () => TakeSpire(p); return true;\n            case Tl.Chest:\n                if (p.Variant == 7 && AltarLocked(p)) { ix.Lines.Add("Сундук сокровищницы запечатан: оживите алтарь тремя лучами света"); return true; }\n                if (p.Kind == PK.Camp && p.State < 2) ix.Lines.Add("Сундук охраняется: победите стражу лагеря");')
    # chest extras
    s=s.replace('        var parts = new List<string>(); int k = 0;','        var parts = new List<string>(); int k = 0;\n        if (p.Tag != null) foreach (var tg in p.Tag.Split(\';\')) GrantTag(tg, p.Pos);\n        { var rr = new Random(W.Seed * 61 + p.Tile.X * 7 + p.Tile.Y); float chance = p.Variant == 3 ? 1f : p.Variant == 1 || p.Variant == 7 ? .7f : p.Kind == PK.Camp ? .5f : .15f + Luck; if (rr.NextDouble() < chance) GrantTag(RollTag(rr, tier, p.Variant == 7 ? .4f : 0), p.Pos); }')
    s=s.replace('Announce(p.Variant == 1 ? "Тайник вскрыт!" : "Сундук открыт!"','Announce(p.Variant == 1 ? "Тайник вскрыт!" : p.Variant == 7 ? "Сокровищница открыта!" : "Сундук открыт!"')
    s+='''
public partial class Game
{
    bool AltarLocked(Poi chest)
    {
        foreach (var q in W.Pois) if (q.Kind == PK.Altar && q.State < 3 && q.Pos.DistanceTo(chest.Pos) < 8 * TS) return true;
        return false;
    }

    void ClaimCache(Poi p)
    {
        SetObj(p.Tile.X, p.Tile.Y, Tl.ShrineUsed); p.State = 3; GiveXp(30);
        foreach (var tg in p.Tag.Split(';')) GrantTag(tg, p.Pos);
        Burst(p.Pos, new Color("#8ff0ff"), 24, 100); Sfx.Play("tech"); Save();
    }

    void TakeSpire(Poi p)
    {
        int n = World.RegionOf(p.Tile.X) == 0 ? 1 : 2;
        SetObj(p.Tile.X, p.Tile.Y, Tl.ObeliskUsed); p.State = 3; GiveXp(20);
        Give(It.AncCrystal, n, p.Pos); Found.Add("Древний кристалл"); Burst(p.Pos, new Color("#ffb0ff"), 24, 100); Sfx.Play("chest"); Save();
    }
}
'''
    return s
print(rw('Game.Poi.cs',poi))

def light(s):
    s=s.replace('        puzzleT -= dt;\n        if (puzzleT <= 0) { puzzleT = .1f; UpdatePuzzles(rec); }','        puzzleT -= dt;\n        if (puzzleT <= 0) { puzzleT = .1f; UpdatePuzzles(rec); RevealSecrets(); }')
    s=s.replace('    void SolvePuzzle(Puzzle pz)\n    {\n        pz.Poi.State = 3; pz.Poi.Seen = true;','    void RevealSecrets()\n    {\n        foreach (var p in W.Pois)\n        {\n            if (p.Kind != PK.Secret || p.State >= 3 || W.Get(p.Tile.X, p.Tile.Y) != Tl.SecretCache) continue;\n            bool lit = false;\n            foreach (var bp in Beams) { for (int i = 0; i + 1 < bp.Pts.Count && !lit; i++) if (DistToSeg(p.Pos, bp.Pts[i], bp.Pts[i + 1]) < 11) lit = true; if (lit) break; }\n            if (!lit) foreach (var b in Beacons) if (p.Pos.DistanceTo(b.Pos) < BeaconR(b) * .85f) { lit = true; break; }\n            if (!lit) continue;\n            SetObj(p.Tile.X, p.Tile.Y, Tl.Chest); p.Seen = true; Burst(p.Pos, new Color("#ffe27a"), 20, 90);\n            Announce("Скрытый тайник проявился!", "Свет выявил то, что скрывала тьма", new Color("#ffd86b"), "tech");\n        }\n    }\n\n    void SolvePuzzle(Puzzle pz)\n    {\n        pz.Poi.State = 3; pz.Poi.Seen = true;\n        if (pz.Poi.Kind == PK.Altar) { SetObj(pz.Poi.Tile.X, pz.Poi.Tile.Y, Tl.AltarOn); Announce("Алтарь лучей пробуждён!", "Сундук сокровищницы открыт", new Color("#ffe27a"), "ancient"); Flash = .4f; GiveXp(80); Save(); return; }')
    return s
print(rw('Game.Light.cs',light))
