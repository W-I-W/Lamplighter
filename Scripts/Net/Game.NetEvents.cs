using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter;

/// <summary>События мира (метеорит, караван, путник, торговец, разлом, осаждённый фонарь, редкий монстр, «пробуждение теней») в кооперативе принадлежат хосту: только он выбирает время и место,
/// ведёт таймеры и ИИ, а клиенты получают снимок списка событий и показывают то же самое. Торговец работает у каждого со своим кошельком (предложения задаёт зерно события),
/// действия над путником и разломом клиент отправляет хосту. Клетки событий (метеоритная руда, теневые столпы) идут через общую синхронизацию клеток.</summary>
public partial class Game
{
    float netEvT, netEvFull; string netEvHash = "";

    public Godot.Collections.Array NetEventRoster()
    {
        var a = new Godot.Collections.Array();
        foreach (var e in Events) a.Add(new Godot.Collections.Dictionary { ["k"] = e.Kind, ["x"] = e.Pos.X, ["y"] = e.Pos.Y, ["t"] = e.T, ["l"] = e.Life, ["v"] = e.V, ["s"] = e.State, ["c"] = e.Count, ["z"] = e.Seed, ["w"] = e.Wave, ["nx"] = e.Npc.X, ["ny"] = e.Npc.Y });
        return a;
    }

    /// <summary>Хост: список событий — при изменении, иначе раз в 3 с (для времени и состояния).</summary>
    void NetEventTick(float dt)
    {
        if (!NetHost || Net == null) return;
        netEvT -= dt; netEvFull -= dt; if (netEvT > 0) return; netEvT = .5f;
        string h = string.Join(";", Events.Select(e => e.Kind + e.Seed + e.State + e.Wave + e.Done));
        if (h != netEvHash || netEvFull <= 0) { netEvHash = h; netEvFull = 3f; Net.BroadcastEvents(); }
    }

    /// <summary>Клиент: время и падение метеорита идут между снимками хоста, чтобы анимации были плавными.</summary>
    void UpdateEventsClient(float dt)
    {
        foreach (var e in Events) { e.T += dt; if (e.Kind == "meteor" && e.State == 0) e.V = Mathf.Max(0, e.V - dt); if (e.State == 1 && e.Kind == "traveler") e.Pos = e.Npc; }
    }

    public void NetApplyEvents(Godot.Collections.Array a)
    {
        if (!NetClient) return; var keep = new List<GEvent>();
        foreach (var o in a)
        {
            var d = o.AsGodotDictionary(); string k = (string)d["k"]; int seed = (int)d["z"]; if (k == null || k.Length > 12) continue;
            var e = Events.Find(x => x.Kind == k && x.Seed == seed);
            if (e == null) { e = new GEvent { Kind = k, Seed = seed }; Announce("Событие: " + EvName(k), "", EvColor(k), k == "rift" ? "boss" : "discover"); }
            int oldState = e.State; e.Pos = new Vector2((float)d["x"], (float)d["y"]); e.T = (float)d["t"]; e.Life = (float)d["l"]; e.V = (float)d["v"]; e.State = (int)d["s"]; e.Count = (int)d["c"]; e.Wave = (int)d["w"]; e.Npc = new Vector2((float)d["nx"], (float)d["ny"]);
            if (k == "meteor" && oldState == 0 && e.State == 1) { Shake = Mathf.Max(Shake, 6); Flash = Mathf.Max(Flash, .4f); Sfx.Play("boom"); Burst(e.Pos, new Color("#fff2b0"), 40, 160); Beacons.Add(new Beacon { Pos = e.Pos, Dur = 2f, R = 90, Ring = 1f }); }     // падение метеорита видят все
            keep.Add(e);
        }
        Events.Clear(); Events.AddRange(keep);
    }

    /// <summary>Хост: участник нажал действие у события (путник, разлом).</summary>
    public bool NetEventAct(int seed)
    {
        var e = Events.Find(x => x.Seed == seed); if (e == null) return false;
        if (e.Kind == "traveler" && e.State == 0) { e.State = 1; if (!Enemies.Exists(q => q.Ev == e)) for (int i = 0; i < 2; i++) EvEnemy(e, e.Pos + Vector2.FromAngle(rng.Randf() * Mathf.Tau) * 110, PoolKind(RegionAt(e.Pos))); return true; }
        if (e.Kind == "rift" && e.State == 0) { StartTrial(e); return true; }
        return false;
    }
}
