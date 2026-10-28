using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter;

/// <summary>Связь игры с сетевым слоем: узел RPC, удалённые персонажи (отрисовка, имена), жизненный цикл сеанса (вход клиента, потеря связи, выход хоста).
/// Одиночная игра этим кодом не затрагивается: при NetService.Role == Offline всё ниже ничего не делает.</summary>
public partial class Game
{
    public NetNode Net;
    static readonly Color[] NetTints = { Colors.White, new Color(.82f, 1f, .82f), new Color(1f, .86f, .8f), new Color(.85f, .9f, 1f) };

    void InitNet()
    {
        Net = new NetNode { Name = "NetNode", G = this }; AddChild(Net);
        NetService.Notice += NetNotice; NetService.Ended += NetEnded;
        if (!testing) NetService.WatchSteamInvites();
    }

    public override void _ExitTree() { NetService.Notice -= NetNotice; NetService.Ended -= NetEnded; }

    void NetNotice(string s) { if (IsInsideTree()) Toast(s); }

    /// <summary>Флаги состояния для сети: бит0 — рывок, бит1 — замах (виден союзникам).</summary>
    public int NetFlags() => (DashT > 0 ? 1 : 0) | (Swing > 0 ? 2 : 0);

    /// <summary>Сеанс завершён (хост вышел или связь потеряна): возврат в главное меню с локальным миром.</summary>
    void NetEnded()
    {
        if (!IsInsideTree()) return;
        skipMenu = false; forceNew = false; GetTree().ReloadCurrentScene();
    }

    /// <summary>Каждый кадр: клиент, получивший мир от хоста, входит в игру. Если его мир отличается — сцена перезагружается с seed хоста.</summary>
    void UpdateNet(float dt)
    {
        if (NetService.Role == NetRole.Offline || !NetService.WorldPending) return;
        NetService.WorldPending = false;
        if (NetService.WorldSeed.HasValue && W.Seed != NetService.WorldSeed.Value) { skipMenu = true; GetTree().ReloadCurrentScene(); return; }
        if (NetService.Role == NetRole.Client) { PPos = NetService.SpawnPos + new Vector2(14, 0); Menu = Mn.None; cam?.ResetSmoothing(); }
    }

    /// <summary>Выйти из сетевого сеанса по воле игрока. Хост остаётся в своём мире, клиент возвращается в главное меню.</summary>
    public void NetLeave()
    {
        bool client = NetService.Role == NetRole.Client;
        if (!client) Save();
        NetService.Leave(false);
        if (client) { skipMenu = false; forceNew = false; GetTree().ReloadCurrentScene(); }
        else Toast("Сетевая игра закрыта");
    }

    // ───────────────────────── удалённые персонажи ─────────────────────────
    IEnumerable<NetPlayer> Remotes() => NetService.Active ? NetService.Players.Values.Where(p => p.Handshaken && p.PeerId != NetService.LocalId) : Enumerable.Empty<NetPlayer>();

    int NetSlot(NetPlayer p) { int i = 0; foreach (var k in NetService.Players.Keys.OrderBy(x => x)) { if (k == p.PeerId) return i % NetTints.Length; i++; } return 0; }

    void AddRemoteDraws(List<(float y, System.Action a)> list, EntityLayer c)
    {
        foreach (var p in Remotes()) { var pp = p; list.Add((p.Shown.Y, () => DrawRemotePlayer(c, pp))); }
    }

    void DrawRemotePlayer(EntityLayer c, NetPlayer p)
    {
        var pos = p.Shown; Shadow(c, pos, 5);
        if (p.Hp <= 0) return;
        bool moving = pos.DistanceSquaredTo(p.PrevShown) > .02f;
        int f = moving ? (int)p.Walk % 2 : 0; float bob = moving ? -Mathf.Abs(Mathf.Sin(p.Walk)) : 0;
        c.DrawSetTransform(pos + new Vector2(0, bob), 0, new Vector2(p.Face < 0 ? -1 : 1, 1));
        var mod = NetTints[NetSlot(p)]; if ((p.Flags & 1) != 0) mod = new Color(.6f, 1f, 1f, .8f);
        DrawFrame(c, Def("player"), f, mod);
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        if ((p.Flags & 2) != 0) { var d = new Vector2(p.Face < 0 ? -1 : 1, 0); Gfx.Line(c, pos + new Vector2(0, -3), pos + new Vector2(0, -3) + d * 12, new Color(.9f, .8f, .5f), 1.5f); }
    }
}
