using Godot;
using System;

namespace Lamplighter;

/// <summary>Снимки боевых эффектов (LAMP_STAGE=fx): враги разных стилей атакуют стоящего игрока, затем получают удары и умирают.</summary>
public partial class Game
{
    void BuildFxScript()
    {
        S(.3f, () => { Menu = Mn.None; Tp(PPos); noHeal = true; Hp = MaxHp; Invuln = 99f; TestAim(new Vector2(0, 90));
            SpawnEnemy(PPos + new Vector2(-44, -10), EK.Wraith); SpawnEnemy(PPos + new Vector2(46, -12), EK.Spitter); SpawnEnemy(PPos + new Vector2(-30, 38), EK.Shade); SpawnEnemy(PPos + new Vector2(34, 36), EK.Imp); SpawnEnemy(PPos + new Vector2(0, -44), EK.Golem);
            foreach (var e in Enemies) e.Cd = 0.2f; });
        for (int i = 0; i < 24; i++) { int n = i; S(.08f, () => Shot($"fx_{n:00}")); }
        S(.1f, () => { foreach (var e in Enemies) Hurt(e, 3f, (e.Pos - PPos).Normalized(), true); });
        S(.03f, () => Shot("fx_hit1")); S(.05f, () => Shot("fx_hit2"));
        S(.2f, () => { foreach (var e in Enemies) { e.Hp = 0; } });
        S(.03f, () => Shot("fx_die1")); S(.1f, () => Shot("fx_die2")); S(.15f, () => Shot("fx_die3"));
        S(.2f, () => GD.Print("[check] FX SCREENSHOTS DONE"));
    }

    /// <summary>Витрина эффектов (LAMP_STAGE=fxshow): разрушение камня, добыча, удар по врагу, сильный удар, гибель, рывок, волна света, уровень, зажжённый фонарь, победа.
    /// Каждый эффект запускается рядом с героем и снимается трижды по ходу анимации, без интерфейса.</summary>
    void BuildFxShowScript()
    {
        Vector2 at(float dx, float dy) => PPos + new Vector2(dx, dy);
        var shows = new (string name, Action run)[]
        {
            ("break", () => { Fx("tile_break", at(-40, 0), new Color("#aeb2c2")); Fx("tile_break", at(40, 0), new Color("#f08a3c")); }),
            ("mine", () => { for (int i = 0; i < 3; i++) Fx("mine_hit", at(-30 + i * 30, -10), new Color("#d9dce6")); }),
            ("hit", () => { foreach (var e in new[] { SpawnEnemy(at(-34, -6), EK.Shade), SpawnEnemy(at(34, -6), EK.Spitter) }) { e.Cd = 99f; e.Age = 9f; e.Hp = e.MaxHp = 1000f; Hurt(e, 20f, (e.Pos - PPos).Normalized(), true); } }),
            ("crit", () => { foreach (var e in new[] { SpawnEnemy(at(-34, -6), EK.Imp), SpawnEnemy(at(34, -6), EK.IceWolf) }) { e.Cd = 99f; e.Age = 9f; e.Hp = e.MaxHp = 50f; Hurt(e, 20f, (e.Pos - PPos).Normalized(), true); } }),
            ("ranged", () => { var e = SpawnEnemy(at(40, -8), EK.Golem); e.Cd = 99f; e.Age = 9f; e.Hp = e.MaxHp = 1000f; Hurt(e, 20f, Vector2.Right, false); }),
            ("death", () => { var e = SpawnEnemy(at(0, -20), EK.Imp); e.Hp = 0; OnEnemyDeath(e); Enemies.Remove(e); }),
            ("dash", () => OnDash(PPos, Vector2.Right)),
            ("pulse", () => OnPulse(PPos)),
            ("level", () => Fx("level_up", PPos)),
            ("lamp", () => { OnLampLit(at(-30, 0), 0); OnLampLit(at(30, 0), 2); }),
            ("victory", () => VictoryFx(at(0, -20), new Color("#ffd86b"))),
        };
        S(.4f, () => { Menu = Mn.None; Tp(PPos); TestAim(new Vector2(0, 90)); noHeal = true; Invuln = 99f; hud.Visible = false; GameHudNode.Visible = false; Enemies.Clear(); });
        foreach (var sh in shows)
        {
            var s = sh;
            S(.02f, () => { Parts.Clear(); Enemies.Clear(); Zones.Clear(); hitSplats.Clear(); Texts.Clear(); s.run(); });
            S(.06f, () => Shot($"fxshow_{s.name}_0")); S(.12f, () => Shot($"fxshow_{s.name}_1")); S(.25f, () => Shot($"fxshow_{s.name}_2"));
        }
        S(.1f, () => GD.Print("[check] FXSHOW DONE"));
    }

    /// <summary>Снимки встряски блоков от удара (LAMP_STAGE=shake): ряд камней, руд и объектов рядом с героем трясётся с тройной силой, десять кадров подряд без интерфейса.
    /// По снимкам видно, что спрайт наклоняется целиком и не срезается краем своей клетки.</summary>
    void BuildShakeScript()
    {
        var kinds = new[] { Tl.Stone, Tl.Copper, Tl.Iron, Tl.Lumen, Tl.Obsidian, Tl.Chest };
        var t0 = Vector2I.Zero;
        S(.4f, () =>
        {
            Menu = Mn.None; Tp(PPos); TestAim(new Vector2(0, 90)); hud.Visible = false; GameHudNode.Visible = false; t0 = ToTile(PPos) + new Vector2I(-5, -3);
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= kinds.Length * 2; dx++) SetObj(t0.X + dx, t0.Y + dy, Tl.None);
            for (int i = 0; i < kinds.Length; i++) SetObj(t0.X + i * 2, t0.Y, kinds[i]);
        });
        S(.05f, () => Shot("shake_rest"));
        S(.02f, () => { for (int i = 0; i < kinds.Length; i++) TileShake(t0.X + i * 2, t0.Y, 3f); });
        for (int i = 0; i < 10; i++) { int n = i; S(.025f, () => Shot($"shake_{n}")); }
        S(.1f, () => GD.Print("[check] SHAKE DONE"));
    }

    /// <summary>Снимки качания травы (LAMP_STAGE=veg): восемь кадров подряд без интерфейса.</summary>
    void BuildVegScript()
    {
        S(.3f, () => { Menu = Mn.None; Tp(PPos); TestAim(new Vector2(0, 90)); hud.Visible = false; GameHudNode.Visible = false; });
        for (int i = 0; i < 8; i++) { int n = i; S(.15f, () => Shot($"veg_{n}")); }
        S(.1f, () => GD.Print("[check] VEG DONE"));
    }
}
