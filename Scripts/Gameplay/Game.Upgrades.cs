using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public enum TechState { Owned, Available, Missing, Locked }

/// <summary>Окно улучшений: состояние выбора, состояния улучшений, изучение и связь с UpgradeProgressionSystem. Рисует окно UpgradeWindowUI (Scenes/UI/UpgradeWindow.tscn).</summary>
public partial class Game
{
    public UpgradeProgressionSystem Upgrades;
    public int TechCur;                                      // выбранная карточка в текущей категории
    public readonly HashSet<string> TabAnim = new();         // вкладки, открытые за время игры: при открытии окна появляются с анимацией
    public float TechLearnT; public int TechLearned = -1;   // всплеск после успешного изучения (для анимаций окна)

    /// <summary>Биом обнаружен: в нём постоянно зажжён хотя бы один древний фонарь (не Центральный маяк).</summary>
    public bool BiomeDiscovered(int region)
    {
        foreach (var a in W.Ancients) if (a.Region == region && a.Lit && a != W.Ancients[0]) return true;
        return false;
    }

    void InitUpgrades(SaveData d)
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

    public TechState StateOfTech(int i)
    {
        var t = Techs[i];
        if (Has((Tech)i)) return TechState.Owned;
        if (t.Reward) return t.Ok(this) ? TechState.Owned : TechState.Locked;
        if (!t.Ok(this)) return TechState.Locked;
        return Can(t.Cost) ? TechState.Available : TechState.Missing;
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
        TechCur = 0; Upgrades.MarkSeen(Upgrades.ByPage(TechPage)?.Id ?? "");
    }

    /// <summary>Переключение категории среди открытых.</summary>
    public void SelectTechPage(int page)
    {
        if (!Upgrades.IsPageUnlocked(page) || page == TechPage) return;
        TechPage = page; TechCur = 0; Upgrades.MarkSeen(Upgrades.ByPage(page).Id); Sfx.Play("tick", -4, 1.2f);
    }

    public void TechCycle(int dir)
    {
        var v = Upgrades.Visible(); if (v.Count < 2) return;
        int k = v.FindIndex(c => c.Page == TechPage); k = ((k + dir) % v.Count + v.Count) % v.Count; SelectTechPage(v[k].Page);
    }

    /// <summary>Клавиши окна улучшений. Возвращает true, если клавиша обработана.</summary>
    bool TechKey(Key k)
    {
        var list = TechList();
        switch (k)
        {
            case Key.Left: TechCycle(-1); return true;
            case Key.Right: TechCycle(1); return true;
            case Key.Up: TechCur = Math.Max(0, TechCur - 1); Sfx.Play("tick", -8, 1.5f); return true;
            case Key.Down: TechCur = Math.Min(Math.Max(0, list.Count - 1), TechCur + 1); Sfx.Play("tick", -8, 1.5f); return true;
            case Key.Enter: case Key.KpEnter: case Key.E: case Key.Space: if (TechCur >= 0 && TechCur < list.Count) LearnTech(list[TechCur]); return true;
        }
        return false;
    }

    /// <summary>Изучение улучшения (кнопка, Enter, цифра): условия, стоимость и награды те же, что раньше.</summary>
    public bool LearnTech(int i)
    {
        var t = Techs[i];
        if (Has((Tech)i)) { Toast("Уже изучено"); return false; }
        if (t.Reward) { Fail("Это награда: её даёт победа над стражем"); return false; }
        if (!t.Ok(this)) { Fail("Пока недоступно: " + t.Req); return false; }
        if (!Can(t.Cost)) { Fail("Не хватает: " + NeedStr(t.Cost)); return false; }
        Pay(t.Cost); TechMask |= 1 << i; GiveXp(30);
        Burst(PPos, new Color("#8ff0ff"), 20, 100);
        Announce("Изучено: " + t.Name, t.Desc, new Color("#8ff0ff"), "tech");
        TechLearned = i; TechLearnT = .6f;
        Save(); return true;
    }
}
