using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Интеграция единой системы взаимодействия: сбор интерактивных объектов вокруг игрока и запуск InteractionController.</summary>
public partial class Game
{
    public readonly InteractionController Interact = new();
    public const float TipDelay = .5f;
    float tipHoverT; string tipHoverKey; public bool TipReady;
    internal IInteractable TooltipTarget { get; private set; }

    /// <summary>Показывать подсказку для объекта под курсором после небольшой задержки.</summary>
    void UpdateTip(float dt)
    {
        string key = TooltipTarget?.Key;
        if (key != tipHoverKey) { tipHoverKey = key; tipHoverT = 0f; }
        tipHoverT = key != null ? tipHoverT + dt : 0f;
        TipReady = key != null && tipHoverT >= TipDelay;
    }

    internal Ix HoverTip()
    {
        var target = TooltipTarget;
        if (!TipReady || target == null) return null;
        var ix = new Ix { Title = target.Title, Key = target.Key };
        if (InteractionController.Distance(PPos, target.Bounds) > InteractionController.Reach)
            ix.Info("Подойдите ближе для взаимодействия");
        else if (!target.Key.StartsWith("resource:") && Interact.Selected?.Key != target.Key)
            ix.Info("ЛКМ — выбрать объект для действий");
        target.Describe(ix);
        return ix;
    }
    public bool OverUi; public float DetailT;                           // курсор над интерфейсом: мир не получает клик

    /// <summary>Объект мира, описываемый замыканием. Действия берутся из существующей логики (BuildingIx, TileIx, BeaconIx…).</summary>
    sealed class Target : IInteractable
    {
        public string Key { get; init; } public string Title { get; init; } public Rect2 Bounds { get; init; } public int Priority { get; init; }
        public Action<Ix> Fill;
        public void Describe(Ix ix) => Fill(ix);
    }

    static Rect2 TileRect(int x, int y) => new(x * TS, y * TS, TS, TS);

    List<IInteractable> CollectInteractables(bool forHover = false)
    {
        var list = new List<IInteractable>();
        Vector2 center = forHover ? Aim : PPos;
        void Add(string key, string title, Rect2 r, int prio, Action<Ix> fill, bool test = true)
        {
            if (test && !forHover) { var probe = new Ix(); fill(probe); if (probe.Lines.Count == 0 && probe.E == null && probe.U == null && probe.X == null) return; }
            list.Add(new Target { Key = key, Title = title, Bounds = r, Priority = prio, Fill = fill });
        }
        // древние фонари и Центральный маяк
        for (int i = 0; i < W.Ancients.Count; i++)
        {
            var a = W.Ancients[i]; int idx = i;
            if (a.Pos.DistanceTo(center) > 80) continue;
            Add("a:" + i, i == 0 ? "Центральный маяк" : a.Boss ? "Фонарь стража" : LampKindName(a.Kind), TileRect(a.Tile.X, a.Tile.Y), 0, ix =>
            {
                if (idx == 0) { BeaconIx(ix); return; }
                if (a.Lit) { if (FastOk(a)) { ix.Lines.Add("Древний фонарь горит"); FastTravelIx(a, ix); } return; }
                if (a.Boss && !BossDown(a.BossId)) ix.Lines.Add("Фонарь стража: победите стража, чтобы зажечь его");
                else if (!LampReady(a, out var why) && a.Kind != LampKind.Damaged) ix.Lines.Add(why);
                else { ix.Action("E", a.Kind == LampKind.Damaged ? "Починить и зажечь фонарь" : "Зажечь древний фонарь"); if (a.Kind == LampKind.Damaged) foreach (var (it, n) in RepairCost) ix.Need((It.Ic(it) + It.Names[it], n, Inv[it])); if (a.Cost > 0) ix.Need((It.Ic(It.Shard) + It.Names[It.Shard], a.Cost, Inv[It.Shard])); ix.Gives("Даст", "Свет и новую территорию", TipRole.Key); ix.E = () => Activate(a, idx); }
            });
        }
        // объекты-тайлы: сундуки, святилища, зеркала, печь и т.д.
        var c = ToTile(center);
        for (int y = c.Y - 3; y <= c.Y + 3; y++)
            for (int x = c.X - 3; x <= c.X + 3; x++)
            {
                var t = W.Get(x, y); int tx = x, ty = y;
                if (forHover && t == Tl.None && IsPlantTile(W.Get(x, y + 1)))
                {
                    var tree = W.Get(x, y + 1);
                    Add($"resource:{x}:{y + 1}", ResourceTitle(tree), TileRect(x, y), 10, _ => { }, false);
                }
                if (t == Tl.Furnace) { var at = TileCenter(x, y); Add($"t:{x}:{y}", "Печь", TileRect(x, y), 5, ix => { ix.Action("E", "Печь: плавка руды"); ix.Gives("Плавка", "слиток = 1 руда + 1 дерево", TipRole.Resource); ix.E = () => OpenStation("furnace"); if (IsPlacedTile(tx, ty)) AddDemolishTile(tx, ty, ix); }); continue; }
                if (t == Tl.Workbench) { Add($"t:{x}:{y}", "Рабочий стол", TileRect(x, y), 5, ix => { ix.Action("E", "Рабочий стол: постройки и сборка"); ix.Gives("Создаёт", "постройки, доски, рукояти, инструменты и оружие", TipRole.Resource); ix.E = () => OpenBench(tx, ty); if (IsPlacedTile(tx, ty)) AddDemolishTile(tx, ty, ix); }); continue; }
                if (t == Tl.StorageChest) { Add($"t:{x}:{y}", "Сундук", TileRect(x, y), 5, ix => { ix.Action("E", "Открыть сундук"); ix.Gives("Хранит", "20 слотов по 99", TipRole.Resource); ix.E = () => OpenChest(tx, ty); if (IsPlacedTile(tx, ty)) AddDemolishTile(tx, ty, ix); }); continue; }
                if (t != Tl.Mirror1 && t != Tl.Mirror2 && IsPlacedTile(x, y)) { Add($"t:{x}:{y}", PlacedTitle(t), TileRect(x, y), 1, ix => AddDemolishTile(tx, ty, ix)); continue; }
                if (!IsTileInteractable(t))
                {
                    if (forHover && Mineable(t)) Add($"resource:{x}:{y}", ResourceTitle(t), TileRect(x, y), 10, _ => { }, false);
                    continue;
                }
                Add($"t:{x}:{y}", TileTitle(t), TileRect(x, y), 1, ix => { TileIx(tx, ty, W.Get(tx, ty), ix); if (IsPlacedTile(tx, ty)) AddDemolishTile(tx, ty, ix); });
            }
        // события (торговец, разлом, путник, осаждённый фонарь)
        foreach (var ev in Events)
        {
            var e = ev; var p = ev.Kind == "traveler" ? ev.Npc : ev.Pos;
            if (p.DistanceTo(center) > 80) continue;
            Add($"e:{ev.Seed}:{ev.Kind}", EvName(ev.Kind), new Rect2(p - new Vector2(8, 8), new Vector2(16, 16)), 2, ix => EventIx1(e, ix));
        }
        // жители деревни: поговорить [E]
        foreach (var v in Villagers)
        {
            if (v.Pos.DistanceTo(center) > 80) continue; var vv = v;
            Add($"v:{v.Id}", v.Name, new Rect2(v.Pos - new Vector2(8, 10), new Vector2(16, 16)), 2, ix => VillagerIx(vv, ix), false);
        }
        // фонари игрока
        foreach (var l in Lamps)
        {
            if (l.Pos.DistanceTo(center) > 80) continue; var lamp = l;
            Add($"l:{l.X}:{l.Y}", LampNames[l.Kind], TileRect(l.X, l.Y), 3, ix => LampIx(lamp, ix), false);
        }
        // здания деревни
        foreach (var b in Buildings)
        {
            if (b.Pos.DistanceTo(center) > 80) continue; var bb = b;
            list.Add(new Target { Key = $"b:{b.X}:{b.Y}", Title = BNames[BIndex(b.Type)], Bounds = TileRect(b.X, b.Y), Priority = 4, Fill = ix => BuildingIx(bb, ix) });
        }
        return list;
    }

    static string ResourceTitle(Tl t) => t switch
    {
        Tl.Tree => "Дерево", Tl.Pine => "Сосна", Tl.DeadTree => "Сухое дерево", Tl.Stone => "Камень",
        Tl.Copper => "Медная руда", Tl.Iron => "Железная руда", Tl.SilverOre => "Серебряная руда",
        Tl.CrystalOre => "Кристалл", Tl.Lumen => "Осколки света", Tl.Aether => "Эфир",
        Tl.Obsidian => "Обсидиан", Tl.Ember => "Уголь пепла", Tl.FrostOre => "Ледяной кристалл",
        Tl.MeteorOre => "Световой метеорит", Tl.Ruin => "Руины", Tl.Rubble => "Обломки",
        Tl.IceBlock => "Лёд", Tl.ShadowPillar => "Теневой столб", Tl.Grave => "Могила",
        Tl.BossTotem => "Тотем стража", Tl.ChestOpen => "Открытый сундук", _ => "Объект"
    };

    static string PlacedTitle(Tl t) => TileStockKey(t) switch { "amp" => "Светокристалл", "barrier" => "Столб-барьер", "decor0" => "Статуя Фонарщика", "decor1" => "Фонтан света", "decor2" => "Знамя маяка", "chest" => "Сундук", "workbench" => "Рабочий стол", _ => "Постройка" };

    static bool IsTileInteractable(Tl t) => t == Tl.Chest || t == Tl.Shrine || t == Tl.Obelisk || t == Tl.Cage || t == Tl.Machine || t == Tl.MachineOn || t == Tl.Mirror1 || t == Tl.Mirror2
        || t == Tl.BridgeBroken || t == Tl.Seal || t == Tl.Receiver || t == Tl.ReceiverOn || t == Tl.TechCache || t == Tl.Spire || t == Tl.Altar || t == Tl.AltarOn
        || t == Tl.Tablet || t == Tl.FalseLantern || t == Tl.Pylon || t == Tl.PylonOn || t == Tl.RiftGate || t == Tl.House || t == Tl.Tent;

    static string TileTitle(Tl t) => t switch
    {
        Tl.Chest => "Сундук", Tl.Shrine => "Святилище света", Tl.Obelisk => "Обелиск", Tl.Cage => "Клетка с пленником", Tl.Machine or Tl.MachineOn => "Механизм древних",
        Tl.Mirror1 or Tl.Mirror2 => "Кристальное зеркало", Tl.BridgeBroken => "Разрушенный мост", Tl.Seal => "Печать", Tl.Receiver or Tl.ReceiverOn => "Приёмник света",
        Tl.TechCache => "Постамент", Tl.Spire => "Шпиль света", Tl.Altar or Tl.AltarOn => "Алтарь лучей", Tl.Tablet => "Записка", Tl.FalseLantern => "Огонёк",
        Tl.Pylon or Tl.PylonOn => "Древнее светило", Tl.RiftGate => "Врата разломов", Tl.Grave => "Могила", Tl.BossTotem => "Тотем стража", Tl.BossGate => "Врата арены", _ => "Объект"
    };

    /// <summary>Один проход системы: сбор объектов, выбор, подготовка Cur для клавиш E/U и панели.</summary>
    void RunInteraction()
    {
        bool enabled = Window == Win.None && !TechOpen && !MapOpen && Menu == Mn.None && FinStage == 0;
        TooltipTarget = null;
        if (RiftOn) { Interact.Reset(); var rix = new Ix(); RiftIx(rix); Cur = rix; return; }
        var cands = enabled ? CollectInteractables() : new List<IInteractable>();
        Interact.Update(PPos, enabled ? Aim : null, OverUi, cands, enabled);
        if (enabled && !OverUi)
        {
            float closest = float.MaxValue;
            foreach (var candidate in CollectInteractables(true))
                if (candidate.Bounds.Grow(InteractionController.HoverPad).HasPoint(Aim))
                {
                    float distance = Aim.DistanceSquaredTo(candidate.Bounds.GetCenter());
                    if (distance < closest) { closest = distance; TooltipTarget = candidate; }
                }
        }
        var ix = new Ix();
        if (Interact.Selected != null) { Interact.Selected.Describe(ix); ix.Title ??= Interact.Selected.Title; ix.Key = Interact.Selected.Key; }
        Cur = ix;
    }

    public Ix FindIx() { RunInteraction(); return Cur; }

    /// <summary>ЛКМ по миру: закрепить наведённый объект (вызывается из _Input, если курсор не над интерфейсом).</summary>
    void ClickWorld()
    {
        if (Window != Win.None || TechOpen || MapOpen || Menu != Mn.None || OverUi) return;
        Interact.Click();
    }

    /// <summary>Только недостающие ресурсы: «Дерево 3/10 · Камень 0/5».</summary>
    string MissingStr(int[][] cost)
    {
        var l = new List<string>(); foreach (var c in cost) { int have = HaveAny(c[0]); if (have < c[1]) l.Add($"{It.Ic(c[0])}{It.Names[c[0]]} {have}/{c[1]}"); }
        return string.Join(" · ", l);
    }

    /// <summary>То же без значков: «Кристалл 2/4 · Серебро 0/3» (для подписей в окнах).</summary>
    public string MissingPlain(int[][] cost)
    {
        var l = new List<string>(); foreach (var c in cost) { int have = HaveAny(c[0]); if (have < c[1]) l.Add($"{Loc.T(It.Names[c[0]])} {have}/{c[1]}"); }
        return string.Join(" · ", l);
    }

    /// <summary>Курсор над любым элементом интерфейса (панели HUD, меню строительства, инвентарь, окна).</summary>
    public bool UiOver(Vector2 logical) => (hud != null && hud.IsOverUi(logical)) || (GameHudNode != null && GameHudNode.IsOver(logical, UiScaleVp)) || (benchWindow != null && benchWindow.IsOver(logical)) || (upgradeWindow != null && upgradeWindow.IsOver(logical)) || (equipWindow != null && equipWindow.IsOver(logical)) || (chestWindow != null && chestWindow.IsOver(logical)) || (invWindow != null && invWindow.IsOver(logical));
    public void PressWindowKey(Key k) => WindowKey(k, false);
    public string NeedText(int[][] cost) => NeedStr(cost);
    public int HaveOf(int item) => HaveAny(item);
    public string NeedShortText(int[][] cost) => NeedShort(cost);
    public string MissingText(int[][] cost) => MissingStr(cost);
}
