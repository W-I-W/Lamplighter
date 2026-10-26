using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public sealed class Offer { public string Key, Kind, Id, Title, Desc; public int Item, N, Price; public Rar Rar; }

/// <summary>Торговля: цена продажи всегда ниже цены покупки, спрос падает при продаже и медленно восстанавливается.</summary>
public partial class Game
{
    public int TradeEpoch; public HashSet<string> Bought = new(); public float[] Demand = { 1, 1, 1, 1, 1 };
    public int TradeTab, TradeCur; public GEvent TradeEv; public List<Offer> CurOffers = new(); public string TradeMsg = "";
    public static readonly string[] CatNames = { "Простые", "Руда", "Слитки и детали", "Редкое", "Свет" };

    static readonly int[] BasePrice = { 2, 2, 4, 6, 7, 10, 16, 40, 45, 10, 28, 30, 26, 24, 0, 90, 0, 60, 85 };
    static int Cat(int item) => item switch
    {
        It.Wood or It.Stone => 0, It.CopperOre or It.IronOre or It.SilverOre => 1, It.Copper or It.Iron or It.Silver or It.Gear or It.Lens => 2,
        It.Crystal or It.Obsidian or It.Ember or It.Aether or It.AncCrystal => 3, It.Shard or It.Relic => 4, _ => -1
    };
    public static bool Tradable(int item) => Cat(item) >= 0;
    public int StallLevel { get { int l = 0; foreach (var b in Buildings) if (b.Type == Tl.Stall) l = Math.Max(l, b.Level); return l; } }

    public int SellPrice(int item)
    {
        int c = Cat(item); if (c < 0) return 0;
        return Math.Max(1, (int)(BasePrice[item] * .5f * Demand[c] * (1f + N("Trade"))));
    }
    public int BuyPrice(int item)
    {
        int c = Cat(item); float d = c < 0 ? 1f : Mathf.Clamp(Demand[c], .5f, 1.05f);
        return Math.Max(2, (int)(BasePrice[item] * 1.45f * (2f - d) * (1f - N("Trade") * .5f)));
    }

    void UpdateTrade(float dt)
    {
        for (int i = 0; i < Demand.Length; i++) Demand[i] = Mathf.Min(1.05f, Demand[i] + .0015f * dt);
    }

    public void OpenTrade(GEvent ev)
    {
        CloseWindows(); Window = Win.Trade; TradeEv = ev; TradeTab = 0; TradeCur = 0; TradeMsg = "";
        CurOffers = BuildOffers(ev); Sfx.Play("tick");
    }

    string BKey(string k) => $"{(TradeEv != null ? "w" + TradeEv.Seed : "s" + TradeEpoch)}:{k}";
    public bool OfferBought(Offer o) => Bought.Contains(BKey(o.Key));

    List<Offer> BuildOffers(GEvent ev)
    {
        int lvl = ev == null ? StallLevel : 3;
        float mult = ev == null ? 1f : 1.3f;
        var r = new Random(ev != null ? ev.Seed : W.Seed * 13 + TradeEpoch * 977 + lvl * 31);
        var l = new List<Offer>();
        void Item(int item, int n) => l.Add(new Offer { Key = $"i{item}x{n}", Kind = "item", Item = item, N = n, Price = (int)(BuyPrice(item) * n * mult), Title = $"{It.Names[item]} ×{n}", Desc = "Материалы без добычи", Rar = Rar.Common });
        void Frag(string bp, int price, Rar rar = Rar.Uncommon)
        {
            var d = Db.Bp(bp); if (d == null || BpDone.Contains(bp)) return;
            l.Add(new Offer { Key = "f" + bp, Kind = "frag", Id = bp, Price = (int)(price * mult), Title = $"Фрагмент: {d.Name}", Desc = $"{Frag_Count(bp)}/{d.Need} · {d.Desc}", Rar = rar });
        }
        void Mod(int m, int price) => l.Add(new Offer { Key = "m" + m, Kind = "mod", Item = m, Price = (int)(price * mult), Title = "Вставка: " + Db.Mods[m].Name, Desc = Db.Mods[m].Desc, Rar = Rar.Uncommon });
        void Relic(string id)
        {
            var d = Db.Relic(id); if (d == null || OwnedRelics.Contains(id)) return;
            l.Add(new Offer { Key = "r" + id, Kind = "relic", Id = id, Price = (int)((400 + 250 * d.Rarity) * mult), Title = "Реликвия: " + d.Title, Desc = d.Desc, Rar = (Rar)d.Rarity });
        }
        if (ev == null)
        {
            Item(It.Wood, 10); Item(It.Stone, 10); Item(It.Copper, 3); Item(It.Iron, 2); Item(It.Shard, 4);
            Frag("bp_hub", 140); Frag("bp_refinery", 130); Frag("bp_mod_multi", 120); Frag("bp_amp", 150);
            Mod(3, 160); Mod(4, 160);
        }
        if (lvl >= 2)
        {
            Item(It.Crystal, 2); Item(It.Silver, 2);
            foreach (var bp in new[] { "bp_sword2", "bp_bow2", "bp_staff2" }) Frag(bp, 180);
            var adv = new List<string>(); foreach (var b in new[] { "bp_mod_ignite", "bp_mod_frost", "bp_mod_shock", "bp_mod_siphon", "bp_barrier" }) if (!BpDone.Contains(b)) adv.Add(b);
            for (int i = 0; i < 2 && adv.Count > 0; i++) { int k = r.Next(adv.Count); Frag(adv[k], 230, Rar.Rare); adv.RemoveAt(k); }
            Mod(r.Next(3), 230); Mod(5, 260);
            l.Add(new Offer { Key = "anc", Kind = "item", Item = It.AncCrystal, N = 1, Price = (int)(BuyPrice(It.AncCrystal) * 1.1f * mult), Title = "Древний кристалл ×1", Desc = "Для улучшений света и зданий", Rar = Rar.Rare });
        }
        if (lvl >= 3)
        {
            var pool = new List<string>(); foreach (var id in CommonRelics) if (!OwnedRelics.Contains(id)) pool.Add(id); foreach (var id in RareRelics) if (!OwnedRelics.Contains(id)) pool.Add(id);
            for (int i = 0; i < 2 && pool.Count > 0; i++) { int k = r.Next(pool.Count); Relic(pool[k]); pool.RemoveAt(k); }
            Item(It.Gear, 2); Item(It.Lens, 2);
            if (BeaconLevel >= 3) { Item(It.Obsidian, 2); Item(It.Ember, 2); }
            foreach (var bp in new[] { "bp_sword3", "bp_bow3", "bp_staff3", "bp_herolamp" }) Frag(bp, 300, Rar.Rare);
        }
        if (ev != null) { Mod(6, 280); Mod(0, 240); }
        return l;
    }

    int Frag_Count(string bp) => Frag.GetValueOrDefault(bp);

    public void BuyOffer(Offer o)
    {
        if (OfferBought(o)) { TradeMsg = "Уже куплено"; return; }
        if (Inv[It.Coins] < o.Price) { Fail($"Не хватает монет: нужно {o.Price}"); return; }
        Inv[It.Coins] -= o.Price; Bought.Add(BKey(o.Key));
        switch (o.Kind)
        {
            case "item": Inv[o.Item] += o.N; break;
            case "frag": AddFragment(o.Id); break;
            case "mod": ModInv[o.Item]++; Toast("Куплена вставка: " + Db.Mods[o.Item].Name); break;
            case "relic": GiveRelic(o.Id); break;
        }
        Sfx.Play("pickup"); TradeMsg = "Куплено: " + o.Title; TradeEpochSoft();
        CurOffers = BuildOffers(TradeEv);
    }

    void TradeEpochSoft() { }

    public void SellItem(int item, int n)
    {
        if (!Tradable(item) || Inv[item] <= 0) return;
        n = Math.Min(n, Inv[item]); int total = 0, c = Cat(item);
        for (int i = 0; i < n; i++) { total += SellPrice(item); Demand[c] = Mathf.Max(.45f, Demand[c] * .965f); }
        Inv[item] -= n; Inv[It.Coins] += total; Sfx.Play("pickup", -3, 1.3f);
        TradeMsg = $"Продано {n} × {It.Names[item]} за {total} монет";
    }

    public void TradeNextEpoch() { TradeEpoch++; }
}
