using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter;

/// <summary>Сохранение и загрузка: герой (CharacterSave) и мир (WorldSave) — два независимых файла, см. SaveManager. Какие именно файлы открыты, решает GameSession.
/// Гость кооператива пишет только своего героя: мир принадлежит хосту.</summary>
public partial class Game
{
    CharacterSave charSave; WorldSave worldSave; bool freshWorld, guestLoaded; int saveCount;
    public Look PlayerLook = DefaultLook();
    public string PlayerName = "";
    public int Diff;                                     // сложность мира: 0 — обычная, 1 — сложная (множители — в CombatTuning)
    public float WorldSecs;                              // время игры в этом мире
    public string WorldName => worldSave?.Name ?? "";
    public string SessionLine => GameSession.Mode == SessionMode.Guest ? $"{PlayerName} · {Loc.T("мир хоста")}" : $"{PlayerName} · {WorldName}";

    public static Look DefaultLook() => new() { Skin = 1, Hair = 4, HairCol = 7, Cloth1 = 1, Cloth2 = 5, Variant = 0 };

    /// <summary>Путь к файлу мира текущего сеанса (для проверок).</summary>
    public string SavePath => SaveManager.WorldPath(GameSession.WorldId ?? "");
    public string CharPath => SaveManager.CharPath(GameSession.CharacterId ?? "");
    public static bool ValidSave(string path)
    {
        try { var a = path.StartsWith("user://") ? ProjectSettings.GlobalizePath(path) : path; if (!System.IO.File.Exists(a)) return false; using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(a)); return doc.RootElement.TryGetProperty("Id", out _); }
        catch { return false; }
    }

    static int[] Flat(List<Building> bs) { var a = new int[bs.Count * It.Count]; for (int i = 0; i < bs.Count; i++) Array.Copy(bs[i].Local, 0, a, i * It.Count, It.Count); return a; }

    // ───────────────────────── сохранение ─────────────────────────
    public void Save() => SaveAll(false);

    /// <summary>Записывает героя и (если мир принадлежит этому игроку) мир. Миниатюра мира обновляется не на каждое автосохранение.</summary>
    public void SaveAll(bool thumb)
    {
        if (!GameSession.Active || W == null || charSave == null) return;
        try
        {
            while (SmeltQ.Count > 0) { int ig = It.IngotOf(SmeltQ[0]); if (ig >= 0) Inv[ig]++; SmeltQ.RemoveAt(0); }
            SaveManager.SaveCharacter(BuildCharacter());
            if (GameSession.Mode != SessionMode.Guest && NetService.Role != NetRole.Client && worldSave != null)
            {
                SaveManager.SaveWorld(BuildWorld());
                if (thumb || saveCount % 6 == 0) SaveManager.WriteThumb(worldSave.Id, W, Expl);
            }
            saveCount++;
        }
        catch (Exception e) { GD.PushWarning("save failed: " + e.Message); }
    }

    CharacterSave BuildCharacter()
    {
        var c = charSave; var sk = SaveSkills();
        c.Look = PlayerLook; c.PlaySecs = PlaySecs; c.Hp = Hp; c.MaxHp = MaxHp; c.Xp = Xp; c.Level = Level; c.Tier = Tier; c.Kills = Kills; c.HeroLamp = HeroLampLv; c.Ann = Ann;
        c.Inv = (int[])Inv.Clone(); c.ModInv = (int[])ModInv.Clone(); c.SkK = sk.k; c.SkV = sk.v;
        c.Hot = (string[])Hot.Clone(); c.HotSel = HotSel; c.Tools = OwnedTools.ToArray(); c.CurTool = CurTool;
        c.WId = Owned.ConvertAll(o => o.Id).ToArray(); c.WMods = Owned.ConvertAll(o => o.Mods.ToArray()).ToArray(); c.CurW = CurW;
        c.Relics = OwnedRelics.ToArray(); c.Equipped = EquippedRelics.ToArray(); c.BpDone = BpDone.ToArray();
        c.FragK = Frag.Keys.ToArray(); c.FragV = Frag.Values.ToArray(); c.BsK = BuildStock.Keys.ToArray(); c.BsV = BuildStock.Values.ToArray();
        c.Hints = HintsShown.ToArray(); c.Found = Found.ToArray(); c.LegendMask = LegendMask; c.CosmMask = CosmMask; c.CosmSel = CosmSel;
        if (GameSession.Mode != SessionMode.Guest && worldSave != null) c.Worlds[worldSave.Id] = new PerWorld { X = PPos.X, Y = PPos.Y, Has = true, Stamp = SaveManager.Stamp() };
        return c;
    }

    WorldSave BuildWorld()
    {
        var d = worldSave;
        d.GenV = W.GenV; d.Seed = W.Seed; d.PlaySecs = WorldSecs; d.Difficulty = Diff;
        d.Obj = Convert.ToBase64String(W.Obj); d.Explored = Convert.ToBase64String(Expl); d.Stock = (int[])Stock.Clone();
        d.DropItem = Drops.Select(x => x.Item).ToArray(); d.DropCount = Drops.Select(x => x.Count).ToArray(); d.DropX = Drops.Select(x => x.Pos.X).ToArray(); d.DropY = Drops.Select(x => x.Pos.Y).ToArray();
        d.Lit = new bool[W.Ancients.Count]; d.AncDone = new byte[W.Ancients.Count]; d.PoiState = new byte[W.Pois.Count];
        for (int i = 0; i < W.Ancients.Count; i++) { d.Lit[i] = W.Ancients[i].Lit; d.AncDone[i] = (byte)(W.Ancients[i].Done ? 1 : 0); }
        for (int i = 0; i < W.Pois.Count; i++) { var p = W.Pois[i]; d.PoiState[i] = (byte)((p.State == 1 ? 0 : p.State) | (p.Seen ? 16 : 0) | (p.Revealed ? 32 : 0)); }
        d.Vills = SaveVillagers(); d.ManualB = SaveManual(); d.VillEvents = VillEvents; d.ForgeMade = forgeMade; d.NextVillId = NextVillagerId;
        d.BossMask = BossMask; d.Beacon = BeaconLevel; d.TradeEpoch = TradeEpoch; d.ExtraV = ExtraVillagers; d.Bought = Bought.ToArray(); d.Demand = Demand; d.EvNext = evNext;
        d.Finished = Finished; d.LoreMask = LoreMask; d.RiftUnlocked = RiftUnlocked; d.RiftFirst = RiftFirst; d.RiftClears = RiftClears;
        d.UpCats = Upgrades?.SaveUnlocked(); d.UpNew = Upgrades?.SaveUnseen(); d.CutB = CutBush.ToArray(); d.PlacedK = Placed.ToArray();
        d.BT = Buildings.ConvertAll(b => (int)b.Type).ToArray(); d.BX = Buildings.ConvertAll(b => b.X).ToArray(); d.BY = Buildings.ConvertAll(b => b.Y).ToArray(); d.BL = Buildings.ConvertAll(b => b.Level).ToArray();
        d.BPrio = Buildings.ConvertAll(b => b.Prio).ToArray(); d.BMode = Buildings.ConvertAll(b => b.Mode).ToArray(); d.BOff = Buildings.ConvertAll(b => b.Off).ToArray(); d.BForced = Buildings.ConvertAll(b => b.Forced).ToArray(); d.BLocal = Flat(Buildings);
        d.LX = Lamps.ConvertAll(l => l.X).ToArray(); d.LY = Lamps.ConvertAll(l => l.Y).ToArray(); d.LK = Lamps.ConvertAll(l => l.Kind).ToArray(); d.LL = Lamps.ConvertAll(l => l.Level).ToArray(); d.LD = Lamps.ConvertAll(l => l.Dir).ToArray();
        d.EvK = Events.ConvertAll(e => e.Kind).ToArray(); d.EvX = Events.ConvertAll(e => e.Pos.X).ToArray(); d.EvY = Events.ConvertAll(e => e.Pos.Y).ToArray(); d.EvT = Events.ConvertAll(e => e.T).ToArray(); d.EvL = Events.ConvertAll(e => e.Life).ToArray();
        d.EvV = Events.ConvertAll(e => e.V).ToArray(); d.EvS = Events.ConvertAll(e => e.State).ToArray(); d.EvW = Events.ConvertAll(e => e.Wave).ToArray(); d.EvSeed = Events.ConvertAll(e => e.Seed).ToArray();
        return d;
    }

    // ───────────────────────── загрузка ─────────────────────────
    /// <summary>Загружает выбранные герой и мир (одиночная игра, хост) либо героя и мир хоста (гость). false — сеанса нет или файлы повреждены: игра остаётся в главном меню.</summary>
    bool LoadSession()
    {
        if (!GameSession.Active) return false;
        charSave = SaveManager.LoadCharacter(GameSession.CharacterId);
        if (charSave == null) { GameSession.Clear(); return false; }
        PlayerLook = charSave.Look ?? DefaultLook(); PlayerName = charSave.Name; NetService.LocalName = charSave.Name;
        if (GameSession.Mode == SessionMode.Guest)
        {
            if (!(NetService.Role == NetRole.Client && NetService.WorldSeed.HasValue)) { GameSession.Clear(); return false; }
            worldSave = null; guestLoaded = true; Diff = 0; WorldSecs = 0;
            W = World.Generate(NetService.WorldSeed.Value, NetService.WorldGen > 0 ? NetService.WorldGen : World.CurrentGen); PPos = NetService.SpawnPos + new Vector2(14, 0);
            ApplyCharacter(charSave, null); InitUpgrades(null); return true;
        }
        worldSave = SaveManager.LoadWorld(GameSession.WorldId);
        if (worldSave == null) { GameSession.Clear(); return false; }
        Diff = worldSave.Difficulty; WorldSecs = worldSave.PlaySecs;
        if (string.IsNullOrEmpty(worldSave.Obj))
        {
            freshWorld = true; W = World.Generate(worldSave.Seed, worldSave.GenV > 0 ? worldSave.GenV : World.CurrentGen); PPos = new Vector2((W.Spawn.X + .5f) * TS, (W.Spawn.Y + .5f) * TS);
            ApplyCharacter(charSave, null); InitUpgrades(null); return true;
        }
        if (!ApplyWorld(worldSave)) { GameSession.Clear(); return false; }
        ApplyCharacter(charSave, worldSave.Id); InitUpgrades(worldSave); return true;
    }

    bool ApplyWorld(WorldSave d)
    {
        try
        {
            W = World.Generate(d.Seed, d.GenV);
            var obj = Convert.FromBase64String(d.Obj); if (obj.Length != W.Obj.Length) return false; W.Obj = obj;
            { int lv = Math.Max(1, d.Beacon); if (lv >= 2) foreach (var t in W.Seal1) W.Set(t.X, t.Y, Tl.None); if (lv >= 3) foreach (var t in W.Seal2) W.Set(t.X, t.Y, Tl.None); if (lv >= 4) foreach (var t in W.Seal3) W.Set(t.X, t.Y, Tl.None); if (lv >= 5) foreach (var t in W.Seal4) W.Set(t.X, t.Y, Tl.None); }
            if (d.Lit != null) for (int i = 0; i < W.Ancients.Count && i < d.Lit.Length; i++) if (d.Lit[i]) { W.Ancients[i].Lit = true; W.Ancients[i].Anim = 1; }
            if (d.AncDone != null) for (int i = 0; i < W.Ancients.Count && i < d.AncDone.Length; i++) W.Ancients[i].Done = (d.AncDone[i] & 1) != 0;
            RestoreMysteryTiles();
            loadedVills = d.Vills; VillEvents = d.VillEvents; forgeMade = d.ForgeMade; if (d.NextVillId > NextVillagerId) NextVillagerId = d.NextVillId;
            if (d.ManualB != null) foreach (var mk in d.ManualB) manualLoaded.Add(mk);
            BossMask = d.BossMask; BeaconLevel = Math.Max(1, d.Beacon);
            if (d.Stock != null) for (int i = 0; i < Stock.Length && i < d.Stock.Length; i++) Stock[i] = d.Stock[i];
            Drops.Clear();
            if (d.DropItem != null && d.DropCount != null && d.DropX != null && d.DropY != null)
                for (int i = 0; i < d.DropItem.Length && i < d.DropCount.Length && i < d.DropX.Length && i < d.DropY.Length; i++)
                    if (Movable(d.DropItem[i]) && d.DropCount[i] > 0 && d.DropCount[i] <= MaxStack) Drops.Add(new WorldDrop { Id = nextDropId++, Item = d.DropItem[i], Count = d.DropCount[i], Pos = new Vector2(d.DropX[i], d.DropY[i]), Owner = -1 });
            if (d.BT != null)
                for (int i = 0; i < d.BT.Length; i++)
                {
                    var bb = new Building { Type = (Tl)d.BT[i], X = d.BX[i], Y = d.BY[i], Level = d.BL[i] };
                    if (d.BPrio != null && i < d.BPrio.Length) bb.Prio = d.BPrio[i]; if (d.BMode != null && i < d.BMode.Length) bb.Mode = d.BMode[i];
                    if (d.BOff != null && i < d.BOff.Length) bb.Off = d.BOff[i]; if (d.BForced != null && i < d.BForced.Length) bb.Forced = d.BForced[i];
                    if (d.BLocal != null && (i + 1) * It.Count <= d.BLocal.Length) Array.Copy(d.BLocal, i * It.Count, bb.Local, 0, It.Count);
                    Buildings.Add(bb);
                }
            if (d.PoiState != null)
                for (int i = 0; i < W.Pois.Count && i < d.PoiState.Length; i++) { var b = d.PoiState[i]; var p = W.Pois[i]; p.State = (byte)(b & 15); p.Seen = (b & 16) != 0; p.Revealed = (b & 32) != 0; }
            TradeEpoch = d.TradeEpoch; ExtraVillagers = d.ExtraV; if (d.Bought != null) foreach (var b in d.Bought) Bought.Add(b);
            if (d.Demand != null) for (int i = 0; i < Demand.Length && i < d.Demand.Length; i++) Demand[i] = d.Demand[i];
            evNext = d.EvNext > 0 ? d.EvNext : 150;
            if (d.EvK != null) for (int i = 0; i < d.EvK.Length; i++) pendingEvents.Add(new GEvent { Kind = d.EvK[i], Pos = new Vector2(d.EvX[i], d.EvY[i]), T = d.EvT[i], Life = d.EvL[i], V = d.EvV[i], State = d.EvS[i], Wave = d.EvW[i], Seed = d.EvSeed[i] });
            if (d.LX != null) for (int i = 0; i < d.LX.Length; i++) Lamps.Add(new Lamp { X = d.LX[i], Y = d.LY[i], Kind = d.LK[i], Level = d.LL[i], Dir = d.LD[i], Anim = 1 });
            if (d.CutB != null) foreach (var cb in d.CutB) CutBush.Add(cb);
            if (d.PlacedK != null) foreach (var pk in d.PlacedK) Placed.Add(pk); else InitPlaced();
            Finished = d.Finished; LoreMask = d.LoreMask; if (Finished) Dawn = 1;
            RiftUnlocked = Math.Max(1, d.RiftUnlocked); RiftFirst = d.RiftFirst; RiftClears = d.RiftClears;
            if (d.Explored != null) { var e = Convert.FromBase64String(d.Explored); if (e.Length == Expl.Length) Expl = e; }
            PPos = new Vector2((W.Spawn.X + .5f) * TS, (W.Spawn.Y + .5f) * TS);
            return true;
        }
        catch (Exception e) { GD.PushWarning("world load failed: " + e.Message); return false; }
    }

    void ApplyCharacter(CharacterSave c, string worldId)
    {
        if (worldId != null && c.Worlds != null && c.Worlds.TryGetValue(worldId, out var pw) && pw.Has) PPos = new Vector2(pw.X, pw.Y);
        PlaySecs = c.PlaySecs; Hp = c.Hp; MaxHp = c.MaxHp; Xp = c.Xp; Level = c.Level; Tier = c.Tier; Kills = c.Kills; HeroLampLv = c.HeroLamp; Ann = c.Ann;
        LoadSkills(c.SkK, c.SkV, 0);
        if (c.Inv != null) for (int i = 0; i < Inv.Length && i < c.Inv.Length; i++) Inv[i] = Math.Max(0, c.Inv[i]);
        if (c.ModInv != null) for (int i = 0; i < ModInv.Length && i < c.ModInv.Length; i++) ModInv[i] = Math.Max(0, c.ModInv[i]);
        if (c.WId != null) for (int i = 0; i < c.WId.Length; i++) { var o = new OwnedWeapon { Id = c.WId[i] }; if (c.WMods != null && i < c.WMods.Length && c.WMods[i] != null) o.Mods.AddRange(c.WMods[i]); if (Db.Weapon(o.Id) != null) Owned.Add(o); }
        CurW = Owned.Count == 0 ? -1 : Math.Clamp(c.CurW, 0, Owned.Count - 1);
        if (c.Tools != null) { OwnedTools.Clear(); foreach (var t in c.Tools) if (Array.IndexOf(ToolDef.Ids, t) >= 0) OwnedTools.Add(t); }
        OwnedTools.Add("pickaxe"); CurTool = c.CurTool != null && OwnedTools.Contains(c.CurTool) ? c.CurTool : "pickaxe";
        if (c.BsK != null && c.BsV != null) for (int i = 0; i < c.BsK.Length && i < c.BsV.Length; i++) if (c.BsV[i] > 0) BuildStock[c.BsK[i]] = c.BsV[i];
        if (c.Hot != null) for (int i = 0; i < HotCount && i < c.Hot.Length; i++) Hot[i] = c.Hot[i];
        HotSel = Math.Clamp(c.HotSel, 0, HotCount - 1);
        if (c.Relics != null) OwnedRelics.AddRange(c.Relics); if (c.Equipped != null) EquippedRelics.AddRange(c.Equipped);
        if (c.BpDone != null) foreach (var b in c.BpDone) BpDone.Add(b);
        if (c.FragK != null && c.FragV != null) for (int i = 0; i < c.FragK.Length && i < c.FragV.Length; i++) Frag[c.FragK[i]] = c.FragV[i];
        if (c.Found != null) Found.AddRange(c.Found);
        if (c.Hints != null) foreach (var h in c.Hints) HintsShown.Add(h);
        LegendMask = c.LegendMask; CosmMask = c.CosmMask | 1; CosmSel = c.CosmSel;
    }

    // ───────────────────────── переходы между сеансами ─────────────────────────
    /// <summary>Выход в главное меню: сохранить, покинуть сеть, сбросить сеанс и перезагрузить сцену.</summary>
    public void LeaveToMenu(string msg = null)
    {
        Save(); NetService.Leave(false); GameSession.Clear(); CarryMsg = msg;
        GetTree().ReloadCurrentScene();
    }

    /// <summary>Запуск одиночной игры героем в мире.</summary>
    public void StartSolo(string charId, string worldId)
    {
        if (GameSession.Active) Save();
        NetService.Leave(false); GameSession.Begin(SessionMode.Solo, charId, worldId); GetTree().ReloadCurrentScene();
    }

    /// <summary>Хост: героя и мир выбрал хост; создаётся лобби, затем та же загрузка, что и в одиночной игре.</summary>
    public bool StartHostSession(string charId, string worldId, string transport, bool friendsOnly)
    {
        var ch = SaveManager.LoadCharacter(charId); if (ch == null) return false; NetService.LocalName = ch.Name;
        var o = new NetHostOptions { FriendsOnly = friendsOnly }; if (testing && int.TryParse(OS.GetEnvironment("LAMP_NET_PORT"), out var tp)) o.Port = tp;      // порт — только для автотестов
        if (!NetService.StartHost(transport, o)) { Sfx.Play("error"); Toast(NetService.Status); return false; }
        GameSession.Begin(SessionMode.Host, charId, worldId);
        CarryMsg = transport == "enet" ? $"Сетевая игра открыта (порт {o.Port})" : "Сетевая игра: создание лобби Steam…";
        GetTree().ReloadCurrentScene(); return true;
    }

    /// <summary>Гость: выбран только герой; мир придёт от хоста после подключения.</summary>
    public bool StartGuestSession(string charId, string transport, string target)
    {
        var ch = SaveManager.LoadCharacter(charId); if (ch == null) return false;
        NetService.LocalName = ch.Name; GameSession.Begin(SessionMode.Guest, charId, null); GameSession.GuestLook = ch.Look;
        if (!NetService.Join(transport, target)) { GameSession.Clear(); Sfx.Play("error"); return false; }
        return true;
    }
}
