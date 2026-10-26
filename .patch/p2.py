def rw(p, f):
    s=open(p,encoding='utf-8').read(); s2=f(s); open(p,'w',encoding='utf-8').write(s2); return s!=s2

def art2(s):
    anchor = '        c.R(7, 0, 2, 2, H("#ffe27a")); c.R(7, 14, 2, 2, H("#ffe27a")); c.R(0, 7, 2, 2, H("#ffe27a")); c.R(14, 7, 2, 2, H("#ffe27a"));\n        c.Outline(H("#3a2a08"));\n'
    add = '''        // 15 древний кристалл 16 монеты 17 шестерня 18 линза
        c = new Cv(img, 15 * 16, 0);
        for (int y = 0; y < 16; y++) { int w = y < 8 ? 1 + y : 16 - y; c.R(8 - w / 2 - 1, y, w + 1, 1, y % 3 == 0 ? H("#ffd0ff") : H("#e07aff")); }
        c.R(7, 3, 1, 8, H("#ffffff")); c.P(10, 6, H("#ffffff")); c.Outline(H("#3a0c4a"));
        c = new Cv(img, 16 * 16, 0);
        c.E(8, 11, 5.5f, 2.6f, H("#c9a24a")); c.E(8, 9.5f, 5.5f, 2.6f, H("#ffd86b")); c.E(8, 7.5f, 5.5f, 2.6f, H("#c9a24a")); c.E(8, 6, 5.5f, 2.6f, H("#ffe27a"));
        c.R(7, 5, 2, 2, H("#fff2b0")); c.Outline(H("#3a2a08"));
        c = new Cv(img, 17 * 16, 0);
        c.E(8, 8, 6, 6, H("#9aa0ae")); c.E(8, 8, 2.6f, 2.6f, Clear);
        foreach (var p in new[] { (7, 0), (7, 14), (0, 7), (14, 7), (2, 2), (12, 2), (2, 12), (12, 12) }) c.R(p.Item1, p.Item2, 2, 2, H("#b8bfce"));
        c.R(6, 6, 4, 4, H("#6a7080")); c.Outline(H("#1c2030"));
        c = new Cv(img, 18 * 16, 0);
        c.E(8, 8, 6, 6, H("#5aa8d0")); c.E(8, 8, 4.5f, 4.5f, H("#9fe8ff")); c.E(8, 8, 2.2f, 2.2f, H("#e8ffff")); c.P(5, 5, H("#ffffff")); c.P(6, 5, H("#ffffff"));
        c.R(7, 14, 2, 2, H("#3a4a6a")); c.Outline(H("#10203a"));
'''
    return s.replace(anchor, anchor + add, 1)
print(rw('ArtExtra2.cs', art2))

def hud(s):
    s=s.replace('float ix = 10, iy = vs.Y - 52, sw = 50;','float ix = 10, iy = vs.Y - 52, sw = 40;')
    s=s.replace('DrawTextureRectRegion(Art.IconTex, new Rect2(p, new Vector2(26, 26)), new Rect2(i * 16, 0, 16, 16), G.Inv[i] > 0 ? Colors.White : new Color(1, 1, 1, .25f));\n            Txt(G.Inv[i].ToString(), p + new Vector2(24, 24), 12, G.Inv[i] > 0 ? Colors.White : Dim);','DrawTextureRectRegion(Art.IconTex, new Rect2(p, new Vector2(22, 22)), new Rect2(i * 16, 0, 16, 16), G.Inv[i] > 0 ? Colors.White : new Color(1, 1, 1, .25f));\n            Txt(G.Inv[i].ToString(), p + new Vector2(19, 26), 10, G.Inv[i] > 0 ? Colors.White : Dim);')
    s=s.replace('var p = new Vector2(ix + 6 + i * sw, iy + 6);','var p = new Vector2(ix + 5 + i * sw, iy + 4);')
    return s
print(rw('Hud.cs', hud))
