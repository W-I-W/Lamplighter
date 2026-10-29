using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Обелиск рун и древо-хранитель (этап 8). Обелиск показывает порядок вспышек четырёх светил вокруг него, игрок повторяет его касанием.
/// Порядок задан миром и сохраняется неявно; незавершённая загадка после загрузки начинается заново. Древо-хранитель — одноразовый дар у корней огромного древа.</summary>
public partial class Game
{
    sealed class RuneRun { public int[] Seq; public int Pos; public bool Showing; public float T; public int Phase; }
    readonly Dictionary<Poi, RuneRun> runes = new();
    Texture2D giantTex;

    RuneRun Rune(Poi p)
    {
        if (runes.TryGetValue(p, out var r)) return r;
        var rnd = new Random(W.Seed * 53 + p.Tile.X * 29 + p.Tile.Y * 7); var seq = new[] { 0, 1, 2, 3 };
        for (int i = 3; i > 0; i--) { int j = rnd.Next(i + 1); (seq[i], seq[j]) = (seq[j], seq[i]); }
        return runes[p] = new RuneRun { Seq = seq };
    }

    Vector2I RunePylon(Poi p, int i) => new(p.Tile.X + World.RuneOffs[i].X, p.Tile.Y + World.RuneOffs[i].Y);

    void RuneReset(Poi p, bool lit)
    {
        for (int i = 0; i < 4; i++) { var t = RunePylon(p, i); var cur = W.Get(t.X, t.Y); if (cur == Tl.Pylon || cur == Tl.PylonOn) SetObj(t.X, t.Y, lit ? Tl.PylonOn : Tl.Pylon); }
    }

    bool RuneIx(Poi p, Ix ix)
    {
        ix.Title = "Обелиск рун";
        if (p.State >= 3) { ix.Lines.Add("Обелиск молчит: порядок разгадан"); return true; }
        var r = Rune(p);
        if (r.Showing) { ix.Lines.Add("Обелиск показывает порядок: запоминайте"); return true; }
        ix.Lines.Add($"Светила горят {r.Pos}/4");
        ix.Action("E", "Выслушать обелиск"); ix.Gives("Даст", "Реликвию за верный порядок", TipRole.Key); ix.E = () => RuneShow(p); return true;
    }

    void RuneShow(Poi p)
    {
        var r = Rune(p); if (r.Showing || p.State >= 3) return;
        RuneReset(p, false); r.Pos = 0; r.Showing = true; r.Phase = 0; r.T = .6f; Sfx.Play("tech", -4, .9f); Toast("Обелиск показывает порядок светил");
    }

    bool RunePylonIx(int x, int y, Ix ix)
    {
        foreach (var p in W.Pois)
        {
            if (p.Kind != PK.RuneObelisk || Math.Abs(p.Tile.X - x) > 3 || Math.Abs(p.Tile.Y - y) > 3) continue;
            for (int i = 0; i < 4; i++)
            {
                if (RunePylon(p, i) != new Vector2I(x, y)) continue;
                ix.Title = "Светило обелиска";
                if (p.State >= 3) { ix.Lines.Add("Светило горит: порядок разгадан"); return true; }
                ix.Lines.Add("[E] Коснуться светила"); int idx = i; ix.E = () => RuneTouch(p, idx); return true;
            }
        }
        return false;
    }

    void RuneTouch(Poi p, int i)
    {
        var r = Rune(p); if (p.State >= 3) return;
        var t = RunePylon(p, i);
        if (r.Showing) { Toast("Обелиск ещё показывает порядок"); return; }
        if (r.Pos == 0 && !runeAsked.Contains(p)) { Toast("Сначала выслушайте обелиск [E]"); return; }
        if (r.Seq[r.Pos] == i && W.Get(t.X, t.Y) == Tl.Pylon)
        {
            r.Pos++; SetObj(t.X, t.Y, Tl.PylonOn); Burst(TileCenter(t.X, t.Y), new Color("#8ff0ff"), 12, 70); Sfx.Play("tick", -2, 1f + .2f * r.Pos);
            if (r.Pos >= 4) RuneSolved(p);
        }
        else { r.Pos = 0; RuneReset(p, false); Burst(TileCenter(t.X, t.Y), new Color("#9a60d0"), 10, 60); Sfx.Play("tick", -3, .6f); Toast("Светила погасли: порядок неверен"); }
    }

    readonly HashSet<Poi> runeAsked = new();

    void RuneSolved(Poi p)
    {
        p.State = 3; p.Seen = true; SetObj(p.Tile.X, p.Tile.Y, Tl.ObeliskUsed);
        if (p.Tag != null) foreach (var tg in p.Tag.Split(';')) GrantTag(tg, p.Pos);
        Burst(p.Pos, new Color("#8ff0ff"), 30, 110); Flash = .3f; Sfx.Play("ancient"); GiveXp(70);
        Announce("Обелиск рун разгадан", "Светила вспомнили порядок: награда получена", new Color("#8ff0ff"), "ancient"); Save();
    }

    void UpdateRunes(float dt)
    {
        foreach (var kv in runes)
        {
            var p = kv.Key; var r = kv.Value; if (!r.Showing) continue;
            r.T -= dt; if (r.T > 0) continue;
            int n = r.Seq.Length;
            if (r.Phase % 2 == 1) { var prev = RunePylon(p, r.Seq[r.Phase / 2]); if (W.Get(prev.X, prev.Y) == Tl.PylonOn) SetObj(prev.X, prev.Y, Tl.Pylon); r.T = .3f; }
            if (r.Phase >= 2 * n) { r.Showing = false; r.Pos = 0; runeAsked.Add(p); Toast("Теперь коснитесь светил в том же порядке"); continue; }
            if (r.Phase % 2 == 0) { var t = RunePylon(p, r.Seq[r.Phase / 2]); SetObj(t.X, t.Y, Tl.PylonOn); Burst(TileCenter(t.X, t.Y), new Color("#ffe27a"), 10, 60); Sfx.Play("tick", -2, 1f + .15f * (r.Phase / 2)); r.T = .6f; }
            r.Phase++;
        }
    }

    /// <summary>После загрузки: решённый обелиск горит, остальные начинают заново.</summary>
    void RestoreRuneTiles()
    {
        foreach (var p in W.Pois) if (p.Kind == PK.RuneObelisk) { var r = Rune(p); r.Pos = 0; r.Showing = false; runeAsked.Remove(p); RuneResetTiles(p, p.State >= 3); }
    }

    void RuneResetTiles(Poi p, bool lit)
    {
        for (int i = 0; i < 4; i++) { var t = RunePylon(p, i); var cur = W.Get(t.X, t.Y); if (cur == Tl.Pylon || cur == Tl.PylonOn) W.Set(t.X, t.Y, lit ? Tl.PylonOn : Tl.Pylon); }
    }

    // ───────────────────────── древо-хранитель ─────────────────────────
    bool GiantTreeIx(Poi p, Ix ix)
    {
        ix.Title = "Древо-хранитель";
        if (p.State >= 3) { ix.Lines.Add("Древо хранит тепло: дар уже получен"); return true; }
        ix.Lines.Add("Корни древа светятся тёплым светом");
        ix.Action("E", "Коснуться корней"); ix.Gives("Даст", "Реликвию, лечение и осколки света", TipRole.Key); ix.E = () => UseGiantTree(p); return true;
    }

    void UseGiantTree(Poi p)
    {
        if (p.State >= 3) return;
        p.State = 3; p.Seen = true; SetObj(p.Tile.X, p.Tile.Y, Tl.ShrineUsed); Hp = MaxHp;
        if (p.Tag != null) foreach (var tg in p.Tag.Split(';')) GrantTag(tg, p.Pos);
        Burst(p.Pos + new Vector2(0, -16), new Color("#9af06a"), 34, 120); Flash = .25f; Sfx.Play("ancient"); GiveXp(60);
        Announce("Древо-хранитель благословило вас", "Здоровье восстановлено, дар получен", new Color("#9af06a"), "ancient"); Save();
    }

    void DrawGiantTrees(List<(float y, Action a)> list, EntityLayer c)
    {
        giantTex ??= GD.Load<Texture2D>("res://Assets/Sprites/Landmarks/giant_tree.png"); if (giantTex == null) return;
        foreach (var p in W.Pois)
        {
            if (p.Kind != PK.GiantTree || p.Pos.DistanceTo(CamCenter) > 420) continue; var pp = p;
            list.Add((p.Tile.Y * TS, () => c.DrawTexture(giantTex, new Vector2(pp.Pos.X - 40, pp.Tile.Y * TS - 96))));
        }
    }
}
