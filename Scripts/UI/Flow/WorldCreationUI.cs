using Godot;
using System;

namespace Lamplighter;

/// <summary>Создание мира: название, сложность и свёрнутые дополнительные настройки (seed). Пустой seed выбирается случайно. После создания мир сразу запускается с выбранным героем.</summary>
public partial class WorldCreationUI : FlowScreen
{
    static readonly string[] Names = { "Тёмный лес", "Новое начало", "Мир друзей", "Долина фонарей", "Ночная застава", "Пепельный край", "Огни севера", "Тихая гавань" };
    static readonly RandomNumberGenerator Rng = new();
    LineEdit name, seed; int difficulty; Button bNormal, bHard, bMore; Control adv; PanelContainer panel; bool open;

    public override void _Ready()
    {
        Rng.Randomize();
        BindNotice(GetNode<Label>("Notice"));
        panel = GetNode<PanelContainer>("Panel");
        var content = panel.GetNode<VBoxContainer>("Margin/Content");
        var title = content.GetNode<Label>("Title");
        title.Text = Loc.T(title.Text);
        name = content.GetNode<LineEdit>("Name");
        name.Text = Loc.T(Names[Rng.RandiRange(0, Names.Length - 1)]);
        name.PlaceholderText = Loc.T(name.PlaceholderText);
        name.TextSubmitted += _ => Create();

        bNormal = Bind(content, "Difficulty/Normal", () => SetDiff(0));
        bHard = Bind(content, "Difficulty/Hard", () => SetDiff(1));
        bNormal.TooltipText = Loc.T(bNormal.TooltipText);
        bHard.TooltipText = Loc.T(bHard.TooltipText);
        SetDiff(0, true);
        bMore = Bind(content, "More", ToggleMore);
        moreCaption = bMore.Text;
        adv = content.GetNode<Control>("Advanced");
        seed = adv.GetNode<LineEdit>("Seed");
        seed.PlaceholderText = Loc.T(seed.PlaceholderText);
        seed.TextSubmitted += _ => Create();
        Bind(adv, "Roll", () => { seed.Text = NewSeed().ToString(); Px.Pop(seed, .04f); });
        Bind(content, "Actions/Back", Back);
        Bind(content, "Actions/Create", Create);
    }

    static Button Bind(Control parent, string path, Action action)
    {
        var button = parent.GetNode<Button>(path);
        button.Text = Loc.T(button.Text);
        button.Pressed += () => { Sfx.Play("tick", -3, 1.3f); action(); };
        return button;
    }

    string moreCaption;

    void ToggleMore()
    {
        open = !open;
        adv.Visible = open;
        bMore.Text = open ? Loc.T("- Дополнительные настройки") : moreCaption;
        panel.CallDeferred(Control.MethodName.ResetSize);
    }

    static int NewSeed() => (int)(Rng.Randi() % 1000000);

    /// <summary>Число используется как есть, слово даёт устойчивый seed (одно и то же слово — один и тот же мир). Пустая строка — 0 (вызывающий выбирает случайный).</summary>
    public static int ParseSeed(string s)
    {
        s = (s ?? "").Trim(); if (s == "") return 0;
        if (int.TryParse(s, out var n) && n >= 0) return n % 2000000000;
        int h = 17; foreach (var ch in s) h = unchecked(h * 31 + ch); return Math.Abs(h % 2000000000);
    }

    void SetDiff(int d, bool quiet = false)
    {
        difficulty = d; bNormal.ThemeTypeVariation = d == 0 ? "ButtonSelected" : ""; bHard.ThemeTypeVariation = d == 1 ? "ButtonSelected" : "";
        if (!quiet) Sfx.Play("tick", -3, 1.3f);
    }

    bool creating;
    void Create()
    {
        if (creating) return; var n = SaveManager.CleanName(name.Text, ""); if (n == "") { Sfx.Play("error"); Px.Shake(name); Toast("Введите название мира"); return; }
        string st = seed.Text.Trim(); int s = st == "" ? NewSeed() : ParseSeed(st);
        creating = true; var w = SaveManager.CreateWorld(n, s, difficulty);
        Flow.WorldId = w.Id; Flow.PlayWorld(w.Id);
    }

    public override bool OnKey(Key k)
    {
        if (k == Key.Escape) { Back(); return true; }
        if (k == Key.Enter || k == Key.KpEnter) { Create(); return true; }
        if (k == Key.Left || k == Key.A) { SetDiff(0); return true; }
        if (k == Key.Right || k == Key.D) { SetDiff(1); return true; }
        return false;
    }

    public override void Back() { if (SaveManager.ListWorlds().Count == 0) { Flow.ShowCharacters(null); return; } Flow.ShowWorlds(null); }
}
