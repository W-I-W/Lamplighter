using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Мост между новым окном экипировки и игрой: окно не меняет состояние само, а вызывает те же команды, что и клавиши (HoldEquipRow, HotAssign, InstallMod, RemoveMod, ToggleRelic, CraftRecipe, UpgradeHeroLamp).
/// Здесь только выбор ячейки вставки и переключатель «Показать все» — состояние интерфейса, которое не сохраняется.</summary>
public partial class Game
{
    public int EqSocket = -1, EqPick = -1; public bool EqShowAll;
    public EquipWindowUI equipWindow;

    public void EqTab(int tab) { if (tab < 0 || tab > 3 || tab == WinTab) return; WinTab = tab; WinCur = 0; EqSocket = -1; EqPick = -1; Sfx.Play("tick", -6, 1.4f); }
    public void EqSelect(int row) { if (row == WinCur) return; WinCur = row; EqSocket = -1; EqPick = -1; Sfx.Play("tick", -8, 1.5f); }
    public void EqEquipRow(int row) { var r = EquipRef(row); if (r == null) return; WinCur = row; HoldEquipRow(row); EqSocket = -1; EqPick = -1; }
    public void EqHotAssign(int row, int slot) { var r = EquipRef(row); if (r != null) HotAssign(slot, r); }
    public void EqHotClear(int row) { var r = EquipRef(row); int s = r == null ? -1 : HotSlotOf(r); if (s >= 0) HotClear(s); }
    public bool EqIsHeld(int row) { var r = EquipRef(row); if (r == null) return false; return IsWeaponRef(r) ? CurW >= 0 && CurW < Owned.Count && RefWeapon(Owned[CurW].Id) == r : CurW < 0 && RefTool(CurTool) == r; }
    public void EqToggleRelic(string id) => ToggleRelic(id);
    public bool EqInstall(int modId) { bool ok = InstallMod(modId); if (ok) { EqPick = -1; EqSocket = -1; } return ok; }
    public void EqRemove(int slot) { RemoveMod(slot); EqSocket = -1; }
    public void EqCraftRow(int row) { var rows = CraftRows(); if (row < 0 || row >= rows.Count) return; WinCur = row; CraftRecipe(rows[row].r); }
    public void EqCraftQty(int row, int qty) { var rows = CraftRows(); if (row < 0 || row >= rows.Count) return; WinCur = row; CraftRecipe(rows[row].r, qty); }
    public void EqCraftMode(string g) { if (g == CraftMode || !CraftGroups().Contains(g)) return; CraftMode = g; WinCur = 0; CraftResetSel(); Sfx.Play("tick", -6, 1.4f); }
    public void EqCraftFilter(bool onlyReady) { if (onlyReady == CraftOnlyReady) return; CraftOnlyReady = onlyReady; WinCur = 0; CraftResetSel(); Sfx.Play("tick", -6, 1.4f); }
    public void EqCycleQty() { CraftQty = (CraftQty + 1) % QtySteps.Length; Sfx.Play("tick", -6, 1.3f); }
    public void EqUpgradeLamp() => UpgradeHeroLamp();

    /// <summary>Сумма эффектов вставок и реликвий (то же, что показывало старое окно).</summary>
    public string EffectsText()
    {
        var parts = new List<string>();
        string[] n = { "Поджог", "Замедл.", "Заморозка", "Молния", "Залп", "Пробой", "Сифон", "Взрыв", "Дуга" };
        for (int i = 0; i < (int)Fxk.Count; i++) if (EffP[i] > 0) parts.Add($"{Loc.T(n[i])} {EffP[i]:0.#}");
        return string.Join(" · ", parts);
    }

    /// <summary>Иконка вставки: берётся значок подходящего ресурса из атласа предметов (вставки отдельных значков не имеют).</summary>
    public static int ModIcon(int modId) => modId switch { 0 => It.Ember, 1 => It.Frost, 2 => It.Aether, 3 => It.Iron, 4 => It.Crystal, 5 => It.Shard, _ => It.Obsidian };
    public static Color ModTint(int modId) => modId switch { 0 => new Color("#ff9a2e"), 1 => new Color("#8fd8ff"), 2 => new Color("#d0b0ff"), 3 => new Color("#e0e0e0"), 4 => new Color("#bfe9ff"), 5 => new Color("#ffe27a"), _ => new Color("#ff7a3a") };
}
