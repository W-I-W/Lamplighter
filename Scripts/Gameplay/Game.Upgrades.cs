using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public enum TechState { Owned, Available, Missing, Locked }

/// <summary>Окно улучшений: состояние выбора, состояния улучшений, изучение и связь с UpgradeProgressionSystem. Рисует окно UpgradeWindowUI (Scenes/UI/UpgradeWindow.tscn).</summary>
public partial class Game
{
    public UpgradeProgressionSystem Upgrades;
    public readonly HashSet<string> TabAnim = new();         // вкладки, открытые за время игры: при открытии окна появляются с анимацией
    public float TechLearnT; public int TechLearned = -1;   // всплеск после успешного изучения (для анимаций окна)

    /// <summary>Биом обнаружен: в нём постоянно зажжён хотя бы один древний фонарь (не Центральный маяк).</summary>
    public bool BiomeDiscovered(int region)
    {
        foreach (var a in W.Ancients) if (a.Region == region && a.Lit && a != W.Ancients[0]) return true;
        return false;
    }

    void InitUpgrades(WorldSave d)
    {
        Upgrades = new UpgradeProgressionSystem(this);
        Upgrades.Restore(d?.UpCats, d?.UpNew);
        Upgrades.CategoryUnlocked += OnCategoryUnlocked;
    }

    void OnCategoryUnlocked(UpgradeCategoryData c)
    {
        TabAnim.Add(c.Id);
        for (int i = 0; i < Techs.Length; i++) if (Techs[i].Page == c.Page && Techs[i].Ok(this)) Ann |= 1L << (12 + i);          // об этих улучшениях уже сказано в уведомлении категории
        Announce("Открыта категория: " + c.Title, "Меню улучшений [T]", new Color("#ffd23f"), "upgrade");
        Burst(PPos, new Color("#ffd23f"), 26, 110); Flash = Mathf.Max(Flash, .12f);
    }

    /// <summary>Улучшения текущей категории (включая награды) в порядке данных.</summary>
    public List<int> TechList() { var c = Upgrades.ByPage(TechPage); return c == null ? new List<int>() : PageList(c.Page); }

    /// <summary>Сколько улучшений изучено и сколько всего — только в открытых категориях (закрытые игрок не видит).</summary>
    public (int own, int total) TechTotals()
    {
        int own = 0, total = 0;
        for (int i = 0; i < Techs.Length; i++) if (Upgrades.IsPageUnlocked(Techs[i].Page)) { total++; if (Has((Tech)i)) own++; }
        return (own, total);
    }

    public void OpenTech(bool open)
    {
        CloseWindows(); TechOpen = open; if (!open) return;
        if (!Upgrades.IsPageUnlocked(TechPage)) { var v = Upgrades.Visible(); TechPage = v.Count > 0 ? v[0].Page : 0; }
        EnsureTechSel(); Upgrades.MarkSeen(Upgrades.ByPage(TechPage)?.Id ?? "");
    }

    /// <summary>Переключение категории среди открытых.</summary>
    public void SelectTechPage(int page)
    {
        if (!Upgrades.IsPageUnlocked(page) || page == TechPage) return;
        TechPage = page; EnsureTechSel(); Upgrades.MarkSeen(Upgrades.ByPage(page).Id); Sfx.Play("tick", -4, 1.2f);
    }

    public void TechCycle(int dir)
    {
        var v = Upgrades.Visible(); if (v.Count < 2) return;
        int k = v.FindIndex(c => c.Page == TechPage); k = ((k + dir) % v.Count + v.Count) % v.Count; SelectTechPage(v[k].Page);
    }

    /// <summary>Выбранный навык должен лежать в текущей категории: иначе выбирается первый (корень).</summary>
    public void EnsureTechSel() { var l = TechList(); if (l.Count == 0) { TechSel = -1; return; } if (TechSel < 0 || Techs[TechSel].Page != Upgrades.ByPage(TechPage)?.Page) TechSel = l[0]; }

    /// <summary>Соседний навык в направлении (dx, dy): ближайший по оси с небольшим штрафом за боковое смещение.</summary>
    public int TechNeighbour(int from, Vector2 dir)
    {
        var p0 = SkillOf(from).Pos; int best = -1; float bs = float.MaxValue;
        foreach (var i in TechList())
        {
            if (i == from) continue; var d = SkillOf(i).Pos - p0; float along = d.Dot(dir), side = Mathf.Abs(d.Dot(new Vector2(-dir.Y, dir.X)));
            if (along < .4f) continue; float sc = along + 1.6f * side; if (sc < bs) { bs = sc; best = i; }
        }
        return best;
    }

    /// <summary>Клавиши окна: стрелки — по дереву, Tab/PgUp/PgDn — категории, Enter/E/Space — улучшить на один уровень, Home — к выбранному.</summary>
    bool TechKey(Key k)
    {
        EnsureTechSel();
        switch (k)
        {
            case Key.Tab: TechCycle(Input.IsKeyPressed(Key.Shift) ? -1 : 1); return true;
            case Key.Pagedown: TechCycle(1); return true;
            case Key.Pageup: TechCycle(-1); return true;
            case Key.Left: case Key.Right: case Key.Up: case Key.Down:
                { var dir = k == Key.Left ? Vector2.Left : k == Key.Right ? Vector2.Right : k == Key.Up ? Vector2.Up : Vector2.Down; int n = TechSel < 0 ? -1 : TechNeighbour(TechSel, dir); if (n >= 0) { TechSel = n; Sfx.Play("tick", -8, 1.5f); } return true; }
            case Key.Home: TechView.Remove(TechPage); return true;
            case Key.Enter: case Key.KpEnter: case Key.E: case Key.Space: if (TechSel >= 0) LearnTech(TechSel); return true;
        }
        return false;
    }
}
