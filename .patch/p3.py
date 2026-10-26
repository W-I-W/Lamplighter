def rw(p, f):
    s=open(p,encoding='utf-8').read(); s2=f(s); open(p,'w',encoding='utf-8').write(s2); return s!=s2

def art(s): return s.replace('TileCount = 85;','TileCount = 101;')
print(rw('Art.cs',art))
def a2(s):
    s=s.replace('        for (int k = 0; k < 4; k++) DrawBuilding(new Cv(img, l4[k] * 16, 0), k, 4, r);','        for (int k = 0; k < 4; k++) DrawBuilding(new Cv(img, l4[k] * 16, 0), k, 4, r);\n        ExtraTiles3(img, r);')
    s=s.replace('        ExtraEntities2();\n','')
    return s
print(rw('ArtExtra2.cs',a2))
def a1(s):
    s=s.replace('        BomberTex = ImageTexture.CreateFromImage(img);\n        ExtraEntities2();\n    }','        BomberTex = ImageTexture.CreateFromImage(img);\n        ExtraEntities2(); ExtraEntities3();\n    }')
    return s
print(rw('ArtExtra.cs',a1))

def world(s):
    s=s.replace('    DeadTree, Obsidian, Ember, AshRuin, Vent\n}','    DeadTree, Obsidian, Ember, AshRuin, Vent,\n    TechCache, Spire, Altar, AltarOn, SecretCache, Stall, Hub, Refinery, AmpCrystal, BarrierPost, MeteorOre\n}')
    s=s.replace('PK { Chest, Ruins, House, Camp, Shrine, Captive, Obelisk, Grove, Workshop, Puzzle, BridgeSite }','PK { Chest, Ruins, House, Camp, Shrine, Captive, Obelisk, Grove, Workshop, Puzzle, BridgeSite, TechCache, Spire, Altar, Secret }')
    s=s.replace('public byte State, Variant; public bool Seen, Revealed; public int Guards;','public byte State, Variant; public bool Seen, Revealed; public int Guards; public string Tag;')
    s=s.replace('public static bool Blocks(Tl t) => t != Tl.None && t != Tl.Bridge;','public static bool Blocks(Tl t) => t != Tl.None && t != Tl.Bridge && t != Tl.SecretCache;')
    s=s.replace('        w.GenAsh(seed);\n        return w;','        w.GenAsh(seed);\n        w.GenFinds(seed);\n        return w;')
    return s
print(rw('World.cs',world))
