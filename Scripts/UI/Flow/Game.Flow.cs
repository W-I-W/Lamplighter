using Godot;

namespace Lamplighter;

/// <summary>Связь меню с экранами выбора героя и мира (FlowUI).</summary>
public partial class Game
{
    FlowUI flow;

    public void OpenFlow(FlowKind kind, string transport = null, string target = null)
    {
        flow?.QueueFree(); Menu = Mn.Flow; MenuCur = 0; RebindAction = null;
        flow = FlowUI.Open(this, uiRoot, kind, transport, target); Sfx.Play("tick", -4, 1.1f);
    }

    /// <summary>Экран выбора закрыт без запуска: возврат в меню, откуда он был открыт.</summary>
    public void FlowClosed(Mn back)
    {
        flow = null; MenuBack = Mn.Main; OpenMenu(back);
        if (back == Mn.Multi) RefreshNetMenu();
    }

    /// <summary>Автотесты: одиночная игра со своим героем и миром в папке теста. С LAMP_LOAD=1 существующие файлы загружаются, иначе создаются заново.</summary>
    void TestSession()
    {
        bool load = OS.GetEnvironment("LAMP_LOAD") != "";
        var c = load ? SaveManager.LoadCharacter("testchar") : null;
        if (c == null) { c = new CharacterSave { Id = "testchar", Name = "Tester", Look = DefaultLook(), Created = 1 }; SaveManager.SaveCharacter(c); }
        var w = load ? SaveManager.LoadWorld("testworld") : null;
        if (w == null) { w = new WorldSave { Id = "testworld", Name = "Test", Seed = 4242, GenV = World.CurrentGen, Created = 1 }; SaveManager.SaveWorld(w); }
        GameSession.Begin(SessionMode.Solo, c.Id, w.Id);
    }
}
