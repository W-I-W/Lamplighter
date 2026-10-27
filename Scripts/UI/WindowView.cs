using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Общее окно игры (Scenes/UI/Window.tscn): рамка, заголовок, вкладки, список строк, панель подробностей, подсказка клавиш.
/// Раскладкой занимаются контейнеры сцены; код только заполняет текст и цвета.</summary>
public partial class WindowView : Control
{
    public Game G; public bool Started;
    ColorRect dim; PanelContainer frame, listPanel, detailPanel; Label title, foot; HBoxContainer chips, tabs, body; VBoxContainer listBox, detailBox; Control mapArea;
    readonly List<WindowRow> rows = new(); readonly List<Label> texts = new(); readonly List<RichTextLabel> riches = new(); int nRich; readonly List<PanelContainer> tabNodes = new(); readonly List<Control> chipNodes = new();
    PackedScene rowScene, tabScene, chipScene;
    int nRow, nText, nTab, nChip, posList, posDetail; bool hasTabs; Vector2 frameSize; float detailW;
    public Control Map => mapArea;
    public Rect2 FrameRect => new(frame.Position, frame.Size);

    public override void _Ready()
    {
        dim = GetNode<ColorRect>("%Dim"); frame = GetNode<PanelContainer>("%Frame"); title = GetNode<Label>("%Title"); foot = GetNode<Label>("%Foot");
        chips = GetNode<HBoxContainer>("%Chips"); tabs = GetNode<HBoxContainer>("%Tabs"); body = GetNode<HBoxContainer>("%Body");
        listPanel = GetNode<PanelContainer>("%ListPanel"); detailPanel = GetNode<PanelContainer>("%DetailPanel");
        listBox = GetNode<VBoxContainer>("%ListBox"); detailBox = GetNode<VBoxContainer>("%DetailBox"); mapArea = GetNode<Control>("%MapArea");
        rowScene = GD.Load<PackedScene>("res://Scenes/UI/Row.tscn"); tabScene = GD.Load<PackedScene>("res://Scenes/UI/Tab.tscn"); chipScene = GD.Load<PackedScene>("res://Scenes/UI/Chip.tscn");
    }

    /// <summary>Начало кадра: размер и положение рамки, заголовок, подсказка. listW: 0 — один список; &gt;0 — слева список такой ширины, справа подробности; -1 — область карты.</summary>
    public void Begin(Vector2 vs, string titleText, Color accent, string footer, Vector2 size, float listW = 0, float dimAlpha = .55f)
    {
        nRow = nText = nRich = nTab = nChip = posList = posDetail = 0; hasTabs = false; ResetMap();
        size = new Vector2(Mathf.Min(size.X, vs.X - 12), Mathf.Min(size.Y, vs.Y - 12)); frameSize = size; detailW = size.X - 12 - Mathf.Max(0, listW) - 6 - 10;
        dim.Color = new Color(0, 0, .03f, dimAlpha);
        frame.CustomMinimumSize = size; frame.Size = size; frame.Position = new Vector2(Mathf.Round(vs.X * .5f - size.X * .5f), Mathf.Round(vs.Y * .5f - size.Y * .5f - 4));
        title.Text = Loc.T(titleText); title.Modulate = accent;
        foot.Visible = footer != null; if (footer != null) foot.Text = Loc.T(footer);
        mapArea.Visible = listW < 0; listPanel.Visible = listW >= 0; detailPanel.Visible = listW > 0;
        listPanel.CustomMinimumSize = new Vector2(listW > 0 ? listW : 0, 0); listPanel.SizeFlagsHorizontal = listW > 0 ? SizeFlags.ShrinkBegin : SizeFlags.ExpandFill;
        Visible = true; Started = true;
    }

    /// <summary>Сколько строк высотой rowH помещается в теле окна (за вычетом extra пикселей).</summary>
    public int Fit(float rowH, float extra = 0) => Mathf.Max(1, Mathf.FloorToInt((frameSize.Y - 54 - (hasTabs ? 16 : 0) - extra) / (rowH + 3)));

    public void SetTabs(string[] names, int cur)
    {
        hasTabs = names.Length > 0;
        for (int i = 0; i < names.Length; i++)
        {
            PanelContainer t;
            if (nTab < tabNodes.Count) t = tabNodes[nTab]; else { t = tabScene.Instantiate<PanelContainer>(); tabNodes.Add(t); tabs.AddChild(t); }
            nTab++; t.Visible = true; if (t.GetIndex() != i) tabs.MoveChild(t, i);
            var l = t.GetNode<Label>("%Text"); string txt = Loc.T(names[i]); if (l.Text != txt) l.Text = txt; l.Modulate = i == cur ? new Color("#ffe08a") : new Color("#8a96b0");
            string v = i == cur ? "TabActive" : "TabNormal"; if (t.ThemeTypeVariation != v) t.ThemeTypeVariation = v;
        }
    }

    public void Chip(Texture2D icon, string text, Color c)
    {
        Control n;
        if (nChip < chipNodes.Count) n = chipNodes[nChip]; else { n = chipScene.Instantiate<Control>(); chipNodes.Add(n); chips.AddChild(n); }
        n.Visible = true; if (n.GetIndex() != nChip) chips.MoveChild(n, nChip); nChip++;
        n.GetNode<TextureRect>("%Icon").Texture = icon; var l = n.GetNode<Label>("%Text"); if (l.Text != text) l.Text = text; l.Modulate = c;
    }

    void Place(Node n, Container box, ref int pos)
    {
        if (n.GetParent() != box) { n.GetParent()?.RemoveChild(n); box.AddChild(n); }
        if (n.GetIndex() != pos) box.MoveChild(n, pos); pos++;
    }

    WindowRow TakeRow(bool detail)
    {
        WindowRow r; if (nRow < rows.Count) r = rows[nRow]; else { r = rowScene.Instantiate<WindowRow>(); rows.Add(r); }
        nRow++; r.Visible = true; if (detail) Place(r, detailBox, ref posDetail); else Place(r, listBox, ref posList); return r;
    }

    /// <summary>Строка в списке слева (или единственном).</summary>
    public void Row(int idx, bool sel, string left, string right, Color col, string sub = null, Color? subCol = null, bool btn = false, bool disabled = false)
        => TakeRow(false).Set(idx, btn, sel, disabled, Loc.T(left), right == null ? null : Loc.T(right), col, null, sub == null ? null : Loc.T(sub), subCol);

    public void Cols(int idx, bool sel, string[] t, float[] w, Color[] c, string right, Color rightCol)
    { for (int i = 0; i < t.Length; i++) t[i] = Loc.T(t[i]); TakeRow(false).SetCols(idx, sel, t, w, c, right, rightCol); }

    /// <summary>Абзац или строка текста в списке (detail=false) или в панели подробностей.</summary>
    public void Text(bool detail, string s, Color c, bool wrap = true, int maxLines = 0)
    {
        if (wrap) { TextFx.Apply(TakeRich(detail), Loc.T(s), c, 220f); return; }
        Label l; if (nText < texts.Count) l = texts[nText]; else { l = new Label { ThemeTypeVariation = "PlainLabel", MouseFilter = MouseFilterEnum.Ignore }; texts.Add(l); }
        nText++; l.Visible = true; if (detail) Place(l, detailBox, ref posDetail); else Place(l, listBox, ref posList);
        s = Loc.T(s); if (l.Text != s) l.Text = s; l.Modulate = c;
        l.AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off; l.ClipText = !wrap; l.MaxLinesVisible = maxLines > 0 ? maxLines : -1;
        l.TextOverrunBehavior = wrap ? TextServer.OverrunBehavior.NoTrimming : TextServer.OverrunBehavior.TrimEllipsis; l.CustomMinimumSize = new Vector2(wrap ? (detail ? detailW : 1) : 0, 0);
    }

    RichTextLabel TakeRich(bool detail)
    {
        RichTextLabel l;
        if (nRich < riches.Count) l = riches[nRich];
        else { l = new RichTextLabel { FitContent = true, MouseFilter = MouseFilterEnum.Ignore }; riches.Add(l); }
        nRich++; l.Visible = true; if (detail) Place(l, detailBox, ref posDetail); else Place(l, listBox, ref posList);
        l.CustomMinimumSize = new Vector2(detail ? detailW : frameSize.X - 30, 0); return l;
    }

    /// <summary>Текст с раскраской ключевых слов и «печатной машинкой»: visible — сколько символов показано (-1 — все).</summary>
    public void Rich(string plain, int visible)
    {
        var l = TakeRich(false); TextFx.Apply(l, plain, new Color("#d8e0f0"), 0); l.VisibleCharacters = visible;
    }

    /// <summary>Конец кадра: прячет неиспользованные узлы.</summary>
    public void End()
    {
        for (int i = nRow; i < rows.Count; i++) rows[i].Visible = false;
        for (int i = nText; i < texts.Count; i++) texts[i].Visible = false;
        for (int i = nRich; i < riches.Count; i++) riches[i].Visible = false;
        for (int i = nTab; i < tabNodes.Count; i++) tabNodes[i].Visible = false;
        for (int i = nChip; i < chipNodes.Count; i++) chipNodes[i].Visible = false;
        tabs.Visible = hasTabs; chips.Visible = nChip > 0; EndMap();
    }

    public void Hide2() { Visible = false; }

    bool Inside(Control c, Vector2 viewportPoint) => c.Visible && new Rect2(Vector2.Zero, c.Size).HasPoint(c.GetGlobalTransform().AffineInverse() * viewportPoint);

    public int RowHit(Vector2 logical)
    {
        if (!Visible) return -1; var p = logical * G.UiScaleVp;
        for (int i = 0; i < nRow; i++) if (rows[i].Index >= 0 && Inside(rows[i], p)) return rows[i].Index;
        return -1;
    }

    public int TabHit(Vector2 logical)
    {
        if (!Visible) return -1; var p = logical * G.UiScaleVp;
        for (int i = 0; i < nTab; i++) if (Inside(tabNodes[i], p)) return i;
        return -1;
    }

    public bool IsOver(Vector2 logical) => Visible && Inside(frame, logical * G.UiScaleVp);

    // ───────────────────────── область карты: TileMapLayer с палитрой из PNG и метки ─────────────────────────
    readonly List<ColorRect> mRects = new(); readonly List<TextureRect> mTexs = new(); readonly List<Label> mLabels = new();
    int nmRect, nmTex, nmLabel, mseq; TileMapLayer mapLayer; World mapWorld; readonly bool[] mapShown = new bool[(World.Wd / 4) * (World.Ht / 4)];
    static Texture2D discTex, diamondTex;

    void ResetMap() { nmRect = nmTex = nmLabel = 0; mseq = 1; if (mapLayer != null) mapLayer.Visible = false; }

    /// <summary>Карта мира: клетки исследованных блоков 4x4 переносятся в TileMapLayer порциями (цвета — из Assets/UI/Map/map_palette.png).</summary>
    public void MapWorld(Vector2 origin, float scale)
    {
        if (mapLayer == null)
        {
            var tex = GD.Load<Texture2D>("res://Assets/UI/Map/map_palette.png");
            var src = new TileSetAtlasSource { Texture = tex, TextureRegionSize = new Vector2I(1, 1) };
            for (int i = 0; i < tex.GetWidth(); i++) src.CreateTile(new Vector2I(i, 0));
            var ts = new TileSet { TileSize = new Vector2I(1, 1) }; ts.AddSource(src, 0);
            mapLayer = new TileMapLayer { TileSet = ts, TextureFilter = TextureFilterEnum.Nearest }; mapArea.AddChild(mapLayer); mapArea.MoveChild(mapLayer, 0);
        }
        if (mapWorld != G.W) { mapWorld = G.W; System.Array.Clear(mapShown); mapLayer.Clear(); }
        int cw = World.Wd / 4, budget = 600;
        for (int b = 0; b < mapShown.Length && budget > 0; b++)
        {
            if (mapShown[b] || G.Expl[b] == 0) continue;
            mapShown[b] = true; budget--; int bx = b % cw * 4, by = b / cw * 4;
            for (int y = by; y < by + 4; y++) for (int x = bx; x < bx + 4; x++) mapLayer.SetCell(new Vector2I(x, y), 0, new Vector2I(G.MapIdx(x, y), 0));
        }
        mapLayer.Visible = true; mapLayer.Position = origin; mapLayer.Scale = new Vector2(scale, scale);
    }

    T MapNode<T>(List<T> list, ref int used) where T : Control, new()
    {
        T n; if (used < list.Count) n = list[used]; else { n = new T { MouseFilter = MouseFilterEnum.Ignore }; list.Add(n); mapArea.AddChild(n); }
        used++; n.Visible = true; n.ZIndex = mseq++; return n;
    }

    /// <summary>Подложка под картой (рисуется ниже слоя карты).</summary>
    public void MapBack(Rect2 r, Color c) { MapRect(r, c); mRects[nmRect - 1].ZIndex = -1; }

    public void MapRect(Rect2 r, Color c) { var n = MapNode(mRects, ref nmRect); n.Position = r.Position; n.Size = r.Size; n.Color = c; }

    public void MapDisc(Vector2 center, float rad, Color c) => MapTex(GD.Load<Texture2D>("res://Assets/Sprites/Vfx/disc.png"), new Rect2(center - new Vector2(rad, rad), new Vector2(rad * 2, rad * 2)), c);
    public void MapDiamond(Vector2 center, float half, Color c) => MapTex(GD.Load<Texture2D>("res://Assets/Sprites/Vfx/diamond.png"), new Rect2(center - new Vector2(half, half), new Vector2(half * 2 + 1, half * 2 + 1)), c);

    public void MapTex(Texture2D t, Rect2 r, Color c)
    {
        var n = MapNode(mTexs, ref nmTex); if (n.Texture != t) n.Texture = t;
        n.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize; n.StretchMode = TextureRect.StretchModeEnum.Scale; n.Position = r.Position; n.Size = r.Size; n.Modulate = c;
    }

    public void MapLabel(string s, Vector2 pos, Color c, float width, HorizontalAlignment al)
    {
        var n = MapNode(mLabels, ref nmLabel); n.ThemeTypeVariation = "PlainLabel"; s = Loc.T(s); if (n.Text != s) n.Text = s;
        n.HorizontalAlignment = al; n.Position = pos; n.Size = new Vector2(width, 11); n.Modulate = c; n.ClipText = true;
    }

    void EndMap()
    {
        for (int i = nmRect; i < mRects.Count; i++) mRects[i].Visible = false;
        for (int i = nmTex; i < mTexs.Count; i++) mTexs[i].Visible = false;
        for (int i = nmLabel; i < mLabels.Count; i++) mLabels[i].Visible = false;
    }
}
