using Godot;
using System;

namespace Lamplighter;

/// <summary>Подтверждение действия или ввод нового названия.</summary>
public partial class Modal : Control
{
    LineEdit edit;
    Action<string> onOk;

    public static Modal Ask(Control host, string title, string text, string ok, Action yes, bool danger = true)
        => Open(host, title, text, null, ok, danger, _ => yes());

    public static Modal Prompt(Control host, string title, string text, string initial, string ok, Action<string> done)
        => Open(host, title, text, initial, ok, false, done);

    static Modal Open(Control host, string title, string text, string initial, string ok, bool danger, Action<string> done)
    {
        var modal = GD.Load<PackedScene>("res://Scenes/UI/FlowDialog.tscn").Instantiate<Modal>();
        host.AddChild(modal);
        modal.ShowDialog(title, text, initial, ok, danger, done);
        return modal;
    }

    void ShowDialog(string title, string text, string initial, string ok, bool danger, Action<string> done)
    {
        onOk = done;
        var content = GetNode<VBoxContainer>("Panel/Margin/Content");
        var heading = content.GetNode<Label>("Title");
        heading.Text = Loc.T(title);
        heading.Modulate = danger ? Px.Warn : Px.Gold;
        content.GetNode<Label>("Message").Text = Loc.T(text);
        if (initial != null)
        {
            edit = content.GetNode<LineEdit>("Input");
            edit.Text = initial;
            edit.Show();
            edit.TextSubmitted += _ => Confirm();
            edit.CallDeferred(Control.MethodName.GrabFocus);
        }

        var confirm = content.GetNode<Button>("Actions/Confirm");
        confirm.Text = Loc.T(ok);
        confirm.GetNode<TextureRect>("Icon").Visible = !danger;
        confirm.GetNode<TextureRect>("DangerIcon").Visible = danger;
        confirm.Pressed += () => { Sfx.Play("tick", -3, 1.3f); Confirm(); };

        var cancel = content.GetNode<Button>("Actions/Cancel");
        cancel.Text = Loc.T(cancel.Text);
        cancel.Pressed += () => { Sfx.Play("tick", -3, 1.3f); Close(); };
        Px.FadeIn(this, .12f, 0);
    }

    void Confirm()
    {
        string value = edit?.Text ?? "";
        if (edit != null && value.Trim() == "") { Sfx.Play("error"); return; }
        Close();
        onOk?.Invoke(value);
    }

    public void Close() => QueueFree();

    public override void _Input(InputEvent e)
    {
        if (e is not InputEventKey key || !key.Pressed || key.Echo) return;
        if (key.Keycode == Key.Escape) Close();
        else if ((key.Keycode == Key.Enter || key.Keycode == Key.KpEnter) && edit == null) Confirm();
        else return;
        GetViewport().SetInputAsHandled();
    }
}
