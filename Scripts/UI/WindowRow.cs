using Godot;

namespace Lamplighter;

/// <summary>Строка списка, таблицы или кнопка меню (Scenes/UI/Row.tscn): до шести колонок, значение справа и необязательная вторая строка.</summary>
public partial class WindowRow : PanelContainer
{
    public int Index = -1;
    /// <summary>Горизонтальный промежуток значения справа (полоса громкости) в координатах окна; false, если значения нет.</summary>
    public bool ValueSpan(out float x0, out float x1)
    {
        x0 = x1 = 0; if (cr == null || !cr.Visible) return false;
        var t = cr.GetGlobalTransform(); x0 = (t * Vector2.Zero).X; x1 = (t * cr.Size).X; return x1 - x0 > 4f;
    }
    readonly Label[] c = new Label[6]; Label cr; RichTextLabel sub; TextureRect icon; Control subM; string variation = "";
    static readonly Color Gold = new("#ffd23f");

    public override void _Ready()
    {
        for (int i = 0; i < 6; i++) c[i] = GetNode<Label>("%C" + i);
        icon = GetNode<TextureRect>("%Icon"); cr = GetNode<Label>("%CR"); sub = GetNode<RichTextLabel>("%Sub"); subM = GetNode<Control>("%SubM");
    }

    void Style(bool btn, bool sel, bool disabled)
    {
        string v = btn ? (disabled ? "BtnDisabled" : sel ? "BtnSelected" : "BtnNormal") : sel ? "RowSelected" : "RowNormal";
        if (v != variation) { variation = v; ThemeTypeVariation = v; }
    }

    static void Put(Label l, string text, Color col, float width)
    {
        l.Visible = true; if (l.Text != text) l.Text = text; l.Modulate = col;
        l.CustomMinimumSize = new Vector2(width, 0); l.SizeFlagsHorizontal = width > 0 ? SizeFlags.ShrinkBegin : SizeFlags.ExpandFill;
    }

    /// <summary>Обычная строка: текст слева, значение справа, под ней при необходимости пояснение.</summary>
    public void Set(int index, bool btn, bool sel, bool disabled, string left, string right, Color col, Color? rightCol = null, string subText = null, Color? subCol = null, Texture2D ic = null)
    {
        Index = index; Style(btn, sel, disabled); icon.Visible = ic != null; if (ic != null) icon.Texture = ic;
        Put(c[0], left, col, 0); for (int i = 1; i < 6; i++) c[i].Visible = false;
        cr.Visible = !string.IsNullOrEmpty(right); if (cr.Visible) { if (cr.Text != right) cr.Text = right; cr.Modulate = rightCol ?? (sel ? Gold : col); }
        subM.Visible = subText != null; if (subText != null) TextFx.Apply(sub, subText, subCol ?? new Color("#8a96b0"), 0);
    }

    /// <summary>Строка-таблица: колонки с фиксированной шириной (0 — занимает остаток).</summary>
    public void SetCols(int index, bool sel, string[] texts, float[] widths, Color[] colors, string right, Color rightCol)
    {
        Index = index; Style(false, sel, false); icon.Visible = false;
        for (int i = 0; i < 6; i++) { if (i < texts.Length) Put(c[i], texts[i], colors[i], widths[i]); else c[i].Visible = false; }
        cr.Visible = !string.IsNullOrEmpty(right); if (cr.Visible) { if (cr.Text != right) cr.Text = right; cr.Modulate = rightCol; }
        subM.Visible = false;
    }
}
