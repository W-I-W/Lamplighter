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
        Net = new NetNode { Name = "NetNode", G = this }; AddChild(Net); InitNetFx();
        if (NetService.Role == NetRole.Host && NetService.Players.TryGetValue(1, out var me)) { me.Name = NetService.LocalName; me.Look = PlayerLook; }      // сцена перезагружена (выбор героя и мира): запись хоста берёт его героя
        NetService.Notice += NetNotice; NetService.Ended += NetEnded; NetService.Invite += OnInvite;
        Notify.Broadcast = m => { if (NetService.Active && NetService.Role == NetRole.Host) Net?.BroadcastChat(m, 0); };      // общие события мира хост рассылает клиентам
        if (!testing) NetService.WatchSteamInvites();
    }

    public override void _ExitTree() { Sfx.Hook = null; NetService.Notice -= NetNotice; NetService.Ended -= NetEnded; NetService.Invite -= OnInvite; }

    void NetNotice(string s) { if (IsInsideTree()) Toast(s); }

    /// <summary>Флаги состояния для сети: бит0 — рывок, бит1 — замах (виден союзникам).</summary>
    public int NetFlags() => (DashT > 0 ? 1 : 0) | (Swing > 0 ? 2 : 0);

    /// <summary>Сеанс завершён (хост вышел или связь потеряна): последнее состояние героя сохраняется, возврат в главное меню.</summary>
    void NetEnded()
    {
        if (!IsInsideTree() || !GameSession.Active) return;
        LeaveToMenu("Сетевая игра завершена");
    }

    /// <summary>Каждый кадр: гость, получивший мир от хоста, входит в игру. Сцена перезагружается, чтобы загрузить героя в мир хоста.</summary>
    void UpdateNet(float dt)
    {
        NetWorldTick(dt); NetVillageTick(dt); NetCombatTick(dt); NetStructTick(dt); NetGoalTick(dt); NetEventTick(dt); NetProgressTick(dt);
        NetJoinTick();
    }

    /// <summary>Гость получил мир хоста (работает и в меню подключения, и в игре).</summary>
    void NetJoinTick()
    {
        if (GameSession.Mode == SessionMode.Guest && !guestLoaded && NetService.State == NetState.Failed) { GameSession.Clear(); return; }
        if (NetService.Role == NetRole.Offline || !NetService.WorldPending) return;
        NetService.WorldPending = false;
        if (NetService.Role != NetRole.Client) return;
        if (!guestLoaded && (!testing || GameSession.Mode == SessionMode.Guest || (NetService.WorldSeed.HasValue && W.Seed != NetService.WorldSeed.Value))) { GetTree().ReloadCurrentScene(); return; }
        PPos = NetService.SpawnPos + new Vector2(14, 0); Menu = Mn.None; cam?.ResetSmoothing();
    }

    /// <summary>Выйти из сетевого сеанса по воле игрока. Хост остаётся в своём мире (одиночная игра), гость возвращается в главное меню. Герой сохраняется.</summary>
    public void NetLeave()
    {
        bool guest = GameSession.Mode == SessionMode.Guest; Save();
        NetService.Leave(false);
        if (guest) { GameSession.Clear(); CarryMsg = "Вы отключились от игры"; }
        else { GameSession.Mode = SessionMode.Solo; CarryMsg = "Сетевая игра закрыта: вы снова в одиночной игре"; }
        GetTree().ReloadCurrentScene();
    }

    // ───────────────────────── удалённые персонажи ─────────────────────────
    /// <summary>Позиции других игроков кооператива для карты.</summary>
    public IEnumerable<Vector2> RemoteMapPositions() { foreach (var p in Remotes()) yield return p.Shown; }

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
        var mod = Colors.White; if ((p.Flags & 1) != 0) mod = new Color(.6f, 1f, 1f, .8f);
        LookArt.Draw(c, p.Look, f, Def("player").Offset, mod, 16, 1, p.Light > 1f);      // у игрока без лампы свет — слабое пятно
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        if ((p.Flags & 2) != 0)                                  // замах: тот предмет, что у игрока в руках, и такой же рабочий цикл, как у своего персонажа
        {
            var dd = Vector2.FromAngle(p.AimAng); float sg = dd.X < 0 ? -1 : 1; float tt = (Time * 4.5f) % 1f; float k = tt < .65f ? tt / .65f : 1f - (tt - .65f) / .35f;
            DrawHeldIcon(c, p.Held, pos + new Vector2(0, -3), p.AimAng + sg * Mathf.Lerp(.55f, -1.45f, k * k));
        }
    }
}
