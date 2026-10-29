using Godot;
using System;

namespace Lamplighter;

/// <summary>Общий прогресс мира в кооперативе: древние фонари (зажжён, условие выполнено) и места интереса (состояние, найдено, тайл).
/// Хост — источник истины: он присылает вошедшему клиенту полный снимок, а затем изменения от любого участника пересылаются через хост остальным.
/// Изменения находятся сравнением со «слепком» каждые 0,4 с, поэтому сюда попадают и прежние механики, и новые места этапа 8 без правок в каждом месте игры.
/// Не синхронизируются: враги, события, постройки, добытые тайлы и ход загадок (светила), пока они не решены.</summary>
public partial class Game
{
    byte[] shPoi, shAnc; float netWorldT;

    byte[] NetPoiBytes()
    {
        var b = new byte[W.Pois.Count * 3];
        for (int i = 0; i < W.Pois.Count; i++) { var p = W.Pois[i]; b[i * 3] = (byte)(p.State == 1 ? 0 : p.State); b[i * 3 + 1] = (byte)((p.Seen ? 1 : 0) | (p.Revealed ? 2 : 0)); b[i * 3 + 2] = (byte)W.Get(p.Tile.X, p.Tile.Y); }
        return b;
    }

    byte[] NetAncBytes()
    {
        var b = new byte[W.Ancients.Count];
        for (int i = 0; i < b.Length; i++) b[i] = (byte)((W.Ancients[i].Lit ? 1 : 0) | (W.Ancients[i].Done ? 2 : 0));
        return b;
    }

    public byte[] NetSnapshotPoi() => NetPoiBytes();
    public byte[] NetSnapshotAnc() => NetAncBytes();

    /// <summary>Каждый кадр сетевого сеанса: применить снимок хоста (клиент) и разослать собственные изменения.</summary>
    void NetWorldTick(float dt)
    {
        if (!NetService.Active || Net == null) { shPoi = null; return; }
        if (NetService.Role == NetRole.Client && NetService.PendingPoi != null && !NetService.WorldPending && NetService.WorldSeed == W.Seed)
        {
            var pp = NetService.PendingPoi; var pa = NetService.PendingAnc; NetService.PendingPoi = null; NetService.PendingAnc = null;
            for (int i = 0; i < pp.Length / 3 && i < W.Pois.Count; i++) NetApplyPoi(i, pp[i * 3], pp[i * 3 + 1], pp[i * 3 + 2]);
            if (pa != null) for (int i = 1; i < pa.Length && i < W.Ancients.Count; i++) NetApplyAnc(i, pa[i]);
            shPoi = NetPoiBytes(); shAnc = NetAncBytes(); return;
        }
        if (NetService.Role == NetRole.Client && NetService.PendingLamps != null && !NetService.WorldPending && NetService.WorldSeed == W.Seed)
        { var pt = NetService.PendingTiles; var pl = NetService.PendingLamps; NetService.PendingTiles = null; NetService.PendingLamps = null; NetApplyStructSnapshot(pt, pl); }
        netWorldT -= dt; if (netWorldT > 0) return; netWorldT = .4f;
        if (shPoi == null || shPoi.Length != W.Pois.Count * 3) { shPoi = NetPoiBytes(); shAnc = NetAncBytes(); return; }
        var cp = NetPoiBytes(); var ca = NetAncBytes();
        for (int i = 0; i < W.Pois.Count; i++)
            if (cp[i * 3] != shPoi[i * 3] || cp[i * 3 + 1] != shPoi[i * 3 + 1] || cp[i * 3 + 2] != shPoi[i * 3 + 2]) Net.SendPoi(i, cp[i * 3], cp[i * 3 + 1], cp[i * 3 + 2]);
        for (int i = 1; i < ca.Length && i < shAnc.Length; i++) if (ca[i] != shAnc[i]) Net.SendAnc(i, ca[i]);
        shPoi = cp; shAnc = ca;
    }

    /// <summary>Изменение места интереса от другого участника. Возвращает false, если данные неверны (сообщение отбрасывается).</summary>
    public bool NetApplyPoi(int i, int state, int flags, int tile)
    {
        if (i < 0 || i >= W.Pois.Count || state < 0 || state > 3 || tile < 0 || !Enum.IsDefined(typeof(Tl), (byte)tile)) return false;
        var p = W.Pois[i];
        if (p.State >= 3 && state < 3) return false;                     // прогресс только вперёд: завершённое место не откатывается
        if (state > p.State) p.State = (byte)state;
        p.Seen |= (flags & 1) != 0; p.Revealed |= (flags & 2) != 0;
        if ((int)W.Get(p.Tile.X, p.Tile.Y) != tile) { SetObj(p.Tile.X, p.Tile.Y, (Tl)tile); Burst(p.Pos, new Color("#ffe27a"), 10, 60); }
        if (shPoi != null && shPoi.Length == W.Pois.Count * 3) { shPoi[i * 3] = (byte)(p.State == 1 ? 0 : p.State); shPoi[i * 3 + 1] = (byte)((p.Seen ? 1 : 0) | (p.Revealed ? 2 : 0)); shPoi[i * 3 + 2] = (byte)W.Get(p.Tile.X, p.Tile.Y); }
        return true;
    }

    /// <summary>Фонарь зажжён или очищен другим участником.</summary>
    public bool NetApplyAnc(int i, int bits)
    {
        if (i < 1 || i >= W.Ancients.Count || bits < 0 || bits > 3) return false;
        var a = W.Ancients[i];
        if ((bits & 2) != 0) a.Done = true;
        if ((bits & 1) != 0 && !a.Lit)
        {
            a.Lit = true; a.Anim = 0; OnAncientLit(a); SetObj(a.Tile.X, a.Tile.Y, a.Boss ? Tl.BossOn : Tl.AncOn);
            foreach (var v in a.Veins) if (W.Get(v.x, v.y) == Tl.None) SetObj(v.x, v.y, v.t);
            TradeEpoch++; Announce(a.Boss ? "Фонарь стража зажжён союзником" : $"Союзник зажёг древний фонарь #{i}", "Территория освобождена", new Color("#ffe9a0"), "ancient");
        }
        if (shAnc != null && i < shAnc.Length) shAnc[i] = (byte)((a.Lit ? 1 : 0) | (a.Done ? 2 : 0));
        return true;
    }
}
