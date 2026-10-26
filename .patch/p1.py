import re
def rw(p, f):
    s=open(p,encoding='utf-8').read(); s2=f(s); open(p,'w',encoding='utf-8').write(s2); return s!=s2

def game(s):
    s=s.replace('SilverOre = 9, Silver = 10, Crystal = 11, Obsidian = 12, Ember = 13, Core = 14, Count = 15;','SilverOre = 9, Silver = 10, Crystal = 11, Obsidian = 12, Ember = 13, Core = 14, AncCrystal = 15, Coins = 16, Gear = 17, Lens = 18, Count = 19;')
    s=s.replace('"Серебряная руда", "Серебро", "Кристалл", "Обсидиан", "Уголь пепла", "Ядро света" };','"Серебряная руда", "Серебро", "Кристалл", "Обсидиан", "Уголь пепла", "Ядро света", "Древний кристалл", "Монеты", "Шестерня", "Линза" };')
    s=s.replace('"с.руда", "сер.", "крист.", "обсид.", "угол.", "ядро" };','"с.руда", "сер.", "крист.", "обсид.", "угол.", "ядро", "др.крист.", "мон.", "шест.", "линза" };')
    s=s.replace('new("#e8eef6"), new("#cfd6e2"), new("#bfe9ff"), new("#a890ff"), new("#ff9a2e"), new("#fff2b0") };','new("#e8eef6"), new("#cfd6e2"), new("#bfe9ff"), new("#a890ff"), new("#ff9a2e"), new("#fff2b0"), new("#ffb0ff"), new("#ffd86b"), new("#b0b8c8"), new("#9fe8ff") };')
    s=s.replace('    void Act(float dt)\n    {\n        var toAim = Aim - PPos;','    void ActPick(float dt, bool melee)\n    {\n        var toAim = Aim - PPos;')
    s=s.replace('        if (best != null)\n        {\n            MineProg = 0; MineTile = new Vector2I(-1, -1);\n            if (AtkCd <= 0) { AtkCd = .42f;','        if (!melee) best = null;\n        if (best != null)\n        {\n            MineProg = 0; MineTile = new Vector2I(-1, -1);\n            if (AtkCd <= 0) { AtkCd = .42f;')
    s=s.replace('* (Has(Tech.HeroLamp) ? 1.25f : 1f) * LightDim;','* (Has(Tech.HeroLamp) ? 1.25f : 1f) * (1f + .12f * HeroLampLv + N("HeroRadius")) * (Surge > 0 ? 1.4f : 1f) * LightDim;')
    s=s.replace('        float speed = 80f * (water ? (Has(Tech.Boots) ? .9f : .55f) : 1f);','        float speed = 80f * (1f + N("Speed")) * (water ? (Has(Tech.Boots) ? .9f : .55f) : 1f);')
    s=s.replace('int ChainLimit => Tier >= 4 ? 5 : Tier >= 3 ? 3 : Has(Tech.WideSwing) ? 1 : 0;','int ChainLimit => (Tier >= 4 ? 5 : Tier >= 3 ? 3 : Has(Tech.WideSwing) ? 1 : 0) + (int)N("MineChain");')
    s=s.replace('        DrawBeams(c);\n','        DrawBeams(c);\n        DrawWeaponFx(c);\n')
    s=s.replace('                case Key.Q: TryFlare(); break;','                case Key.Q: TryFlare(); break;\n                case Key.V: CycleWeapon(); break;')
    s=s.replace('        UpdateBeams(dt);\n','        UpdateBeams(dt);\n        UpdateWeaponFx(dt);\n        UpdateRelics(dt);\n',1)
    s=s.replace('    public int TechMask; public long Ann;','    public int HeroLampLv;\n    public int TechMask; public long Ann;')
    s=s.replace('    public int[] LX, LY, LK, LL, LD; public string Explored;\n}','''    public int[] LX, LY, LK, LL, LD; public string Explored;
    // этап 4
    public string[] WId; public int[][] WMods; public int CurW, HeroLamp; public int[] ModInv;
    public string[] Relics, Equipped, BpDone, FragK, Found; public int[] FragV;
}''')
    s=s.replace('    public int Version = 3, Seed;','    public int Version = 4, Seed;')
    s=s.replace('                LX = new int[Lamps.Count],','                WId = Owned.ConvertAll(o => o.Id).ToArray(), WMods = Owned.ConvertAll(o => o.Mods.ToArray()).ToArray(), CurW = CurW, HeroLamp = HeroLampLv, ModInv = ModInv,\n                Relics = OwnedRelics.ToArray(), Equipped = EquippedRelics.ToArray(), BpDone = new List<string>(BpDone).ToArray(), FragK = new List<string>(Frag.Keys).ToArray(), FragV = new List<int>(Frag.Values).ToArray(), Found = Found.ToArray(),\n                LX = new int[Lamps.Count],')
    s=s.replace('            if (d.Explored != null) {','''            if (d.WId != null) for (int i = 0; i < d.WId.Length; i++) { var o = new OwnedWeapon { Id = d.WId[i] }; if (d.WMods != null && i < d.WMods.Length && d.WMods[i] != null) o.Mods.AddRange(d.WMods[i]); if (Db.Weapon(o.Id) != null) Owned.Add(o); }
            CurW = Math.Min(d.CurW, Owned.Count - 1); HeroLampLv = d.HeroLamp;
            if (d.ModInv != null) for (int i = 0; i < ModInv.Length && i < d.ModInv.Length; i++) ModInv[i] = d.ModInv[i];
            if (d.Relics != null) OwnedRelics.AddRange(d.Relics); if (d.Equipped != null) EquippedRelics.AddRange(d.Equipped);
            if (d.BpDone != null) foreach (var b in d.BpDone) BpDone.Add(b);
            if (d.FragK != null && d.FragV != null) for (int i = 0; i < d.FragK.Length && i < d.FragV.Length; i++) Frag[d.FragK[i]] = d.FragV[i];
            if (d.Found != null) Found.AddRange(d.Found);
            if (d.Explored != null) {''',1)
    s=s.replace('        Art.Build();\n','        Art.Build();\n        Db.Init();\n',1)
    return s
print(rw('Game.cs',game))

def combat(s):
    s=s.replace('    public int State; public bool Hidden; public Vector2 Mark;','    public int State; public bool Hidden, Elite, Carrier; public Vector2 Mark; public float Slow, Freeze; public GEvent Ev;')
    s=s.replace('DashDir = d; DashT = .18f; DashCd = 1.6f; Invuln = Mathf.Max(Invuln, .3f); Sfx.Play("dash");','DashDir = d; DashT = .18f; DashCd = 1.6f; Invuln = Mathf.Max(Invuln, .3f); Sfx.Play("dash"); RelicOnDash();')
    s=s.replace('PulseCd = 12f; Sfx.Play("pulse"); Shake = 5; Flash = .3f; LightBlockT = 0;','PulseCd = 12f; Sfx.Play("pulse"); Shake = 5; Flash = .3f; LightBlockT = 0; RelicOnPulse();')
    s=s.replace('        if (fire && Has(Tech.HeatShield)) dmg *= .4f;','        if (fire && Has(Tech.HeatShield)) dmg *= .4f;\n        if (fire) dmg *= 1f - N("HeatResist");')
    s=s.replace('            if (e.Burn > 0) { e.Burn -= dt;','            e.Slow = Mathf.Max(0, e.Slow - dt); e.Freeze = Mathf.Max(0, e.Freeze - dt);\n            if (e.Burn > 0) { e.Burn -= dt;')
    s=s.replace('            bool guard = e.Guard != null || e.IsBoss;','            bool guard = e.Guard != null || e.IsBoss || e.Ev != null;')
    s=s.replace('            else switch (e.Kind)\n','            else if (e.Freeze > 0) { }\n            else switch (e.Kind)\n',1)
    s=s.replace('            else e.Pos = Move(e.Pos, (vel * (','            else e.Pos = Move(e.Pos, ((e.Slow > 0 ? vel * (e.IsBoss ? .75f : .5f) : vel) * (')
    s=s.replace('        if (Has(Tech.Reaper)) {','        if (E(Fxk.Blast) > 0 && !inBlast) BlastAt(e.Pos, CurDef != null ? WeaponDmg(CurDef) : PlayerDamage);\n        if (N("KillHeal") > 0) Hp = Mathf.Min(MaxHp, Hp + N("KillHeal"));\n        if (Has(Tech.Reaper)) {')
    s=s.replace('        BossMask |= 1 << (id - 1); if (Boss == e) Boss = null;','        BossMask |= 1 << (id - 1); if (Boss == e) Boss = null;\n        GiveWeapon(id == 1 ? "sword_u" : id == 2 ? "bow_u" : "staff_u");\n        GiveRelic(id == 1 ? "r_dawnprism" : id == 2 ? "r_winterheart" : "r_emberheart");')
    s=s.replace('        Color mod = e.Flash > 0 ? new Color(1.8f, .6f, .6f, a) : new Color(1, 1, 1, a);','        Color mod = e.Flash > 0 ? new Color(1.8f, .6f, .6f, a) : e.Freeze > 0 ? new Color(.6f, .9f, 1.6f, a) : e.Slow > 0 ? new Color(.75f, .95f, 1.25f, a) : e.Elite ? new Color(1.3f, 1.15f, .7f, a) : new Color(1, 1, 1, a);')
    return s
print(rw('Game.Combat.cs',combat))

def ash(s):
    return s.replace('        if (fire && Has(Tech.HeatShield)) amt *= .4f;','        if (fire && Has(Tech.HeatShield)) amt *= .4f;\n        if (fire) amt *= 1f - N("HeatResist");')
print(rw('Game.Ash.cs',ash))

def art(s):
    s=s.replace('var img = NewImg(16 * 16);\n        var r = new Random(77);','var img = NewImg(16 * 20);\n        var r = new Random(77);')
    s=s.replace('c = new Cv(img, 240, 0);\n        c.R(7, 4, 2, 11','c = new Cv(img, 304, 0);\n        c.R(7, 4, 2, 11')
    return s
print(rw('Art.cs',art))

def tech(s): return s.replace('    static int[][] C(params int[] flat)','    public static int[][] C(params int[] flat)')
print(rw('Game.Tech.cs',tech))

def hud(s): return s.replace('Txt(Game.PickNames[G.Tier], new Vector2(18, 28), 12, Dim, HorizontalAlignment.Right, 216);','Txt(G.CurDef != null ? G.CurDef.Name : Game.PickNames[G.Tier], new Vector2(18, 28), 12, G.CurDef != null ? Db.RarCol[(int)G.CurDef.Rar] : Dim, HorizontalAlignment.Right, 216);')
print(rw('Hud.cs',hud))
