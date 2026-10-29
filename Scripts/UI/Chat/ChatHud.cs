using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lamplighter;

/// <summary>Компактный журнал и чат в левом нижнем углу. Обычно — последние 3–4 сообщения без рамки, с лёгкой подложкой; старые плавно гаснут через 8–12 секунд, без сообщений ничего не видно.
/// Enter открывает историю (колёсико мыши) и поле ввода, Enter отправляет, Esc закрывает; пока чат открыт, игровой ввод отключён (Game.ChatOpen).</summary>
public partial class ChatHud : Control
{
    public Game G;
    RichTextLabel mini, full; ColorRect back; PanelContainer panel; LineEdit input; Label count;
    public bool IsOpen { get; private set; }
    string miniText = "", fullSig = ""; float openA; double lastSend;
    const int MiniLines = 4;

    public override void _Ready()
    {
        Theme = GD.Load<Theme>(Hud.ThemePath); MouseFilter = MouseFilterEnum.Ignore; TextureFilter = TextureFilterEnum.Nearest; ZIndex = 3000;
        back = new ColorRect { MouseFilter = MouseFilterEnum.Ignore, Visible = false }; AddChild(back);
        mini = Rich(false); AddChild(mini);
        panel = new PanelContainer { ThemeTypeVariation = "PanelDark", MouseFilter = MouseFilterEnum.Stop, Visible = false }; AddChild(panel);
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 3); panel.AddChild(v);
        full = Rich(true); full.CustomMinimumSize = new Vector2(100, 104); full.MouseFilter = MouseFilterEnum.Stop; v.AddChild(full);
        input = new LineEdit { MaxLength = GameNotificationService.MaxLen, PlaceholderText = Loc.T("Сообщение…"), CustomMinimumSize = new Vector2(0, 16), CaretBlink = true };
        Modal.Style(input); v.AddChild(input); input.TextSubmitted += Submit;
        count = new Label { ThemeTypeVariation = "PlainLabel", Modulate = new Color(.55f, .62f, .75f), MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Right, Visible = false }; AddChild(count);
    }

    RichTextLabel Rich(bool scroll)
    {
        var r = new RichTextLabel { BbcodeEnabled = true, FitContent = !scroll, ScrollActive = scroll, ScrollFollowing = scroll, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.Nearest, ShortcutKeysEnabled = false };
        r.AddThemeConstantOverride("line_separation", 0); r.AddThemeColorOverride("font_shadow_color", new Color(0, 0, .04f, .9f)); r.AddThemeConstantOverride("shadow_offset_x", 1); r.AddThemeConstantOverride("shadow_offset_y", 1);
        return r;
    }

    float ChatW => Mathf.Clamp(Size.X * .5f - 130f, 170f, 260f);
    static string Esc(string s) => s.Replace("[", "[lb]");
    static string Hex(Color c, float a) => "#" + c.ToHtml(false) + ((int)Mathf.Clamp(a * 255f, 0, 255)).ToString("x2");

    public void OpenChat()
    {
        if (IsOpen) return; IsOpen = true; openA = 0; input.Text = ""; panel.Visible = true; input.CallDeferred(Control.MethodName.GrabFocus); Sfx.Play("tick", -6, 1.6f);
    }

    public void CloseChat() { if (!IsOpen) return; IsOpen = false; input.ReleaseFocus(); input.Text = ""; panel.Visible = false; count.Visible = false; }

    void Submit(string text)
    {
        text = GameNotificationService.Clean(text, GameNotificationService.MaxLen);
        if (text != "" && Time.GetTicksMsec() / 1000.0 - lastSend >= .5) { lastSend = Time.GetTicksMsec() / 1000.0; G.SendChat(text); }
        CloseChat();
    }

    /// <summary>Строка журнала: «[Имя] текст» для игрока, «[Система]»/«[Мир]» для служебных сообщений; цвет текста — по смыслу сообщения.</summary>
    static string Line(GameMessage m, float a)
    {
        var tone = GameNotificationService.ToneColor(m.Tone);
        string tag = m.Kind == MsgKind.Chat ? m.Sender : m.Kind == MsgKind.World ? Loc.T("Мир") : Loc.T("Система");
        var tagCol = m.Kind == MsgKind.Chat ? GameNotificationService.NameColor(m.Sender) : m.Kind == MsgKind.World ? new Color("#6fb8c8") : new Color("#7d88a3");
        string rep = m.Repeat > 1 ? $" ×{m.Repeat}" : "";
        return $"[color={Hex(tagCol, a)}][lb]{Esc(tag)}][/color] [color={Hex(tone, a)}]{Esc(m.Text)}{rep}[/color]";
    }

    static bool Shown(GameMessage m) => m.Kind == MsgKind.Chat ? Settings.ShowChat : Settings.ShowNotes;

    public override void _Process(double delta)
    {
        if (G == null || G.W == null) return;
        bool ui = G.Menu == Mn.None && G.Window == Win.None && !G.TechOpen && !G.MapOpen && G.FinStage == 0;
        if (!ui && IsOpen) CloseChat();
        Visible = ui; if (!ui) return;
        var vs = Size; int fs = Settings.ChatSize == 1 ? 18 : 9;
        mini.AddThemeFontSizeOverride("normal_font_size", fs); full.AddThemeFontSizeOverride("normal_font_size", fs);
        float w = ChatW, life = Mathf.Clamp(Settings.ChatSecs, 6f, 14f), now = G.Notify.Clock;
        if (IsOpen)
        {
            mini.Visible = false; back.Visible = false; openA = Mathf.Min(1, openA + (float)delta * 10f);
            var hist = G.Notify.History; var lastM = hist.Count > 0 ? hist[^1] : null;
            string sig = hist.Count + ":" + (lastM != null ? lastM.Id + "." + lastM.Repeat : "") + Settings.ShowChat + Settings.ShowNotes + fs;
            if (sig != fullSig)
            {
                fullSig = sig; var sb = new StringBuilder(); bool first = true;
                foreach (var m in hist) if (Shown(m)) { if (!first) sb.Append('\n'); first = false; sb.Append(Line(m, 1f)); }
                full.Text = sb.ToString(); full.ScrollToLine(Math.Max(0, full.GetLineCount() - 1));
            }
            full.CustomMinimumSize = new Vector2(w - 12, fs == 18 ? 150 : 104); panel.CustomMinimumSize = new Vector2(w, 0); panel.Modulate = new Color(1, 1, 1, openA);
            panel.ResetSize(); panel.Position = new Vector2(4, vs.Y - 4 - panel.Size.Y).Round();
            count.Visible = true; count.Text = $"{input.Text.Length}/{GameNotificationService.MaxLen}"; count.Size = new Vector2(60, 10); count.Position = new Vector2(panel.Position.X + panel.Size.X - 64, panel.Position.Y - 10);
            if (!input.HasFocus()) input.GrabFocus();
            return;
        }
        // компактный режим: последние сообщения, ещё не погасшие
        var shown = new List<GameMessage>();
        for (int i = G.Notify.History.Count - 1; i >= 0 && shown.Count < MiniLines; i--)
        {
            var m = G.Notify.History[i]; if (m.Silent || !Shown(m) || now - m.Time >= life) continue;
            shown.Insert(0, m);
        }
        if (shown.Count == 0) { mini.Visible = false; back.Visible = false; miniText = ""; return; }
        var s2 = new StringBuilder(); float maxA = 0;
        for (int i = 0; i < shown.Count; i++)
        {
            float age = now - shown[i].Time, a = Mathf.Clamp(age / .3f, 0, 1) * Mathf.Clamp((life - age) / 2f, 0, 1); a = Mathf.Round(a * 10f) / 10f; maxA = Mathf.Max(maxA, a);
            if (i > 0) s2.Append('\n');
            s2.Append(Line(shown[i], a));
        }
        string txt = s2.ToString(); if (txt != miniText) { miniText = txt; mini.Text = txt; }
        mini.Visible = true; mini.CustomMinimumSize = new Vector2(w, 0); mini.Size = new Vector2(w, 0);
        float h = mini.GetContentHeight(); mini.Size = new Vector2(w, h); mini.Position = new Vector2(6, vs.Y - 6 - h).Round();
        back.Visible = Settings.ChatBack > .01f; back.Position = mini.Position - new Vector2(3, 2); back.Size = new Vector2(w + 6, h + 4); back.Color = new Color(.02f, .04f, .1f, Settings.ChatBack * Mathf.Min(1f, maxA * 1.4f));
    }
}
