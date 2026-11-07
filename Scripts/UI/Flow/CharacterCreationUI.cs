using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Создание героя: имя, цвет кожи, причёска, цвет волос, два цвета одежды и фасон. Предпросмотр меняется сразу; внешность не влияет на характеристики.</summary>
public partial class CharacterCreationUI : FlowScreen
{
    public bool First;
    static readonly string[][] Names = { new[] { "Лис", "Волк", "Сова", "Ёж", "Барсук", "Филин", "Рысь", "Ворон", "Бобр", "Олень", "Медведь", "Сокол", "Тигр", "Псина" },
        new[] { "Барсик", "Мурка", "Пушок", "Рыжик", "Снежок", "Васька", "Багира", "Тигруля", "Уголёк", "Мурзик", "Симба", "Персик" },
        new[] { "Шарик", "Рекс", "Бобик", "Тузик", "Дружок", "Жучка", "Лайка", "Барон", "Граф", "Бублик", "Найда", "Пират" } };      // по виду героя: лесные звери, кошачьи и собачьи клички
    static string RandomName(int species) { var l = Names[Math.Clamp(species, 0, Names.Length - 1)]; return Loc.T(l[Rng.RandiRange(0, l.Length - 1)]); }
    string autoName = "";                                                     // имя выбрано случайно: при смене вида меняется на кличку нового вида
    readonly Look look = Game.DefaultLook(); LineEdit name; LookView view; Label hairName, varName, specName; Control stage;
    readonly List<Swatch> skin = new(), hairC = new(), cl1 = new(), cl2 = new(); readonly List<Control> hairRows = new(); bool walking, flip;
    static readonly RandomNumberGenerator Rng = new();

    public override void _Ready()
    {
        Rng.Randomize();
        BindNotice(GetNode<Label>("Notice"));
        var title = GetNode<Label>("Title");
        title.Text = Loc.T(First ? "Ваш первый герой" : title.Text);
        GetNode<Label>("Subtitle").Text = Flow.ModeLine;
        var hint = GetNode<Label>("Preview/Content/Hint");
        hint.Text = Loc.T(hint.Text);
        var stageHolder = GetNode<Control>("Preview/Content/Stage");
        stage = new StageView { Size = stageHolder.Size, Zoom = 8, FloorY = 134 };
        stageHolder.AddChild(stage);
        view = new LookView { Look = look, Zoom = 8, Position = new Vector2(55, 6) };
        stage.AddChild(view);
        var walk = GetNode<Button>("Preview/Content/Actions/Walk");
        string idleCaption = Loc.T(walk.Text);
        BindButton("Preview/Content/Actions/Walk", () =>
        {
            walking = !walking;
            view.Walking = walking;
            walk.Text = walking ? Loc.T("Ходьба") : idleCaption;
            Px.Pop(view, .06f);
        });
        BindButton("Preview/Content/Actions/Flip", () => { flip = !flip; view.Flip = flip; Px.Pop(view, .06f); });
        BindButton("Preview/Content/Actions/Random", Randomize);

        const string form = "Appearance/Content/Form";
        foreach (var row in GetNode<VBoxContainer>(form).GetChildren())
        {
            var caption = row.GetNode<Label>("Caption");
            caption.Text = Loc.T(caption.Text);
        }
        name = GetNode<LineEdit>(form + "/Name/Options/Input");
        name.Text = autoName = RandomName(look.Species);
        name.PlaceholderText = Loc.T(name.PlaceholderText);
        BindButton(form + "/Name/Options/Roll", () => { name.Text = autoName = RandomName(look.Species); Px.Pop(name, .04f); });
        specName = BindCycler(form + "/Species", d =>
        {
            look.Species = (look.Species + d + Look.SpeciesN) % Look.SpeciesN;
            if (name.Text == autoName) name.Text = autoName = RandomName(look.Species);
        });
        AddSwatches(form + "/Skin", Look.SkinCols, skin, i => look.Skin = i);
        hairName = BindCycler(form + "/Hair", d => look.Hair = (look.Hair + d + Look.HairN) % Look.HairN);
        hairRows.Add(GetNode<Control>(form + "/Hair"));
        AddSwatches(form + "/HairColor", Look.HairCols, hairC, i => look.HairCol = i);
        hairRows.Add(GetNode<Control>(form + "/HairColor"));
        AddSwatches(form + "/Clothing", Look.ClothCols, cl1, i => look.Cloth1 = i);
        AddSwatches(form + "/Accent", Look.ClothCols, cl2, i => look.Cloth2 = i);
        varName = BindCycler(form + "/Variant", d => look.Variant = (look.Variant + d + Look.VariantN) % Look.VariantN);
        Refresh();

        BindButton("Back", Back);
        BindButton("Actions/Create", Create);
    }

    void AddSwatches(string path, Color[] colors, List<Swatch> store, Action<int> set)
    {
        var options = GetNode<HBoxContainer>(path + "/Options");
        for (int i = 0; i < colors.Length; i++)
        {
            int index = i;
            var swatch = new Swatch
            {
                Col = colors[i],
                Clicked = () => { set(index); Refresh(); Sfx.Play("tick", -5, 1.5f); Px.Pop(view, .05f); }
            };
            options.AddChild(swatch);
            store.Add(swatch);
        }
    }

    Label BindCycler(string path, Action<int> step)
    {
        path += "/Options";
        void Cycle(int direction) { step(direction); Refresh(); Px.Pop(view, .05f); }
        BindButton(path + "/Previous", () => Cycle(-1));
        BindButton(path + "/Next", () => Cycle(1));
        return GetNode<Label>(path + "/Value");
    }

    void Refresh()
    {
        look.Clamp(); view.Look = look;
        for (int i = 0; i < skin.Count; i++) skin[i].Selected = i == look.Skin; for (int i = 0; i < hairC.Count; i++) hairC[i].Selected = i == look.HairCol;
        for (int i = 0; i < cl1.Count; i++) cl1[i].Selected = i == look.Cloth1; for (int i = 0; i < cl2.Count; i++) cl2[i].Selected = i == look.Cloth2;
        hairName.Text = Loc.T(Look.HairNames[look.Hair]); varName.Text = Loc.T(Look.VariantNames[look.Variant]); specName.Text = Loc.T(Look.SpeciesNames[look.Species]);
        for (int i = 0; i < skin.Count; i++) skin[i].Col = look.Species == 0 ? Look.SkinCols[i] : Look.FurCols[i];          // у кота и собаки вместо кожи — шерсть
        foreach (var c in hairRows) c.Visible = look.Species == 0;
        foreach (var s in skin) s.QueueRedraw(); foreach (var s in hairC) s.QueueRedraw(); foreach (var s in cl1) s.QueueRedraw(); foreach (var s in cl2) s.QueueRedraw();
    }

    void Randomize() { var r = Look.Random(Rng); look.Skin = r.Skin; look.Hair = r.Hair; look.HairCol = r.HairCol; look.Cloth1 = r.Cloth1; look.Cloth2 = r.Cloth2; look.Variant = r.Variant; Refresh(); Px.Pop(view, .1f); Sfx.Play("tick", -3, 1.6f); }

    bool creating;
    void Create()
    {
        if (creating) return; var n = SaveManager.CleanName(name.Text, ""); if (n == "") { Sfx.Play("error"); Px.Shake(name); name.GrabFocus(); return; }
        creating = true; var c = SaveManager.CreateCharacter(n, look);
        Sfx.Play("upgrade", -3, 1.3f); view.Walking = false; var t = CreateTween(); var p0 = view.Position;
        t.TweenProperty(view, "position:y", p0.Y - 18, .14f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out); t.TweenProperty(view, "position:y", p0.Y, .14f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        t.TweenInterval(.15f); t.TweenCallback(Callable.From(() => Flow.ShowCharacters(c.Id)));
    }

    public override bool OnKey(Key k)
    {
        if (k == Key.Escape) { Back(); return true; }
        if (k == Key.Enter || k == Key.KpEnter) { Create(); return true; }
        return false;
    }


    public override void Back()
    {
        if (SaveManager.ListCharacters().Count == 0) { Flow.Leave(); return; }          // героев нет: вернуться можно только в меню
        Flow.ShowCharacters(null);
    }
}
