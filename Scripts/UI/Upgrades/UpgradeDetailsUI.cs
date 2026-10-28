using Godot;
using System;
using System.Text;

namespace Lamplighter;

/// <summary>Правая панель окна улучшений: крупная иконка, название, короткое описание с выделенными числами, требование, стоимость с иконками и кнопка «Изучить».
/// Только показывает данные и сообщает о нажатии кнопки; логика изучения — в Game.LearnTech.</summary>
public partial class UpgradeDetailsUI : PanelContainer
{
    public event Action LearnPressed;

    static readonly Color Green = new("#9be07a"), Gold = new("#ffd23f"), Red = new("#f06a5e"), Grey = new("#8a96b0"), Cyan = new("#8ff0ff");
    TextureRect icon; Label nameL, stateL, reqTitle, costTitle; RichTextLabel desc, req, cost; Button learn;
    string lastKey = "";

    public override void _Ready()
    {
        icon = GetNode<TextureRect>("%Icon"); nameL = GetNode<Label>("%Name"); stateL = GetNode<Label>("%State"); reqTitle = GetNode<Label>("%ReqTitle"); costTitle = GetNode<Label>("%CostTitle");
        desc = GetNode<RichTextLabel>("%Desc"); req = GetNode<RichTextLabel>("%Req"); cost = GetNode<RichTextLabel>("%Cost"); learn = GetNode<Button>("%Learn");
        learn.Pressed += () => LearnPressed?.Invoke();
    }

    public Vector2 IconCentre => icon.GetGlobalRect().GetCenter();
    public Control IconNode => icon;

    static string Col(Color c, string t) => $"[color=#{c.ToHtml(false)}]{t}[/color]";

    /// <summary>Показывает улучшение. Перерисовывается только когда меняется содержимое (выбор, состояние, количество ресурсов).</summary>
    public void Show(Game g, int tech)
    {
        if (tech < 0) { lastKey = ""; return; }
        var t = Game.Techs[tech]; var st = g.StateOfTech(tech);
        var sb = new StringBuilder(); sb.Append(tech).Append('|').Append((int)st).Append('|').Append(Loc.Lang);
        if (st != TechState.Owned && !t.Reward) foreach (var c in t.Cost) sb.Append('|').Append(g.HaveAny(c[0]));
        string key = sb.ToString(); if (key == lastKey) return; lastKey = key;

        icon.Texture = UpgradeIcons.ForTech(tech); icon.Modulate = st == TechState.Locked ? new Color(.6f, .65f, .75f) : Colors.White;
        nameL.Text = Loc.T(t.Name);
        stateL.Text = Loc.T(st switch { TechState.Owned => t.Reward ? "Награда получена" : "Изучено", TechState.Available => "Можно изучить", TechState.Missing => "Не хватает ресурсов", _ => t.Reward ? "Награда" : "Закрыто" });
        stateL.AddThemeColorOverride("font_color", st switch { TechState.Owned => Green, TechState.Available => Green, TechState.Missing => Red, _ => Grey });
        desc.Text = TextFx.Colorize(Loc.T(t.Desc));

        // требование: серое, пока не выполнено; зелёное с галочкой, когда выполнено
        bool showReq = st != TechState.Owned && !string.IsNullOrEmpty(t.Req);
        reqTitle.Visible = req.Visible = showReq;
        if (showReq) { reqTitle.Text = Loc.T("Требование"); bool ok = t.Ok(g); string rt = Loc.T(t.Req).Replace("[", "[lb]"); req.Text = ok ? Col(Green, "✓ " + rt) : Col(Grey, rt); }

        // стоимость: недостающее красным, хватает — зелёным
        bool showCost = st != TechState.Owned && !t.Reward && t.Cost.Length > 0;
        costTitle.Visible = cost.Visible = showCost;
        if (showCost)
        {
            costTitle.Text = Loc.T("Стоимость");
            var cs = new StringBuilder();
            foreach (var c in t.Cost) { int have = g.HaveAny(c[0]); cs.Append(Col(have >= c[1] ? Green : Red, $"{It.Ic(c[0])}{Loc.T(It.Names[c[0]])} {have}/{c[1]}")).Append('\n'); }
            cost.Text = cs.ToString().TrimEnd();
        }

        learn.Visible = !t.Reward;
        learn.Text = Loc.T(st == TechState.Owned ? "Изучено" : "Изучить"); learn.Disabled = st != TechState.Available;
    }
}
