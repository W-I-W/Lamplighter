using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter;

/// <summary>Проверки героев, миров и экранов выбора.
/// flow — скриншоты всех экранов (LAMP_STAGE=flow, разные --resolution), sessions — сквозные сценарии между перезагрузками сцены (герой в разных мирах, разные герои в одном мире, удаление).</summary>
public partial class Game
{
    static int sessionsPhase; static string sB, sY;                      // состояние между перезагрузками сцены в одном процессе

    void BuildFlowScript()
    {
        string cA = null, cB = null;
        S(.3f, () =>
        {
            var a = SaveManager.CreateCharacter("Алекс", new Look { Skin = 2, Hair = 2, HairCol = 3, Cloth1 = 1, Cloth2 = 0, Variant = 1 }); a.Level = 9; a.MaxHp = 130; a.Kills = 240; a.PlaySecs = 7300; a.Xp = 120; a.WId = new[] { "sword1", "bow1" }; a.SkK = new[] { "Lens", "Dash" }; a.SkV = new[] { 3, 1 }; a.Equipped = new[] { "r_embersp" }; a.CurTool = "pickaxe"; SaveManager.SaveCharacter(a); cA = a.Id;
            var b = SaveManager.CreateCharacter("Марк", new Look { Skin = 5, Hair = 1, HairCol = 7, Cloth1 = 6, Cloth2 = 8, Variant = 3 }); b.SkK = new[] { "Spark" }; b.SkV = new[] { 1 }; SaveManager.SaveCharacter(b); cB = b.Id;
            var w1 = SaveManager.CreateWorld("Тёмный лес", 12345, 0); var w2 = SaveManager.CreateWorld("Новое начало", 777, 1); SaveManager.CreateWorld("Мир друзей", 42, 0);
            Check("flow: lists created", SaveManager.ListCharacters().Count == 3 && SaveManager.ListWorlds().Count == 4, $"chars={SaveManager.ListCharacters().Count} worlds={SaveManager.ListWorlds().Count}");
        });
        S(.9f, () => OpenFlow(FlowKind.Solo));
        S(.1f, () => Shot("flow1_characters"));
        S(.9f, () => flow.ShowCreateCharacter(false));
        S(.1f, () => Shot("flow2_create_character"));
        int before = 0;
        S(.2f, () => { before = SaveManager.ListCharacters().Count; Check("flow: Create hero button found", ClickButton("Создать героя")); });
        S(1.2f, () => Check("flow: a real mouse click on Create hero creates a character", SaveManager.ListCharacters().Count == before + 1, $"{before}->{SaveManager.ListCharacters().Count}"));
        S(.9f, () => { flow.CharId = cA; flow.ShowWorlds(null); });
        S(.1f, () => Shot("flow3_worlds"));
        S(1.8f, () => flow.ShowCreateWorld());
        S(.1f, () => Shot("flow4_create_world"));
        S(.3f, () => { Check("flow: world screen has the compact panel", flow.Cur is WorldCreationUI); Check("flow: click Advanced opens the seed row", ClickButton("+ Дополнительные настройки")); });
        S(.4f, () => { Check("flow: click Hard selects it", ClickButton("Сложная")); });
        S(.3f, () => Shot("flow4b_create_world_advanced"));
        S(.2f, () => { ClickButton("- Дополнительные настройки"); });
        S(.5f, () => { flow.QueueFree(); OpenFlow(FlowKind.Host, "steam"); });
        S(.9f, () => { flow.CharId = cA; flow.ShowWorlds(null); });
        S(.1f, () => Shot("flow5_host_worlds"));
        S(.5f, () => { flow.QueueFree(); OpenFlow(FlowKind.Guest, "steam", "109775240918889"); });
        S(.9f, () => Shot("flow6_guest_characters"));
        S(.1f, () => { flow.QueueFree(); OpenFlow(FlowKind.Solo); });
        S(.9f, () => Check("flow: click Delete opens dialog", ClickButton("Удалить")));
        S(.3f, () => Shot("flow7_delete_modal"));
        S(.1f, () =>
        {
            flow.QueueFree(); OpenFlow(FlowKind.Solo); flow.CharId = cA;
        });
        S(.9f, () => { flow.ShowCreateWorld(); });
        int n0 = 0;
        S(.15f, () => { n0 = SaveManager.ListWorlds().Count; ClickButton("Сложная"); Check("flow: Create world clicked with closed advanced settings", ClickButton("Создать мир")); });
        S(.05f, () =>
        {
            var made = SaveManager.ListWorlds(); Check("flow: the world was created with a generated seed and Hard difficulty", made.Count == n0 + 1 && made.Exists(w => w.Difficulty == 1 && w.Seed >= 0 && w.Id == flow.WorldId));
            Log("flow shots done"); GD.Print("[check] SUMMARY failures=" + checkFails); GD.Print("FLOW DONE");
        });
    }

    /// <summary>Настоящий щелчок мышью по кнопке экрана выбора (по тексту).</summary>
    bool ClickButton(string text)
    {
        text = Loc.T(text);
        foreach (var n in flow.FindChildren("*", "Button", true, false))
            if (n is Button b && b.Text == text && b.IsVisibleInTree())
            {
                var p = b.GetGlobalTransformWithCanvas() * (b.Size / 2); p *= (Vector2)GetWindow().Size / GetViewport().GetVisibleRect().Size;      // настоящее нажатие приходит в пикселях окна
                Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p });
                foreach (bool down in new[] { true, false }) Input.ParseInputEvent(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = down, ButtonMask = down ? MouseButtonMask.Left : 0 });
                return true;
            }
        return false;
    }

    /// <summary>Сквозной сценарий в одном процессе: каждая фаза начинается после перезагрузки сцены с новой парой герой/мир.</summary>
    void BuildSessionsScript()
    {
        int ph = sessionsPhase; GD.Print($"[sessions] phase {ph} char={GameSession.CharacterId} world={GameSession.WorldId} lit={LitCount} buildings={Buildings.Count} wood={Inv[It.Wood]} level={Level}");
        if (ph == 0)
        {
            S(.3f, () =>
            {
                Check("sessions: user:// root builds valid paths", SaveManager.DirOf("user://", "characters") == "user://characters" && SaveManager.DirOf("C:/x", "worlds") == "C:/x/worlds");
                Check("sessions: new character gets the look it was created with", PlayerLook.Skin == DefaultLook().Skin);
                var look = new Look { Skin = 4, Hair = 5, HairCol = 6, Cloth1 = 7, Cloth2 = 9, Variant = 2 };
                var b = SaveManager.CreateCharacter("Марк", look); sB = b.Id;
                var y = SaveManager.CreateWorld("Второй мир", 4242, 1); sY = y.Id;                     // тот же seed, что у первого мира
                Check("sessions: two worlds with one seed are different saves", y.Id != "testworld" && SaveManager.LoadWorld("testworld").Seed == y.Seed);
                Inv[It.Wood] = 77; Level = 7; GiveWeapon("sword1", false); SkillLv[0] = 2; PlayerLook = new Look { Skin = 3, Hair = 2, HairCol = 1, Cloth1 = 2, Cloth2 = 3, Variant = 1 };
                var anc = W.Ancients.Find(a => a.Region == 0 && !a.Boss && a != W.Ancients[0]); anc.Lit = true; anc.Anim = 1;
                AddB(Tl.Warehouse, 2, 0, 2); Stock[It.Stone] = 33; Hot[0] = "tool:pickaxe"; Save();
                var cs = SaveManager.LoadCharacter("testchar"); var ws = SaveManager.LoadWorld("testworld");
                Check("sessions: character file keeps items, weapon, level, skills, look", cs.Inv[It.Wood] == 77 && cs.Level == 7 && cs.WId.Length == 1 && cs.SkK.Length > 0 && cs.Look.Hair == 2 && cs.Look.Variant == 1);
                Check("sessions: world file keeps buildings, lamps, stock and not the inventory", ws.BT.Length == 1 && ws.Lit.Count(x => x) >= 1 && ws.Stock[It.Stone] == 33 && ws.Difficulty == 0);
                Check("sessions: files are in characters/ and worlds/", System.IO.File.Exists(CharPath) && System.IO.File.Exists(SavePath));
                Check("sessions: world thumbnail exists", System.IO.File.Exists(System.IO.Path.ChangeExtension(SavePath, ".png")));
                sessionsPhase = 1; StartSolo("testchar", sY);                                       // тот же герой — другой мир
            });
        }
        else if (ph == 1)
        {
            S(.3f, () =>
            {
                Check("sessions: one character in another world keeps inventory, weapon, level, skills", Inv[It.Wood] == 77 && Level == 7 && Owned.Count == 1 && SkillLv[0] == 2);
                Check("sessions: appearance is saved", PlayerLook.Skin == 3 && PlayerLook.Hair == 2 && PlayerLook.HairCol == 1 && PlayerLook.Cloth1 == 2 && PlayerLook.Cloth2 == 3 && PlayerLook.Variant == 1);
                Check("sessions: the other world has no buildings, lit lamps or stock of the first", Buildings.Count == 0 && LitCount == 0 && Stock[It.Stone] == 0);
                Check("sessions: world difficulty is applied (hard)", Diff == 1 && EnemyMuls(EK.Shade, PPos).hp > 1.5f * 1.3f);
                Inv[It.Wood] += 5; AddB(Tl.Sawmill, 3, 0, 1); Save(); sessionsPhase = 2; StartSolo(sB, "testworld");  // другой герой — первый мир
            });
        }
        else if (ph == 2)
        {
            S(.3f, () =>
            {
                Check("sessions: another character in the same world sees its buildings and lamps", Buildings.Count == 1 && Buildings[0].Type == Tl.Warehouse && LitCount >= 1 && Stock[It.Stone] == 33);
                Check("sessions: the other character is independent (no items, level 1)", Inv[It.Wood] == 0 && Level == 1 && Owned.Count == 0 && PlayerLook.Skin == 4 && PlayerLook.Hair == 5);
                Check("sessions: normal difficulty", Diff == 0);
                Inv[It.Copper] = 9; Save();
                var cs = SaveManager.LoadCharacter("testchar"); Check("sessions: first character not touched by the second", cs.Inv[It.Wood] == 82 && cs.Inv[It.Copper] == 0);
                sessionsPhase = 3; StartSolo("testchar", "testworld");
            });
        }
        else if (ph == 3)
        {
            S(.3f, () =>
            {
                Check("sessions: back in the first world: items and buildings intact, nothing duplicated", Inv[It.Wood] == 82 && Buildings.Count == 1 && Inv[It.Copper] == 0 && Owned.Count == 1);
                var w = SaveManager.LoadWorld("testworld"); var wi = SaveManager.ListWorlds().Find(x => x.Id == "testworld");
                Check("sessions: world list info", wi != null && wi.Seed == 4242 && wi.Lit >= 1);
                SaveManager.DeleteCharacter(sB);
                Check("sessions: deleting a character does not touch worlds", SaveManager.LoadWorld("testworld") != null && SaveManager.LoadWorld(sY) != null && SaveManager.LoadCharacter(sB) == null && SaveManager.LoadCharacter("testchar") != null);
                SaveManager.DeleteWorld(sY);
                Check("sessions: deleting a world does not touch characters", SaveManager.LoadWorld(sY) == null && SaveManager.LoadCharacter("testchar") != null && !System.IO.File.Exists(SaveManager.ThumbPath(sY).Replace("user://", "")));
                Check("sessions: lists after deletion", SaveManager.ListCharacters().Count == 1 && SaveManager.ListWorlds().Count == 1);
                Check("sessions: invalid session cannot start without character", !SaveManager.ValidId("../x") && SaveManager.LoadCharacter("nope") == null);
                GD.Print("[check] SUMMARY failures=" + checkFails); GD.Print("SESSIONS DONE");
            });
        }
    }
}
