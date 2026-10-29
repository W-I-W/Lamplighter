using Godot;

namespace Lamplighter;

/// <summary>Меню «Мультиплеер»: создание игры (герой → мир → лобби) и подключение к лобби Steam или адресу (герой → подключение к миру хоста).
/// Steam-пункты доступны только когда транспорт Steam действительно может работать (плагин GodotSteam + запущенный Steam); иначе показывается причина.</summary>
public partial class Game
{
    public string NetAddr = "127.0.0.1"; public bool NetJoinSteam, NetFriendsOnly = true;
    public bool NetSteamOk; public string NetSteamWhy = "";

    void RefreshNetMenu()
    {
        var t = NetService.Make("steam"); NetSteamOk = t.IsAvailable(out var why); NetSteamWhy = why;
        if (NetService.State == NetState.Failed) NetService.State = NetState.Offline;
    }

    void MultiAct(int i)
    {
        switch (i)
        {
            case 0: if (NetSteamOk) OpenFlow(FlowKind.Host, "steam"); else Fail(NetSteamWhy); break;
            case 1: if (NetSteamOk) { NetJoinSteam = true; NetAddr = ""; NetService.Status = ""; OpenMenu(Mn.NetJoin); } else Fail(NetSteamWhy); break;
            case 2: OpenFlow(FlowKind.Host, "enet"); break;
            case 3: NetJoinSteam = false; if (NetAddr == "") NetAddr = "127.0.0.1"; NetService.Status = ""; OpenMenu(Mn.NetJoin); break;
            case 4: MenuBackKey(); break;
        }
    }

    /// <summary>Гость: сначала выбор героя, затем подключение. Адрес или ID лобби известны заранее (ввод или приглашение Steam).</summary>
    public void OpenGuestFlow(string transport, string target)
    {
        GameSession.PendingTransport = transport; GameSession.PendingTarget = target; NetJoinSteam = transport == "steam"; NetAddr = target;
        if (Menu == Mn.None) Menu = Mn.Main;
        OpenFlow(FlowKind.Guest, transport, target);
    }

    /// <summary>Приглашение Steam или аргумент +connect_lobby. В игре: герой сохраняется и игрок возвращается в меню выбора героя.</summary>
    void OnInvite(string transport, string target)
    {
        if (!IsInsideTree() || testing) return;
        if (uiRoot == null) { GameSession.PendingTransport = transport; GameSession.PendingTarget = target; return; }      // приглашение пришло при запуске: меню откроет StartMenuOnLaunch
        if (GameSession.Active) { GameSession.PendingTransport = transport; GameSession.PendingTarget = target; LeaveToMenu("Приглашение Steam: выберите героя"); return; }
        if (NetService.State == NetState.Connecting || NetService.State == NetState.Handshaking) return;
        OpenGuestFlow(transport, target);
    }

    void NetInfoAct(int i)
    {
        switch (i)
        {
            case 0:
                if (NetService.Transport is SteamTransport st) { st.OpenInviteDialog(); if (st.InviteInfo != "") { DisplayServer.ClipboardSet(st.InviteInfo); Toast("ID лобби скопирован: " + st.InviteInfo); } }
                else if (NetService.Active) Toast($"Адрес для подключения: ваш IP, порт {NetConst.DefaultPort}");
                else Fail("Сетевая игра не запущена");
                break;
            case 1: if (NetService.Active) { CloseMenu(); NetLeave(); } else Fail("Сетевая игра не запущена"); break;
            case 2: MenuBackKey(); break;
        }
    }

    /// <summary>Ввод адреса или идентификатора лобби: клавиатура с Unicode, Ctrl+V — вставка. Enter — выбор героя и подключение.</summary>
    void NetJoinKey(InputEventKey k)
    {
        bool busy = NetService.State == NetState.Connecting || NetService.State == NetState.Handshaking;
        if (k.Keycode == Key.Escape)
        {
            if (busy || GameSession.Mode == SessionMode.Guest) { NetService.Leave(false); GameSession.Clear(); NetService.Status = ""; }
            OpenMenu(Mn.Multi); return;
        }
        if (busy) return;
        if (k.Keycode == Key.Enter || k.Keycode == Key.KpEnter)
        {
            if (k.Echo) return;
            if (NetAddr.Trim() == "") { Sfx.Play("error"); return; }
            Sfx.Play("tick"); OpenGuestFlow(NetJoinSteam ? "steam" : "enet", NetAddr.Trim());
            return;
        }
        if (k.Keycode == Key.Backspace) { if (NetAddr.Length > 0) NetAddr = NetAddr.Substring(0, NetAddr.Length - 1); return; }
        if (k.Keycode == Key.V && k.CtrlPressed) { var clip = DisplayServer.ClipboardGet().Trim(); if (clip.Length > 0 && clip.Length < 80) NetAddr = clip; return; }
        char ch = (char)k.Unicode;
        if (ch != 0 && NetAddr.Length < 60 && (char.IsLetterOrDigit(ch) || ch == '.' || ch == ':' || ch == '-' || ch == '_')) NetAddr += ch;
    }
}
