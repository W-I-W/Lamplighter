using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Окно записок, финальная сцена (волна света → короткие кадры → титры) и режим «после финала».</summary>
public partial class Game
{
    public bool Finished;                        // главный маяк активирован: мир освобождён, игра продолжается
    public int FinStage, FinScene;               // 0 — нет, 1 — волна света, 2 — сцены, 3 — титры
    public float FinT, Dawn;
    public string StoryTitle = "", StoryText = "", StorySub = "";

    public void OpenStory(string title, string text, string sub = "")
    {
        StoryTitle = title; StoryText = text; StorySub = sub;
        CloseWindows(); Window = Win.Story; Sfx.Play("tick", -2, .9f);
    }

    void StartFinale()
    {
        BeaconLevel = 6; Finished = true; FinStage = 1; FinT = 0; FinScene = 0; Enemies.Clear(); Projs.Clear(); Zones.Clear(); Boss = null;
        CloseWindows(); Invuln = 60; Flash = 1f; Sfx.Play("ancient");
        Announce("Центральный маяк пробуждён!", "Пятое ядро света питает Сеть Маяков", new Color("#fff2b0"), "ancient");
        Save();
    }

    public void StartCredits() { CloseWindows(); FinStage = 3; FinT = 0; }

    /// <summary>Вызывается вместо обычного обновления на кадрах финала (стадии 2 и 3).</summary>
    void UpdateFinale(float dt)
    {
        FinT += dt;
        if (FinStage == 1)
        {
            var bc = W.Ancients[0].Pos;
            Dawn = Mathf.Clamp(FinT / 9f, 0, 1);
            if (FinT > 1.5f) foreach (var a in W.Ancients) if (!a.Lit) { a.Lit = true; a.Anim = 0; a.Pulse = 1; }
            if (rng.Randf() < dt * 40) Parts.Add(new Particle { P = bc + Vector2.FromAngle(rng.Randf() * Mathf.Tau) * rng.RandfRange(10, 120), V = new Vector2(rng.RandfRange(-20, 20), -rng.RandfRange(30, 90)), Life = 1.4f, Max = 1.4f, C = rng.Randf() < .5f ? new Color("#fff2b0") : new Color("#ffb0ff"), Size = 2.2f });
            Shake = FinT < 6 ? 1.2f : 0;
            if (FinT > 11f) { FinStage = 2; FinT = 0; FinScene = 0; Invuln = 5; }
        }
        else if (FinStage == 3 && FinT > 4f + Story.Credits.Length * 1.6f) EndFinale();
    }

    void FinaleKey(Key k)
    {
        if (FinStage == 1) { if (k == Key.Escape) { FinT = 11.1f; } return; }
        if (FinStage == 2)
        {
            if (k == Key.Escape || FinScene >= Story.Finale.Length - 1 && (k == Key.Enter || k == Key.Space || k == Key.E || k == Key.KpEnter)) { FinStage = 3; FinT = 0; return; }
            if (k == Key.Enter || k == Key.Space || k == Key.E || k == Key.KpEnter || k == Key.Right) { FinScene++; FinT = 0; Sfx.Play("tick", -2, 1.2f); }
            return;
        }
        if (FinStage == 3 && (k == Key.Escape || k == Key.Enter || k == Key.Space || k == Key.E || k == Key.KpEnter)) EndFinale();
    }

    void EndFinale()
    {
        FinStage = 0; FinT = 0; Invuln = 3; Dawn = 1;
        Announce("Мир освобождён", "Вы можете продолжить играть: исследовать, строить и бросать вызов Разломам Тьмы", new Color("#fff2b0"), "ancient");
        Found.Add("Центральный маяк активирован — Тьма отступила");
        Save();
    }

    /// <summary>Плавное затухание тьмы после финала (в Сердце остаётся полумрак).</summary>
    float DawnTarget => FinStage == 1 ? Dawn : Finished ? (RegionAt(PPos) == 4 ? .3f : .55f) : 0f;
}
