def rw(p, f):
    s=open(p,encoding='utf-8').read(); s2=f(s); open(p,'w',encoding='utf-8').write(s2); return s!=s2

def t(s):
    s=s.replace('        if (OS.GetEnvironment("LAMP_STAGE") == "s4b") { BuildS4bScript(); return; }','        if (OS.GetEnvironment("LAMP_STAGE") == "s4b") { BuildS4bScript(); return; }\n        if (OS.GetEnvironment("LAMP_STAGE") == "perf") { BuildPerfScript(); return; }')
    return s
print(rw('Game.Test.cs',t))

def p(s):
    s=s.rstrip()
    assert s.endswith('}')
    s=s[:-1]+'''
    void BuildPerfScript()
    {
        var spawn = TileCenter(W.Spawn.X, W.Spawn.Y);
        S(.3f, () =>
        {
            for (int i = 0; i < Inv.Length; i++) Inv[i] = 80; Tp(spawn + new Vector2(0, 60)); aimOverride = null;
            for (int i = 0; i < 4; i++) { var a = TileCenter(W.Spawn.X + 4 + i * 3, W.Spawn.Y - 6); Buildings.Add(new Building { Type = i % 2 == 0 ? Tl.Sawmill : Tl.Mine, X = W.Spawn.X + 4 + i * 3, Y = W.Spawn.Y - 6, Forced = true }); }
            for (int i = 0; i < 30; i++) { var e = SpawnEnemy(PPos + new Vector2(rng.RandfRange(-120, 120), rng.RandfRange(-90, 90)), i % 3 == 0 ? EK.Wraith : EK.Shade); e.Age = 1; e.Hp = e.MaxHp = 9999; }
            for (int i = 0; i < 12; i++) Lamps.Add(new Lamp { X = W.Spawn.X - 10 + i, Y = W.Spawn.Y + 8, Kind = i % 3, Anim = 1 });
            GiveWeapon("staff1", false); EquipWeapon(Owned.Count - 1); ModInv[2] = 2; InstallMod(2); EventsEnabled = true;
            SpawnEvent("elite", PPos + new Vector2(150, 0)); SpawnEvent("trader", spawn + new Vector2(20, 30));
        });
        S(4f, () => { perfT0 = Time; });
        S(3f, () =>
        {
            var tp = Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000; var fps = Engine.GetFramesPerSecond();
            Log($"PERF: fps={fps:F0} processMs={tp:F2} enemies={Enemies.Count} parts={Parts.Count} lamps={Lamps.Count} objects={Performance.GetMonitor(Performance.Monitor.ObjectCount)} mem={Performance.GetMonitor(Performance.Monitor.MemoryStatic) / 1048576:F0}MB");
        });
    }
    float perfT0;
}
'''
    return s
print(rw('Game.TestS4.cs',p))
