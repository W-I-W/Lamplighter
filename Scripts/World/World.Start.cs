using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Стартовая поляна нового мира: гарантированный запас ресурсов рядом с Центральным маяком, достижимый без инструментов и без выхода в Тьму. Объём считается по настоящим рецептам
/// (Искра света, топор, печь, кирка) с запасом. Проверка и добавление детерминированы по seed, чанки и остальная генерация не меняются.</summary>
public sealed partial class World
{
    public const int StartRadius = 10;                         // клеток от центра: внутри света Центрального маяка (радиус 11)

    /// <summary>Сколько объектов каждого вида нужно на поляне: потребность рецептов × 1,4 / минимальная добыча с объекта (дерево 1,3, камень и медь 1).</summary>
    public static (int trees, int stones, int copper) StartNeeds()
    {
        int wood = 0, stone = 0, copper = 0;
        void Add(int[][] cost) { if (cost == null) return; foreach (var c in cost) { if (c[0] == It.Wood) wood += c[1]; else if (c[0] == It.Stone) stone += c[1]; else if (c[0] == It.Copper) copper += c[1]; } }
        Add(Game.Techs[(int)Tech.Spark].Cost);                 // первый навык света
        Add(Game.CostFurnace); Add(Game.CostPick[0]);          // печь и медная кирка
        wood += 5; stone += 3;                                 // топор: дерево 5, камень 3
        return ((int)Math.Ceiling(wood * 1.4 / 1.3), (int)Math.Ceiling(stone * 1.4), (int)Math.Ceiling(copper * 1.4) + 1);
    }

    static bool IsResource(Tl t) => t == Tl.Tree || t == Tl.Stone || t == Tl.Copper;

    /// <summary>Объекты ресурсов в радиусе поляны, до которых можно добраться от старта, ломая только деревья, камни и руду (не стены, не обрывы).</summary>
    public (int trees, int stones, int copper) StartCounts()
    {
        var seen = new HashSet<int>(); var q = new Queue<Vector2I>(); int t = 0, s = 0, cu = 0;
        q.Enqueue(Spawn); seen.Add(Key(Spawn.X, Spawn.Y));
        while (q.Count > 0)
        {
            var p = q.Dequeue();
            foreach (var d in D4)
            {
                var n = p + d; if (!In(n.X, n.Y)) continue; int k = Key(n.X, n.Y); if (seen.Contains(k)) continue;
                int dx = n.X - N / 2, dy = n.Y - N / 2; if (dx * dx + dy * dy > StartRadius * StartRadius) continue;
                var o = Get(n.X, n.Y); if (!(Walkable(n.X, n.Y) || IsResource(o))) continue;
                seen.Add(k); q.Enqueue(n);
                if (o == Tl.Tree) t++; else if (o == Tl.Stone) s++; else if (o == Tl.Copper) cu++;
            }
        }
        return (t, s, cu);
    }

    /// <summary>Добавляет недостающие деревья, камни и медь на свободные клетки поляны, не перекрывая проходы (вокруг клетки всё свободно), затем проверяет достижимость.</summary>
    void EnsureStartResources(int seed)
    {
        var need = StartNeeds(); var rng = new Random(seed * 7919 + 13);
        for (int pass = 0; pass < 5; pass++)
        {
            var have = StartCounts();
            Tl[] kinds = { Tl.Tree, Tl.Stone, Tl.Copper }; int[] miss = { need.trees - have.trees + (pass > 0 ? 2 : 0), need.stones - have.stones + (pass > 0 ? 2 : 0), need.copper - have.copper + (pass > 0 ? 1 : 0) };
            if (need.trees <= have.trees && need.stones <= have.stones && need.copper <= have.copper) return; bool strict = pass < 2;
            if (miss[0] <= 0 && miss[1] <= 0 && miss[2] <= 0) return;
            var spots = new List<Vector2I>();
            for (int y = N / 2 - StartRadius; y <= N / 2 + StartRadius; y++)
                for (int x = N / 2 - StartRadius; x <= N / 2 + StartRadius; x++)
                {
                    int dx = x - N / 2, dy = y - N / 2, d2 = dx * dx + dy * dy; if (d2 < 36 || d2 > (StartRadius - 1) * (StartRadius - 1)) continue;      // не на самой площадке и не на краю света маяка
                    if (!Walkable(x, y)) continue; bool free = true;
                    if (strict) for (int ey = -1; ey <= 1 && free; ey++) for (int ex = -1; ex <= 1; ex++) if ((ex != 0 || ey != 0) && !Walkable(x + ex, y + ey) && Get(x + ex, y + ey) != Tl.Tree) { free = false; break; }
                    if (free) spots.Add(new Vector2I(x, y));
                }
            for (int i = spots.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (spots[i], spots[j]) = (spots[j], spots[i]); }
            int at = 0;
            for (int k = 0; k < 3; k++)
                for (int n = 0; n < miss[k] && at < spots.Count; n++)
                {
                    var s = spots[at++];
                    // клетка рядом с уже занятой не берётся: соседние объекты могут запереть проход
                    bool ok = true; if (strict) foreach (var d in D8) if (IsResource(Get(s.X + d.X, s.Y + d.Y)) && Get(s.X + d.X, s.Y + d.Y) != Tl.Tree) { ok = false; break; }
                    if (!ok) { n--; continue; }
                    Set(s.X, s.Y, kinds[k]);
                }
        }
    }

    static readonly Vector2I[] D8 = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, 1), new(-1, 1), new(1, -1), new(-1, -1) };
}
