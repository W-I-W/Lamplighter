using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Окна интерфейса этапа 4: экипировка, журнал, производство, постройки, торговля и маркеры событий.</summary>
public partial class Hud
{
    Rect2 Frame(Rect2 r, string title, Color border, string footer = null)
    {
        DrawRect(new Rect2(Vector2.Zero, G.ViewSize), new Color(0, 0, 0, .35f));
        DrawRect(r, new Color(.035f, .045f, .08f, .985f));
        Box(r, border);
        Txt(title, new Vector2(r.Position.X + 16, r.Position.Y + 24), 17, border);
        if (footer != null) Txt(footer, new Vector2(r.Position.X + 16, r.End.Y - 10), 11, Dim);
        return r;
    }

    void Tabs(Rect2 r, string[] names, int cur, float y)
    {
        float x = r.Position.X + 16;
        for (int i = 0; i < names.Length; i++)
        {
            float w = TextW(names[i], 13) + 24; var tr = new Rect2(x, y, w, 24);
            DrawRect(tr, i == cur ? new Color(.2f, .4f, .5f, .9f) : new Color(.1f, .12f, .18f, .9f)); DrawRect(tr, i == cur ? new Color("#8ff0ff") : Edge, false, 1);
            Txt(names[i], new Vector2(tr.Position.X, tr.Position.Y + 17), 13, i == cur ? Colors.White : Dim, HorizontalAlignment.Center, w); x += w + 6;
        }
    }

    void Row(Rect2 area, int idx, int cur, string left, string right, Color col, int scroll = 0, float rowH = 22)
    {
        float y = area.Position.Y + (idx - scroll) * rowH;
        if (y < area.Position.Y - 1 || y > area.End.Y - rowH) return;
        if (idx == cur) { DrawRect(new Rect2(area.Position.X - 4, y - 14, area.Size.X + 8, rowH - 2), new Color(.3f, .5f, .7f, .35f)); DrawRect(new Rect2(area.Position.X - 4, y - 14, 3, rowH - 2), new Color("#8ff0ff")); }
        Txt(left, new Vector2(area.Position.X + 4, y), 13, col, HorizontalAlignment.Left, area.Size.X - 90);
        if (right != null) Txt(right, new Vector2(area.Position.X, y), 11, col, HorizontalAlignment.Right, area.Size.X - 4);
    }

    int Scroll(int cur, int visible) => Mathf.Max(0, cur - visible + 1);

    void DrawWindows(Vector2 vs)
    {
        switch (G.Window)
        {
            case Win.Equip: DrawEquip(vs); break;
            case Win.Journal: DrawJournal(vs); break;
            case Win.Prod: DrawProd(vs); break;
            case Win.Build: DrawBuildWin(vs); break;
            case Win.Trade: DrawTrade(vs); break;
        }
    }

    // ───────────────────────── экипировка ─────────────────────────
    string EffText()
    {
        var parts = new List<string>();
        string[] n = { "Поджог", "Замедл.", "Заморозка", "Молния", "Залп", "Пробой", "Сифон", "Взрыв", "Дуга" };
        for (int i = 0; i < (int)Fxk.Count; i++) if (G.EffP[i] > 0) parts.Add($"{n[i]} {G.EffP[i]:0.#}");
        return parts.Count > 0 ? string.Join(" · ", parts) : "нет";
    }

    void DrawSynergies(Rect2 a, float y)
    {
        Txt("Действующие синергии", new Vector2(a.Position.X, y), 12, new Color("#ffe27a")); y += 16;
        if (G.ActiveSyn.Count == 0) { Txt("пока нет — соберите подходящие реликвии и оружие", new Vector2(a.Position.X + 6, y), 11, Dim); y += 16; }
        foreach (var s in Db.Synergies) if (G.ActiveSyn.Contains(s.Id)) { Txt($"✦ {s.Title} ({s.Path})", new Vector2(a.Position.X + 6, y), 12, Good); y += 14; Txt(s.Desc, new Vector2(a.Position.X + 16, y), 10, Dim, HorizontalAlignment.Left, a.Size.X - 16); y += 14; }
        y += 6; Txt("Подсказки комбинаций", new Vector2(a.Position.X, y), 12, new Color("#8ff0ff")); y += 16;
        int shown = 0;
        foreach (var s in Db.Synergies)
        {
            if (G.ActiveSyn.Contains(s.Id) || shown >= 5) continue;
            Txt($"• {s.Title}: {s.Hint}", new Vector2(a.Position.X + 6, y), 10, new Color(.8f, .82f, .9f), HorizontalAlignment.Left, a.Size.X - 8); y += 14; shown++;
        }
    }

    void DrawEquip(Vector2 vs)
    {
        var r = Frame(new Rect2(vs.X * .5f - 450, 60, 900, 590), "Экипировка", new Color("#8ff0ff"), "←/→ вкладка · ↑/↓ выбор · Enter действие · C/Esc закрыть");
        Tabs(r, Game.EquipTabs, G.WinTab, r.Position.Y + 36);
        var list = new Rect2(r.Position.X + 20, r.Position.Y + 100, 340, 440);
        var det = new Rect2(r.Position.X + 380, r.Position.Y + 100, 500, 450);
        switch (G.WinTab)
        {
            case 0:
                {
                    Row(list, 0, G.WinCur, "Кирка (добыча и ближний бой)", G.CurW == -1 ? "в руках" : null, G.CurW == -1 ? Gold : Colors.White);
                    for (int i = 0; i < G.Owned.Count; i++)
                    {
                        var d = Db.Weapon(G.Owned[i].Id); Row(list, i + 1, G.WinCur, $"{d.Name}", (G.CurW == i ? "в руках · " : "") + Db.ClassNames[(int)d.Cls], Db.RarCol[(int)d.Rar]);
                    }
                    int sel = G.WinCur - 1; float y = det.Position.Y;
                    var w = sel >= 0 && sel < G.Owned.Count ? Db.Weapon(G.Owned[sel].Id) : null;
                    if (w == null) { Txt("Кирка: добыча руды и слабый удар. Оружие даёт классовое поведение:", new Vector2(det.Position.X, y), 12, Dim); Txt("меч — дуга по области, лук — стрелы, посох — магические шары.", new Vector2(det.Position.X, y + 16), 12, Dim); }
                    else
                    {
                        Txt($"{w.Name} · {Db.RarName[(int)w.Rar]}", new Vector2(det.Position.X, y), 15, Db.RarCol[(int)w.Rar]); y += 18;
                        Txt(w.Desc, new Vector2(det.Position.X, y), 12, Dim); y += 16;
                        Txt($"Урон {G.WeaponDmg(w):0.#} · темп {1f / w.Cd:0.0}/с · слотов {w.Slots}", new Vector2(det.Position.X, y), 12, Colors.White); y += 20;
                        var ow = G.Owned[sel];
                        Txt("Вставки (Backspace — снять последнюю):", new Vector2(det.Position.X, y), 12, Gold); y += 16;
                        for (int s = 0; s < w.Slots; s++) { Txt(s < ow.Mods.Count ? $"▣ {Db.Mods[ow.Mods[s]].Name} — {Db.Mods[ow.Mods[s]].Desc}" : "▢ пусто", new Vector2(det.Position.X + 6, y), 12, s < ow.Mods.Count ? Good : Dim); y += 15; }
                        y += 6; Txt("Ваши вставки (нажмите цифру, чтобы вставить):", new Vector2(det.Position.X, y), 12, Gold); y += 16;
                        for (int m = 0; m < Db.Mods.Length; m++) { Txt($"[{m + 1}] {Db.Mods[m].Name} ×{G.ModInv[m]}", new Vector2(det.Position.X + 6, y), 12, G.ModInv[m] > 0 ? Colors.White : new Color(.5f, .52f, .58f)); Txt(Db.Mods[m].Desc, new Vector2(det.Position.X + 150, y), 11, Dim); y += 15; }
                        y += 6; Txt("Эффекты в руках: " + EffText(), new Vector2(det.Position.X, y), 12, new Color("#ffe9a0"), HorizontalAlignment.Left, det.Size.X); y += 18;
                    }
                    break;
                }
            case 1:
                {
                    Txt($"Слоты реликвий: {G.EquippedRelics.Count}/{G.RelicSlots} (+1 за каждого побеждённого стража)", new Vector2(list.Position.X, list.Position.Y - 6), 12, Dim);
                    var l2 = new Rect2(list.Position.X, list.Position.Y + 14, list.Size.X, list.Size.Y);
                    for (int i = 0; i < G.OwnedRelics.Count; i++)
                    {
                        var d = Db.Relic(G.OwnedRelics[i]); if (d == null) continue; bool eq = G.EquippedRelics.Contains(d.Id);
                        Row(l2, i, G.WinCur, (eq ? "✓ " : "  ") + d.Title, string.Join("/", d.Tags), eq ? Good : Db.RarCol[d.Rarity], Scroll(G.WinCur, 18));
                    }
                    if (G.OwnedRelics.Count == 0) Txt("Реликвий пока нет: ищите их в сундуках, тайниках, у торговцев и за боссов", new Vector2(list.Position.X, list.Position.Y + 40), 12, Dim, HorizontalAlignment.Left, 330);
                    float y = det.Position.Y;
                    if (G.WinCur < G.OwnedRelics.Count)
                    {
                        var d = Db.Relic(G.OwnedRelics[G.WinCur]);
                        if (d != null) { Txt($"{d.Title} · {Db.RarName[d.Rarity]}", new Vector2(det.Position.X, y), 15, Db.RarCol[d.Rarity]); y += 18; Txt(d.Desc, new Vector2(det.Position.X, y), 12, Dim, HorizontalAlignment.Left, 490); y += 18; Txt("Теги: " + (d.Tags.Length > 0 ? string.Join(", ", d.Tags) : "нет") + " · Enter — надеть/снять", new Vector2(det.Position.X, y), 11, Gold); y += 22; }
                    }
                    var tc = new List<string>(); foreach (var kv in G.TagCount) tc.Add($"{kv.Key} ×{kv.Value}");
                    Txt("Теги эффектов: " + (tc.Count > 0 ? string.Join(", ", tc) : "нет"), new Vector2(det.Position.X, y), 12, new Color("#ffe9a0"), HorizontalAlignment.Left, 490); y += 22;
                    DrawSynergies(det, y);
                    break;
                }
            case 2:
                {
                    var ws = G.CraftableWeapons(); float y = det.Position.Y;
                    for (int i = 0; i < ws.Count + Db.Mods.Length; i++)
                    {
                        string nm, cost; Color col;
                        if (i < ws.Count) { var w = ws[i]; bool have = G.HasWeapon(w.Id), okb = G.BpOk(w.Bp); nm = $"{w.Name}"; cost = have ? "есть" : !okb ? "чертёж" : Game.CostStr(w.Cost); col = have ? Dim : !okb ? new Color(.55f, .57f, .63f) : G.Can(w.Cost) ? Good : Warn; }
                        else { var m = Db.Mods[i - ws.Count]; nm = "Вставка: " + m.Name; bool okb = G.BpOk(m.Bp); cost = !okb ? "чертёж" : Game.CostStr(m.Cost); col = !okb ? new Color(.55f, .57f, .63f) : G.Can(m.Cost) ? Good : Warn; }
                        Row(list, i, G.WinCur, nm, cost, col, Scroll(G.WinCur, 19));
                    }
                    if (G.WinCur < ws.Count)
                    {
                        var w = ws[G.WinCur]; Txt(w.Name, new Vector2(det.Position.X, y), 15, Db.RarCol[(int)w.Rar]); y += 18;
                        Txt($"{Db.ClassNames[(int)w.Cls]} · урон {w.Dmg} · слотов {w.Slots}", new Vector2(det.Position.X, y), 12, Colors.White); y += 16; Txt(w.Desc, new Vector2(det.Position.X, y), 12, Dim, HorizontalAlignment.Left, 490); y += 18;
                        Txt("Нужно: " + G.NeedStr(w.Cost), new Vector2(det.Position.X, y), 12, Gold, HorizontalAlignment.Left, 490); y += 16;
                        if (w.Bp != null) Txt("Чертёж: " + Db.Bp(w.Bp).Name + (G.BpDone.Contains(w.Bp) ? " ✓" : $" ({G.Frag.GetValueOrDefault(w.Bp)}/{Db.Bp(w.Bp).Need})"), new Vector2(det.Position.X, y), 12, G.BpDone.Contains(w.Bp) ? Good : Warn);
                    }
                    else
                    {
                        var m = Db.Mods[G.WinCur - ws.Count]; Txt("Вставка: " + m.Name, new Vector2(det.Position.X, y), 15, Gold); y += 18; Txt(m.Desc + (m.Tag != null ? $" · тег «{m.Tag}» для синергий" : ""), new Vector2(det.Position.X, y), 12, Dim, HorizontalAlignment.Left, 490); y += 18;
                        Txt("Нужно: " + G.NeedStr(m.Cost), new Vector2(det.Position.X, y), 12, Gold, HorizontalAlignment.Left, 490); y += 16;
                        Txt("Чертёж: " + Db.Bp(m.Bp).Name + (G.BpDone.Contains(m.Bp) ? " ✓" : $" ({G.Frag.GetValueOrDefault(m.Bp)}/{Db.Bp(m.Bp).Need})"), new Vector2(det.Position.X, y), 12, G.BpDone.Contains(m.Bp) ? Good : Warn);
                    }
                    Txt("Создаётся рядом с печью или кузницей", new Vector2(det.Position.X, det.End.Y - 20), 11, Dim);
                    break;
                }
            default:
                {
                    float y = list.Position.Y;
                    Txt($"Фонарь героя: уровень {G.HeroLampLv}/3", new Vector2(list.Position.X, y), 16, new Color("#fff2b0")); y += 24;
                    Txt($"Радиус света: {(int)G.PlayerLightR} px", new Vector2(list.Position.X, y), 13, Colors.White); y += 20;
                    Txt("Каждый уровень: +12% радиуса. Ур.2: свет жжёт врагов вокруг. Ур.3: мощная аура.", new Vector2(list.Position.X, y), 12, Dim, HorizontalAlignment.Left, 420); y += 40;
                    if (G.HeroLampLv < 3) { var c = Game.HeroCostFor(G.HeroLampLv); Txt($"Следующий уровень: {G.NeedStr(c)}", new Vector2(list.Position.X, y), 13, G.Can(c) ? Good : Warn, HorizontalAlignment.Left, 440); y += 22; Txt(G.BpDone.Contains("bp_herolamp") ? "[Enter] улучшить" : "Нужен чертёж «Фонарь героя» (журнал [J])", new Vector2(list.Position.X, y), 12, G.BpDone.Contains("bp_herolamp") ? Gold : Warn); }
                    y += 40;
                    int cry = 0; foreach (var l in G.Lamps) if (l.Kind == 2) cry++;
                    Txt($"Фонарей: {G.Lamps.Count} (кристальных {cry}) · светокристаллы усиливают соседние фонари на 12% каждый (до 3)", new Vector2(list.Position.X, y), 12, Dim, HorizontalAlignment.Left, 780);
                    break;
                }
        }
    }

    // ───────────────────────── журнал ─────────────────────────
    void DrawJournal(Vector2 vs)
    {
        var r = Frame(new Rect2(vs.X * .5f - 400, 70, 800, 560), "Журнал чертежей и находок", new Color("#ffd86b"), "←/→ вкладка · ↑/↓ прокрутка · J/Esc закрыть");
        Tabs(r, new[] { "Чертежи", "Находки" }, G.WinTab, r.Position.Y + 36);
        var a = new Rect2(r.Position.X + 24, r.Position.Y + 100, 750, 420);
        if (G.WinTab == 0)
        {
            int done = G.BpDone.Count;
            Txt($"Собрано чертежей: {done}/{Db.Bps.Length}. Недостающие фрагменты можно купить в лавке.", new Vector2(a.Position.X, a.Position.Y - 12), 12, Dim);
            for (int i = 0; i < Db.Bps.Length; i++)
            {
                var b = Db.Bps[i]; bool ok = G.BpDone.Contains(b.Id); int fr = G.Frag.GetValueOrDefault(b.Id);
                Row(new Rect2(a.Position.X, a.Position.Y + 8, a.Size.X, a.Size.Y), i, G.WinCur, (ok ? "✓ " : "  ") + b.Name, ok ? "изучен" : $"фрагменты {fr}/{b.Need}", ok ? Good : fr > 0 ? Warn : Dim, Scroll(G.WinCur, 17));
            }
            if (G.WinCur < Db.Bps.Length) Txt(Db.Bps[G.WinCur].Desc, new Vector2(a.Position.X, r.End.Y - 34), 13, new Color("#ffe9a0"));
        }
        else
        {
            if (G.Found.Count == 0) Txt("Редких находок пока нет — исследуйте сундуки, тайники, обелиски и события.", new Vector2(a.Position.X, a.Position.Y + 10), 13, Dim);
            for (int i = 0; i < G.Found.Count; i++)
            {
                var s = G.Found[i]; Color col = Colors.White; for (int k = 0; k < 5; k++) if (s.StartsWith(Db.RarName[k])) col = Db.RarCol[k];
                Row(new Rect2(a.Position.X, a.Position.Y + 8, a.Size.X, a.Size.Y), i, G.WinCur, s, null, col, Scroll(G.WinCur, 17));
            }
        }
    }

    // ───────────────────────── производство ─────────────────────────
    string StatusText(Building b)
    {
        if (b.Off) return "пауза";
        return b.Status switch { 0 => "работает", 1 => "нет сырья", 2 => "склад полон", 3 => "нет связи", 5 => "нет режима", _ => "работает" };
    }

    void DrawProd(Vector2 vs)
    {
        var r = Frame(new Rect2(vs.X * .5f - 450, 80, 900, 540), "Управление производством", new Color("#ff9a2e"), "↑/↓ здание · ←/→ режим · 1/2/3 приоритет · Enter пауза · U улучшить · P/Esc закрыть");
        var list = G.ProdList();
        Txt($"Склад {G.StockTotal}/{G.StockCap} на ресурс · скорость ×{G.ProdMul:F2} · жителей {G.Villagers.Count}", new Vector2(r.Position.X + 20, r.Position.Y + 48), 12, Dim);
        float x0 = r.Position.X + 20, y0 = r.Position.Y + 90;
        string[] heads = { "Здание", "Ур", "Состояние", "Связь", "Приоритет", "Режим" }; float[] xs = { 0, 230, 270, 400, 480, 590 };
        for (int i = 0; i < heads.Length; i++) Txt(heads[i], new Vector2(x0 + xs[i], y0 - 12), 11, Gold);
        if (list.Count == 0) Txt("Производственных зданий нет: стройте лесопилку [5], шахту [6], кузницу [7], цех [B] — они работают сами.", new Vector2(x0, y0 + 14), 13, Dim);
        for (int i = 0; i < list.Count; i++)
        {
            var b = list[i]; int bk = Game.BIndex(b.Type); float y = y0 + 14 + i * 26;
            if (i == G.WinCur) { DrawRect(new Rect2(x0 - 6, y - 16, 860, 24), new Color(.3f, .5f, .7f, .35f)); DrawRect(new Rect2(x0 - 6, y - 16, 3, 24), new Color("#8ff0ff")); }
            bool lk = b.Linked || b.Forced;
            Txt(Game.BNames[bk], new Vector2(x0, y), 13, b.Off ? Dim : Colors.White);
            Txt(b.Level.ToString(), new Vector2(x0 + xs[1], y), 13, Gold);
            Txt(StatusText(b), new Vector2(x0 + xs[2], y), 12, b.Status == 0 && !b.Off ? Good : b.Status == 4 || b.Off ? Dim : Warn);
            Txt(lk ? (b.Forced ? "авто" : "подключено") : "нет связи", new Vector2(x0 + xs[3], y), 12, lk ? Good : new Color("#ff7a5a"));
            Txt(Game.PrioNames[b.Prio], new Vector2(x0 + xs[4], y), 12, b.Prio == 0 ? new Color("#ffb36a") : b.Prio == 2 ? Dim : Colors.White);
            var modes = Game.BModes[bk]; Txt(modes != null ? modes[b.Mode % modes.Length] : "—", new Vector2(x0 + xs[5], y), 12, new Color("#8ff0ff"));
            Txt($"{(int)(b.Prog / Mathf.Max(.1f, Game.BIntervalOf(bk, b.Level)) * 100)}%", new Vector2(x0 + 800, y), 11, Dim);
        }
        float yy = r.End.Y - 120;
        Txt("Приоритет задаёт, кто первым берёт сырьё со склада (высокий — раньше). Режим выбирает продукт.", new Vector2(x0, yy), 12, Dim, HorizontalAlignment.Left, 860);
        Txt("Цепочка: шахта → кузница (слитки) → цех (шестерни: 2 медь+1 железо; линзы: кристалл+серебро) → улучшения 5 ур. и фонари.", new Vector2(x0, yy + 16), 12, Dim, HorizontalAlignment.Left, 860);
        Txt("Здания вне радиуса склада/узлов копят продукцию у себя (до 8) и не получают сырьё: соединяйте узлами снабжения.", new Vector2(x0, yy + 32), 12, Dim, HorizontalAlignment.Left, 860);
        float sx = x0; int sh = 0;
        for (int i = 0; i < It.Count && sh < 14; i++) if (G.Stock[i] > 0) { DrawTextureRectRegion(Art.IconTex, new Rect2(sx, yy + 50, 18, 18), new Rect2(i * 16, 0, 16, 16)); Txt(G.Stock[i].ToString(), new Vector2(sx + 20, yy + 64), 11); sx += 54; sh++; }
    }

    // ───────────────────────── постройки ─────────────────────────
    void DrawBuildWin(Vector2 vs)
    {
        var r = new Rect2(10, 130, 400, 480);
        DrawRect(r, new Color(.035f, .045f, .08f, .985f));
        Box(r, new Color("#d9b27c"));
        Txt("Постройки · курсор на клетку, Enter", new Vector2(r.Position.X + 12, r.Position.Y + 22), 14, new Color("#d9b27c"));
        var l = G.BuildEntries(); var a = new Rect2(r.Position.X + 14, r.Position.Y + 50, 372, 400);
        for (int i = 0; i < l.Count; i++)
        {
            var e = l[i]; var col = !e.Unlocked ? new Color(.55f, .57f, .63f) : e.Affordable ? Good : Warn;
            Row(a, i, G.WinCur, e.Name, e.Unlocked ? e.Cost : "🔒", col, Scroll(G.WinCur, 18), 21);
        }
        if (G.WinCur < l.Count && !l[G.WinCur].Unlocked) Txt("Нужно: " + l[G.WinCur].Lock, new Vector2(r.Position.X + 14, r.End.Y - 12), 12, Warn, HorizontalAlignment.Left, 380);
    }

    // ───────────────────────── торговля ─────────────────────────
    void DrawTrade(Vector2 vs)
    {
        bool w = G.TradeEv != null;
        var r = Frame(new Rect2(vs.X * .5f - 400, 80, 800, 540), w ? "Странствующий торговец" : $"Торговая лавка · ур.{G.StallLevel}", w ? new Color("#7ad45a") : new Color("#e0c080"), "←/→ купить/продать · ↑/↓ выбор · Enter сделка (Shift — 10 шт.) · Esc закрыть");
        Tabs(r, new[] { "Купить", "Продать" }, G.TradeTab, r.Position.Y + 36);
        DrawTextureRectRegion(Art.IconTex, new Rect2(r.End.X - 160, r.Position.Y + 12, 20, 20), new Rect2(It.Coins * 16, 0, 16, 16));
        Txt($"{G.Inv[It.Coins]} монет", new Vector2(r.End.X - 134, r.Position.Y + 28), 15, new Color("#ffd86b"));
        var a = new Rect2(r.Position.X + 24, r.Position.Y + 106, 750, 360);
        if (G.TradeTab == 0)
        {
            if (G.CurOffers.Count == 0) Txt("Сегодня нет товаров. Улучшайте лавку и продвигайтесь по миру — ассортимент растёт.", new Vector2(a.Position.X, a.Position.Y + 10), 13, Dim);
            for (int i = 0; i < G.CurOffers.Count; i++)
            {
                var o = G.CurOffers[i]; bool bought = G.OfferBought(o);
                Row(a, i, G.WinCur, (bought ? "✓ " : "") + o.Title, bought ? "куплено" : $"{o.Price} мон.", bought ? Dim : G.Inv[It.Coins] >= o.Price ? Db.RarCol[(int)o.Rar] : Warn, Scroll(G.WinCur, 16));
            }
            if (G.WinCur < G.CurOffers.Count) Txt(G.CurOffers[G.WinCur].Desc, new Vector2(r.Position.X + 24, r.End.Y - 44), 12, new Color("#ffe9a0"), HorizontalAlignment.Left, 750);
        }
        else
        {
            var sl = G.SellList();
            if (sl.Count == 0) Txt("Нечего продавать.", new Vector2(a.Position.X, a.Position.Y + 10), 13, Dim);
            for (int i = 0; i < sl.Count; i++) Row(a, i, G.WinCur, $"{It.Names[sl[i]]} ×{G.Inv[sl[i]]}", $"{G.SellPrice(sl[i])} мон. за шт.", It.Col[sl[i]], Scroll(G.WinCur, 16));
            string d = ""; for (int i = 0; i < G.Demand.Length; i++) d += $"{Game.CatNames[i]} {(int)(G.Demand[i] * 100)}%  ";
            Txt("Спрос: " + d, new Vector2(r.Position.X + 24, r.End.Y - 44), 12, Dim, HorizontalAlignment.Left, 760);
        }
        if (G.TradeMsg != "") Txt(G.TradeMsg, new Vector2(r.Position.X + 24, r.End.Y - 28), 12, Good, HorizontalAlignment.Left, 760);
    }

    // ───────────────────────── события, реликвии, всплывающие панели ─────────────────────────
    void DrawEventMarkers(Vector2 vs)
    {
        foreach (var ev in G.Events)
        {
            var s = ToScreen(ev.Pos); var col = Game.EvColor(ev.Kind);
            bool inside = s.X > 30 && s.X < vs.X - 30 && s.Y > 110 && s.Y < vs.Y - 110;
            float dist = ev.Pos.DistanceTo(G.PPos) / 16, pulse = .7f + .3f * Mathf.Sin(G.Time * 4f);
            string nm = Game.EvName(ev.Kind);
            if (inside)
            {
                DrawColoredPolygon(new[] { s + new Vector2(0, -9), s + new Vector2(7, 0), s + new Vector2(0, 9), s + new Vector2(-7, 0) }, new Color(col.R, col.G, col.B, pulse));
                Txt($"{nm} · {(int)dist}", s + new Vector2(-80, 26), 11, col, HorizontalAlignment.Center, 160);
            }
            else
            {
                var c = new Vector2(Mathf.Clamp(s.X, 30, vs.X - 30), Mathf.Clamp(s.Y, 110, vs.Y - 110)); var dir = (s - vs * .5f).Normalized();
                Arrow(c, dir, new Color(col.R, col.G, col.B, pulse)); Txt($"{(int)dist}", c - dir * 14 - new Vector2(10, -4), 10, col, HorizontalAlignment.Center, 20);
            }
        }
    }

    void DrawStage4Hud(Vector2 vs)
    {
        // экипированные реликвии (маленькие ромбы) рядом с панелью здоровья
        float x = 254;
        foreach (var id in G.EquippedRelics.ToArray().Length > 0 ? G.EquippedRelics : new List<string>())
        {
            var d = Db.Relic(id); if (d == null) continue;
            DrawColoredPolygon(new[] { new Vector2(x, 20), new Vector2(x + 7, 27), new Vector2(x, 34), new Vector2(x - 7, 27) }, d.Tint);
            DrawPolyline(new[] { new Vector2(x, 20), new Vector2(x + 7, 27), new Vector2(x, 34), new Vector2(x - 7, 27), new Vector2(x, 20) }, Db.RarCol[d.Rarity], 1f); x += 20;
        }
        foreach (var id in G.TempRelics) { var d = Db.Relic(id); if (d == null) continue; DrawColoredPolygon(new[] { new Vector2(x, 20), new Vector2(x + 7, 27), new Vector2(x, 34), new Vector2(x - 7, 27) }, new Color(d.Tint.R, d.Tint.G, d.Tint.B, .6f + .4f * Mathf.Sin(G.Time * 8))); x += 20; }
        if (G.ActiveSyn.Count > 0) Txt($"✦{G.ActiveSyn.Count}", new Vector2(x - 6, 34), 12, Good);
        if (G.SurgeOn) { var rr = new Rect2(vs.X * .5f - 120, 40, 240, 16); Bar(rr, G.Surge / 8f, new Color("#ffe27a")); Txt("ЛУЧЕЗАРНОСТЬ", new Vector2(rr.Position.X, rr.Position.Y + 13), 11, Colors.Black, HorizontalAlignment.Center, 240); }
        if (G.TrialActive)
        {
            var rr = new Rect2(vs.X * .5f - 190, G.Boss != null ? 54 : 8, 380, 30); Box(rr, new Color("#c070ff"));
            Txt($"Испытание Тьмы · осталось {(int)G.TrialLeft} с", new Vector2(rr.Position.X, rr.Position.Y + 20), 14, new Color("#e0b8ff"), HorizontalAlignment.Center, 380);
        }
        if (G.Events.Count > 0 && G.Window == Win.None)
        {
            float y = G.Buildings.Count > 0 || G.Villagers.Count > 0 ? 128 : 62;
            Box(new Rect2(vs.X - 220, y, 210, 14 + G.Events.Count * 16 + 10));
            Txt("События", new Vector2(vs.X - 212, y + 14), 11, Dim);
            for (int i = 0; i < G.Events.Count; i++) Txt($"◆ {Game.EvName(G.Events[i].Kind)} · {(int)(G.Events[i].Pos.DistanceTo(G.PPos) / 16)}", new Vector2(vs.X - 212, y + 30 + i * 16), 11, Game.EvColor(G.Events[i].Kind));
        }
    }
}
