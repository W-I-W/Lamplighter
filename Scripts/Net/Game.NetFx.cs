using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Общие звуки и эффекты в кооперативе: действия других игроков (удары, рывок, выстрелы, постройка, вспышки) слышны с расстоянием, победа над стражем видна и слышна всем,
/// а у других игроков рисуется то, что они держат в руках. Декоративные частицы не пересылаются: каждый рисует их у себя.</summary>
public partial class Game
{
    static readonly string[] NetSfxNames = { "mine", "break", "hit", "hurt", "dash", "spin", "shoot", "flare", "pulse", "boom", "build", "boss", "chest", "ancient" };
    readonly Dictionary<string, (float t, int n)> sfxRate = new();

    void InitNetFx() { Sfx.Hook = NetSfxOut; }

    /// <summary>Каждое «игровое» звучание у этого игрока отправляется остальным (не чаще 6 раз в секунду на звук), интерфейсные звуки — нет.</summary>
    void NetSfxOut(string name, float db, float pitch)
    {
        if (!NetService.Active || Net == null) return; int idx = Array.IndexOf(NetSfxNames, name); if (idx < 0) return;
        float now = (float)(Godot.Time.GetTicksMsec() / 1000.0); sfxRate.TryGetValue(name, out var r); if (now - r.t >= 1f) r = (now, 0); if (++r.n > 6) { sfxRate[name] = r; return; } sfxRate[name] = r;
        Net.SendSfx(idx, db, pitch, PPos.X, PPos.Y);
    }

    /// <summary>Чужой звук: слышен в пределах 420 пикселей, тише с расстоянием.</summary>
    public void NetSfxIn(int idx, float db, float pitch, float x, float y)
    {
        if (idx < 0 || idx >= NetSfxNames.Length || !float.IsFinite(db) || !float.IsFinite(pitch) || !float.IsFinite(x) || !float.IsFinite(y)) return;
        float d = new Vector2(x, y).DistanceTo(PPos); if (d > 420f) return;
        Sfx.PlayRemote(NetSfxNames[idx], Mathf.Clamp(db, -30, 6) - d / 60f - 2f, Mathf.Clamp(pitch, .4f, 2.5f));
    }

    static string BossDownTitle(int id) => id switch { 1 => "Хранитель Тёмного леса повержен!", 2 => "Хранитель глубин повержен!", 3 => "Пепельный страж повержен!", 4 => "Хранитель Зимы повержен!", _ => "Источник Тьмы повержен!" };

    /// <summary>Победа над стражем: вспышка, тряска, волна света и объявление у всех игроков (награды получает тот, кто победил, прогресс общий).</summary>
    public void NetBossDown(int id, Vector2 pos)
    {
        if (id < 1 || id > 5) return;
        Flash = Mathf.Max(Flash, .8f); Shake = Mathf.Max(Shake, 8f); Sfx.PlayRemote("ancient", 0, 1f); Sfx.PlayRemote("boom", -4, .8f);
        VictoryFx(pos, id == 4 ? new Color("#bfe9ff") : id == 3 ? new Color("#ff9a5a") : id == 2 ? new Color("#7ac8ff") : new Color("#d58cff")); Burst(pos, new Color("#d58cff"), 50, 180);
        Announce(BossDownTitle(id), "Победа общая: прогресс записан всем", new Color("#ffe9a0"), "ancient");
    }

    /// <summary>Что игрок держит в руках (значок для показа другим).</summary>
    public int HeldIcon() { string r = CurW >= 0 && CurW < Owned.Count ? RefWeapon(Owned[CurW].Id) : RefTool(CurTool); return HotIcon(r); }

    /// <summary>Рисует предмет в руке удалённого игрока во время его замаха — тот же значок и поворот, что у своего персонажа.</summary>
    void DrawHeldIcon(CanvasItem c, int icon, Vector2 hand, float ang)
    {
        if (icon < 0) return;
        float baseAng = icon == UiIcons.Pickaxe ? -Mathf.Pi / 2 : icon == UiIcons.Bow ? 0f : -Mathf.Pi / 4;
        var centre = hand + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 8f;
        c.DrawSetTransform(centre, ang - baseAng, Vector2.One);
        c.DrawTextureRect(UiIcons.Item(icon), new Rect2(-8, -8, 16, 16), false);
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}
