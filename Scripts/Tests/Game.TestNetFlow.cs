using Godot;
using System;
using System.Linq;

namespace Lamplighter;

/// <summary>Настоящий путь запуска кооператива (не LAMP_NET): хост — герой и мир выбраны → лобби → перезагрузка в мир; гость — только герой → подключение → мир хоста.
/// Два процесса через ENet; Steam использует тот же код, отличается только транспорт. Запуск: tools/run_checks.ps1 (net flow).</summary>
public partial class Game
{
    static int netFlowPhase; static string netFlowChar;

    static readonly Look HostFlowLook = new() { Skin = 2, Hair = 6, HairCol = 1, Cloth1 = 3, Cloth2 = 4, Variant = 1 };
    static readonly Look GuestFlowLook = new() { Skin = 5, Hair = 3, HairCol = 2, Cloth1 = 9, Cloth2 = 10, Variant = 2 };

    void BuildNetFlowScript(bool host)
    {
        int fails = 0; void Check(bool ok, string what) { if (!ok) fails++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {what}"); }
        int port = int.TryParse(OS.GetEnvironment("LAMP_NET_PORT"), out var pp) ? pp : NetConst.DefaultPort;
        void Done() { GD.Print($"[check] SUMMARY failures={fails}"); GD.Print("[check] NET DONE"); }
        NetPlayer Other() => NetService.Players.Values.FirstOrDefault(p => p.PeerId != NetService.LocalId && p.Handshaken);
        GD.Print($"[netflow] host={host} phase={netFlowPhase} mode={GameSession.Mode} role={NetService.Role}");
        if (host)
        {
            if (netFlowPhase == 0)
                S(.5f, () =>
                {
                    var c = SaveManager.CreateCharacter("Хост", HostFlowLook); c.Inv = new int[It.Count]; c.Inv[It.Wood] = 11; SaveManager.SaveCharacter(c); netFlowChar = c.Id;
                    var w = SaveManager.CreateWorld("Сетевой мир", 777, 0); netFlowPhase = 1; Check(StartHostSession(c.Id, w.Id, "enet", true), "netflow host: character and world chosen, lobby created");
                });
            else
            {
                S(.3f, () => { Menu = Mn.None; noHeal = true; Hp = MaxHp; Invuln = 99f; Enemies.Clear(); EventsEnabled = false; TestNoSpawn = true; });
                S(.3f, () =>
                {
                    Check(GameSession.Mode == SessionMode.Host && NetService.Role == NetRole.Host && NetService.Online, "netflow host: session mode is Host and the lobby is online");
                    Check(W.Seed == 777 && WorldName == "Сетевой мир" && PlayerName == "Хост", "netflow host: the chosen world and character are loaded");
                    Check(Inv[It.Wood] == 11 && PlayerLook.Hair == 6 && PlayerLook.Variant == 1, "netflow host: the character's inventory and look are loaded");
                });
                WaitStepFlow(30f, () => NetService.Count >= 2 && Other() != null, ok => Check(ok, "netflow host: the guest joined"));
                S(.3f, () =>
                {
                    var o = Other(); Check(o != null && o.Look.Skin == 5 && o.Look.Hair == 3 && o.Look.Cloth1 == 9 && o.Look.Variant == 2, "netflow host: the guest's own look is used");
                    Check(NetService.Players[1].Look.Hair == 6, "netflow host: the host look is in the roster");
                    NetGrantTo(o);
                });
                WaitStepFlow(40f, () => NetService.Count <= 1, ok => Check(ok, "netflow host: the guest left"));
                S(.5f, () =>
                {
                    Save(); var cs = SaveManager.LoadCharacter(netFlowChar); var ws = SaveManager.LoadWorld(GameSession.WorldId);
                    Check(cs != null && cs.Inv[It.Wood] == 11 && cs.Inv.Sum() == 11, "netflow host: the host character was saved without the guest's items");
                    Check(ws != null && ws.Seed == 777 && SaveManager.ListWorlds().Count == 2, "netflow host: the host world is saved, the guest's world copy was not created");
                    Done();
                });
            }
        }
        else
        {
            if (netFlowPhase == 0)
                S(.3f, () =>
                {
                    var c = SaveManager.CreateCharacter("Гость", GuestFlowLook); c.Inv = new int[It.Count]; c.Inv[It.Wood] = 33; SaveManager.SaveCharacter(c); netFlowChar = c.Id; netFlowPhase = 1;
                    Check(SaveManager.ListWorlds().Count == 1, "netflow guest: only the test world exists locally before joining");
                    Check(StartGuestSession(c.Id, "enet", $"127.0.0.1:{port}"), "netflow guest: character chosen, connecting to the host's world");
                });
            else if (netFlowPhase == 1)
            {
                S(.3f, () =>
                {
                    Check(GameSession.Mode == SessionMode.Guest && NetService.Role == NetRole.Client && NetService.Online, "netflow guest: session mode is Guest and connected");
                    Check(W.Seed == 777, $"netflow guest: the world is the host's (seed {W.Seed})");
                    Check(PlayerName == "Гость" && PlayerLook.Hair == 3 && PlayerLook.Skin == 5 && Inv[It.Wood] >= 33, "netflow guest: own character, look and inventory are loaded");
                    Check(NetService.Players.ContainsKey(1) && NetService.Players[1].Look.Hair == 6, "netflow guest: the host is drawn with its own look");
                    Menu = Mn.None; noHeal = true; Hp = MaxHp; Invuln = 99f; Enemies.Clear(); EventsEnabled = false; TestNoSpawn = true;
                });
                WaitStepFlow(12f, () => Inv[It.Wood] == 38, ok => Check(ok, "netflow guest: the host-granted item arrived once"));
                S(.5f, () =>
                {
                    Save(); var cs = SaveManager.LoadCharacter(netFlowChar);
                    Check(cs != null && cs.Inv[It.Wood] == 38, "netflow guest: the received item is saved to the guest character");
                    Check(SaveManager.ListWorlds().Count == 1, "netflow guest: the host's world was not saved on the guest side");
                    netFlowPhase = 2; NetLeave();
                });
            }
            else
            {
                S(.5f, () =>
                {
                    var cs = SaveManager.LoadCharacter(netFlowChar);
                    Check(!NetService.Active && cs != null && cs.Inv[It.Wood] == 38, "netflow guest: after leaving, the character keeps the item (no duplication)");
                    Check(SaveManager.ListWorlds().Count == 1, "netflow guest: leaving did not create a world");
                    Done();
                });
            }
        }
    }

    /// <summary>Хост выдаёт гостю предмет (единственный путь получения предметов от сети).</summary>
    void NetGrantTo(NetPlayer p) { if (p != null) { var g = new int[It.Count]; g[It.Wood] = 5; Net.RpcId(p.PeerId, nameof(NetNode.RpcGrant), g); } }

    void WaitStepFlow(float timeout, Func<bool> cond, Action<bool> onDone) => WaitStep(timeout, cond, onDone);
}
