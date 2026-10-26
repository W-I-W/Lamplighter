def rw(p, f):
    s=open(p,encoding='utf-8').read(); s2=f(s); open(p,'w',encoding='utf-8').write(s2); return s!=s2

def win(s):
    s=s.replace('        DrawRect(new Rect2(Vector2.Zero, G.ViewSize), new Color(0, 0, 0, .35f));\n        Box(r, border);','        DrawRect(new Rect2(Vector2.Zero, G.ViewSize), new Color(0, 0, 0, .35f));\n        DrawRect(r, new Color(.035f, .045f, .08f, .985f));\n        Box(r, border);')
    s=s.replace('        var r = new Rect2(10, 130, 400, 480);\n        Box(r, new Color("#d9b27c"));','        var r = new Rect2(10, 130, 400, 480);\n        DrawRect(r, new Color(.035f, .045f, .08f, .985f));\n        Box(r, new Color("#d9b27c"));')
    return s
print(rw('HudWin.cs',win))

def game(s):
    s=s.replace('if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) dir.Y -= 1;','bool arrows = Window == Win.None && !TechOpen;\n            if (Input.IsKeyPressed(Key.W) || (arrows && Input.IsKeyPressed(Key.Up))) dir.Y -= 1;')
    s=s.replace('if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) dir.Y += 1;','if (Input.IsKeyPressed(Key.S) || (arrows && Input.IsKeyPressed(Key.Down))) dir.Y += 1;')
    s=s.replace('if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) dir.X -= 1;','if (Input.IsKeyPressed(Key.A) || (arrows && Input.IsKeyPressed(Key.Left))) dir.X -= 1;')
    s=s.replace('if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) dir.X += 1;','if (Input.IsKeyPressed(Key.D) || (arrows && Input.IsKeyPressed(Key.Right))) dir.X += 1;')
    # overlay of new finds on load
    s=s.replace('            var obj = Convert.FromBase64String(d.Obj);\n            bool legacy = d.Version < 3;','            var obj = Convert.FromBase64String(d.Obj);\n            bool legacy = d.Version < 3; var genObj = (byte[])W.Obj.Clone();')
    s=s.replace('            for (int i = 0; i < W.Ancients.Count && i < d.Lit.Length; i++) if (d.Lit[i])','''            OverlayNewFinds(genObj, d.PoiState != null ? d.PoiState.Length : 0, legacy);
            for (int i = 0; i < W.Ancients.Count && i < d.Lit.Length; i++) if (d.Lit[i])''',1)
    s=s.replace('    public override void _Notification(int what)','''    /// <summary>Старые сохранения не знают о новых находках: переносим их плитки из свежесгенерированного мира.</summary>
    void OverlayNewFinds(byte[] gen, int known, bool legacy)
    {
        for (int i = known; i < W.Pois.Count; i++)
        {
            var p = W.Pois[i]; if (legacy && World.RegionOf(p.Tile.X) != 0) continue;
            for (int y = p.Tile.Y - 5; y <= p.Tile.Y + 5; y++)
                for (int x = p.Tile.X - 5; x <= p.Tile.X + 5; x++)
                {
                    if (!World.In(x, y)) continue; int k = World.Key(x, y);
                    if (W.Obj[k] == (byte)Tl.None && gen[k] != (byte)Tl.None) W.Obj[k] = gen[k];
                }
        }
    }

    public override void _Notification(int what)''',1)
    return s
print(rw('Game.cs',game))

def hud(s):
    s=s.replace('        foreach (var b in G.Buildings) DrawRect(new Rect2(o + b.Pos / 16f * s - new Vector2(2, 2), new Vector2(4, 4)), new Color("#d9b27c"));','        foreach (var b in G.Buildings) DrawRect(new Rect2(o + b.Pos / 16f * s - new Vector2(2, 2), new Vector2(4, 4)), new Color("#d9b27c"));\n        foreach (var ev in G.Events) { var ep = o + ev.Pos / 16f * s; var ec = Game.EvColor(ev.Kind); DrawColoredPolygon(new[] { ep + new Vector2(0, -5), ep + new Vector2(4, 0), ep + new Vector2(0, 5), ep + new Vector2(-4, 0) }, ec); }')
    return s
print(rw('Hud.cs',hud))

def t(s):
    s=s.replace('var a = ToTile(PPos + new Vector2(-60, -40)); SetObj(a.X, a.Y, Tl.BarrierPost);','var a = ToTile(PPos + new Vector2(-60, -40)); for (int yy = a.Y - 1; yy <= a.Y + 8; yy++) for (int xx = a.X - 7; xx <= a.X + 7; xx++) SetObj(xx, yy, Tl.None); SetObj(a.X, a.Y, Tl.BarrierPost);')
    return s
print(rw('Game.TestS4b.cs',t))

def t2(s):
    s=s.replace('machines={MachinesRestored} sealOpen=','machines={MachinesRestored} weapons=[{string.Join(",", Owned.ConvertAll(o => o.Id + ":" + o.Mods.Count))}] cur={CurW} relics={OwnedRelics.Count}/{EquippedRelics.Count} bps={BpDone.Count} frag={Frag.Count} mods=[{string.Join(",", ModInv)}] hero={HeroLampLv} epoch={TradeEpoch} bought={Bought.Count} extraV={ExtraVillagers} found={Found.Count} syn={ActiveSyn.Count} sealOpen=')
    return s
print(rw('Game.Test.cs',t2))
