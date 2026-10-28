using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Единый хотбар из 8 слотов: в нём только инструменты и оружие. Слот хранит стабильную ссылку («tool:axe», «weapon:sword1»), а не копию предмета.
/// Выбор слота (1–8, колесо мыши, V) делает предмет «в руках»: инструмент — CurTool, оружие — CurW. Использование — прежняя система Act (ЛКМ).</summary>
public partial class Game
{
    public const int HotCount = 8;
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

    /// <summary>Назначение предмета в слот; предмет не дублируется (если уже лежит в другом слоте — переносится).</summary>
    public bool HotAssign(int slot, string r)
    {
        if (slot < 0 || slot >= HotCount) return false;
        if (!HotValid(r)) { Fail("В хотбар можно поставить только инструмент или оружие"); return false; }
        int old = HotSlotOf(r); if (old >= 0) Hot[old] = null;
        Hot[slot] = r; if (slot == HotSel) ApplyHot();
        Sfx.Play("tick", -4, 1.3f); return true;
    }

    public void HotClear(int slot)
    {
        if (slot < 0 || slot >= HotCount || Hot[slot] == null) return;
        Hot[slot] = null; if (slot == HotSel) { CurTool = "pickaxe"; EquipNone(); }
        Sfx.Play("tick", -4, .8f);
    }

    void AutoAssign(string r) { if (!HotValid(r) || HotSlotOf(r) >= 0) return; for (int i = 0; i < HotCount; i++) if (Hot[i] == null) { Hot[i] = r; return; } }

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
        Toast(HotName(r));
    }

    /// <summary>Выбор предмета вне хотбара (окно экипировки): слот выбирается вслед за предметом.</summary>
    void HotFollow()
    {
        string r = CurW >= 0 && CurW < Owned.Count ? RefWeapon(Owned[CurW].Id) : RefTool(CurTool);
        int s = HotSlotOf(r); if (s >= 0) HotSel = s;
    }

    /// <summary>Начальное заполнение: кирка в первом слоте, имеющееся оружие — следом (для новых игр и старых сохранений).</summary>
    void InitHotbar()
    {
        bool any = false; foreach (var h in Hot) if (h != null) any = true;
        if (!any)
        {
            AutoAssign(RefTool("pickaxe")); foreach (var o in Owned) AutoAssign(RefWeapon(o.Id));
            foreach (var t in OwnedTools) AutoAssign(RefTool(t));
        }
        for (int i = 0; i < HotCount; i++) if (!HotValid(Hot[i])) Hot[i] = null;
        HotFollow();
    }

    // ───────────────────────── строки окна экипировки (вкладка «Оружие»): инструменты, затем оружие ─────────────────────────
    public List<string> ToolList() { var l = new List<string>(); foreach (var id in ToolDef.Ids) if (OwnedTools.Contains(id)) l.Add(id); return l; }

    /// <summary>Ссылка на предмет в строке окна экипировки (инструмент или оружие).</summary>
    public string EquipRef(int row) { var tl = ToolList(); if (row < tl.Count) return RefTool(tl[row]); int w = row - tl.Count; return w >= 0 && w < Owned.Count ? RefWeapon(Owned[w].Id) : null; }

    void HoldEquipRow(int row)
    {
        var tl = ToolList();
        if (row < tl.Count) { CurTool = tl[row]; EquipNone(); HotFollow(); Sfx.Play("tick", 0, 1.4f); }
        else EquipWeapon(row - tl.Count);
    }

    /// <summary>Создание инструмента (ручной крафт): появляется в списке и сам встаёт в свободный слот хотбара.</summary>
    public bool CraftTool(string id, int[][] cost)
    {
        if (OwnedTools.Contains(id)) { Toast("Этот инструмент уже есть"); return false; }
        if (!Can(cost)) { Fail("Не хватает: " + NeedStr(cost)); return false; }
        Pay(cost); OwnedTools.Add(id); AutoAssign(RefTool(id)); GiveXp(10); Sfx.Play("build"); Announce("Инструмент: " + ToolDef.Get(id).Name, ToolDef.Get(id).Desc, new Color("#ffd23f"), "upgrade"); Save(); return true;
    }
}
