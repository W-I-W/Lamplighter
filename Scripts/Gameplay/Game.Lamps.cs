using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Виды древних фонарей (этап 8). Зажигание по-прежнему стоит искр света и освобождает территорию навсегда; вид добавляет небольшое условие рядом с фонарём.
/// Обычный и повреждённый не требуют боя и загадок, чтобы прогресс чувствовался быстро.</summary>
public partial class Game
{
    public static readonly (int item, int n)[] RepairCost = { (It.Wood, 8), (It.Stone, 6), (It.Copper, 3) };

    public static string LampKindName(LampKind k) => k switch { LampKind.Guarded => "Охраняемый фонарь", LampKind.Damaged => "Повреждённый фонарь", LampKind.Infected => "Заражённый фонарь", LampKind.Mystery => "Загадочный фонарь", _ => "Древний фонарь" };

    public static Color LampKindColor(LampKind k) => k switch { LampKind.Guarded => new Color("#ff6a5a"), LampKind.Damaged => new Color("#ffb04a"), LampKind.Infected => new Color("#c070ff"), LampKind.Mystery => new Color("#6ad0ff"), _ => Colors.White };

    /// <summary>Выполнены ли условия вида. why — что мешает.</summary>
    public bool LampReady(Ancient a, out string why)
    {
        why = "";
        switch (a.Kind)
        {
            case LampKind.Guarded when !a.Done: why = a.Phase == 1 ? "Победите стражей фонаря" : "Фонарь охраняют тени: подойдите ближе"; return false;
            case LampKind.Mystery when !a.Done: why = "Загадка светил: коснитесь трёх светил в порядке из записки"; return false;
            case LampKind.Infected when !a.Done: why = "Разрушите теневой кристалл у фонаря"; return false;
            case LampKind.Damaged:
                foreach (var (it, n) in RepairCost) if (Inv[it] < n) { why = "Фонарь повреждён: не хватает материалов для починки"; return false; }
                break;
        }
        return true;
    }

    void PayRepair(Ancient a) { if (a.Kind == LampKind.Damaged) foreach (var (it, n) in RepairCost) Inv[it] -= n; }

    // ───────────────────────── загадочный фонарь: три светила ─────────────────────────
    static readonly string[] PylonSide = { "западное", "северное", "восточное" };

    /// <summary>Светило или записка загадочного фонаря: возвращает true, если клетка относится к такому фонарю.</summary>
    bool MysteryIx(int x, int y, Tl t, Ix ix)
    {
        var v = new Vector2I(x, y);
        foreach (var a in W.Ancients)
        {
            if (a.Kind != LampKind.Mystery || a.Pylons == null) continue;
            if (t == Tl.Tablet && a.Tablet == v)
            {
                ix.Title = "Записка у фонаря";
                if (a.Done) { ix.Lines.Add("Светила вспомнили порядок: загадка разгадана"); return true; }
                ix.Lines.Add($"«Свет помнит порядок: {string.Join(" — ", System.Linq.Enumerable.Select(a.Seq, i => PylonSide[i]))}»"); return true;
            }
            int pi = System.Array.IndexOf(a.Pylons, v); if (pi < 0) continue;
            ix.Title = "Светило фонаря";
            if (a.Done) { ix.Lines.Add("Светило горит: загадка разгадана"); return true; }
            ix.Lines.Add($"Светило {PylonSide[pi]} · горит {a.SeqPos}/3");
            ix.Lines.Add("[E] Коснуться светила"); int idx = pi; ix.E = () => TouchPylon(a, idx); return true;
        }
        return false;
    }

    void TouchPylon(Ancient a, int i)
    {
        if (a.Done || a.Seq == null) return;
        var pt = a.Pylons[i]; var c = TileCenter(pt.X, pt.Y);
        if (a.Seq[a.SeqPos] == i && W.Get(pt.X, pt.Y) == Tl.Pylon)
        {
            a.SeqPos++; SetObj(pt.X, pt.Y, Tl.PylonOn); Burst(c, new Color("#6ad0ff"), 14, 80); Sfx.Play("tick", -2, 1f + .25f * a.SeqPos);
            if (a.SeqPos >= 3)
            {
                a.Done = true; GiveXp(40); Flash = .25f; Sfx.Play("tech");
                Announce("Загадка разгадана", "Светила вспомнили порядок — фонарь можно зажечь [E]", new Color("#7ad45a"), "discover"); Save();
            }
        }
        else
        {
            a.SeqPos = 0; foreach (var p in a.Pylons) if (W.Get(p.X, p.Y) == Tl.PylonOn) SetObj(p.X, p.Y, Tl.Pylon);
            Burst(c, new Color("#9a60d0"), 10, 60); Sfx.Play("tick", -3, .6f); Toast("Светила погасли: порядок неверен");
        }
    }

    /// <summary>После загрузки: незавершённая загадка начинается заново, решённая остаётся горящей.</summary>
    void RestoreMysteryTiles()
    {
        RestoreRuneTiles();
        foreach (var a in W.Ancients)
        {
            if (a.Kind != LampKind.Mystery || a.Pylons == null) continue; a.SeqPos = 0;
            foreach (var p in a.Pylons) { var cur = W.Get(p.X, p.Y); if (cur != Tl.Pylon && cur != Tl.PylonOn) continue; W.Set(p.X, p.Y, a.Done ? Tl.PylonOn : Tl.Pylon); }
        }
    }

    // ───────────────────────── алтарь искр ─────────────────────────
    public const int SparkAltarCost = 3;

    bool SparkAltarIx(Poi p, Ix ix)
    {
        ix.Title = "Алтарь искр";
        if (p.State >= 3) { ix.Lines.Add("Алтарь угас: тайна леса раскрыта"); return true; }
        ix.Lines.Add("Алтарь ждёт искр света");
        ix.Action("E", "Отдать искры алтарю").Need((It.Ic(It.Shard) + It.Names[It.Shard], SparkAltarCost, Inv[It.Shard]));
        ix.Gives("Даст", "Реликвию и путь к скрытой сокровищнице", TipRole.Key); ix.E = () => UseSparkAltar(p); return true;
    }

    void UseSparkAltar(Poi p)
    {
        if (p.State >= 3) return;
        if (Inv[It.Shard] < SparkAltarCost) { Toast("Алтарю нужны три осколка света"); return; }
        Inv[It.Shard] -= SparkAltarCost; p.State = 3; p.Seen = true; SetObj(p.Tile.X, p.Tile.Y, Tl.AltarOn);
        if (p.Tag != null) foreach (var tg in p.Tag.Split(';')) GrantTag(tg, p.Pos);
        Poi vault = null; float bd = float.MaxValue;
        foreach (var q in W.Pois) if (q.Kind == PK.Vault && q.State < 3 && !q.Seen && !q.Revealed) { float d = q.Pos.DistanceTo(p.Pos); if (d < bd) { bd = d; vault = q; } }
        if (vault != null) vault.Revealed = true;
        Burst(p.Pos, new Color("#ffe27a"), 30, 110); Flash = .3f; Sfx.Play("ancient"); GiveXp(60);
        Announce("Алтарь искр пробуждён", vault != null ? $"Он показал скрытую сокровищницу ({(int)(vault.Pos.DistanceTo(PPos) / TS)}) — метка на карте" : "Награда получена", new Color("#ffe27a"), "ancient"); Save();
    }

    float lampT;

    /// <summary>Раз в 0,4 с: стража охраняемых фонарей выходит при приближении, заражённый фонарь очищается после разрушения кристалла.</summary>
    void UpdateAncientKinds(float dt)
    {
        lampT -= dt; if (lampT > 0) return; lampT = .4f;
        for (int i = 1; i < W.Ancients.Count; i++)
        {
            var a = W.Ancients[i]; if (a.Lit || a.Boss || a.Kind == LampKind.Normal || a.Kind == LampKind.Mystery || a.Done) continue;
            float d = a.Pos.DistanceTo(PPos);
            if (a.Kind == LampKind.Guarded)
            {
                if (a.Phase == 0 && d < 13 * TS) { a.Phase = 1; a.GuardPoi.Tile = a.Tile; a.GuardPoi.Guards = 3 + (i >= 4 ? 1 : 0); SpawnGuards(a.GuardPoi); a.GuardPoi.State = 1; }
                else if (a.Phase == 1)
                {
                    bool any = false; foreach (var e in Enemies) if (e.Guard == a.GuardPoi) { any = true; break; }
                    if (!any) { a.Done = true; GiveXp(20); Sfx.Play("tick"); Announce("Стража фонаря побеждена", "Теперь фонарь можно зажечь [E]", new Color("#7ad45a"), "discover"); Save(); }
                }
            }
            else if (a.Kind == LampKind.Infected && W.Get(a.Crystal.X, a.Crystal.Y) != Tl.ShadowPillar)
            {
                a.Done = true; GiveXp(20); Burst(TileCenter(a.Crystal.X, a.Crystal.Y), new Color("#c070ff"), 24, 90); Sfx.Play("tech");
                Announce("Тень рассеялась", "Фонарь очищен — его можно зажечь [E]", new Color("#7ad45a"), "discover"); Save();
            }
        }
    }

    /// <summary>Сообщение при первом приближении к фонарю с особенностью, чтобы игрок понимал, что делать.</summary>
    readonly HashSet<int> lampSeen = new();
    void LampHints()
    {
        for (int i = 1; i < W.Ancients.Count; i++)
        {
            var a = W.Ancients[i]; if (a.Lit || a.Boss || a.Kind == LampKind.Normal || a.Done || lampSeen.Contains(i) || a.Pos.DistanceTo(PPos) > 9 * TS) continue;
            lampSeen.Add(i);
            if (a.Kind == LampKind.Damaged) Announce("Повреждённый фонарь", "Для починки нужны дерево, камень и медь", LampKindColor(a.Kind), "discover");
            else if (a.Kind == LampKind.Mystery) Announce("Загадочный фонарь", "Рядом записка и три светила: порядок касания скрыт в записке", LampKindColor(a.Kind), "discover");
            else if (a.Kind == LampKind.Infected) Announce("Заражённый фонарь", "Разрушите теневой кристалл рядом киркой", LampKindColor(a.Kind), "discover");
        }
    }
}
