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
}
