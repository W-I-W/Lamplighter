using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Хотбар: по слоту на каждый инструмент и ещё один слот под оружие (кирка, топор, одно оружие). Слот хранит стабильную ссылку («tool:axe», «weapon:sword1»), а не копию предмета.
/// В хотбаре может быть только одно оружие: созданное оружие надевается в окне экипировки (или перетаскиванием в слот) и заменяет прежнее.
/// Выбор слота (1–3, колесо мыши, V) делает предмет «в руках»: инструмент — CurTool, оружие — CurW. Использование — прежняя система Act (ЛКМ).</summary>
public partial class Game
{
    public const int HotCount = 3;
    public readonly string[] Hot = new string[HotCount];
    public int HotSel;
    public readonly HashSet<string> OwnedTools = new() { "pickaxe" };
    public string CurTool = "pickaxe";
    public ToolDef CurToolDef => ToolDef.Get(CurTool);

    public static string RefTool(string id) => "tool:" + id;
    public static string RefWeapon(string id) => "weapon:" + id;
    public static bool IsToolRef(string r) => r != null && r.StartsWith("tool:");
    public static bool IsWeaponRef(string r) => r != null && r.StartsWith("weapon:");
    static string RefId(string r) => r.Substring(r.IndexOf(':') + 1);

    /// <summary>В хотбар можно ставить только инструменты и оружие, которые у игрока есть (дерево, камень, руда, постройки и чертежи отсекаются).</summary>
    public bool HotValid(string r)
    {
        if (string.IsNullOrEmpty(r)) return false;
        if (IsToolRef(r)) return OwnedTools.Contains(RefId(r));
        if (IsWeaponRef(r)) return HasWeapon(RefId(r));
        return false;
    }

    public string HotName(string r) => IsToolRef(r) ? ToolDef.Get(RefId(r)).Name : IsWeaponRef(r) ? Db.Weapon(RefId(r))?.Name ?? "" : "";

    /// <summary>Значок предмета хотбара: инструменты — свои, оружие — по классу.</summary>
    public int HotIcon(string r)
    {
        if (IsToolRef(r)) return ToolDef.Get(RefId(r)).Icon;
        if (IsWeaponRef(r)) { var w = Db.Weapon(RefId(r)); return w == null ? UiIcons.Sword : w.Cls == WClass.Bow ? UiIcons.Bow : w.Cls == WClass.Staff ? UiIcons.Staff : UiIcons.Sword; }
        return -1;
    }

    public int HotSlotOf(string r) { for (int i = 0; i < HotCount; i++) if (Hot[i] == r) return i; return -1; }
    int HotWeaponSlot() { for (int i = 0; i < HotCount; i++) if (IsWeaponRef(Hot[i])) return i; return -1; }
    int HotFreeSlot() { for (int i = 0; i < HotCount; i++) if (Hot[i] == null) return i; return -1; }

    /// <summary>Назначение предмета в слот. Предмет не дублируется: из другого слота он переносится, а прежний жилец слота меняется с ним местами.
    /// Оружие в хотбаре одно: новое вытесняет прежнее.</summary>
    public bool HotAssign(int slot, string r)
    {
        if (slot < 0 || slot >= HotCount) return false;
        if (!HotValid(r)) { Fail("В хотбар можно поставить только инструмент или оружие"); return false; }
        int old = HotSlotOf(r); if (old == slot) return true;
        string displaced = Hot[slot]; bool swapOut = displaced != null && !(IsWeaponRef(displaced) && IsWeaponRef(r));     // оружие оружие не меняет, а заменяет
        if (IsWeaponRef(r)) for (int i = 0; i < HotCount; i++) if (i != old && i != slot && IsWeaponRef(Hot[i])) Hot[i] = null;
        Hot[slot] = r;
        if (old >= 0) Hot[old] = swapOut ? displaced : null;
        else if (swapOut) { int f = HotFreeSlot(); if (f >= 0) Hot[f] = displaced; }
        if (IsWeaponRef(displaced) && IsWeaponRef(r) && CurW >= 0 && CurW < Owned.Count && Owned[CurW].Id == RefId(displaced)) CurW = -1;
        if (!HotValid(Hot[HotSel])) { CurTool = "pickaxe"; EquipNone(); } else if (HotSel == slot || HotSel == old) ApplyHot();
        Sfx.Play("tick", -4, 1.3f); return true;
    }

    public void HotClear(int slot)
    {
        if (slot < 0 || slot >= HotCount || Hot[slot] == null) return;
        bool held = slot == HotSel; Hot[slot] = null;
        if (held) { CurTool = "pickaxe"; EquipNone(); }
        Sfx.Play("tick", -4, .8f);
    }

    /// <summary>Новый предмет встаёт в свободный слот; оружие — только если в хотбаре ещё нет оружия (остальные надеваются вручную).</summary>
    void AutoAssign(string r)
    {
        if (!HotValid(r) || HotSlotOf(r) >= 0) return;
        if (IsWeaponRef(r) && HotWeaponSlot() >= 0) return;
        int f = HotFreeSlot(); if (f >= 0) Hot[f] = r;
    }

    /// <summary>Надеть предмет: ставит его в хотбар (оружие заменяет прежнее) и берёт в руки.</summary>
    public void HotEquip(string r)
    {
        if (!HotValid(r)) return;
        int s = HotSlotOf(r);
        if (s < 0) { s = IsWeaponRef(r) ? HotWeaponSlot() : -1; if (s < 0) s = HotFreeSlot(); if (s < 0) s = HotCount - 1; HotAssign(s, r); s = HotSlotOf(r); }
        if (s >= 0) HotSelect(s);
    }

    /// <summary>Выбор слота: предмет берётся в руки. Пустой слот игнорируется.</summary>
    public bool HotSelect(int slot)
    {
        if (slot < 0 || slot >= HotCount || !HotValid(Hot[slot])) return false;
        HotSel = slot; ApplyHot(); return true;
    }

    public void HotCycle(int d)
    {
        for (int n = 1; n <= HotCount; n++)
        {
            int i = ((HotSel + d * n) % HotCount + HotCount) % HotCount;
            if (HotValid(Hot[i])) { HotSel = i; ApplyHot(); Sfx.Play("tick", -6, 1.4f); return; }
        }
    }

    void EquipNone() { CurW = -1; MineProg = 0; RecomputeEffects(); }

    void ApplyHot()
    {
        var r = Hot[HotSel]; if (!HotValid(r)) return;
        if (IsToolRef(r)) { CurTool = RefId(r); EquipNone(); }
        else { int i = Owned.FindIndex(o => o.Id == RefId(r)); if (i >= 0) { CurW = i; MineProg = 0; RecomputeEffects(); } }
        
    }

    /// <summary>Выбор предмета вне хотбара: слот выбирается вслед за предметом.</summary>
    void HotFollow()
    {
        string r = CurW >= 0 && CurW < Owned.Count ? RefWeapon(Owned[CurW].Id) : RefTool(CurTool);
        int s = HotSlotOf(r); if (s >= 0) HotSel = s;
    }

    /// <summary>Начальное заполнение: кирка, затем надетое оружие и инструменты (новые игры и старые сохранения). В хотбаре остаётся одно оружие.</summary>
    void InitHotbar()
    {
        bool any = false; foreach (var h in Hot) if (h != null) any = true;
        string cur = CurW >= 0 && CurW < Owned.Count ? RefWeapon(Owned[CurW].Id) : null;
        for (int i = 0; i < HotCount; i++) if (!HotValid(Hot[i])) Hot[i] = null;
        if (!any)
        {
            AutoAssign(RefTool("pickaxe")); AutoAssign(cur ?? (Owned.Count > 0 ? RefWeapon(Owned[0].Id) : null));
            foreach (var t in OwnedTools) AutoAssign(RefTool(t));
        }
        int keep = cur != null ? HotSlotOf(cur) : -1; if (keep < 0) keep = HotWeaponSlot();
        for (int i = 0; i < HotCount; i++) if (IsWeaponRef(Hot[i]) && i != keep) Hot[i] = null;
        if (HotWeaponSlot() < 0 && Owned.Count > 0) AutoAssign(cur ?? RefWeapon(Owned[0].Id));
        if (CurW >= 0 && CurW < Owned.Count && HotSlotOf(RefWeapon(Owned[CurW].Id)) < 0) CurW = -1;      // оружие, которого нет в хотбаре, в руках не держим
        HotFollow();
    }

    // ───────────────────────── строки окна экипировки (вкладка «Оружие»): инструменты, затем оружие ─────────────────────────
    public List<string> ToolList() { var l = new List<string>(); foreach (var id in ToolDef.Ids) if (OwnedTools.Contains(id)) l.Add(id); return l; }

    /// <summary>Ссылка на предмет в строке окна экипировки (инструмент или оружие).</summary>
    public string EquipRef(int row) { var tl = ToolList(); if (row < tl.Count) return RefTool(tl[row]); int w = row - tl.Count; return w >= 0 && w < Owned.Count ? RefWeapon(Owned[w].Id) : null; }

    void HoldEquipRow(int row) { var r = EquipRef(row); if (r != null) { HotEquip(r); Sfx.Play("tick", 0, 1.4f); } }

    /// <summary>Создание инструмента (ручной крафт): появляется в списке и сам встаёт в свободный слот хотбара.</summary>
    public bool CraftTool(string id, int[][] cost)
    {
        if (OwnedTools.Contains(id)) { Toast("Этот инструмент уже есть"); return false; }
        if (!Can(cost)) { Fail("Не хватает: " + NeedStr(cost)); return false; }
        Pay(cost); OwnedTools.Add(id); AutoAssign(RefTool(id)); GiveXp(10); Sfx.Play("build"); Announce("Инструмент: " + ToolDef.Get(id).Name, ToolDef.Get(id).Desc, new Color("#ffd23f"), "upgrade"); Save(); return true;
    }
}
