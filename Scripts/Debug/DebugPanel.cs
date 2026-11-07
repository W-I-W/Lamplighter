using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Lamplighter;

/// <summary>Окно отладки (F10). Вкладки: Состояние, Развитие (смена этапа), Баланс (числа CombatTuning на лету), Утилиты.
/// Разметка панели задана сценой, содержимое вкладок создаётся в коде. Правки чисел действуют сразу на расчёты и новых врагов. Только для разработки.</summary>
public partial class DebugPanel : PanelContainer
{
    public Game G;
    SpinBox spLv, spTier, spBc; RichTextLabel info; Label status; TabContainer tabs; bool right = true; ulong lastRefresh;
    VBoxContainer tuneList; LineEdit tuneFilter; readonly List<TuneRow> rows = new();
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    sealed class TuneRow { public FieldInfo F; public LineEdit Edit; public Label Name; public string Original; public HBoxContainer Box; }

    public void Bind(Game game)
    {
        G = game;
        var content = GetNode<VBoxContainer>("Content");
        content.GetNode<Button>("Header/Move").Pressed += () => { right = !right; Layout(); };
        content.GetNode<Button>("Header/Close").Pressed += Toggle;
        tabs = content.GetNode<TabContainer>("Tabs");
        status = content.GetNode<Label>("Status");
        BuildInfoTab(); BuildStageTab(); BuildTuneTab(); BuildToolsTab();
        Layout();
    }

    // ───────────── вспомогательные ─────────────
    static Button Btn(string text, Action a, string tip = null) { var b = new Button { Text = text, FocusMode = FocusModeEnum.None, TooltipText = tip ?? "" }; b.Pressed += a; return b; }
    VBoxContainer Page(string name, bool scroll = true)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        if (!scroll) { var host = new VBoxContainer { Name = name }; host.AddChild(box); tabs.AddChild(host); return box; }
        var sc = new ScrollContainer { Name = name, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; sc.AddChild(box); tabs.AddChild(sc); return box;
    }
    void Say(string s, bool bad = false) { if (status == null) return; status.Text = s; status.AddThemeColorOverride("font_color", bad ? new Color("#ff8a7a") : new Color("#9fd89f")); }
    static Label Head(string t) { var l = new Label { Text = t, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(1, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill }; l.AddThemeColorOverride("font_color", new Color("#c8a84a")); return l; }
    SpinBox Spin(double min, double max, double val, double step = 1) => new SpinBox { MinValue = min, MaxValue = max, Value = val, Step = step, CustomMinimumSize = new Vector2(90, 0) };
    HBoxContainer Row(params Control[] c) { var h = new HBoxContainer(); foreach (var x in c) h.AddChild(x); return h; }

    public bool Contains(Vector2 viewportPos) => Visible && GetGlobalRect().HasPoint(viewportPos);
    public bool HasTextFocus() { var f = GetViewport()?.GuiGetFocusOwner(); return f != null && IsAncestorOf(f) && (f is LineEdit || f is TextEdit); }
    public void DropFocus() { GetViewport()?.GuiGetFocusOwner()?.ReleaseFocus(); }
    public void Toggle()
    {
        Visible = !Visible; if (!Visible) { DropFocus(); Engine.TimeScale = 1; }                      // закрытая панель не оставляет игру на паузе
        if (Visible) { Layout(); Refresh(); spLv.Value = G.Level; spTier.Value = G.Tier; spBc.Value = G.BeaconLevel; }
    }

    /// <summary>Для автопроверок и скриптов: задать число так же, как вводом в поле.</summary>
    public bool SetTuning(string name, string text)
    {
        var r = rows.Find(x => x.F.Name == name); if (r == null) return false;
        r.Edit.Text = text; Commit(r); return Fmt(r.F.GetValue(Tune.T)) == Fmt(TryParse(r.F.FieldType, text, out var v) ? v : null);
    }
    public int TuneRowCount => rows.Count;
    public LineEdit FirstTuneEdit => rows.Count > 0 ? rows[0].Edit : null;
    public void ShowTab(int i) { tabs.CurrentTab = i; }

    void Layout()
    {
        var vs = GetViewport().GetVisibleRect().Size; float w = Mathf.Clamp(vs.X * .38f, 420, 560), h = Mathf.Clamp(vs.Y - 80, 360, 760);
        CustomMinimumSize = new Vector2(w, 0); Size = new Vector2(w, h); Position = new Vector2(right ? vs.X - w - 8 : 8, 40);
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        if (G.DebugGod) { G.Hp = G.MaxHp; }
        ulong now = Time.GetTicksMsec(); if (now - lastRefresh >= 250) { lastRefresh = now; Refresh(); }       // по реальному времени: работает и на паузе
    }

    void Refresh() { if (info != null) info.Text = G.DebugInfo(); }

    // ───────────── вкладка «Состояние» ─────────────
    void BuildInfoTab()
    {
        var p = Page("Состояние", false);
        info = new RichTextLabel { FitContent = false, SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 220), ScrollActive = true, BbcodeEnabled = false, SelectionEnabled = true };
        p.AddChild(info);
        p.AddChild(Row(
            Btn("Копировать снимок", () => { DisplayServer.ClipboardSet(G.DebugSnapshotJson()); Say("Снимок состояния в буфере обмена (JSON)."); }, "для баг-репорта и заметок плейтеста"),
            Btn("Сохранить игру", () => { G.Save(); Say("Игра сохранена."); })));
    }

    // ───────────── вкладка «Развитие» ─────────────
    void BuildStageTab()
    {
        var p = Page("Развитие");
        p.AddChild(Head("Готовые этапы (только повышают; перед прыжком делается копия сохранения)"));
        p.AddChild(Row(
            Btn("Середина", () => Say(G.DebugStage("mid")), "уровень 9, босс 2 побеждён, маяк 3"),
            Btn("Поздняя", () => Say(G.DebugStage("late")), "уровень 18, 4 босса, маяк 5"),
            Btn("После финала", () => Say(G.DebugStage("post")), "уровень 24, всё открыто")));

        p.AddChild(Head("Точная настройка"));
        var lv = spLv = Spin(1, 60, G.Level); p.AddChild(Row(new Label { Text = "Уровень героя" }, lv, Btn("Применить", () => { G.DebugSetLevel((int)lv.Value); Say($"Уровень {G.Level}, здоровье восстановлено."); })));
        var tier = spTier = Spin(0, 4, G.Tier); p.AddChild(Row(new Label { Text = "Инструмент (этап)" }, tier, Btn("Применить", () => { G.Tier = (int)tier.Value; G.RecomputeEffects(); Say($"Инструмент {G.Tier}."); })));
        var bc = spBc = Spin(1, 6, G.BeaconLevel); p.AddChild(Row(new Label { Text = "Уровень маяка" }, bc, Btn("Применить", () => { G.DebugSetBeacon((int)bc.Value); Say($"Маяк {G.BeaconLevel} (печати открываются по порогам)."); })));

        var bossRow = new HBoxContainer(); bossRow.AddChild(new Label { Text = "Боссы повержены:" });
        for (int i = 1; i <= 5; i++) { int id = i; var cb = new CheckBox { Text = i.ToString(), ButtonPressed = G.BossDown(i), FocusMode = FocusModeEnum.None }; cb.Toggled += on => { G.DebugSetBoss(id, on); Say($"Босс {id}: {(on ? "побеждён" : "снова жив")}."); }; bossRow.AddChild(cb); }
        p.AddChild(bossRow);

        p.AddChild(Head("Фонари"));
        var lit = new HFlowContainer(); for (int r = 0; r <= 4; r++) { int rr = r; lit.AddChild(Btn($"до региона {r}", () => { G.DebugLight(rr, false); Say($"Фонари до региона {rr} зажжены."); })); lit.AddChild(Btn($"+боссы {r}", () => { G.DebugLight(rr, true); Say($"Фонари и стражи до региона {rr}."); })); }
        p.AddChild(lit);

        p.AddChild(Head("Ресурсы и навыки"));
        p.AddChild(Row(
            Btn("Все навыки", () => { G.DebugAllTech(); Say("Навыки открыты."); }),
            Btn("Ресурсы ×99", () => { G.DebugGiveAll(99); Say("Каждого ресурса не меньше 99."); }),
            Btn("+уровень", () => { G.GiveXp(G.XpNeed, true); Say("Выдан опыт на уровень."); })));

        p.AddChild(Head("Телепорт"));
        var tp = new HFlowContainer();
        tp.AddChild(Btn("Маяк", () => G.DebugTeleportAncient(G.W.Ancients[0])));
        for (int i = 1; i <= 5; i++) { int id = i; tp.AddChild(Btn($"Страж {i}", () => { var a = G.BossLantern(id); if (a == null) Say("Фонаря стража нет.", true); else G.DebugTeleportAncient(a); })); }
        for (int r = 1; r <= 4; r++) { int rr = r; tp.AddChild(Btn($"Регион {r}", () => { var a = G.W.Ancients.Find(x => x.Region == rr && !x.Boss); if (a == null) Say("В регионе нет фонаря.", true); else G.DebugTeleportAncient(a); })); }
        p.AddChild(tp);
    }

    // ───────────── вкладка «Баланс» ─────────────
    void BuildTuneTab()
    {
        var p = Page("Баланс", false);
        tuneFilter = new LineEdit { PlaceholderText = "фильтр по имени (Enemy, Boss, Xp, Coop…)", SizeFlagsHorizontal = SizeFlags.ExpandFill }; tuneFilter.TextChanged += _ => ApplyFilter();
        var diff = new CheckBox { Text = "Сложная", ButtonPressed = G.Diff == 1, FocusMode = FocusModeEnum.None }; diff.Toggled += on => { G.Diff = on ? 1 : 0; Say(on ? "Сложность мира: сложная." : "Сложность мира: обычная."); };
        p.AddChild(Row(tuneFilter, diff));
        var sc = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(0, 260) };
        tuneList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; sc.AddChild(tuneList); p.AddChild(sc);
        p.AddChild(Row(
            Btn("Сбросить всё", () => { foreach (var r in rows) { r.Edit.Text = r.Original; Commit(r); } Say("Все числа вернулись к значениям на момент открытия."); }),
            Btn("Копировать изменения", CopyChanges, "строки для tuning.tres"),
            Btn("Сохранить в tuning.tres", SaveTuning, "только в запуске из проекта; копия в user://tuning_backups"),
            Btn("Перечитать файл", () => { Tune.Reload(); RebuildRows(); Say("Числа перечитаны из tuning.tres."); })));
        RebuildRows();
    }

    static bool Supported(Type t) => t == typeof(float) || t == typeof(int) || t == typeof(bool) || t == typeof(float[]) || t == typeof(int[]);

    static string Fmt(object v) => v switch
    {
        float f => f.ToString("0.####", Inv), int i => i.ToString(Inv), bool b => b ? "true" : "false",
        float[] fa => string.Join(", ", fa.Select(x => x.ToString("0.####", Inv))), int[] ia => string.Join(", ", ia), _ => ""
    };

    static bool TryParse(Type t, string s, out object v)
    {
        v = null; s = s.Trim();
        try
        {
            if (t == typeof(float)) { if (!float.TryParse(s, NumberStyles.Float, Inv, out var f) || !float.IsFinite(f)) return false; v = f; return true; }
            if (t == typeof(int)) { if (!int.TryParse(s, NumberStyles.Integer, Inv, out var i)) return false; v = i; return true; }
            if (t == typeof(bool)) { if (s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase)) { v = true; return true; } if (s == "0" || s.Equals("false", StringComparison.OrdinalIgnoreCase)) { v = false; return true; } return false; }
            var parts = s.Length == 0 ? new string[0] : s.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (t == typeof(float[])) { var a = new float[parts.Length]; for (int k = 0; k < a.Length; k++) if (!float.TryParse(parts[k], NumberStyles.Float, Inv, out a[k]) || !float.IsFinite(a[k])) return false; v = a; return a.Length > 0; }
            if (t == typeof(int[])) { var a = new int[parts.Length]; for (int k = 0; k < a.Length; k++) if (!int.TryParse(parts[k], NumberStyles.Integer, Inv, out a[k])) return false; v = a; return a.Length > 0; }
        }
        catch { }
        return false;
    }

    void RebuildRows()
    {
        foreach (var c in tuneList.GetChildren()) c.QueueFree(); rows.Clear();
        var t = Tune.T;
        foreach (var f in typeof(CombatTuning).GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => Supported(f.FieldType)))
        {
            var row = new TuneRow { F = f, Original = Fmt(f.GetValue(t)) };
            row.Name = new Label { Text = f.Name, CustomMinimumSize = new Vector2(150, 0), SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
            row.Edit = new LineEdit { Text = row.Original, SizeFlagsHorizontal = SizeFlags.ExpandFill, SelectAllOnFocus = true };
            var rr = row;
            row.Edit.TextSubmitted += _ => { Commit(rr); rr.Edit.ReleaseFocus(); }; row.Edit.FocusExited += () => Commit(rr);
            var reset = Btn("↺", () => { rr.Edit.Text = rr.Original; Commit(rr); }, "вернуть исходное");
            row.Box = new HBoxContainer(); row.Box.AddChild(row.Name); row.Box.AddChild(row.Edit); row.Box.AddChild(reset);
            tuneList.AddChild(row.Box); rows.Add(row);
        }
        ApplyFilter();
    }

    void ApplyFilter()
    {
        string q = tuneFilter?.Text?.Trim() ?? "";
        foreach (var r in rows) r.Box.Visible = q.Length == 0 || r.F.Name.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    void Commit(TuneRow r)
    {
        if (!GodotObject.IsInstanceValid(r.Edit)) return;
        if (!TryParse(r.F.FieldType, r.Edit.Text, out var v)) { r.Edit.Modulate = new Color("#ff6a6a"); Say($"{r.F.Name}: не удалось разобрать «{r.Edit.Text}»", true); return; }
        var cur = Fmt(r.F.GetValue(Tune.T)); var neu = Fmt(v);
        if (cur != neu) { r.F.SetValue(Tune.T, v); Say($"{r.F.Name} = {neu}"); }
        r.Edit.Text = neu; r.Edit.Modulate = neu == r.Original ? Colors.White : new Color("#ffd86a");
        r.Name.Modulate = r.Edit.Modulate;
    }

    static string TresValue(FieldInfo f, object v) => v switch
    {
        float[] fa => "PackedFloat32Array(" + string.Join(", ", fa.Select(x => x.ToString("0.####", Inv))) + ")",
        int[] ia => "PackedInt32Array(" + string.Join(", ", ia) + ")", bool b => b ? "true" : "false", _ => Fmt(v)
    };

    void CopyChanges()
    {
        var sb = new StringBuilder();
        foreach (var r in rows) if (Fmt(r.F.GetValue(Tune.T)) != r.Original) sb.AppendLine($"{r.F.Name} = {TresValue(r.F, r.F.GetValue(Tune.T))}    # было {r.Original}");
        if (sb.Length == 0) { Say("Изменённых чисел нет."); return; }
        DisplayServer.ClipboardSet(sb.ToString()); Say("Изменения скопированы в буфер обмена.");
    }

    void SaveTuning()
    {
        const string path = "res://Resources/Combat/tuning.tres";
        if (OS.HasFeature("template")) { Say("В экспортированной сборке tuning.tres не изменить. Используйте «Копировать изменения».", true); return; }
        try
        {
            var dir = ProjectSettings.GlobalizePath("user://tuning_backups"); System.IO.Directory.CreateDirectory(dir);
            var src = ProjectSettings.GlobalizePath(path);
            if (System.IO.File.Exists(src)) System.IO.File.Copy(src, System.IO.Path.Combine(dir, $"tuning_{DateTime.Now:yyyyMMdd_HHmmss}.tres"), true);
            var err = ResourceSaver.Save(Tune.T, path);
            if (err != Error.Ok) { Say("Не удалось сохранить: " + err, true); return; }
            foreach (var r in rows) { r.Original = Fmt(r.F.GetValue(Tune.T)); r.Edit.Modulate = Colors.White; r.Name.Modulate = Colors.White; }
            Say("tuning.tres сохранён. Копия предыдущего файла: user://tuning_backups. Прогоните tools\\check.ps1 balance.");
        }
        catch (Exception ex) { Say("Ошибка сохранения: " + ex.Message, true); }
    }

    // ───────────── вкладка «Утилиты» ─────────────
    void BuildToolsTab()
    {
        var p = Page("Утилиты");
        var god = new CheckBox { Text = "Бессмертие", FocusMode = FocusModeEnum.None }; god.Toggled += on => { G.DebugGod = on; Say(on ? "Бессмертие включено." : "Бессмертие выключено."); };
        p.AddChild(god);
        p.AddChild(Head("Скорость игры"));
        var spd = new HBoxContainer();
        foreach (var s in new[] { 0f, .25f, .5f, 1f, 2f, 4f }) { float ss = s; spd.AddChild(Btn(s == 0 ? "пауза" : $"×{s}", () => { Engine.TimeScale = ss == 0 ? 0.0001f : ss; Say($"Скорость ×{ss}."); })); }
        p.AddChild(spd);
        p.AddChild(Head("Игрок и враги"));
        p.AddChild(Row(Btn("Лечить", () => { G.DebugHeal(); Say("Здоровье восстановлено."); }), Btn("Убить врагов", () => { G.DebugKillAll(); Say("Враги убиты."); })));
        var kind = new OptionButton { FocusMode = FocusModeEnum.None }; foreach (var n in Enum.GetNames(typeof(EK))) kind.AddItem(n);
        var cnt = Spin(1, 30, 3);
        p.AddChild(Row(new Label { Text = "Создать" }, kind, cnt, Btn("Создать", () => { var k = (EK)Enum.Parse(typeof(EK), kind.GetItemText(kind.Selected)); G.DebugSpawn(k, (int)cnt.Value); Say($"{k} ×{(int)cnt.Value} рядом с героем."); })));
        p.AddChild(Head("Прочее"));
        p.AddChild(Row(Btn("Сохранить игру", () => { G.Save(); Say("Игра сохранена."); }), Btn("Копировать снимок", () => { DisplayServer.ClipboardSet(G.DebugSnapshotJson()); Say("Снимок в буфере обмена."); })));
    }
}
