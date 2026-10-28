using Godot;

namespace Lamplighter;

/// <summary>Меню «Мультиплеер»: создание игры, подключение по адресу или к лобби Steam, сетевая информация во время игры.
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
            case 0: HostGame("enet"); break;
            case 1: NetJoinSteam = false; if (NetAddr == "") NetAddr = "127.0.0.1"; NetService.Status = ""; OpenMenu(Mn.NetJoin); break;
            case 2: if (NetSteamOk) HostGame("steam"); else { Fail(NetSteamWhy); } break;
            case 3: NetFriendsOnly = !NetFriendsOnly; Sfx.Play("tick", -4, 1.2f); break;
            case 4: if (NetSteamOk) { NetJoinSteam = true; NetAddr = ""; NetService.Status = ""; OpenMenu(Mn.NetJoin); } else Fail(NetSteamWhy); break;
            case 5: MenuBackKey(); break;
        }
    }

    void HostGame(string transport)
    {
        var o = new NetHostOptions { FriendsOnly = NetFriendsOnly };
        if (!NetService.StartHost(transport, o)) { Sfx.Play("error"); Toast(NetService.Status); return; }
        if (HasSave) CloseMenu(); else NewGame();
        Toast(transport == "enet" ? $"Игра открыта (порт {o.Port}). Друзья подключаются по вашему адресу" : "Создание лобби Steam…");
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

    /// <summary>Ввод адреса или идентификатора лобби: клавиатура с Unicode, Ctrl+V — вставка.</summary>
    void NetJoinKey(InputEventKey k)
    {
        if (k.Keycode == Key.Escape) { OpenMenu(Mn.Multi); return; }
        if (k.Keycode == Key.Enter || k.Keycode == Key.KpEnter)
        {
            if (k.Echo) return;
            NetService.LocalName = NetService.LocalName == "" ? "Игрок" : NetService.LocalName;
            bool ok = NetService.Join(NetJoinSteam ? "steam" : "enet", NetAddr);
            if (!ok) Sfx.Play("error"); else Sfx.Play("tick");
            return;
        }
        if (k.Keycode == Key.Backspace) { if (NetAddr.Length > 0) NetAddr = NetAddr.Substring(0, NetAddr.Length - 1); return; }
        if (k.Keycode == Key.V && k.CtrlPressed) { var clip = DisplayServer.ClipboardGet().Trim(); if (clip.Length > 0 && clip.Length < 80) NetAddr = clip; return; }
        char ch = (char)k.Unicode;
        if (ch != 0 && NetAddr.Length < 60 && (char.IsLetterOrDigit(ch) || ch == '.' || ch == ':' || ch == '-' || ch == '_')) NetAddr += ch;
    }
}
