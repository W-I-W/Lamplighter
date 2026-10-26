import re
def rw(p, f):
    s=open(p,encoding='utf-8').read(); s2=f(s); open(p,'w',encoding='utf-8').write(s2); return s!=s2

def game(s):
    s=s.replace('|| t == Tl.CrystalOre || t == Tl.SilverOre || t == Tl.Rubble || t == Tl.DeadTree || t == Tl.Obsidian || t == Tl.Ember;','|| t == Tl.CrystalOre || t == Tl.SilverOre || t == Tl.Rubble || t == Tl.DeadTree || t == Tl.Obsidian || t == Tl.Ember || t == Tl.MeteorOre;')
    s=s.replace('Tl.Iron => 1, Tl.Lumen => 2,','Tl.Iron => 1, Tl.MeteorOre => 1, Tl.Lumen => 2,')
    s=s.replace('Tl.Obsidian => new Color("#a890ff"), Tl.Ember => new Color("#ff9a2e"), _ => Colors.White','Tl.Obsidian => new Color("#a890ff"), Tl.Ember => new Color("#ff9a2e"), Tl.MeteorOre => new Color("#fff2b0"), _ => Colors.White')
    s=s.replace('Tl.Obsidian => 4.2f, Tl.Ember => 4f, _ => 4f };','Tl.Obsidian => 4.2f, Tl.Ember => 4f, Tl.MeteorOre => 3f, _ => 4f };')
    s=s.replace('            case Tl.Ember: Give(It.Ember, rng.RandiRange(1, 2), c); GiveXp(24); break;','            case Tl.Ember: Give(It.Ember, rng.RandiRange(1, 2), c); GiveXp(24); break;\n            case Tl.MeteorOre: Give(It.AncCrystal, 1, c); Give(It.Shard, 3, c + new Vector2(0, -6)); Give(It.Crystal, 1, c + new Vector2(0, -12)); GiveXp(40); Found.Add("Осколок светового метеорита"); break;')
    s=s.replace('        Pay(cost); Tier++;','        Pay(cost); Tier++;')
    # progress-based market refresh
    s=s.replace('        GiveXp(a.Boss ? 120 : 60); Shake = 5; Flash = .7f;','        GiveXp(a.Boss ? 120 : 60); Shake = 5; Flash = .7f; TradeEpoch++;')
    return s
print(rw('Game.cs',game))

def poi(s):
    s=s.replace('        Add("Построить печь [1]", HasFurnace, NeedStr(CostFurnace));','        Add("Построить печь [1]", HasFurnace, NeedStr(CostFurnace));\n        Add("Создать оружие [C]", Owned.Count > 0, "Вкладка «Создание» у печи: меч — Медь 4, Дерево 3; лук; посох");')
    s=s.replace('        Add("Построить склад [4]", HasBuilding(Tl.Warehouse), NeedStr(BCost(0, 1)));','        Add("Построить склад [4]", HasBuilding(Tl.Warehouse), NeedStr(BCost(0, 1)));\n        Add("Найти забытую технологию", BpDone.Contains("bp_hub"), "Светящийся постамент даёт чертёж «Логистика» (его можно и купить в лавке)", NearestPoi(PK.TechCache, true)?.Pos);')
    s=s.replace('        Add("Построить лесопилку [5]", HasBuilding(Tl.Sawmill), NeedStr(BCost(1, 1)));','        Add("Построить торговую лавку [B]", HasBuilding(Tl.Stall), rescued < 1 ? "Чертёж лавки дарит спасённый житель" : NeedStr(BCost(8, 1)));\n        Add("Построить лесопилку [5]", HasBuilding(Tl.Sawmill), NeedStr(BCost(1, 1)));')
    s=s.replace('        Add("Исследовать 4 места", PoisDone >= 4,','        Add("Подключить производство узлами [B]", HasBuilding(Tl.Hub) || !HasBuilding(Tl.Mine), BpDone.Contains("bp_hub") ? NeedStr(BCost(7, 1)) : "Нужен чертёж «Логистика» [J]");\n        Add("Построить цех переработки [B]", HasBuilding(Tl.Refinery), BpDone.Contains("bp_refinery") ? NeedStr(BCost(6, 1)) : "Нужен чертёж «Цех» [J]: шестерни и линзы для улучшений");\n        Add("Исследовать 4 места", PoisDone >= 4,')
    s=s.replace('        Add("Улучшить Центральный маяк", BeaconLevel >= 2,','        Add("Экипировать реликвию [C]", EquippedRelics.Count > 0, "Реликвии усиливают оружие и свет; синергии — в окне экипировки");\n        Add("Улучшить Центральный маяк", BeaconLevel >= 2,')
    return s
print(rw('Game.Poi.cs',poi))

def hud(s):
    s=s.replace('        Txt("Строить и создавать   [T] улучшения", new Vector2(cx + 8, cy + 16), 11, Dim);','        Txt("Строить   [B] все · [C] экип. · [J] журнал · [P] произв.", new Vector2(cx + 8, cy + 16), 11, Dim);')
    return s
print(rw('Hud.cs',hud))
