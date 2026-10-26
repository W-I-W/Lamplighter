def rw(p, f):
    s=open(p,encoding='utf-8').read(); s2=f(s); open(p,'w',encoding='utf-8').write(s2); return s!=s2

def game(s):
    # Building fields
    s=s.replace('public sealed class Building { public Tl Type; public int X, Y, Level = 1, Count, Status; public float Prog; public bool Worker; public Vector2 Pos => Game.TileCenter(X, Y); }',
        'public sealed class Building { public Tl Type; public int X, Y, Level = 1, Count, Status, Prio = 1, Mode; public float Prog; public bool Worker, Off, Forced, Linked; public int[] Local = new int[It.Count]; public Vector2 Pos => Game.TileCenter(X, Y); }')
    # save fields
    s=s.replace('    public string[] Relics, Equipped, BpDone, FragK, Found; public int[] FragV;\n}','''    public string[] Relics, Equipped, BpDone, FragK, Found; public int[] FragV;
    public int[] BPrio, BMode; public bool[] BOff, BForced; public int[] BLocal;
    public int TradeEpoch, ExtraV; public string[] Bought; public float[] Demand; public float EvNext;
    public string[] EvK; public float[] EvX, EvY, EvT, EvL, EvV; public int[] EvS, EvW, EvSeed;
}''')
    s=s.replace('                LX = new int[Lamps.Count],','''                BPrio = Buildings.ConvertAll(b => b.Prio).ToArray(), BMode = Buildings.ConvertAll(b => b.Mode).ToArray(), BOff = Buildings.ConvertAll(b => b.Off).ToArray(), BForced = Buildings.ConvertAll(b => b.Forced).ToArray(),
                BLocal = Flat(Buildings), TradeEpoch = TradeEpoch, ExtraV = ExtraVillagers, Bought = new List<string>(Bought).ToArray(), Demand = Demand, EvNext = evNext,
                EvK = Events.ConvertAll(e => e.Kind).ToArray(), EvX = Events.ConvertAll(e => e.Pos.X).ToArray(), EvY = Events.ConvertAll(e => e.Pos.Y).ToArray(), EvT = Events.ConvertAll(e => e.T).ToArray(), EvL = Events.ConvertAll(e => e.Life).ToArray(),
                EvV = Events.ConvertAll(e => e.V).ToArray(), EvS = Events.ConvertAll(e => e.State).ToArray(), EvW = Events.ConvertAll(e => e.Wave).ToArray(), EvSeed = Events.ConvertAll(e => e.Seed).ToArray(),
                LX = new int[Lamps.Count],''',1)
    s=s.replace('            if (d.BT != null) for (int i = 0; i < d.BT.Length; i++) Buildings.Add(new Building { Type = (Tl)d.BT[i], X = d.BX[i], Y = d.BY[i], Level = d.BL[i] });','''            if (d.BT != null) for (int i = 0; i < d.BT.Length; i++)
                {
                    var bb = new Building { Type = (Tl)d.BT[i], X = d.BX[i], Y = d.BY[i], Level = d.BL[i] };
                    if (!legacy)
                    {
                        if (d.BPrio != null && i < d.BPrio.Length) bb.Prio = d.BPrio[i]; if (d.BMode != null && i < d.BMode.Length) bb.Mode = d.BMode[i];
                        if (d.BOff != null && i < d.BOff.Length) bb.Off = d.BOff[i]; if (d.BForced != null && i < d.BForced.Length) bb.Forced = d.BForced[i];
                        if (d.BLocal != null && (i + 1) * It.Count <= d.BLocal.Length) Array.Copy(d.BLocal, i * It.Count, bb.Local, 0, It.Count);
                    }
                    else bb.Forced = true;
                    Buildings.Add(bb);
                }
            if (legacy) { Ann = -1L; pendingMsg2 = "Сохранение обновлено: старые постройки связаны со складом автоматически, новые нужно подключать узлами снабжения"; }''')
    s=s.replace('            if (!legacy && d.LX != null)','''            if (!legacy)
            {
                TradeEpoch = d.TradeEpoch; ExtraVillagers = d.ExtraV; if (d.Bought != null) foreach (var b in d.Bought) Bought.Add(b);
                if (d.Demand != null) for (int i = 0; i < Demand.Length && i < d.Demand.Length; i++) Demand[i] = d.Demand[i];
                evNext = d.EvNext > 0 ? d.EvNext : 150;
                if (d.EvK != null) for (int i = 0; i < d.EvK.Length; i++) pendingEvents.Add(new GEvent { Kind = d.EvK[i], Pos = new Vector2(d.EvX[i], d.EvY[i]), T = d.EvT[i], Life = d.EvL[i], V = d.EvV[i], State = d.EvS[i], Wave = d.EvW[i], Seed = d.EvSeed[i] });
            }
            if (!legacy && d.LX != null)''')
    s=s.replace('    string pendingMsg;','    string pendingMsg, pendingMsg2; readonly List<GEvent> pendingEvents = new();\n    static int[] Flat(List<Building> bs) { var a = new int[bs.Count * It.Count]; for (int i = 0; i < bs.Count; i++) Array.Copy(bs[i].Local, 0, a, i * It.Count, It.Count); return a; }')
    s=s.replace('        if (pendingMsg != null) Toast(pendingMsg);','        if (pendingMsg != null) Toast(pendingMsg);\n        if (pendingMsg2 != null) Toast(pendingMsg2);\n        InitLightTech();\n        foreach (var ev in pendingEvents) { InitEventState(ev); Events.Add(ev); }\n        EventsEnabled = !testing;')
    # process hooks
    s=s.replace('        UpdateWeaponFx(dt);\n','        UpdateWeaponFx(dt);\n        UpdateEvents(dt);\n        UpdateTrade(dt);\n        UpdateLightTech(dt);\n',1)
    s=s.replace('        DrawWeaponFx(c);\n','        DrawWeaponFx(c);\n        DrawBarriers(c);\n        DrawEvents(c);\n')
    s=s.replace('        foreach (var z in ZoneLights()) if (n < Fx.MaxLights - 12) arr[n++] = z;','        foreach (var z in ZoneLights()) if (n < Fx.MaxLights - 12) arr[n++] = z;\n        foreach (var z in EventLights()) if (n < Fx.MaxLights - 6) arr[n++] = z;')
    # input
    s=s.replace('        if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right) TrySpin();\n        if (e is InputEventKey k && k.Pressed && !k.Echo)\n        {\n            switch (k.Keycode)','        if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right && Window == Win.None) TrySpin();\n        if (e is InputEventKey k && k.Pressed && !k.Echo)\n        {\n            if (Window != Win.None && k.Keycode != Key.F9) { WindowKey(k.Keycode, k.ShiftPressed); return; }\n            switch (k.Keycode)')
    s=s.replace('                case Key.V: CycleWeapon(); break;','                case Key.V: CycleWeapon(); break;\n                case Key.C: OpenWin(Win.Equip); break;\n                case Key.J: OpenWin(Win.Journal); break;\n                case Key.P: OpenWin(Win.Prod); break;\n                case Key.B: OpenWin(Win.Build); break;')
    s=s.replace('case Key.T: TechOpen = !TechOpen; MapOpen = false; Sfx.Play("tick"); break;','case Key.T: { bool o = !TechOpen; CloseWindows(); TechOpen = o; Sfx.Play("tick"); break; }')
    s=s.replace('case Key.M: MapOpen = !MapOpen; TechOpen = false; Sfx.Play("tick"); break;','case Key.M: { bool o = !MapOpen; CloseWindows(); MapOpen = o; Sfx.Play("tick"); break; }')
    s=s.replace('case Key.Escape: TechOpen = false; MapOpen = false; break;','case Key.Escape: CloseWindows(); break;')
    # lamp radius
    s=s.replace('    float lightT;','')
    return s
print(rw('Game.cs',game))

def light(s):
    s=s.replace('public int X, Y, Kind, Level = 1, Dir; public float Anim;','public int X, Y, Kind, Level = 1, Dir, Amp; public float Anim;')
    s=s.replace('        f *= (LightBlockT > 0 ? .4f : 1f) * (1f - .55f * SmokeAt(l.Pos));','        f *= (LightBlockT > 0 ? .4f : 1f) * (1f - .55f * SmokeAt(l.Pos)) * (1f + .12f * l.Amp) * (1f + N("LampRadius")) * (Surge > 0 ? 1.25f : 1f);')
    return s
print(rw('Game.Light.cs',light))

def combat(s):
    s=s.replace('        Kills++;\n        if (e.IsBoss) { BossKilled(e); return; }','        Kills++;\n        if (e.Ev != null) EvEnemyKilled(e);\n        if (e.IsBoss) { BossKilled(e); return; }')
    s=s.replace('    void Die()\n    {\n        Enemies.Clear();','    void Die()\n    {\n        EventsOnDeath();\n        Enemies.Clear();')
    s=s.replace('float sc = e.Kind switch { EK.Warden => 2.6f,','float sc = (e.Elite ? 1.25f : 1f) * e.Kind switch { EK.Warden => 2.6f,')
    s=s.replace('            if ((Has(Tech.Searing) || Has(Tech.HeroLamp)) && dist < auraR && !e.Hidden)','            if ((Has(Tech.Searing) || Has(Tech.HeroLamp) || HeroLampLv >= 2) && dist < auraR && !e.Hidden)')
    s=s.replace('    public void HurtPlayer(float dmg, Vector2 kb, bool fire = false)\n    {','    public void HurtPlayer(float dmg, Vector2 kb, bool fire = false)\n    {\n        if (Window == Win.Trade) Window = Win.None;')
    # barrier wrapper
    import re
    m=re.search(r'            else e\.Pos = Move\(e\.Pos, (.*?), e\.Kind == EK\.Wraith \|\| e\.Kind == EK\.Warden \|\| e\.Kind == EK\.DepthWarden \|\| e\.Kind == EK\.AshWarden \|\| e\.Kind == EK\.Spirit\);', s)
    if m:
        inner=m.group(0).replace('            else e.Pos = Move(','            else { var np = Move(').rstrip(';')+';\n                if (!e.IsBoss && Barriers.Count > 0 && BarrierCross(e.Pos, np)) { e.Kb = Vector2.Zero; } else e.Pos = np; }'
        s=s.replace(m.group(0), inner)
    return s
print(rw('Game.Combat.cs',combat))

def trade(s):
    s=s.replace('    public bool TradeOpen; public int TradeTab','    public int TradeTab')
    s=s.replace('CloseWindows(); TradeOpen = true; TradeEv = ev;','CloseWindows(); Window = Win.Trade; TradeEv = ev;')
    return s
print(rw('Game.Trade.cs',trade))

def prog(s):
    s=s.replace('        if (BeaconLevel == 2) { OpenSeal(W.Seal1, "Затопленные шахты"); }','        TradeEpoch++;\n        if (BeaconLevel == 2) { OpenSeal(W.Seal1, "Затопленные шахты"); }')
    s=s.replace('        var lamp = LampNear(PPos, 32);','        if (EventIx(ix)) return ix;\n        var lamp = LampNear(PPos, 32);')
    return s
print(rw('Game.Progress.cs',prog))

def ev(s):
    s=s.replace('''        for (int i = Enemies.Count - 1; i >= 0; i--) if (Enemies[i].Ev == ev && !ev.Kind.Equals("lamp") || (Enemies[i].Ev == ev && ev.Kind == "lamp")) { if (Enemies[i] == Boss) Boss = null; Enemies.RemoveAt(i); }''','''        for (int i = Enemies.Count - 1; i >= 0; i--) if (Enemies[i].Ev == ev) { if (Enemies[i] == Boss) Boss = null; Enemies.RemoveAt(i); }''')
    s=s.replace('    void InitEvent(GEvent ev)\n    {','''    /// <summary>Восстановление события из сохранения: заново создаёт его сущности.</summary>
    void InitEventState(GEvent ev)
    {
        int reg = World.RegionOf((int)(ev.Pos.X / TS));
        switch (ev.Kind)
        {
            case "elite": EvEnemy(ev, ev.Pos, PoolKind(reg, true), 3.5f, true); break;
            case "caravan": { var c = EvEnemy(ev, ev.Pos, PoolKind(reg, true), 2.2f, true); c.Carrier = true; for (int i = 0; i < 3; i++) EvEnemy(ev, ev.Pos + Vector2.FromAngle(i * 2.1f) * 26, PoolKind(reg)); break; }
            case "trader": case "traveler": ev.Npc = ev.Pos; break;
            case "rift": if (ev.State == 1) { ev.State = 0; ev.Life = 240; ev.T = 0; } break;
        }
    }

    void InitEvent(GEvent ev)
    {''')
    return s
print(rw('Game.Events.cs',ev))
