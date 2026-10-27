using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Окно врат разломов и индикатор активного разлома.</summary>
public partial class Hud
{
    void DrawRiftWin(Vector2 vs)
    {
        float w = Mathf.Min(640, vs.X - 40), h = 360;
        var r = Frame(new Rect2((vs.X - w) / 2, (vs.Y - h) / 2, w, h), G.RiftState == 1 ? "Разлом: выберите временную реликвию" : "Врата разломов", new Color("#c070ff"), G.RiftState == 1 ? "[1]-[3] или Enter — выбрать · пропадёт после разлома" : "←/→ вкладки · ↑/↓ выбор · Enter — действие · Esc — закрыть");
        if (G.RiftState == 1)
        {
            var a = new Rect2(r.Position.X + 16, r.Position.Y + 70, r.Size.X - 32, r.Size.Y - 100);
            for (int i = 0; i < G.RiftOffer.Count; i++)
            {
                var d = Db.Relic(G.RiftOffer[i]);
                Row(a, i, G.WinCur, $"[{i + 1}] {d.Title}", null, new Color("#e8d8ff"), 0, 40);
                Txt(d.Desc, new Vector2(a.Position.X + 28, a.Position.Y + i * 40 + 14), 11, Dim);
            }
            return;
        }
        Tabs(r, Game.RiftTabs, G.WinTab, r.Position.Y + 36);
        var area = new Rect2(r.Position.X + 16, r.Position.Y + 96, r.Size.X - 32, r.Size.Y - 130);
        Txt($"Осколков разлома: {G.Inv[It.RiftShard]}", new Vector2(r.End.X - 16, r.Position.Y + 24), 12, It.Col[It.RiftShard], HorizontalAlignment.Right);
        if (G.WinTab == 0)
        {
            for (int i = 0; i < Game.MaxRiftTier; i++)
            {
                bool open = G.RiftTierOpen(i + 1); bool done = (G.RiftFirst & (1 << (i + 1))) != 0;
                Row(area, i, G.WinCur, $"Уровень {i + 1}" + (done ? "  ✓" : ""), open ? $"волн {3 + (i + 1 >= 4 ? 1 : 0) + (i + 1 >= 7 ? 1 : 0)} · враги ×{1 + .22f * (i + 1):0.0} · осколков {3 + 2 * (i + 1)}" : "закрыт: пройдите предыдущий", open ? Colors.White : new Color(.5f, .5f, .55f), 0, 26);
            }
        }
        else if (G.WinTab == 1)
        {
            for (int i = 0; i < Game.Legends.Length; i++)
            {
                var l = Game.Legends[i]; bool has = G.HasLegend(i); bool can = G.Can(l.Cost);
                Row(area, i, G.WinCur, l.Name + (has ? "  ✓" : ""), has ? "получено" : Game.CostStr(l.Cost), has ? Good : can ? Gold : new Color(.7f, .7f, .75f), 0, 40);
                Txt(l.Desc, new Vector2(area.Position.X + 28, area.Position.Y + i * 40 + 14), 11, Dim);
            }
        }
        else
        {
            for (int i = 0; i < Game.CosmNames.Length; i++)
            {
                bool own = (G.CosmMask & (1 << i)) != 0;
                Row(area, i, G.WinCur, Game.CosmNames[i] + (G.CosmSel == i ? "  (надет)" : ""), own ? "" : "закрыт (1, 3, 5 уровни разломов)", own ? Colors.White : new Color(.5f, .5f, .55f), 0, 26);
            }
        }
    }

    void DrawEndgameHud(Vector2 vs)
    {
        if (!G.RiftOn) return;
        int left = G.Enemies.FindAll(e => e.Ev != null && e.Ev.Kind == "riftx").Count;
        var r = new Rect2(vs.X / 2 - 150, 52, 300, 34);
        Box(r, new Color("#c070ff"));
        Txt($"Разлом {G.RiftTier} · волна {G.RiftWave}/{G.RiftWaves} · врагов {left}", new Vector2(r.Position.X, r.Position.Y + 14), 13, Colors.White, HorizontalAlignment.Center, r.Size.X);
        Txt($"{Game.AffixNames[G.RiftAffix[0]]} + {Game.AffixNames[G.RiftAffix[1]]}", new Vector2(r.Position.X, r.Position.Y + 28), 11, Dim, HorizontalAlignment.Center, r.Size.X);
    }
}
