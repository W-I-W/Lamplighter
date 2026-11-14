using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Разовая добыча мира в кооперативе решается хостом (см. NetNode.Claims). Гость ломает блок у себя сразу (видно без задержки), но добычу получает,
/// только когда хост подтвердит, что блок у него ещё цел, — и хост в тот же момент убирает блок у себя. Места интереса с наградой гость открывает после
/// такого же подтверждения; подтверждённое место хост сразу помечает пройденным, поэтому второй игрок его уже не откроет.</summary>
public partial class Game
{
    const float MineClaimReach = 320f;                     // цепная добыча ломает соседние блоки на несколько клеток от удара, плюс запас на задержку сети: защита от добычи через полкарты, а не точная дистанция
    const float PoiClaimReach = 120f;
    readonly Dictionary<int, (int code, int fid)> pendingMine = new();
    readonly Dictionary<int, Action> pendingPoi = new();

    /// <summary>Блоки, с которых падает добыча: гость получает её только по ответу хоста. Могила (своя), сундук хранения (своё содержимое) и блоки без добычи — как раньше, сразу.</summary>
    static bool ClaimedLoot(Tl t) => t is Tl.Tree or Tl.DeadTree or Tl.Pine or Tl.Stone or Tl.Ruin or Tl.Rubble or Tl.ChestOpen or Tl.Copper or Tl.Iron or Tl.Lumen
        or Tl.Aether or Tl.SilverOre or Tl.CrystalOre or Tl.Obsidian or Tl.Ember or Tl.FrostOre or Tl.MeteorOre;

    static bool ClaimedPoi(Tl t) => t is Tl.Chest or Tl.Shrine or Tl.Obelisk or Tl.TechCache or Tl.Spire;

    void ClaimMine(Vector2I tile, int code, int fid = 0)
    {
        int key = World.Key(tile.X, tile.Y);
        pendingMine[key] = (code, fid);
        if (code != BushCutCode && shObj != null && key < shObj.Length) shObj[key] = (byte)Tl.None;      // клетку разошлёт хост по заявке; общим слепком её не слать (при расхождении он убрал бы у хоста другой блок)
        Net?.SendMineClaim(tile.X, tile.Y, code);
    }

    /// <summary>Хост: гость разбил блок (или срубил куст). true — блок был цел и теперь убран у хоста, добыча гостя; false — его уже забрал другой игрок.</summary>
    public bool HostMineClaim(long sender, int x, int y, int code)
    {
        if (!World.In(x, y) || PosOf(sender) is not Vector2 sp || sp.DistanceTo(TileCenter(x, y)) > MineClaimReach) return false;
        int key = World.Key(x, y);
        if (code == BushCutCode)
        {
            if (CutBush.Contains(key)) return false;
            NetApplyTile(x, y, BushCutCode, 0); Net?.RelayTile(sender, x, y, BushCutCode, 0);
            return true;
        }
        var cur = W.Get(x, y);
        if (code < 0 || code > 254 || !ClaimedLoot((Tl)code) || cur != (Tl)code || BuildingByKey(key) != null)
        {
            if (cur != Tl.None && SyncTile(Tl.None, cur)) Net?.SendTileTo(sender, x, y, (int)cur, Placed.Contains(key) ? 1 : 0);      // у гостя на этом месте было не то, что у хоста: вернуть ему настоящую клетку
            return false;
        }
        SetObj(x, y, Tl.None); Placed.Remove(key); mineMemo.Remove(key);
        if (MineTile == new Vector2I(x, y)) { MineProg = 0; MineTile = new Vector2I(-1, -1); }      // хост бил этот же блок: его удар больше ничего не даст
        Burst(TileCenter(x, y), TileColor(cur), 6, 45);
        return true;                                                                       // пустую клетку остальные гости получат общим слепком
    }

    /// <summary>Гость: ответ хоста на заявку о блоке. Добыча (с умениями и опытом самого гостя) выдаётся только при «да».</summary>
    public void NetMineResult(int x, int y, int code, bool ok)
    {
        if (!World.In(x, y) || !pendingMine.Remove(World.Key(x, y), out var pm) || pm.code != code || !ok) return;
        var c = TileCenter(x, y);
        if (code == BushCutCode) { BushLoot(pm.fid, c); return; }
        inBreak = true;
        try { BreakLoot(new Vector2I(x, y), (Tl)code, c); }
        finally { inBreak = false; }
    }

    /// <summary>Действие у места интереса. В сети награду открывает только тот, кому хост подтвердил: гость спрашивает хоста, а уже пройденное место (State 3) не открывается никем.</summary>
    bool PoiIxNet(Poi p, Ix ix)
    {
        bool r = PoiIx(p, ix);
        if (!r || ix.E == null || !NetService.Active || !ClaimedPoi(W.Get(p.Tile.X, p.Tile.Y))) return r;
        if (p.State >= 3) { ix.E = null; ix.Lines.Add("Уже открыто другим игроком"); return r; }       // хост отдал место гостю, его клетка ещё не пришла
        if (NetClient) { int idx = W.Pois.IndexOf(p); var act = ix.E; ix.E = () => ClaimPoi(idx, act); }
        return r;
    }

    void ClaimPoi(int idx, Action act)
    {
        if (idx < 0 || pendingPoi.ContainsKey(idx)) return;
        pendingPoi[idx] = act; Net?.SendPoiClaim(idx);
    }

    /// <summary>Хост: гость хочет открыть место интереса. Первому — «да», и место сразу считается пройденным (второй игрок, хост или гость, его уже не откроет).</summary>
    public bool HostPoiClaim(long sender, int idx)
    {
        if (idx < 0 || idx >= W.Pois.Count) return false;
        var p = W.Pois[idx];
        if (p.State >= 3 || !ClaimedPoi(W.Get(p.Tile.X, p.Tile.Y)) || PosOf(sender) is not Vector2 sp || sp.DistanceTo(p.Pos) > PoiClaimReach) return false;
        p.State = 3; return true;
    }

    /// <summary>Гость: ответ хоста на заявку о месте интереса.</summary>
    public void NetPoiResult(int idx, bool ok)
    {
        if (!pendingPoi.Remove(idx, out var act)) return;
        if (ok) act(); else Fail("Уже открыто другим игроком");
    }
}
