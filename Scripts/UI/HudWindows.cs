using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Игровые окна: экипировка, журнал, производство, торговля, улучшения, карта, записки, врата разломов, финал.
/// Окно — сцена Scenes/UI/Window.tscn (WindowView): здесь только наполнение данными.</summary>
public partial class Hud
{
    static readonly Color Muted = new("#6a7288"), Hint = new("#ffe9a0"), Violet = new("#c070ff");

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

    int Scroll(int cur, int visible) => Mathf.Max(0, cur - visible + 1);
    bool Sel(int i) => G.WinCur == i;
    Texture2D Icon(int item) => UiIcons.Item(item);
    void Det(string s, Color c, bool wrap = true, int maxLines = 0) => wv.Text(true, s, c, wrap, maxLines);

    // ───────────────────────── экипировка ─────────────────────────
    string EffText()
    {
        var parts = new List<string>();
        string[] n = { "Поджог", "Замедл.", "Заморозка", "Молния", "Залп", "Пробой", "Сифон", "Взрыв", "Дуга" };
        for (int i = 0; i < (int)Fxk.Count; i++) if (G.EffP[i] > 0) parts.Add($"{Loc.T(n[i])} {G.EffP[i]:0.#}");
        return parts.Count > 0 ? string.Join(" · ", parts) : Loc.T("нет");
    }

    void Synergies(int budget)
    {
        Det("Действующие синергии", Gold, false);
        if (G.ActiveSyn.Count == 0) Det("пока нет — соберите подходящие реликвии и оружие", Dim);
        foreach (var s in Db.Synergies) if (G.ActiveSyn.Contains(s.Id)) { Det($"✦ {s.Title} ({s.Path})", Good, false); Det(s.Desc, Dim, true, 2); }
        Det("Подсказки комбинаций", Cyan, false); int shown = 0;
        foreach (var s in Db.Synergies) { if (G.ActiveSyn.Contains(s.Id) || shown >= budget) continue; Det($"• {s.Title}: {s.Hint}", Text, true, 2); shown++; }
    }

    void DrawEquip(Vector2 vs)
    {
        wv.Begin(vs, "Экипировка", Cyan, G.WinTab == 2 ? "←/→ вкладка · ↑/↓ выбор · Enter создать · Q количество · C/Esc закрыть" : G.WinTab == 0 ? "←/→ вкладка · ↑/↓ · Enter надеть · Esc закрыть" : "←/→ вкладка · ↑/↓ · Enter · Esc закрыть", new Vector2(600, 330), 190);
        wv.SetTabs(Game.EquipTabs, G.WinTab);
        int fit = wv.Fit(11);
        switch (G.WinTab)
        {
            case 0:
                {
                    var tl = G.ToolList(); int nt = tl.Count;
                    string Slot(string r) { int sl = G.HotSlotOf(r); return sl >= 0 ? $"[{sl + 1}]" : ""; }
                    for (int i = 0; i < nt; i++)
                    {
                        var td = ToolDef.Get(tl[i]); bool held = G.CurW == -1 && G.CurTool == tl[i]; string nm = tl[i] == "pickaxe" ? Game.PickNames[G.Tier] : td.Name;
                        wv.Row(i, Sel(i), nm, ((held ? "★ " : "") + Slot(Game.RefTool(tl[i]))).Trim(), held ? Gold : Text);
                    }
                    for (int i = 0; i < G.Owned.Count; i++) { var d = Db.Weapon(G.Owned[i].Id); wv.Row(nt + i, Sel(nt + i), d.Name, ((G.CurW == i ? "★ " : "") + Slot(Game.RefWeapon(d.Id)) + " " + Db.ClassNames[(int)d.Cls]).Trim(), Db.RarCol[(int)d.Rar]); }
                    if (G.WinCur < nt)
                    {
                        var td = ToolDef.Get(tl[G.WinCur]); string nm = tl[G.WinCur] == "pickaxe" ? Game.PickNames[G.Tier] : td.Name;
                        Det($"{nm} · {Loc.T("инструмент")}", Gold, false); Det(td.Desc, Dim);
                        Det($"{Loc.T("Урон врагам")}: {Mathf.RoundToInt(td.CombatDamage * 100)}% {Loc.T("базового")} · {Loc.T("добыча")} ×{td.MiningPower:0.#}", Text, false);
                        Det($"{Loc.T("Дерево")} ×{td.EffWood:0.#} · {Loc.T("Камень")} ×{td.EffStone:0.#} · {Loc.T("Руда")} ×{td.EffOre:0.#} · {Loc.T("Завалы")} ×{td.EffRubble:0.#}", Dim, true, 2);
                        Det("[Enter] надеть · [Shift]+[1-3] в хотбар · [X] убрать", Hint, true, 2);
                        break;
                    }
                    int sel = G.WinCur - nt; var w = sel >= 0 && sel < G.Owned.Count ? Db.Weapon(G.Owned[sel].Id) : null;
                    if (w == null) break;
                    Det($"{w.Name} · {Db.RarName[(int)w.Rar]}", Db.RarCol[(int)w.Rar], false); Det(w.Desc, Dim);
                    Det($"Урон {G.WeaponDmg(w):0.#} · темп {1f / w.Cd:0.0}/с · слотов {w.Slots}", Text, false);
                    var ow = G.Owned[sel]; Det("Вставки (Backspace — снять):", Gold, false);
                    for (int s = 0; s < w.Slots; s++) Det(s < ow.Mods.Count ? $"▣ {Db.Mods[ow.Mods[s]].Name}" : "▢ пусто", s < ow.Mods.Count ? Good : Dim, false);
                    Det("Ваши вставки (цифра — вставить):", Gold, false);
                    for (int m = 0; m < Db.Mods.Length; m++) Det($"[{m + 1}] {Db.Mods[m].Name} ×{G.ModInv[m]}", G.ModInv[m] > 0 ? Text : Muted, false);
                    Det("Эффекты: " + EffText(), Hint, true, 2);
                    Det("[Enter] надеть · [Shift]+[1-3] в хотбар · [X] убрать", Hint, true, 2);
                    break;
                }
            case 1:
                {
                    wv.Text(false, $"Слоты: {G.EquippedRelics.Count}/{G.RelicSlots}", Dim, false); int sc = Scroll(G.WinCur, fit - 1);
                    for (int i = sc; i < G.OwnedRelics.Count && i < sc + fit - 1; i++) { var d = Db.Relic(G.OwnedRelics[i]); if (d == null) continue; bool eq = G.EquippedRelics.Contains(d.Id); wv.Row(i, Sel(i), (eq ? "✓ " : "  ") + d.Title, null, eq ? Good : Db.RarCol[d.Rarity]); }
                    if (G.OwnedRelics.Count == 0) wv.Text(false, "Реликвий нет: ищите в сундуках и у торговцев", Dim);
                    if (G.WinCur < G.OwnedRelics.Count)
                    {
                        var d = Db.Relic(G.OwnedRelics[G.WinCur]);
                        if (d != null) { Det($"{d.Title} · {Db.RarName[d.Rarity]}", Db.RarCol[d.Rarity], false); Det(d.Desc, Dim); Det("Теги: " + (d.Tags.Length > 0 ? string.Join(", ", d.Tags) : "нет") + " · Enter — надеть/снять", Gold, true, 2); }
                    }
                    var tc = new List<string>(); foreach (var kv in G.TagCount) tc.Add($"{kv.Key} ×{kv.Value}");
                    Det("Теги: " + (tc.Count > 0 ? string.Join(", ", tc) : "нет"), Hint, true, 2);
                    Synergies(3);
                    break;
                }
            case 2:
                {
                    var rows = G.CraftRows(); int sc = Scroll(G.WinCur, fit);
                    Texture2D GroupIcon(string g) => g == "hand" ? UiIcons.Item(UiIcons.Pickaxe) : g == "furnace" ? UiIcons.Tile(Art.Furnace) : UiIcons.Tile(Art.Forge);
                    for (int i = sc; i < rows.Count && i < sc + fit; i++)
                    {
                        var (grp, rec) = rows[i];
                        if (rec == null)
                        {
                            int cnt = G.RecipesOf(grp).Count; bool fold = G.CraftFold.Contains(grp); bool flash = grp != "hand" && G.StationFlashT > 0 && ((int)(G.UiTime * 8) % 2 == 0);
                            wv.Row(i, Sel(i), (fold ? "+ " : "- ") + Game.GroupName(grp), cnt.ToString(), flash ? Colors.White : Gold, null, null, false, false, GroupIcon(grp));
                            continue;
                        }
                        var st = G.StateOf(rec);
                        var col = st == RecState.Ready ? Good : st == RecState.Owned ? Dim : Warn;
                        string right = st == RecState.Owned ? "есть" : st == RecState.Missing ? "не хватает" : (rec.Extra != null && rec.Extra() > 0 ? $"в очереди {rec.Extra()}" : null);
                        wv.Row(i, Sel(i), "   " + rec.Name, right, col, null, null, false, false, rec.Icon >= 0 ? UiIcons.Item(rec.Icon) : null, st == RecState.Missing ? Bad : null);
                    }
                    if (G.WinCur >= rows.Count) break;
                    var (sg, sr) = rows[G.WinCur];
                    if (sr == null)
                    {
                        Det(Game.GroupName(sg), Gold, false);
                        Det($"Рецептов: {G.RecipesOf(sg).Count} · [Enter] — свернуть или развернуть", Text, false);
                        break;
                    }
                    var stt = G.StateOf(sr);
                    Det(sr.Name, stt == RecState.Ready ? Good : stt == RecState.Owned ? Text : Warn, false);
                    var scost = sr.CostNow;
                    if (scost != null)
                    {
                        var sb = new System.Text.StringBuilder(Loc.T("Нужно") + ":\n");
                        foreach (var c in scost) { int have = G.Inv[c[0]]; string clr = have >= c[1] ? "#9be07a" : "#f06a5e"; sb.Append($"[color={clr}]{It.Ic(c[0])}{Loc.T(It.Names[c[0]])} {have}/{c[1]}[/color]\n"); }
                        wv.Bb(true, sb.ToString().TrimEnd(), Text);
                    }
                    if (sr.Multi) Det($"{Loc.T("Количество")}: ×{(Game.QtySteps[G.CraftQty] == int.MaxValue ? Loc.T("макс.") + $" ({Math.Max(1, G.MaxCraft(sr))})" : Game.QtySteps[G.CraftQty].ToString())} · [Q]", Gold, false);
                    Det(stt == RecState.Ready ? "[Enter] — создать" : stt == RecState.Owned ? "Уже есть" : "Не хватает материалов", stt == RecState.Ready ? Good : stt == RecState.Owned ? Text : Bad, false);
                    break;
                }
            default:
                {
                    Det($"Фонарь героя: уровень {G.HeroLampLv}/3", new Color("#fff2b0"), false);
                    Det($"Радиус света: {(int)G.PlayerLightR} px", Text, false);
                    Det("Уровень: +12% радиуса. Ур.2: свет жжёт. Ур.3: аура", Dim);
                    if (G.HeroLampLv < 3)
                    {
                        var c = Game.HeroCostFor(G.HeroLampLv); Det($"Следующий уровень: {G.NeedStr(c)}", G.Can(c) ? Good : Warn);
                        Det(G.BpDone.Contains("bp_herolamp") ? "[Enter] улучшить" : "Нужен чертёж «Фонарь героя» (журнал [J])", G.BpDone.Contains("bp_herolamp") ? Gold : Warn);
                    }
                    int cry = 0; foreach (var l in G.Lamps) if (l.Kind == 2) cry++;
                    Det($"Фонарей: {G.Lamps.Count} (кристальных {cry}) · кристаллы +12% (до 3)", Dim);
                    break;
                }
        }
    }

    // ───────────────────────── журнал ─────────────────────────
    void DrawJournal(Vector2 vs)
    {
        wv.Begin(vs, "Журнал", Gold, "←/→ вкладка · ↑/↓ прокрутка · J/Esc закрыть", new Vector2(420, 320));
        wv.SetTabs(new[] { "Чертежи", "Находки", "Цели" }, G.WinTab); int vis = wv.Fit(11, 24);
        if (G.WinTab == 0)
        {
            wv.Text(false, $"Чертежей: {G.BpDone.Count}/{Db.Bps.Length}. Остальные — в лавке", Dim, false); int sc = Scroll(G.WinCur, vis);
            for (int i = sc; i < Db.Bps.Length && i < sc + vis; i++) { var b = Db.Bps[i]; bool ok = G.BpDone.Contains(b.Id); int fr = G.Frag.GetValueOrDefault(b.Id); wv.Row(i, Sel(i), (ok ? "✓ " : "  ") + b.Name, ok ? "изучен" : $"{fr}/{b.Need}", ok ? Good : fr > 0 ? Warn : Dim); }
            if (G.WinCur < Db.Bps.Length) wv.Text(false, Db.Bps[G.WinCur].Desc, Hint, true, 2);
        }
        else if (G.WinTab == 2)
        {
            int per = Math.Max(1, vis / 2); int sc = Scroll(G.WinCur, per);
            for (int i = sc; i < G.Goals.Count && i < sc + per; i++) { var gl = G.Goals[i]; wv.Row(i, Sel(i), (i == 0 ? "▶ " : "• ") + gl.Title, null, i == 0 ? Gold : Text, gl.Needs, Dim); }
            if (G.Goals.Count == 0) wv.Text(false, "Все цели выполнены!", Good, false);
        }
        else
        {
            if (G.Found.Count == 0) wv.Text(false, "Редких находок нет: ищите в сундуках и тайниках", Dim);
            int sc = Scroll(G.WinCur, vis);
            for (int i = sc; i < G.Found.Count && i < sc + vis; i++) { var s = G.Found[i]; Color col = Text; for (int k = 0; k < 5; k++) if (s.StartsWith(Db.RarName[k])) col = Db.RarCol[k]; wv.Row(i, Sel(i), s, null, col); }
        }
    }

    // ───────────────────────── производство ─────────────────────────
    string StatusText(Building b) => b.Off ? "пауза" : b.Status switch { 0 => "работает", 1 => "нет сырья", 2 => "склад полон", 3 => "нет связи", 5 => "нет режима", _ => "работает" };

    void DrawProd(Vector2 vs)
    {
        wv.Begin(vs, "Производство", new Color("#ff9a2e"), "↑/↓ здание · ←/→ режим · 1-3 приоритет · Enter пауза · U ап", new Vector2(600, 330));
        var list = G.ProdList();
        for (int i = 0, sh = 0; i < It.Count && sh < 12; i++) if (G.Stock[i] > 0) { wv.Chip(Icon(i), G.Stock[i].ToString(), Text); sh++; }
        wv.Text(false, $"Склад {G.StockTotal}/{G.StockCap} на ресурс · скорость ×{G.ProdMul:F2} · жителей {G.Villagers.Count}", Dim, false);
        float[] ws = { 100, 20, 72, 56, 66, 0 };
        wv.Cols(-1, false, new[] { "Здание", "Ур", "Состояние", "Связь", "Приоритет", "Режим" }, ws, new[] { Gold, Gold, Gold, Gold, Gold, Gold }, null, Dim);
        if (list.Count == 0) wv.Text(false, "Нет зданий: стройте лесопилку, шахту, цех [B]", Dim);
        int vis = wv.Fit(11, 60), sc = Scroll(G.WinCur, vis);
        for (int i = sc; i < list.Count && i < sc + vis; i++)
        {
            var b = list[i]; int bk = Game.BIndex(b.Type); bool lk = b.Linked || b.Forced; var modes = Game.BModes[bk];
            wv.Cols(i, i == G.WinCur,
                new[] { Game.BNames[bk], b.Level.ToString(), StatusText(b), lk ? (b.Forced ? "авто" : "связь") : "нет связи", Game.PrioNames[b.Prio], modes != null ? modes[b.Mode % modes.Length] : "—" }, ws,
                new[] { b.Off ? Dim : Text, Gold, b.Status == 0 && !b.Off ? Good : b.Status == 4 || b.Off ? Dim : Warn, lk ? Good : Bad, b.Prio == 0 ? Warn : b.Prio == 2 ? Dim : Text, Cyan },
                $"{(int)(b.Prog / Mathf.Max(.1f, Game.BIntervalOf(bk, b.Level)) * 100)}%", Dim);
        }
        wv.Text(false, "Приоритет: кто первым берёт сырьё. Режим: продукт. Далеко от склада — соединяйте узлами", Dim, true, 3);
    }

    // ───────────────────────── торговля ─────────────────────────
    void DrawTrade(Vector2 vs)
    {
        bool w = G.TradeEv != null;
        wv.Begin(vs, w ? "Странствующий торговец" : $"Торговая лавка · ур.{G.StallLevel}", w ? Good : new Color("#e0c080"), "←/→ купить/продать · ↑/↓ · Enter сделка (Shift ×10) · Esc", new Vector2(420, 320));
        wv.SetTabs(new[] { "Купить", "Продать" }, G.TradeTab); wv.Chip(Icon(It.Coins), $"{G.Inv[It.Coins]}", new Color("#ffd86b"));
        int vis = wv.Fit(11, 36);
        if (G.TradeTab == 0)
        {
            if (G.CurOffers.Count == 0) wv.Text(false, "Товаров нет. Улучшайте лавку", Dim);
            int sc = Scroll(G.WinCur, vis);
            for (int i = sc; i < G.CurOffers.Count && i < sc + vis; i++) { var o = G.CurOffers[i]; bool bought = G.OfferBought(o); wv.Row(i, Sel(i), (bought ? "✓ " : "") + o.Title, bought ? "куплено" : $"{o.Price}", bought ? Dim : G.Inv[It.Coins] >= o.Price ? Db.RarCol[(int)o.Rar] : Warn); }
            if (G.WinCur < G.CurOffers.Count) wv.Text(false, G.CurOffers[G.WinCur].Desc, Hint, true, 2);
        }
        else
        {
            var sl = G.SellList(); if (sl.Count == 0) wv.Text(false, "Нечего продавать.", Dim, false);
            int sc = Scroll(G.WinCur, vis);
            for (int i = sc; i < sl.Count && i < sc + vis; i++) wv.Row(i, Sel(i), $"{It.Ic(sl[i])}{Loc.T(It.Names[sl[i]])} ×{G.Inv[sl[i]]}", $"{G.SellPrice(sl[i])}", It.Col[sl[i]]);
            string d = ""; for (int i = 0; i < G.Demand.Length; i++) d += $"{Loc.T(Game.CatNames[i])} {(int)(G.Demand[i] * 100)}%  ";
            wv.Text(false, Loc.T("Спрос: ") + d, Dim, true, 2);
        }
        if (G.TradeMsg != "") wv.Text(false, G.TradeMsg, Good, false);
    }

    // ───────────────────────── карта ─────────────────────────
    void DrawMap(Vector2 vs)
    {
        float s = Mathf.Min(1.4f, (vs.X - 60) / World.Wd); float mw = Mathf.Round(World.Wd * s), mh = Mathf.Round(World.Ht * s);
        wv.Begin(vs, "Карта · M/Esc — закрыть", Cyan, "● вы  ◆ страж  золото — горит  ▮ печать", new Vector2(mw + 24, mh + 68), -1, .7f);
        wv.Map.CustomMinimumSize = new Vector2(mw, mh + 12);
        var o = Vector2.Zero;
        wv.MapBack(new Rect2(o, new Vector2(mw, mh)), new Color(.03f, .04f, .07f));
        wv.MapWorld(o, s);
        for (int i = 1; i < 5; i++) wv.MapRect(new Rect2(o.X + i * 128 * s, o.Y, 1, mh), new Color(1, 1, 1, .2f));
        for (int i = 0; i < 5; i++) wv.MapLabel(Game.RegionNames[i], o + new Vector2(i * 128 * s, mh + 1), Game.RegionCol(i), 128 * s, HorizontalAlignment.Center);
        foreach (var an in G.W.Ancients) if (an.Lit) wv.MapDisc(o + an.Pos / 16f * s, G.AncientR(an) / 16f * s, new Color(1f, .85f, .4f, .18f));
        foreach (var l in G.Lamps) wv.MapDisc(o + l.Pos / 16f * s, 5 * s, new Color(1f, .85f, .4f, .16f));
        foreach (var seal in new[] { G.W.Seal1, G.W.Seal2, G.W.Seal3, G.W.Seal4 })
            if (seal.Count > 0 && G.W.Get(seal[0].X, seal[0].Y) == Tl.Seal) wv.MapRect(new Rect2(o + new Vector2(seal[0].X, seal[0].Y - 1) * s, new Vector2(2 * s, 9 * s)), new Color("#d58cff"));
        foreach (var an in G.W.Ancients)
        {
            var p = (o + an.Pos / 16f * s).Round(); if (!(an.Lit || G.IsExplored(an.Pos))) continue;
            if (an.Boss) wv.MapDiamond(p, 3f, an.Lit ? Gold : new Color("#d58cff"));
            else wv.MapRect(new Rect2(p - new Vector2(1.5f, 1.5f), new Vector2(3, 3)), an.Lit ? Gold : Colors.White);
        }
        foreach (var p in G.W.Pois) { if (!(p.Seen || p.Revealed) || p.State >= 3) continue; wv.MapRect(new Rect2((o + p.Pos / 16f * s).Round(), new Vector2(1, 1)), Game.PoiColor(p.Kind)); }
        foreach (var b in G.Buildings) wv.MapRect(new Rect2((o + b.Pos / 16f * s).Round() - new Vector2(1, 1), new Vector2(2, 2)), new Color("#d9b27c"));
        foreach (var ev in G.Events) { var ep = (o + ev.Pos / 16f * s).Round(); wv.MapRect(new Rect2(ep - new Vector2(1.5f, 1.5f), new Vector2(3, 3)), Game.EvColor(ev.Kind)); }
        var pp = (o + G.PPos / 16f * s).Round(); bool blink = Mathf.Sin(G.Time * 6f) > 0;
        wv.MapRect(new Rect2(pp - new Vector2(2, 2), new Vector2(4, 4)), new Color("#090c14")); wv.MapRect(new Rect2(pp - new Vector2(1, 1), new Vector2(2, 2)), blink ? new Color("#9be07a") : new Color("#5fbf4a"));
    }

    // ───────────────────────── записки ─────────────────────────
    // ───────────────────────── записки: раскраска ключевых слов и «печатная машинка» ─────────────────────────
    string storyPlain = ""; float storyStart; bool storyTyping; int storyTicks;

    /// <summary>Пропуск печати: первое нажатие показывает текст целиком. Возвращает true, если нажатие потрачено на это.</summary>
    public bool StorySkip() { if (!storyTyping) return false; storyStart = -1000f; return true; }

    void DrawStory(Vector2 vs)
    {
        wv.Begin(vs, G.StoryTitle, Gold, "[E] / [Esc] — закрыть", new Vector2(330, 190));
        string plain = Loc.T(G.StoryText);
        if (plain != storyPlain) { storyPlain = plain; storyStart = G.UiTime; storyTicks = 0; }
        int n = (int)((G.UiTime - storyStart) * 70f); storyTyping = n < plain.Length;
        if (G.StorySub != "") wv.Text(false, G.StorySub, Dim, false);
        wv.Rich(plain, storyTyping ? n : -1);
        if (storyTyping && n / 4 > storyTicks) { storyTicks = n / 4; Sfx.Play("tick", -16, 1.5f + .1f * (storyTicks % 3)); }
    }

    string finKey = ""; float finStart;

    void DrawFinale(Vector2 vs)
    {
        if (G.FinStage == 0) return;
        if (G.FinStage == 1)
        {
            float k = Mathf.Clamp((G.FinT - 6f) / 3f, 0, 1); mv.Begin(false, 0, null, false);
            if (k > 0) mv.Line("СВЕТ ВОЗВРАЩАЕТСЯ", new Vector2(0, vs.Y * .4f - 14), vs.X, true, new Color(1, .95f, .7f, k)); return;
        }
        float fade = Mathf.Clamp(G.FinT / 1.2f, 0, 1);
        if (G.FinStage == 2)
        {
            mv.Begin(false, .82f, G.FinScene >= Story.Finale.Length - 1 ? "[Enter] — титры" : "[Enter] — дальше · [Esc] — пропустить", false);
            float w = Mathf.Min(360, vs.X - 40); string txt = Loc.T(Story.Finale[G.FinScene]);
            if (txt != finKey) { finKey = txt; finStart = G.UiTime; }
            mv.RichLine(txt, new Vector2((vs.X - w) / 2, vs.Y * .38f - 7), w, new Color(1, .96f, .85f, fade), TextFx.Typed(txt, G.UiTime - finStart, 45f));
            return;
        }
        mv.Begin(false, .94f, "[Enter] — продолжить игру", false);
        float y = vs.Y - G.FinT * 24f + 10;
        for (int i = 0; i < Story.Credits.Length; i++)
        {
            float yy = y + i * 18; if (yy < -20 || yy > vs.Y + 20) continue; bool head = i == 0 || (i > 0 && Story.Credits[i - 1] == "");
            mv.Line(Story.Credits[i], new Vector2(0, yy - (head ? 14 : 7)), vs.X, head, head ? Gold : Text);
        }
    }

    // ───────────────────────── врата разломов ─────────────────────────
    void DrawRiftWin(Vector2 vs)
    {
        bool offer = G.RiftState == 1;
        wv.Begin(vs, offer ? "Разлом: временная реликвия" : "Врата разломов", Violet, offer ? "[1]-[3] или Enter — выбрать" : "←/→ вкладки · ↑/↓ · Enter · Esc", new Vector2(400, 260));
        if (offer)
        {
            for (int i = 0; i < G.RiftOffer.Count; i++) { var d = Db.Relic(G.RiftOffer[i]); wv.Row(i, Sel(i), $"[{i + 1}] {d.Title}", null, new Color("#e8d8ff"), d.Desc, Dim); }
            return;
        }
        wv.SetTabs(Game.RiftTabs, G.WinTab); wv.Chip(Icon(It.RiftShard), $"{G.Inv[It.RiftShard]}", It.Col[It.RiftShard]);
        if (G.WinTab == 0)
            for (int i = 0; i < Game.MaxRiftTier; i++) { bool open = G.RiftTierOpen(i + 1), done = (G.RiftFirst & (1 << (i + 1))) != 0; wv.Row(i, Sel(i), $"{Loc.T("Уровень")} {i + 1}" + (done ? " ✓" : ""), open ? $"×{1 + .22f * (i + 1):0.0} · {3 + 2 * (i + 1)}" : "закрыт", open ? Text : Muted); }
        else if (G.WinTab == 1)
            for (int i = 0; i < Game.Legends.Length; i++) { var l = Game.Legends[i]; bool has = G.HasLegend(i); wv.Row(i, Sel(i), l.Name + (has ? " ✓" : ""), has ? "получено" : null, has ? Good : G.Can(l.Cost) ? Gold : Dim, has ? l.Desc : l.Desc + " · " + Game.CostStr(l.Cost), Dim); }
        else
            for (int i = 0; i < Game.CosmNames.Length; i++) { bool own = (G.CosmMask & (1 << i)) != 0; wv.Row(i, Sel(i), Game.CosmNames[i] + (G.CosmSel == i ? Loc.T(" (надет)") : ""), own ? null : "закрыт", own ? Text : Muted); }
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
