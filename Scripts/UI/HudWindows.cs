using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Игровые окна в едином пиксельном стиле: экипировка, журнал, производство, торговля, улучшения, карта, записки, врата разломов.</summary>
public partial class Hud
{
    Rect2 WinRect(Vector2 vs, float w, float h) => new(Mathf.Round(vs.X * .5f - Mathf.Min(w, vs.X - 12) * .5f), Mathf.Round(vs.Y * .5f - Mathf.Min(h, vs.Y - 12) * .5f - 4), Mathf.Min(w, vs.X - 12), Mathf.Min(h, vs.Y - 12));

    void DrawWindows(Vector2 vs)
    {
        switch (G.Window)
        {
            case Win.Equip: DrawEquip(vs); break;
            case Win.Journal: DrawJournal(vs); break;
            case Win.Prod: DrawProd(vs); break;
            case Win.Trade: DrawTrade(vs); break;
            case Win.Story: DrawStory(vs); break;
            case Win.Rift: DrawRiftWin(vs); break;
        }
    }

    void Icon(int item, Vector2 p) => Tex(UiIcons.Item(item), new Rect2(p.Round(), new Vector2(16, 16)));

    // ───────────────────────── экипировка ─────────────────────────
    string EffText()
    {
        var parts = new List<string>();
        string[] n = { "Поджог", "Замедл.", "Заморозка", "Молния", "Залп", "Пробой", "Сифон", "Взрыв", "Дуга" };
        for (int i = 0; i < (int)Fxk.Count; i++) if (G.EffP[i] > 0) parts.Add($"{n[i]} {G.EffP[i]:0.#}");
        return parts.Count > 0 ? string.Join(" · ", parts) : "нет";
    }

    float Synergies(float x, float y, float w, int budget)
    {
        Txt("Действующие синергии", new Vector2(x, y), 9, Gold); y += 10;
        if (G.ActiveSyn.Count == 0) { y = Para("пока нет — соберите подходящие реликвии и оружие", x + 4, y, w - 4, Dim); }
        foreach (var s in Db.Synergies) if (G.ActiveSyn.Contains(s.Id)) { Txt($"✦ {s.Title} ({s.Path})", new Vector2(x + 4, y), 9, Good, HorizontalAlignment.Left, w - 4); y += 10; y = Para(s.Desc, x + 10, y, w - 10, Dim, 2); }
        y += 3; Txt("Подсказки комбинаций", new Vector2(x, y), 9, Cyan); y += 10; int shown = 0;
        foreach (var s in Db.Synergies) { if (G.ActiveSyn.Contains(s.Id) || shown >= budget) continue; y = Para($"• {s.Title}: {s.Hint}", x + 4, y, w - 4, Text, 2); shown++; }
        return y;
    }

    void DrawEquip(Vector2 vs)
    {
        var r = WinRect(vs, 600, 330); var a = Frame(r, "Экипировка", "←/→ вкладка · ↑/↓ выбор · Enter действие · C/Esc закрыть", Cyan);
        float y0 = Tabs(a.Position.X, a.Position.Y + 2, Game.EquipTabs, G.WinTab) + 8;
        var list = new Rect2(a.Position.X, y0, 190, a.End.Y - y0); var det = new Rect2(a.Position.X + 200, y0, a.Size.X - 200, a.End.Y - y0);
        Panel(new Rect2(det.Position.X - 4, y0 - 8, det.Size.X + 8, det.Size.Y + 6), "PanelDark");
        float dx = det.Position.X + 2, dw = det.Size.X - 4, y = y0 + 2, ly = y0 + 2;
        switch (G.WinTab)
        {
            case 0:
                {
                    ly = Row(list, ly, Sel(0), "Кирка", G.CurW == -1 ? "в руках" : null, G.CurW == -1 ? Gold : Text);
                    for (int i = 0; i < G.Owned.Count; i++) { var d = Db.Weapon(G.Owned[i].Id); ly = Row(list, ly, Sel(i + 1), d.Name, (G.CurW == i ? "★ " : "") + Db.ClassNames[(int)d.Cls], Db.RarCol[(int)d.Rar]); }
                    int sel = G.WinCur - 1; var w = sel >= 0 && sel < G.Owned.Count ? Db.Weapon(G.Owned[sel].Id) : null;
                    if (w == null) { y = Para("Кирка: добыча руды и слабый удар. Оружие даёт классовое поведение: меч — дуга по области, лук — стрелы, посох — магические шары.", dx, y + 8, dw, Dim); break; }
                    Txt($"{w.Name} · {Db.RarName[(int)w.Rar]}", new Vector2(dx, y + 8), 9, Db.RarCol[(int)w.Rar], HorizontalAlignment.Left, dw); y += 11; y = Para(w.Desc, dx, y + 8, dw, Dim);
                    Txt($"Урон {G.WeaponDmg(w):0.#} · темп {1f / w.Cd:0.0}/с · слотов {w.Slots}", new Vector2(dx, y + 8), 9, Text, HorizontalAlignment.Left, dw); y += 12;
                    var ow = G.Owned[sel]; Txt("Вставки (Backspace — снять):", new Vector2(dx, y + 8), 9, Gold); y += 10;
                    for (int s = 0; s < w.Slots; s++) { Txt(s < ow.Mods.Count ? $"▣ {Db.Mods[ow.Mods[s]].Name}" : "▢ пусто", new Vector2(dx + 4, y + 8), 9, s < ow.Mods.Count ? Good : Dim, HorizontalAlignment.Left, dw - 4); y += 10; }
                    y += 2; Txt("Ваши вставки (цифра — вставить):", new Vector2(dx, y + 8), 9, Gold); y += 10;
                    for (int m = 0; m < Db.Mods.Length && y < det.End.Y - 22; m++) { Txt($"[{m + 1}] {Db.Mods[m].Name} ×{G.ModInv[m]}", new Vector2(dx + 4, y + 8), 9, G.ModInv[m] > 0 ? Text : new Color("#6a7288"), HorizontalAlignment.Left, dw - 4); y += 10; }
                    Para("Эффекты: " + EffText(), dx, det.End.Y - 14, dw, new Color("#ffe9a0"), 2);
                    break;
                }
            case 1:
                {
                    Txt($"Слоты: {G.EquippedRelics.Count}/{G.RelicSlots}", new Vector2(list.Position.X + 2, ly + 6), 9, Dim); ly += 12; int sc = Scroll(G.WinCur, 14);
                    for (int i = sc; i < G.OwnedRelics.Count && i < sc + 14; i++) { var d = Db.Relic(G.OwnedRelics[i]); if (d == null) continue; bool eq = G.EquippedRelics.Contains(d.Id); ly = Row(list, ly + 6, Sel(i), (eq ? "✓ " : "  ") + d.Title, null, eq ? Good : Db.RarCol[d.Rarity]) - 6; }
                    if (G.OwnedRelics.Count == 0) { Para("Реликвий пока нет: ищите их в сундуках, тайниках, у торговцев и за боссов", list.Position.X + 2, ly + 8, list.Size.X - 4, Dim); }
                    if (G.WinCur < G.OwnedRelics.Count)
                    {
                        var d = Db.Relic(G.OwnedRelics[G.WinCur]);
                        if (d != null) { Txt($"{d.Title} · {Db.RarName[d.Rarity]}", new Vector2(dx, y + 8), 9, Db.RarCol[d.Rarity], HorizontalAlignment.Left, dw); y += 11; y = Para(d.Desc, dx, y + 8, dw, Dim); Txt("Теги: " + (d.Tags.Length > 0 ? string.Join(", ", d.Tags) : "нет") + " · Enter — надеть/снять", new Vector2(dx, y + 8), 9, Gold, HorizontalAlignment.Left, dw); y += 12; }
                    }
                    var tc = new List<string>(); foreach (var kv in G.TagCount) tc.Add($"{kv.Key} ×{kv.Value}");
                    y = Para("Теги: " + (tc.Count > 0 ? string.Join(", ", tc) : "нет"), dx, y + 8, dw, new Color("#ffe9a0"), 2) + 2;
                    Synergies(dx, y + 8, dw, 3);
                    break;
                }
            case 2:
                {
                    var ws = G.CraftableWeapons(); int total = ws.Count + Db.Mods.Length; int sc = Scroll(G.WinCur, 14);
                    for (int i = sc; i < total && i < sc + 14; i++)
                    {
                        string nm, cost; Color col;
                        if (i < ws.Count) { var w = ws[i]; bool have = G.HasWeapon(w.Id), okb = G.BpOk(w.Bp); nm = w.Name; cost = have ? "есть" : !okb ? "чертёж" : ""; col = have ? Dim : !okb ? new Color("#6a7288") : G.Can(w.Cost) ? Good : Warn; }
                        else { var m = Db.Mods[i - ws.Count]; nm = "Вставка: " + m.Name; cost = !G.BpOk(m.Bp) ? "чертёж" : ""; col = !G.BpOk(m.Bp) ? new Color("#6a7288") : G.Can(m.Cost) ? Good : Warn; }
                        ly = Row(list, ly + 6, Sel(i), nm, cost == "" ? null : cost, col) - 6;
                    }
                    if (G.WinCur < ws.Count)
                    {
                        var w = ws[G.WinCur]; Txt(w.Name, new Vector2(dx, y + 8), 9, Db.RarCol[(int)w.Rar], HorizontalAlignment.Left, dw); y += 11;
                        Txt($"{Db.ClassNames[(int)w.Cls]} · урон {w.Dmg} · слотов {w.Slots}", new Vector2(dx, y + 8), 9, Text, HorizontalAlignment.Left, dw); y += 11; y = Para(w.Desc, dx, y + 8, dw, Dim);
                        y = Para("Нужно: " + G.NeedStr(w.Cost), dx, y + 4, dw, Gold);
                        if (w.Bp != null) Para("Чертёж: " + Db.Bp(w.Bp).Name + (G.BpDone.Contains(w.Bp) ? " ✓" : $" ({G.Frag.GetValueOrDefault(w.Bp)}/{Db.Bp(w.Bp).Need})"), dx, y + 2, dw, G.BpDone.Contains(w.Bp) ? Good : Warn);
                    }
                    else
                    {
                        var m = Db.Mods[G.WinCur - ws.Count]; Txt("Вставка: " + m.Name, new Vector2(dx, y + 8), 9, Gold, HorizontalAlignment.Left, dw); y += 11;
                        y = Para(m.Desc + (m.Tag != null ? $" · тег «{m.Tag}» для синергий" : ""), dx, y + 8, dw, Dim);
                        y = Para("Нужно: " + G.NeedStr(m.Cost), dx, y + 4, dw, Gold);
                        Para("Чертёж: " + Db.Bp(m.Bp).Name + (G.BpDone.Contains(m.Bp) ? " ✓" : $" ({G.Frag.GetValueOrDefault(m.Bp)}/{Db.Bp(m.Bp).Need})"), dx, y + 2, dw, G.BpDone.Contains(m.Bp) ? Good : Warn);
                    }
                    Txt("Создаётся рядом с печью или кузницей", new Vector2(dx, det.End.Y - 4), 9, Dim, HorizontalAlignment.Left, dw);
                    break;
                }
            default:
                {
                    Txt($"Фонарь героя: уровень {G.HeroLampLv}/3", new Vector2(dx, y + 8), 9, new Color("#fff2b0")); y += 12;
                    Txt($"Радиус света: {(int)G.PlayerLightR} px", new Vector2(dx, y + 8), 9, Text); y += 12;
                    y = Para("Каждый уровень: +12% радиуса. Ур.2: свет жжёт врагов вокруг. Ур.3: мощная аура.", dx, y + 8, dw, Dim);
                    if (G.HeroLampLv < 3) { var c = Game.HeroCostFor(G.HeroLampLv); y = Para($"Следующий уровень: {G.NeedStr(c)}", dx, y + 6, dw, G.Can(c) ? Good : Warn); Para(G.BpDone.Contains("bp_herolamp") ? "[Enter] улучшить" : "Нужен чертёж «Фонарь героя» (журнал [J])", dx, y + 2, dw, G.BpDone.Contains("bp_herolamp") ? Gold : Warn); }
                    int cry = 0; foreach (var l in G.Lamps) if (l.Kind == 2) cry++;
                    Para($"Фонарей: {G.Lamps.Count} (кристальных {cry}). Светокристаллы усиливают соседние фонари на 12% каждый (до 3)", dx, det.End.Y - 22, dw, Dim);
                    break;
                }
        }
    }

    // ───────────────────────── журнал ─────────────────────────
    void DrawJournal(Vector2 vs)
    {
        var r = WinRect(vs, 420, 320); var a = Frame(r, "Журнал", "←/→ вкладка · ↑/↓ прокрутка · J/Esc закрыть", Gold);
        float y = Tabs(a.Position.X, a.Position.Y + 2, new[] { "Чертежи", "Находки", "Цели" }, G.WinTab) + 8; float h = 11;
        int vis = (int)((a.End.Y - y - 22) / h);
        if (G.WinTab == 0)
        {
            Txt($"Собрано чертежей: {G.BpDone.Count}/{Db.Bps.Length}. Недостающие фрагменты — в лавке.", new Vector2(a.Position.X + 2, y), 9, Dim, HorizontalAlignment.Left, a.Size.X); y += 12; int sc = Scroll(G.WinCur, vis);
            for (int i = sc; i < Db.Bps.Length && i < sc + vis; i++) { var b = Db.Bps[i]; bool ok = G.BpDone.Contains(b.Id); int fr = G.Frag.GetValueOrDefault(b.Id); y = Row(a, y, Sel(i), (ok ? "✓ " : "  ") + b.Name, ok ? "изучен" : $"{fr}/{b.Need}", ok ? Good : fr > 0 ? Warn : Dim, h); }
            if (G.WinCur < Db.Bps.Length) Para(Db.Bps[G.WinCur].Desc, a.Position.X + 2, a.End.Y - 8, a.Size.X, new Color("#ffe9a0"), 1);
        }
        else if (G.WinTab == 2)
        {
            int per = Math.Max(1, vis / 2); int sc = Scroll(G.WinCur, per);
            for (int i = sc; i < G.Goals.Count && i < sc + per; i++) { var gl = G.Goals[i]; y = Row(a, y, Sel(i), (i == 0 ? "▶ " : "• ") + gl.Title, null, i == 0 ? Gold : Text, h); y = Para(gl.Needs, a.Position.X + 10, y, a.Size.X - 12, Dim, 1); }
            if (G.Goals.Count == 0) Txt("Все цели выполнены!", new Vector2(a.Position.X + 2, y + 8), 9, Good);
        }
        else
        {
            if (G.Found.Count == 0) Para("Редких находок пока нет — исследуйте сундуки, тайники, обелиски и события.", a.Position.X + 2, y + 8, a.Size.X, Dim);
            int sc = Scroll(G.WinCur, vis);
            for (int i = sc; i < G.Found.Count && i < sc + vis; i++) { var s = G.Found[i]; Color col = Text; for (int k = 0; k < 5; k++) if (s.StartsWith(Db.RarName[k])) col = Db.RarCol[k]; y = Row(a, y, Sel(i), s, null, col, h); }
        }
    }

    // ───────────────────────── производство ─────────────────────────
    string StatusText(Building b) => b.Off ? "пауза" : b.Status switch { 0 => "работает", 1 => "нет сырья", 2 => "склад полон", 3 => "нет связи", 5 => "нет режима", _ => "работает" };

    void DrawProd(Vector2 vs)
    {
        var r = WinRect(vs, 600, 330); var a = Frame(r, "Производство", "↑/↓ здание · ←/→ режим · 1/2/3 приоритет · Enter пауза · U улучшить · P/Esc", new Color("#ff9a2e"));
        var list = G.ProdList(); float y = a.Position.Y + 8;
        Txt($"Склад {G.StockTotal}/{G.StockCap} на ресурс · скорость ×{G.ProdMul:F2} · жителей {G.Villagers.Count}", new Vector2(a.Position.X + 2, y), 9, Dim, HorizontalAlignment.Left, a.Size.X); y += 14;
        string[] heads = { "Здание", "Ур", "Состояние", "Связь", "Приоритет", "Режим" }; float[] xs = { 0, 100, 118, 190, 245, 310 };
        for (int i = 0; i < heads.Length; i++) Txt(heads[i], new Vector2(a.Position.X + 4 + xs[i], y), 9, Gold);
        y += 4;
        if (list.Count == 0) Para("Производственных зданий нет: стройте лесопилку [5], шахту [6], кузницу [7], цех [B] — они работают сами.", a.Position.X + 2, y + 10, a.Size.X, Dim);
        int vis = 11, sc = Scroll(G.WinCur, vis);
        for (int i = sc; i < list.Count && i < sc + vis; i++)
        {
            var b = list[i]; int bk = Game.BIndex(b.Type); y += 12; bool lk = b.Linked || b.Forced;
            winRects.Add((new Rect2(a.Position.X, y - 8, a.Size.X, 11), i));
            if (i == G.WinCur) Sty(new Rect2(a.Position.X, y - 8, a.Size.X, 11), "BtnSelected");
            Txt(Game.BNames[bk], new Vector2(a.Position.X + 4, y), 9, b.Off ? Dim : Text, HorizontalAlignment.Left, 96);
            Txt(b.Level.ToString(), new Vector2(a.Position.X + 4 + xs[1], y), 9, Gold);
            Txt(StatusText(b), new Vector2(a.Position.X + 4 + xs[2], y), 9, b.Status == 0 && !b.Off ? Good : b.Status == 4 || b.Off ? Dim : Warn, HorizontalAlignment.Left, 70);
            Txt(lk ? (b.Forced ? "авто" : "связь") : "нет связи", new Vector2(a.Position.X + 4 + xs[3], y), 9, lk ? Good : Bad, HorizontalAlignment.Left, 54);
            Txt(Game.PrioNames[b.Prio], new Vector2(a.Position.X + 4 + xs[4], y), 9, b.Prio == 0 ? Warn : b.Prio == 2 ? Dim : Text, HorizontalAlignment.Left, 54);
            var modes = Game.BModes[bk]; Txt(modes != null ? modes[b.Mode % modes.Length] : "—", new Vector2(a.Position.X + 4 + xs[5], y), 9, Cyan, HorizontalAlignment.Left, 100);
            Txt($"{(int)(b.Prog / Mathf.Max(.1f, Game.BIntervalOf(bk, b.Level)) * 100)}%", new Vector2(a.Position.X, y), 9, Dim, HorizontalAlignment.Right, a.Size.X - 4);
        }
        float yy = a.End.Y - 46;
        yy = Para("Приоритет задаёт, кто первым берёт сырьё со склада. Режим выбирает продукт. Здания вне радиуса склада копят продукцию у себя — соединяйте узлами снабжения.", a.Position.X + 2, yy, a.Size.X - 4, Dim, 3);
        float sx = a.Position.X + 2; int sh = 0;
        for (int i = 0; i < It.Count && sh < 12; i++) if (G.Stock[i] > 0) { Icon(i, new Vector2(sx, yy - 6)); Txt(G.Stock[i].ToString(), new Vector2(sx + 17, yy + 7), 9); sx += 40; sh++; }
    }

    // ───────────────────────── торговля ─────────────────────────
    void DrawTrade(Vector2 vs)
    {
        bool w = G.TradeEv != null;
        var r = WinRect(vs, 420, 320); var a = Frame(r, w ? "Странствующий торговец" : $"Торговая лавка · ур.{G.StallLevel}", "←/→ купить/продать · ↑/↓ · Enter сделка (Shift ×10) · Esc", w ? Good : new Color("#e0c080"));
        float y = Tabs(a.Position.X, a.Position.Y + 2, new[] { "Купить", "Продать" }, G.TradeTab) + 8;
        Icon(It.Coins, new Vector2(a.End.X - 56, a.Position.Y - 4)); Txt($"{G.Inv[It.Coins]}", new Vector2(a.End.X - 38, a.Position.Y + 8), 9, new Color("#ffd86b"));
        int vis = (int)((a.End.Y - y - 36) / 11);
        if (G.TradeTab == 0)
        {
            if (G.CurOffers.Count == 0) Para("Сегодня нет товаров. Улучшайте лавку и продвигайтесь по миру — ассортимент растёт.", a.Position.X + 2, y + 8, a.Size.X, Dim);
            int sc = Scroll(G.WinCur, vis);
            for (int i = sc; i < G.CurOffers.Count && i < sc + vis; i++) { var o = G.CurOffers[i]; bool bought = G.OfferBought(o); y = Row(a, y + 8, Sel(i), (bought ? "✓ " : "") + o.Title, bought ? "куплено" : $"{o.Price}", bought ? Dim : G.Inv[It.Coins] >= o.Price ? Db.RarCol[(int)o.Rar] : Warn) - 8; }
            if (G.WinCur < G.CurOffers.Count) Para(G.CurOffers[G.WinCur].Desc, a.Position.X + 2, a.End.Y - 22, a.Size.X, new Color("#ffe9a0"), 2);
        }
        else
        {
            var sl = G.SellList(); if (sl.Count == 0) Txt("Нечего продавать.", new Vector2(a.Position.X + 2, y + 8), 9, Dim);
            int sc = Scroll(G.WinCur, vis);
            for (int i = sc; i < sl.Count && i < sc + vis; i++) y = Row(a, y + 8, Sel(i), $"{It.Names[sl[i]]} ×{G.Inv[sl[i]]}", $"{G.SellPrice(sl[i])}", It.Col[sl[i]]) - 8;
            string d = ""; for (int i = 0; i < G.Demand.Length; i++) d += $"{Game.CatNames[i]} {(int)(G.Demand[i] * 100)}%  ";
            Para("Спрос: " + d, a.Position.X + 2, a.End.Y - 22, a.Size.X, Dim, 2);
        }
        if (G.TradeMsg != "") Txt(G.TradeMsg, new Vector2(a.Position.X + 2, a.End.Y - 8), 9, Good, HorizontalAlignment.Left, a.Size.X);
    }

    // ───────────────────────── улучшения ─────────────────────────
    void DrawTech(Vector2 vs)
    {
        var all = Game.PageList(G.TechPage);
        var r = WinRect(vs, 460, 100 + all.Count * 24); var a = Frame(r, $"Улучшения · {G.TechCount}/{Game.Techs.Length}", "цифра — изучить · ←/→ вкладка · T/Esc — закрыть", Cyan);
        float y = Tabs(a.Position.X, a.Position.Y + 2, Game.TechPages, G.TechPage) + 4; int num = 0;
        for (int k = 0; k < all.Count; k++)
        {
            int i = all[k]; var t = Game.Techs[i]; bool own = G.Has((Tech)i), ok = t.Ok(G), can = ok && !t.Reward && G.Can(t.Cost);
            var col = own ? Cyan : can ? Good : ok ? Warn : new Color("#6a7288"); string key = t.Reward ? "[★]" : $"[{++num}]";
            if (!t.Reward) winRects.Add((new Rect2(a.Position.X, y + 1, a.Size.X, 23), num));
            y += 10; Txt($"{key} {t.Name}", new Vector2(a.Position.X + 2, y), 9, col, HorizontalAlignment.Left, 250);
            string right = own ? "✓" : !ok ? "нужно: " + t.Req : t.Reward ? "награда" : Game.CostStr(t.Cost);
            Txt(right, new Vector2(a.Position.X, y), 9, col, HorizontalAlignment.Right, a.Size.X - 2);
            y += 10; Txt(t.Desc, new Vector2(a.Position.X + 14, y), 9, own ? Dim : new Color("#a8b2c8"), HorizontalAlignment.Left, a.Size.X - 16); y += 3;
        }
    }

    // ───────────────────────── карта ─────────────────────────
    void DrawMap(Vector2 vs)
    {
        float s = Mathf.Min(1.4f, (vs.X - 40) / World.Wd); float mw = Mathf.Round(World.Wd * s), mh = Mathf.Round(World.Ht * s);
        var o = new Vector2(Mathf.Round(vs.X * .5f - mw * .5f), Mathf.Round(vs.Y * .5f - mh * .5f - 6));
        Rect(new Rect2(Vector2.Zero, vs), new Color(0, 0, .03f, .7f));
        Panel(new Rect2(o - new Vector2(8, 18), new Vector2(mw + 16, mh + 44)));
        Txt("Карта · M/Esc — закрыть", new Vector2(o.X, o.Y - 8), 9, Cyan);
        Rect(new Rect2(o, new Vector2(mw, mh)), new Color(.03f, .04f, .07f));
        Tex(G.MapTex, new Rect2(o, new Vector2(mw, mh)));
        for (int i = 1; i < 5; i++) Rect(new Rect2(o.X + i * 128 * s, o.Y, 1, mh), new Color(1, 1, 1, .2f));
        for (int i = 0; i < 5; i++) Txt(Game.RegionNames[i], o + new Vector2(i * 128 * s, mh + 9), 9, Game.RegionCol(i), HorizontalAlignment.Center, 128 * s);
        foreach (var an in G.W.Ancients) if (an.Lit) Disc(o + an.Pos / 16f * s, G.AncientR(an) / 16f * s, new Color(1f, .85f, .4f, .18f));
        foreach (var l in G.Lamps) Disc(o + l.Pos / 16f * s, 5 * s, new Color(1f, .85f, .4f, .16f));
        foreach (var seal in new[] { G.W.Seal1, G.W.Seal2, G.W.Seal3, G.W.Seal4 })
            if (seal.Count > 0 && G.W.Get(seal[0].X, seal[0].Y) == Tl.Seal) Rect(new Rect2(o + new Vector2(seal[0].X, seal[0].Y - 1) * s, new Vector2(2 * s, 9 * s)), new Color("#d58cff"));
        foreach (var an in G.W.Ancients)
        {
            var p = (o + an.Pos / 16f * s).Round(); if (!(an.Lit || G.IsExplored(an.Pos))) continue;
            if (an.Boss) Diamond(p, 3f, an.Lit ? Gold : new Color("#d58cff"));
            else Rect(new Rect2(p - new Vector2(1.5f, 1.5f), new Vector2(3, 3)), an.Lit ? Gold : Colors.White);
        }
        foreach (var p in G.W.Pois) { if (!(p.Seen || p.Revealed) || p.State >= 3) continue; Rect(new Rect2((o + p.Pos / 16f * s).Round(), new Vector2(1, 1)), Game.PoiColor(p.Kind)); }
        foreach (var b in G.Buildings) Rect(new Rect2((o + b.Pos / 16f * s).Round() - new Vector2(1, 1), new Vector2(2, 2)), new Color("#d9b27c"));
        foreach (var ev in G.Events) { var ep = (o + ev.Pos / 16f * s).Round(); Rect(new Rect2(ep - new Vector2(1.5f, 1.5f), new Vector2(3, 3)), Game.EvColor(ev.Kind)); }
        var pp = (o + G.PPos / 16f * s).Round(); bool blink = Mathf.Sin(G.Time * 6f) > 0;
        Rect(new Rect2(pp - new Vector2(2, 2), new Vector2(4, 4)), new Color("#090c14")); Rect(new Rect2(pp - new Vector2(1, 1), new Vector2(2, 2)), blink ? new Color("#9be07a") : new Color("#5fbf4a"));
        Txt("● вы  ◆ страж  золото — горит  ▮ печать", new Vector2(o.X, o.Y + mh + 20), 9, Dim, HorizontalAlignment.Left, mw);
    }

    // ───────────────────────── записки ─────────────────────────
    void DrawStory(Vector2 vs)
    {
        var r = WinRect(vs, 330, 190); var a = Frame(r, G.StoryTitle, "[E] / [Esc] — закрыть", Gold);
        float y = a.Position.Y + 8;
        if (G.StorySub != "") { Txt(G.StorySub, new Vector2(a.Position.X + 2, y), 9, Dim); y += 14; }
        Para(G.StoryText, a.Position.X + 2, y + 4, a.Size.X - 4, Text);
    }

    void DrawFinale(Vector2 vs)
    {
        if (G.FinStage == 0) return;
        if (G.FinStage == 1) { float k = Mathf.Clamp((G.FinT - 6f) / 3f, 0, 1); if (k > 0) Txt("СВЕТ ВОЗВРАЩАЕТСЯ", new Vector2(0, vs.Y * .4f), 14, new Color(1, .95f, .7f, k), HorizontalAlignment.Center, vs.X); return; }
        float fade = Mathf.Clamp(G.FinT / 1.2f, 0, 1);
        Rect(new Rect2(Vector2.Zero, vs), new Color(.01f, .01f, .03f, G.FinStage == 3 ? .94f : .82f));
        var old = curMod;
        if (G.FinStage == 2)
        {
            curMod = new Color(1, 1, 1, fade); float w = Mathf.Min(360, vs.X - 40); Para(Story.Finale[G.FinScene], (vs.X - w) / 2, vs.Y * .38f, w, new Color(1, .96f, .85f)); curMod = old;
            Txt(G.FinScene >= Story.Finale.Length - 1 ? "[Enter] — титры" : "[Enter] — дальше · [Esc] — пропустить", new Vector2(0, vs.Y - 12), 9, Dim, HorizontalAlignment.Center, vs.X);
            return;
        }
        float y = vs.Y - G.FinT * 24f + 10;
        for (int i = 0; i < Story.Credits.Length; i++)
        {
            float yy = y + i * 18; if (yy < -20 || yy > vs.Y + 20) continue; bool head = i == 0 || (i > 0 && Story.Credits[i - 1] == "");
            Txt(Story.Credits[i], new Vector2(0, yy), head ? 14 : 9, head ? Gold : Text, HorizontalAlignment.Center, vs.X);
        }
        Txt("[Enter] — продолжить игру", new Vector2(0, vs.Y - 12), 9, Dim, HorizontalAlignment.Center, vs.X);
    }

    // ───────────────────────── врата разломов ─────────────────────────
    void DrawRiftWin(Vector2 vs)
    {
        var r = WinRect(vs, 400, 260); var a = Frame(r, G.RiftState == 1 ? "Разлом: временная реликвия" : "Врата разломов", G.RiftState == 1 ? "[1]-[3] или Enter — выбрать" : "←/→ вкладки · ↑/↓ · Enter · Esc", new Color("#c070ff"));
        if (G.RiftState == 1)
        {
            float yy = a.Position.Y + 8;
            for (int i = 0; i < G.RiftOffer.Count; i++) { var d = Db.Relic(G.RiftOffer[i]); yy = Row(a, yy + 8, Sel(i), $"[{i + 1}] {d.Title}", null, new Color("#e8d8ff"), 11) - 8; yy = Para(d.Desc, a.Position.X + 14, yy + 8, a.Size.X - 16, Dim, 2) + 2; }
            return;
        }
        float y = Tabs(a.Position.X, a.Position.Y + 2, Game.RiftTabs, G.WinTab) + 8;
        Icon(It.RiftShard, new Vector2(a.End.X - 40, a.Position.Y - 4)); Txt($"{G.Inv[It.RiftShard]}", new Vector2(a.End.X - 22, a.Position.Y + 8), 9, It.Col[It.RiftShard]);
        if (G.WinTab == 0)
            for (int i = 0; i < Game.MaxRiftTier; i++) { bool open = G.RiftTierOpen(i + 1), done = (G.RiftFirst & (1 << (i + 1))) != 0; y = Row(a, y + 8, Sel(i), $"Уровень {i + 1}" + (done ? " ✓" : ""), open ? $"×{1 + .22f * (i + 1):0.0} · {3 + 2 * (i + 1)}" : "закрыт", open ? Text : new Color("#6a7288")) - 8; }
        else if (G.WinTab == 1)
            for (int i = 0; i < Game.Legends.Length; i++) { var l = Game.Legends[i]; bool has = G.HasLegend(i); y = Row(a, y + 8, Sel(i), l.Name + (has ? " ✓" : ""), has ? "получено" : null, has ? Good : G.Can(l.Cost) ? Gold : Dim) - 8; y = Para(has ? l.Desc : l.Desc + " · " + Game.CostStr(l.Cost), a.Position.X + 10, y + 8, a.Size.X - 12, Dim, 2); }
        else
            for (int i = 0; i < Game.CosmNames.Length; i++) { bool own = (G.CosmMask & (1 << i)) != 0; y = Row(a, y + 8, Sel(i), Game.CosmNames[i] + (G.CosmSel == i ? " (надет)" : ""), own ? null : "закрыт", own ? Text : new Color("#6a7288")) - 8; }
    }

    void DrawEndgameHud(Vector2 vs)
    {
        if (!G.RiftOn) return;
        int left = G.Enemies.FindAll(e => e.Ev != null && e.Ev.Kind == "riftx").Count;
        var r = new Rect2(vs.X * .5f - 90, 4, 180, 24); Panel(r, "PanelGold");
        Txt($"Разлом {G.RiftTier} · волна {G.RiftWave}/{G.RiftWaves} · {left}", new Vector2(r.Position.X, r.Position.Y + 10), 9, Text, HorizontalAlignment.Center, r.Size.X);
        Txt($"{Game.AffixNames[G.RiftAffix[0]]} + {Game.AffixNames[G.RiftAffix[1]]}", new Vector2(r.Position.X, r.Position.Y + 20), 9, Dim, HorizontalAlignment.Center, r.Size.X);
    }
}
