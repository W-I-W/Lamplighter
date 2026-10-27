using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Тест эндгейма (LAMP_STAGE=end): врата разломов, волны, награды, легендарные улучшения, украшения, облики.</summary>
public partial class Game
{
    void BuildEndScript()
    {
        S(.3f, () =>
        {
            BossMask |= 31; BeaconLevel = 6; Finished = true; Dawn = 1; Level = 12; MaxHp = Hp = 200; ApplyBeaconRadius();
            foreach (var a in W.Ancients) { a.Lit = true; a.Anim = 1; }
            Inv[It.Stone] = 99; Inv[It.Silver] = 40; Inv[It.Coins] = 500; Inv[It.Crystal] = 20; Inv[It.Wood] = 50;
            riftGateChecked = false; UpdateEndgame(.01f);
            var g = RiftGateTile(); Log($"rift gate tile={g} obj={W.Get(g.X, g.Y)}");
            Tp(TileCenter(g.X, g.Y + 1));
        });
        S(.5f, () => { Cur = FindIx(); Log("gate ix: " + string.Join(" | ", Cur.Lines)); Cur.E?.Invoke(); Log($"window={Window}"); });
        S(.3f, () => Shot("e1_gate_menu"));
        S(.5f, () => { WindowKey(Key.Enter, false); Log($"rift on={RiftOn} tier={RiftTier} waves={RiftWaves} affix={AffixNames[RiftAffix[0]]}+{AffixNames[RiftAffix[1]]} pos={ToTile(PPos)} arena={W.HeartArena}"); });
        // опрос: убиваем врагов, выбираем временную реликвию, пока разлом не пройден
        for (int n = 0; n < 40; n++)
        {
            int nn = n;
            S(.5f, () =>
            {
                if (!RiftOn) return;
                if (RiftState == 1) { if (nn % 3 == 0) Shot("e3_offer"); WindowKey(Key.Key1, false); return; }
                if (Enemies.Count > 0 && nn % 4 == 0) Shot("e2_rift");
                foreach (var e in Enemies.ToArray()) if (e.Age > 0) e.Hp = 0;
            });
        }
        S(1.5f, () => { Log($"after clear: on={RiftOn} unlocked={RiftUnlocked} shards={Inv[It.RiftShard]} clears={RiftClears} cosm={CosmMask} pos={ToTile(PPos)}"); Shot("e4_win"); });
        S(.2f, () =>
        {
            Inv[It.RiftShard] = 100; Inv[It.AncCrystal] = 8; Inv[It.Gear] = 10; Inv[It.Lens] = 12; Inv[It.Iron] = 20; Inv[It.Frost] = 12; Inv[It.Ember] = 12;
            float d0 = PlayerDamage; for (int i = 0; i < Legends.Length; i++) BuyLegend(i);
            Log($"legends={LegendMask} dmg {d0:F1}->{PlayerDamage:F1} prod={ProdMul:F2} mine={N("MineSpd"):F2} speed={N("Speed"):F2}");
        });
        S(.2f, () => { CosmSel = 1; var f = TileCenter(W.Spawn.X + 3, W.Spawn.Y + 6); Tp(f); for (int dk = 0; dk < 3; dk++) { var d = FreeAdj(); TestAim(d); BuildDecor(dk); Tp(PPos + new Vector2(0, 30)); } Log($"decor={DecorCount} regions={DecorRegions} prod={ProdMul:F2}"); });
        S(.6f, () => { Shot("e5_decor"); aimOverride = null; Save(); });
    }
}
