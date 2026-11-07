using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Стол исследований: общая постройка мира (одна на мир, пять уровней), единственное место, где герой покупает уровни навыков.
/// Кузница делает и улучшает предметы, печь плавит; стол только учит. Уровень стола открывает ступень исследований, а не выдаёт навыки: у каждого навыка остаются свои условия (Techs[i].Ok, родители, цена).
/// Уровень стола и его положение лежат в мире (Buildings, синхронизируются хостом); изученные навыки принадлежат герою и работают в любом мире, даже без стола.
/// Вне стола окно навыков (T) только показывает дерево. Уровни: I — основы, II — ремесленные, III — кристальные (Глубины), IV — пепел, V — мороз.</summary>
public partial class Game
{
    public const int ResearchBk = 11;                        // индекс стола в BTypes/BNames/BCosts
    public const float ResearchRange = 64f;                  // расстояние до стола, на котором работает окно исследований
    public static readonly string[] TableStages = { "Основы", "Ремесленные исследования", "Кристальные исследования", "Исследования пепла", "Исследования мороза" };
    public bool TechAtTable;                                 // окно навыков открыто у стола (иначе — только просмотр дерева)
    public bool StrictResearch;                              // проверки: в автотестах стол обязателен (по умолчанию тесты учат навыки без стола)

    /// <summary>В автотестах без StrictResearch навыки учатся где угодно: старые проверки дерева не зависят от стола.</summary>
    public bool FreeResearch => testing && !StrictResearch;

    /// <summary>Нужный уровень стола для навыка. Награды стражей: первый уровень даёт победа, дальнейшие — у стола любого уровня.</summary>
    static readonly Dictionary<Tech, int> TableReqs = new()
    {
        [Tech.Spark] = 1, [Tech.Pack] = 1, [Tech.Regen] = 1, [Tech.Lens] = 1, [Tech.FarLanterns] = 1, [Tech.Reaper] = 1,
        [Tech.Dash] = 2, [Tech.WideSwing] = 2, [Tech.Spin] = 2, [Tech.Searing] = 2, [Tech.Flare] = 2, [Tech.Sight] = 2, [Tech.Marksman] = 2, [Tech.QuickDraw] = 2, [Tech.Piercing] = 2,
        [Tech.SilverEdge] = 3, [Tech.Optics] = 3, [Tech.CombatLamp] = 3, [Tech.HeroLamp] = 3, [Tech.Boots] = 3, [Tech.LensII] = 3, [Tech.FarLanternsII] = 3, [Tech.Overcharge] = 3,
        [Tech.FlameEdge] = 4, [Tech.HeatShield] = 4, [Tech.FireLens] = 4,
        [Tech.SpikeBoots] = 5, [Tech.FrostEdge] = 5, [Tech.FrostArmor] = 5,
    };

    public static int TableReq(int tech) => TableReqs.TryGetValue((Tech)tech, out var l) ? l : 1;

    /// <summary>Высший уровень стола в этом мире; 0 — стола нет.</summary>
    public int TableLevel { get { int l = 0; foreach (var b in Buildings) if (b.Type == Tl.ResearchTable) l = Math.Max(l, b.Level); return l; } }
    int EffTableLevel => FreeResearch ? 5 : TableLevel;

    /// <summary>Игрок стоит у стола (в тестах без строгого режима — всегда).</summary>
    public bool NearTable()
    {
        if (FreeResearch) return true;
        foreach (var b in Buildings) if (b.Type == Tl.ResearchTable && b.Pos.DistanceTo(PPos) <= ResearchRange) return true;
        return false;
    }

    /// <summary>Почему стол не позволяет изучить навык (кратко), либо null: стол есть и его уровня хватает. Расстояние до стола не проверяется — оно в LearnTech.</summary>
    public string TableReason(int tech)
    {
        int need = TableReq(tech), have = EffTableLevel;
        if (have == 0) return "Постройте Стол исследований [B]";
        if (have < need) return $"Улучшите Стол исследований до {need} уровня";
        return null;
    }

    /// <summary>Почему стол нельзя улучшить до следующего уровня (регион не открыт), либо null. Ресурсы проверяются отдельно.</summary>
    public string BuildUpBlock(Building b)
    {
        if (b == null || b.Type != Tl.ResearchTable || b.Level >= MaxLv(ResearchBk)) return null;
        int to = b.Level + 1;
        if (to == 3 && !BiomeDiscovered(1)) return "Зажгите древний фонарь в шахтах";
        if (to == 4 && !BiomeDiscovered(2)) return "Зажгите древний фонарь в пустошах";
        if (to == 5 && !BiomeDiscovered(3)) return "Зажгите древний фонарь в монастыре";
        return null;
    }

    /// <summary>Строки подсказки у стола: ступень исследований, число доступных навыков.</summary>
    void ResearchIx(Building b, Ix ix)
    {
        ix.Gives("Исследования", TableStages[Math.Clamp(b.Level, 1, 5) - 1], TipRole.Zone);
    }

    /// <summary>Сколько навыков героя можно улучшить прямо сейчас у стола (для подсказки в окне).</summary>
    public int ResearchReady()
    {
        int n = 0;
        for (int i = 0; i < Techs.Length; i++) if (!Retired(i) && Upgrades.IsPageUnlocked(Techs[i].Page) && StateOfTech(i) == TechState.Available && TableReason(i) == null) n++;
        return n;
    }

    /// <summary>Окно навыков закрывается, если герой отошёл от стола.</summary>
    void TickResearch()
    {
        if (TechOpen && TechAtTable && !NearTable()) { CloseWindows(); Toast("Вы отошли от Стола исследований"); }
    }
}
