using Godot;
using System;
using System.Linq;

namespace Lamplighter;

/// <summary>Начало игры и ресурсы (LAMP_STAGE=res, одиночная игра): нет личного света до «Искры света», цели обучения, эффект изучения, выбрасывание и подбор стопок, склад, меню по ПКМ. Скриншоты по шагам.</summary>
public partial class Game
{
    void BuildResScript()
    {
        int fails = 0; void Check(bool ok, string what) { if (!ok) fails++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {what}"); }
        S(.3f, () =>
        {
            Menu = Mn.None; noHeal = true; Hp = MaxHp; Invuln = 99f; Enemies.Clear(); TestNoSpawn = true; Notify.History.Clear();
            SkillLv[(int)Tech.Spark] = 0; for (int i = 0; i < It.Count; i++) Inv[i] = 0; Ann = 0; RecomputeEffects(); lastTutorial = "";
            Check(!HasHandLamp && PlayerLightR == 0f, "start: a new hero has no personal light (radius 0)");
            Check(IsLit(PPos), "start: the starting clearing is lit by the Central Beacon, not by the hero");
            var nd = World.StartNeeds(); var hv = W.StartCounts(); Check(hv.trees >= nd.trees && hv.stones >= nd.stones && hv.copper >= nd.copper, $"start: guaranteed resources in the clearing ({hv.trees}/{nd.trees} trees, {hv.stones}/{nd.stones} stones, {hv.copper}/{nd.copper} copper)");
        });
        S(.6f, () => { Shot("res1_start_dark"); Check(Goals.Count > 0 && Goals[0].Title == "Собрать первые ресурсы", $"tutorial: the first goal is collecting resources ({(Goals.Count > 0 ? Goals[0].Title : "-")})"); Check(Notify.History.Exists(m => m.Text.Contains("Цель")), "tutorial: the current goal is announced in the chat"); });
        S(2.5f, () => { foreach (var c in LevelCost((int)Tech.Spark, 1)) Inv[c[0]] = c[1]; });
        S(2.5f, () => { Check(Goals.Count > 0 && Goals[0].Title == "Открыть навыки [T]", $"tutorial: next goal is opening the skills ({(Goals.Count > 0 ? Goals[0].Title : "-")})"); OpenTech(true); });
        S(2.5f, () => { Check(Goals.Count > 0 && Goals[0].Title == "Изучить «Искру света»", $"tutorial: next goal is learning the Spark ({(Goals.Count > 0 ? Goals[0].Title : "-")})"); OpenTech(false); Check(LearnTech((int)Tech.Spark), "spark: learned for wood and stone only"); });
        S(2.5f, () => Shot("res2_spark_start"));
        S(2.5f, () =>
        {
            Check(HasHandLamp && PlayerLightR > 20f && Notify.History.Exists(m => m.Text.Contains("Искра света")), $"spark: the light circle appeared ({PlayerLightR:F0}) and the chat says so");
            Shot("res3_spark_lit"); Check(Goals.Count > 0 && Goals[0].Title == "Исследовать Тьму", $"tutorial: next goal is exploring the Darkness ({(Goals.Count > 0 ? Goals[0].Title : "-")})");
        });
        // выбрасывание, подбор, слияние
        S(.2f, () =>
        {
            Notify.History.Clear(); Inv[It.Wood] = 50; Drops.Clear(); dropClock = 100;
            Check(!TryDrop(It.Wood, 999) && Inv[It.Wood] == 50, "drop: more than you have is refused and nothing is lost");
            Check(TryDrop(It.Wood, 20) && Inv[It.Wood] == 30 && Drops.Count == 1 && Drops[0].Count == 20, "drop: 20 wood left the inventory and lay on the ground");
            TryDrop(It.Wood, 5); Check(Drops.Count == 1 && Drops[0].Count == 25 && Inv[It.Wood] == 25, "drop: the same resource merges into one stack");
            UpdateDrops(.1f); Check(Drops.Count == 1 && Inv[It.Wood] == 25, "drop: the dropper does not pick the stack up at once");
            dropClock += 3f; UpdateDrops(.1f); Check(Drops.Count == 0 && Inv[It.Wood] == 50, "drop: after the delay the stack is picked up exactly once");
            UpdateDrops(.1f); Check(Inv[It.Wood] == 50, "drop: a second pass gives nothing more");
        });
        // склад
        S(.2f, () =>
        {
            Check(!TryChest(true, It.Wood, 5) && Inv[It.Wood] == 50, "chest: no warehouse near — refused and nothing is lost");
            Vector2I t = default; for (int r = 2; r < 8 && t == default; r++) for (int dx = -r; dx <= r && t == default; dx++) if (W.Get(W.Spawn.X + dx, W.Spawn.Y + r) == Tl.None && W.Get(W.Spawn.X + dx + 1, W.Spawn.Y + r) == Tl.None) t = new Vector2I(W.Spawn.X + dx, W.Spawn.Y + r);
            CreateBuilding(0, t); Stock[It.Wood] = 0;
            Check(TryChest(true, It.Wood, 10) && Inv[It.Wood] == 40 && Stock[It.Wood] == 10, "chest: deposit moves 10 wood to the shared storage");
            Check(TryChest(false, It.Wood, 4) && Inv[It.Wood] == 44 && Stock[It.Wood] == 6, "chest: withdrawal takes 4 back");
            Check(!TrySend(2, It.Wood, 1) && Inv[It.Wood] == 44, "transfer: with nobody to send to nothing happens");
            Inv[It.Wood] = 100; Stock[It.Wood] = StockCap - 3; TryChest(true, It.Wood, 10); Check(Stock[It.Wood] == StockCap && Inv[It.Wood] == 97, "chest: when full the extra returns to the sender");
            Stock[It.Wood] = 0; Inv[It.Wood] = 60; Inv[It.Stone] = 5;
        });
        // меню по ПКМ
        S(.2f, () => { OpenWin(Win.Inventory); });
        S(.5f, () => { Check(ResUi.HasActions(It.Wood), "menu: the resource has actions"); ResUi.Open(It.Wood); });
        S(.4f, () => { Check(ResUi.Active, "menu: opens on a resource"); Shot("res4_menu"); ResUi.DialogForTest(1); });
        S(.4f, () => { Shot("res5_drop_dialog"); ResUi.Close(); CloseWindows(); Check(!ResUi.Active, "menu: closes"); Log("res done"); GD.Print("[check] SUMMARY failures=" + fails); GD.Print("RES DONE"); });
        // анимация добычи: замах вверх, удар вниз, возврат (три фазы цикла)
        foreach (var (name, ph) in new[] { ("res6_swing_windup", .27f), ("res6_swing_strike", .49f), ("res6_swing_return", .85f) })
        {
            string nm = name; float phase = ph;
            S(.3f, () => { Tp(TileCenter(W.Spawn.X, W.Spawn.Y)); TestKeepMine = true; TestAim(new Vector2(40, 0)); Facing = 1; MineTile = new Vector2I(W.Spawn.X + 2, W.Spawn.Y); MineProg = .1f; MineNeed = 1f; AtkCd = 0; Swing = 5f; Invuln = 0; });
            S(.02f, () => { Swing = 5f; MineProg = .1f; Time = Mathf.Floor(Time * 3f) / 3f + phase / 3f; });
            S(.01f, () => Shot(nm));
        }
    }
}
