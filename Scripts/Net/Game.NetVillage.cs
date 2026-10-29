using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter;

/// <summary>Деревня в кооперативе. Хост — хозяин жителей, зданий, склада и производства; клиент показывает присланное, плавно двигает жителей и отправляет хосту команды
/// (назначить работника, построить, улучшить, разобрать, настроить, забрать ресурсы, освободить жителя). Игровые ресурсы игрока (его инвентарь) остаются у него.</summary>
public partial class Game
{
    float netVillT, netPosT, netVillFull; string netVillHash = "";
    bool NetClient => NetService.Active && NetService.Role == NetRole.Client;
    bool NetHost => NetService.Active && NetService.Role == NetRole.Host;

    public Godot.Collections.Array NetVillRoster()
    {
        var a = new Godot.Collections.Array();
        foreach (var v in Villagers) a.Add(new Godot.Collections.Dictionary { ["id"] = v.Id, ["name"] = v.Name, ["prof"] = v.Prof, ["src"] = v.Src, ["work"] = v.Work, ["home"] = v.Home, ["x"] = v.Pos.X, ["y"] = v.Pos.Y, ["talk"] = v.Talk });
        return a;
    }

    public Godot.Collections.Array NetBldRoster()
    {
        var a = new Godot.Collections.Array();
        foreach (var b in Buildings) a.Add(new Godot.Collections.Dictionary { ["t"] = (int)b.Type, ["x"] = b.X, ["y"] = b.Y, ["l"] = b.Level, ["m"] = b.Mode, ["p"] = b.Prio, ["o"] = b.Off, ["u"] = b.Manual, ["f"] = b.Forced });
        return a;
    }

    public int[] NetStock() => (int[])Stock.Clone();

    /// <summary>Хост: раз в полсекунды проверить, изменилась ли деревня (жители, здания), и разослать снимок; позиции жителей — 5 раз в секунду; полный снимок — раз в 3 с (склад).</summary>
    void NetVillageTick(float dt)
    {
        if (!NetHost || Net == null) return;
        netVillT -= dt; netPosT -= dt; netVillFull -= dt;
        if (netPosT <= 0 && Villagers.Count > 0)
        {
            netPosT = .2f; var d = new Godot.Collections.Array<float>();
            foreach (var v in Villagers) { d.Add(v.Id); d.Add(v.Pos.X); d.Add(v.Pos.Y); d.Add((int)v.State); d.Add(v.Face); }
            Net.SendVillPos(d);
        }
        if (netVillT > 0) return; netVillT = .5f;
        var sb = new System.Text.StringBuilder();
        foreach (var v in Villagers) sb.Append(v.Id).Append(v.Prof).Append(v.Work).Append(v.Home).Append(';');
        foreach (var b in Buildings) sb.Append((int)b.Type).Append(b.X).Append(',').Append(b.Y).Append(b.Level).Append(b.Mode).Append(b.Prio).Append(b.Off).Append(b.Manual).Append(';');
        string h = sb.ToString();
        if (h != netVillHash || netVillFull <= 0) { netVillHash = h; netVillFull = 3f; Net.BroadcastVillage(); }
    }

    /// <summary>Клиент: применить снимок деревни хоста. Новые жители создаются тем же Id (дублей нет), лишние здания убираются, склад заменяется.</summary>
    public void NetApplyVillage(Godot.Collections.Array vills, Godot.Collections.Array blds, int[] stock)
    {
        if (!NetClient) return;
        // здания
        var keep = new HashSet<int>();
        foreach (var o in blds)
        {
            var d = o.AsGodotDictionary(); int x = (int)d["x"], y = (int)d["y"], t = (int)d["t"]; if (!World.In(x, y) || !Enum.IsDefined(typeof(Tl), (byte)t)) continue;
            int key = World.Key(x, y); keep.Add(key); var b = BuildingByKey(key);
            if (b == null) { b = new Building { Type = (Tl)t, X = x, Y = y }; Buildings.Add(b); SetObj(x, y, b.Type); Burst(b.Pos, new Color("#d9b27c"), 12, 60); }
            int lv = Math.Clamp((int)d["l"], 1, 5); if (b.Level != lv) { b.Level = lv; SetObj(x, y, b.Type); }
            b.Mode = (int)d["m"]; b.Prio = Math.Clamp((int)d["p"], 0, 2); b.Off = (bool)d["o"]; b.Manual = (bool)d["u"]; b.Forced = (bool)d["f"];
        }
        for (int i = Buildings.Count - 1; i >= 0; i--) if (!keep.Contains(BKey(Buildings[i]))) { var b = Buildings[i]; Buildings.RemoveAt(i); SetObj(b.X, b.Y, Tl.None); }
        if (stock != null) for (int i = 0; i < Stock.Length && i < stock.Length; i++) Stock[i] = stock[i];
        // жители
        var ids = new HashSet<int>();
        foreach (var o in vills)
        {
            var d = o.AsGodotDictionary(); int id = (int)d["id"]; ids.Add(id); var v = VillagerById(id);
            if (v == null) { v = AddVillager(id, new Vector2((float)d["x"], (float)d["y"]), (string)d["prof"], (string)d["src"], (string)d["name"]); if (v == null) continue; Texts.Add(new FText { P = v.Pos + new Vector2(0, -18), S = $"{v.Name} — новый житель", Life = 2.5f, C = Colors.White }); }
            v.Prof = (string)d["prof"]; v.Work = (int)d["work"]; v.Home = (int)d["home"]; v.Talk |= (int)d["talk"]; if (!v.NetInit) { v.NetPos = new Vector2((float)d["x"], (float)d["y"]); }
        }
        Villagers.RemoveAll(v => !ids.Contains(v.Id));
        RecomputeVillagers();
    }

    public void NetApplyVillPos(Godot.Collections.Array<float> data)
    {
        if (!NetClient) return;
        for (int i = 0; i + 4 < data.Count; i += 5)
        {
            var v = VillagerById((int)data[i]); if (v == null) continue;
            v.NetPos = new Vector2(data[i + 1], data[i + 2]); int st = (int)data[i + 3]; if (st >= 0 && st <= 3) v.State = (VState)st; int f = (int)data[i + 4]; if (f != 0) v.Face = f < 0 ? -1 : 1;
            if (!v.NetInit) { v.Pos = v.NetPos; v.NetInit = true; }
        }
    }

    /// <summary>Хост: команда строительства от клиента (стоимость клиент уже оплатил из своих ресурсов). Возвращает ресурсы для выдачи клиенту (команда «забрать») или null.</summary>
    public int[] NetBuildCmd(int op, int a, int b, int c)
    {
        switch (op)
        {
            case 1:
                {
                    if (a < 0 || a >= BCount) return null;
                    // отказ (клетка занята, здание такого типа уже есть — например, два игрока ставят склад одновременно): стоимость возвращается тому, кто её заплатил
                    bool bad = !World.In(b, c) || W.Get(b, c) != Tl.None || (a != 10 && HasBuilding(BTypes[a])) || (a == 10 && Buildings.FindAll(h => h.Type == Tl.House).Count >= 8);
                    if (bad) { var refund = new int[It.Count]; foreach (var cc in BCost(a, 1)) refund[cc[0]] += cc[1]; return refund; }
                    CreateBuilding(a, new Vector2I(b, c)); return null;
                }
        }
        var bd = BuildingByKey(a); if (bd == null) return null; int bk = BIndex(bd.Type);
        switch (op)
        {
            case 2: if (bd.Level < MaxLv(bk)) { bd.Level++; SetObj(bd.X, bd.Y, bd.Type); Fx("building_upgrade", bd.Pos); TradeEpoch++; netT = 0; } break;
            case 3: Buildings.Remove(bd); SetObj(bd.X, bd.Y, Tl.None); netT = 0; break;
            case 4: { var modes = BModes[bk]; if (modes != null) { bd.Mode = Math.Clamp(b, 0, modes.Length - 1); bd.Prog = 0; } break; }
            case 5: bd.Prio = Math.Clamp(b, 0, 2); netT = 0; break;
            case 6: bd.Off = b != 0; break;
            case 7:
                {
                    var g = new int[It.Count]; bool any = false;
                    for (int i = 0; i < It.Count; i++) { int n = Stock[i] + bd.Local[i]; if (n > 0) { g[i] = n; any = true; Stock[i] = 0; bd.Local[i] = 0; } }
                    return any ? g : null;
                }
        }
        return null;
    }

    /// <summary>Клиент: ресурсы, которые хост выдал по команде «забрать».</summary>
    public void NetGrant(int[] items)
    {
        int tot = 0, row = 0;
        for (int i = 0; i < It.Count && i < items.Length; i++) if (items[i] > 0) { Inv[i] += items[i]; tot += items[i]; Texts.Add(new FText { P = PPos + new Vector2(0, -12 - row++ * 13), S = $"{It.Ic(i)}+{items[i]} {It.Names[i]}", Life = 1.2f, C = It.Col[i] }); }
        if (tot > 0) Sfx.Play("pickup");
    }
}
