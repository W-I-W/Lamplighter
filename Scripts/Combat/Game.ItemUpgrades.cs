using Godot;
using System;

namespace Lamplighter;

/// <summary>Улучшение инструментов и оружия в снаряжении: уровни +1…+5 за ресурсы у печи или кузницы. Кирка улучшается по своим ступеням (Tier), топор и всё оружие — по этим уровням.
/// Уровень оружия даёт +10% урона за ступень (на пятом уровне оружие тира N всё ещё слабее тира N+1), уровень топора — +30% к добыче и +10% к урону.</summary>
public partial class Game
{
    public const int WeaponMaxLv = 5, AxeMaxLv = 4;
    public const float WeaponLvStep = .10f, AxeMineStep = .30f, AxeDmgStep = .10f;
    public int AxeLv;

    public int WeaponLv(string id) { foreach (var o in Owned) if (o.Id == id) return o.Lv; return 0; }
    float WeaponLvMul(string id) => 1f + WeaponLvStep * WeaponLv(id);
    float ToolMineMul(string id) => id == "axe" ? 1f + AxeMineStep * AxeLv : 1f;
    float ToolDmgMul(string id) => id == "axe" ? 1f + AxeDmgStep * AxeLv : 1f;
    public int ToolLv(string id) => id == "axe" ? AxeLv : 0;

    /// <summary>Цена перехода на уровень next (1..5) для оружия данного тира.</summary>
    public static int[][] WeaponUpCost(int tier, int next) => tier <= 1 ? C(It.Copper, 2 * next, It.Wood, 2 + next)
        : tier == 2 ? C(It.Iron, 2 * next, It.Copper, next + 1)
        : C(It.Silver, 2 * next, It.Crystal, next);

    public static int[][] AxeUpCost(int next) => next <= 1 ? C(It.Wood, 8, It.Stone, 6) : next == 2 ? C(It.Copper, 4, It.Wood, 8, It.Stone, 8) : next == 3 ? C(It.Iron, 5, It.Wood, 10) : C(It.Silver, 5, It.Iron, 5);

    public int[][] NextWeaponUpCost(int idx) => idx >= 0 && idx < Owned.Count && Owned[idx].Lv < WeaponMaxLv ? WeaponUpCost(Db.Weapon(Owned[idx].Id).Tier, Owned[idx].Lv + 1) : null;
    public int[][] NextAxeUpCost => AxeLv < AxeMaxLv ? AxeUpCost(AxeLv + 1) : null;

    public bool NearUpgrade() => HasFurnace && FurnaceNear(56, out _) || BuildingNear(PPos, 56) is { Type: Tl.Forge };

    bool NearForge(string what)
    {
        if (FurnaceNear(56, out _) || BuildingNear(PPos, 56) is { Type: Tl.Forge }) return true;
        Fail($"Улучшать {what} можно только рядом с печью или кузницей"); return false;
    }

    void UpgradeDone(string title, string gain)
    {
        Diag.Mark("first_tool_upgrade"); GiveXp(25); Shake = 3; Burst(PPos, new Color("#ffe27a"), 14, 80); Sfx.Play("upgrade"); Announce(title, gain, new Color("#ffe27a"), "upgrade"); Save();
    }

    public bool UpgradeWeapon(int idx)
    {
        if (idx < 0 || idx >= Owned.Count) return false; var ow = Owned[idx]; var d = Db.Weapon(ow.Id);
        if (ow.Lv >= WeaponMaxLv) { Fail("Оружие уже улучшено до максимума"); return false; }
        if (!NearForge("оружие")) return false;
        var cost = WeaponUpCost(d.Tier, ow.Lv + 1); if (!Can(cost)) { Fail($"Для улучшения нужно: {NeedStr(cost)}"); return false; }
        Pay(cost); ow.Lv++; UpgradeDone($"{d.Name} +{ow.Lv}", $"урон {WeaponDmg(d):0.#}"); return true;
    }

    public bool UpgradeAxe()
    {
        if (AxeLv >= AxeMaxLv) { Fail("Топор уже улучшен до максимума"); return false; }
        if (!NearForge("топор")) return false;
        var cost = AxeUpCost(AxeLv + 1); if (!Can(cost)) { Fail($"Для улучшения нужно: {NeedStr(cost)}"); return false; }
        Pay(cost); AxeLv++; UpgradeDone($"Топор +{AxeLv}", "добыча дерева быстрее, удар сильнее"); return true;
    }

    public bool UpgradePickaxe() { int t = Tier; UpgradePick(); return Tier > t; }
}
