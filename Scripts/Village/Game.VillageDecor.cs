using Godot;
using System;

namespace Lamplighter;

/// <summary>Визуальное развитие деревни: небольшие наборы декораций вокруг зданий по типу и уровню (брёвна, ящики, цветы, скамейка, костёр, фонари), дым и искры.
/// Декор — только рисунок поверх свободных клеток (Assets/Sprites/Village/decor.png): он не блокирует ходьбу, не мешает строительству (клетка с объектом пропускается) и не имеет логики.</summary>
public partial class Game
{
    Texture2D decorTex;
    const int DLogs = 0, DCrates = 1, DFlowerR = 2, DFlowerB = 3, DBench = 4, DFire = 5, DPost = 7;

    bool DecorFree(int x, int y) => World.In(x, y) && W.Get(x, y) == Tl.None && W.Ground[World.Key(x, y)] != Art.WaterG && !Lamps.Exists(l => l.X == x && l.Y == y);

    void Dec(EntityLayer c, int x, int y, int id)
    {
        if (!DecorFree(x, y)) return;
        c.DrawTextureRectRegion(decorTex, new Rect2(x * TS, y * TS, TS, TS), new Rect2(id * 16, 0, 16, 16));
    }

    void DrawVillageDecor(EntityLayer c)
    {
        decorTex ??= GD.Load<Texture2D>("res://Assets/Sprites/Village/decor.png"); if (decorTex == null) return;
        var cc = CamCenter;
        foreach (var b in Buildings)
        {
            if (b.Pos.DistanceTo(cc) > 440) continue;
            switch (b.Type)
            {
                case Tl.Sawmill: Dec(c, b.X - 1, b.Y + 1, DLogs); if (b.Level >= 3) Dec(c, b.X + 1, b.Y + 1, DLogs); break;
                case Tl.Warehouse: if (b.Level >= 2) Dec(c, b.X + 1, b.Y + 1, DCrates); if (b.Level >= 4) Dec(c, b.X - 1, b.Y + 1, DCrates); break;
                case Tl.Forge: Dec(c, b.X - 1, b.Y + 1, DLogs); if (b.Level >= 3) Dec(c, b.X + 1, b.Y + 1, DCrates); break;
                case Tl.Mine: if (b.Level >= 2) Dec(c, b.X + 1, b.Y + 1, DCrates); break;
                case Tl.House:
                    if (b.Level >= 2) { Dec(c, b.X - 1, b.Y + 1, ((b.X + b.Y) & 1) == 0 ? DFlowerR : DFlowerB); Dec(c, b.X + 1, b.Y + 1, ((b.X + b.Y) & 1) == 0 ? DFlowerB : DFlowerR); }
                    if (b.Level >= 3) { Dec(c, b.X + 2, b.Y, DBench); Dec(c, b.X - 2, b.Y, DPost); }
                    break;
            }
        }
        // убежище у маяка: костёр, пока кому-то не хватает дома; фонари вдоль дороги по мере зажжённых фонарей
        var sp = W.Spawn;
        if (Villagers.Exists(v => v.Home < 0)) Dec(c, sp.X + 3, sp.Y + 2, DFire + (int)(Time * 4) % 2);
        if (LitCount >= 2) { Dec(c, sp.X - 3, sp.Y + 3, DPost); Dec(c, sp.X + 4, sp.Y + 3, DPost); }
        if (LitCount >= 4) { Dec(c, sp.X - 3, sp.Y - 2, DPost); Dec(c, sp.X + 4, sp.Y - 2, DPost); }
    }

    float decorFx;

    /// <summary>Дым из работающей кузницы и из жилых домов; искры кузницы идут от работника (WorkFx). Только рядом с игроком.</summary>
    void UpdateDecorFx(float dt)
    {
        decorFx -= dt; if (decorFx > 0) return; decorFx = .45f;
        foreach (var b in Buildings)
        {
            if (b.Pos.DistanceTo(PPos) > 220) continue;
            bool smoke = (b.Type == Tl.Forge && b.Status == 0 && !b.Off) || (b.Type == Tl.House && b.Level >= 2 && HomeCount(b) > 0);
            if (smoke) Parts.Add(new Particle { P = b.Pos + new Vector2(b.Type == Tl.Forge ? 4 : 3, -10), V = new Vector2(rng.RandfRange(-3, 5), -rng.RandfRange(8, 14)), Life = 1.3f, Max = 1.3f, C = new Color(.7f, .72f, .78f, .8f), Size = 2f });
        }
    }
}
