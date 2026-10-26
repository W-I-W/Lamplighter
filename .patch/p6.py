def rw(p, f):
    s=open(p,encoding='utf-8').read(); s2=f(s); open(p,'w',encoding='utf-8').write(s2); return s!=s2

def hud(s):
    s=s.replace('        DrawMarkers(vs);\n        DrawStatus();','        DrawMarkers(vs);\n        DrawEventMarkers(vs);\n        DrawStatus();')
    s=s.replace('        if (G.TechOpen) DrawTech(vs);\n        if (G.MapOpen) DrawMap(vs);','        DrawStage4Hud(vs);\n        if (G.TechOpen) DrawTech(vs);\n        if (G.MapOpen) DrawMap(vs);\n        DrawWindows(vs);')
    return s
print(rw('Hud.cs',hud))

def ev(s):
    s=s.replace('    public bool TrialActive;\n    float trialT;','    public bool TrialActive;\n    float trialT; public float TrialLeft => trialT;')
    return s
print(rw('Game.Events.cs',ev))

def vil(s):
    s=s.replace('    public static int MaxLv(int bk)','    public static float BIntervalOf(int bk, int lv) => BInterval[bk] != null ? BInterval[bk][lv - 1] : 1f;\n    public static int MaxLv(int bk)')
    return s
print(rw('Game.Village.cs',vil))
