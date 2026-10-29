using Godot;
using System;

namespace Lamplighter;

/// <summary>Выжившие в местах интереса (PK.Survivor): за освобождение нужно небольшое условие. Вариант 1 — принести дерево и камень, 2 — зажечь ближайший древний фонарь,
/// 3 — разобрать хрупкую стену. Прежние пленники (PK.Captive) требуют победить стражу и работают как раньше. Профессию задаёт тег «villager:id» или очерёдность в деревне.</summary>
public partial class Game
{
    public static readonly (int item, int n)[] SurvivorCost = { (It.Wood, 8), (It.Stone, 4) };

    Ancient NearestAncient(Vector2 p)
    {
        Ancient best = null; float bd = float.MaxValue;
        for (int i = 1; i < W.Ancients.Count; i++) { var a = W.Ancients[i]; if (a.Boss) continue; float d = a.Pos.DistanceTo(p); if (d < bd) { bd = d; best = a; } }
        return best;
    }

    bool SurvivorIx(Poi p, Ix ix)
    {
        ix.Title = p.Variant == 1 ? "Заброшенный дом" : "Убежище выжившего";
        if (p.State >= 3) { ix.Lines.Add("Здесь больше никого нет"); return true; }
        if (p.Variant == 1)
        {
            ix.Lines.Add("Из дома слышен голос: нужна помощь");
            ix.Action("E", "Принести припасы и вывести выжившего"); foreach (var (it, n) in SurvivorCost) ix.Need((It.Ic(it) + It.Names[it], n, Inv[it]));
            ix.Gives("Даст", "Нового жителя", TipRole.Key); ix.E = () => RescueSurvivor(p); return true;
        }
        if (p.Variant == 2)
        {
            var a = NearestAncient(p.Pos);
            if (a != null && !a.Lit) { ix.Lines.Add("Выживший боится тьмы: зажгите ближайший древний фонарь"); return true; }
        }
        ix.Lines.Add("Выживший просит вывести его к свету");
        ix.Action("E", "Освободить выжившего"); ix.Gives("Даст", "Нового жителя", TipRole.Key); ix.E = () => RescueSurvivor(p); return true;
    }

    void RescueSurvivor(Poi p)
    {
        if (p.State >= 3) return;
        if (p.Variant == 1)
        {
            foreach (var (it, n) in SurvivorCost) if (Inv[it] < n) { Fail("Не хватает припасов: дерево 8, камень 4"); return; }
            foreach (var (it, n) in SurvivorCost) Inv[it] -= n;
        }
        Rescue(p);
    }

    /// <summary>Хост: участник кооператива просит освободить жителя. Состояние места и появление жителя — общие для всех.</summary>
    public bool NetRescue(int poiIdx)
    {
        if (poiIdx < 0 || poiIdx >= W.Pois.Count) return false; var p = W.Pois[poiIdx];
        if (p.Kind != PK.Survivor && p.Kind != PK.Captive) return false; if (p.State >= 3) return false;
        Rescue(p); return true;
    }
}
