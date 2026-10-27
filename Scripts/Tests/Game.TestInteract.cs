using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Тесты системы взаимодействия (LAMP_STAGE=interact; для сохранений — interactload).</summary>
public partial class Game
{
    Building PutB(Tl t, int x, int y, int lv = 1) { var b = new Building { Type = t, X = x, Y = y, Level = lv, Forced = true }; Buildings.Add(b); SetObj(x, y, t); return b; }
    string SelKey() { FindIx(); return Interact.Selected?.Key; }
    void Mouse(float x, float y) { aimOverride = Aim = new Vector2(x, y); }
    void At(float px, float py) { PPos = new Vector2(px, py); TestAim(new Vector2(900, 900)); }

    void BuildInteractScript()
    {
        S(.3f, () =>
        {
            int ax = W.Spawn.X + 12, ay = W.Spawn.Y - 14;
            for (int y = ay - 3; y <= ay + 4; y++) for (int x = ax - 4; x <= ax + 6; x++) SetObj(x, y, Tl.None);
            Buildings.Clear(); Interact.Reset(); Inv[It.Wood] = 500; Inv[It.Stone] = 500; Inv[It.Copper] = 500; Inv[It.Iron] = 500; Inv[It.Silver] = 500; Inv[It.Gear] = 99; Inv[It.Lens] = 99;
            var A = PutB(Tl.Sawmill, ax, ay); var B = PutB(Tl.Sawmill, ax + 1, ay);
            string kA = $"b:{ax}:{ay}", kB = $"b:{ax + 1}:{ay}";
            float cx = ax * 16 + 8, belowY = ay * 16 + 16 + 10;
            // 1) два здания рядом — выбирается ближайшее; результат не зависит от порядка в списке
            At(cx + 3, belowY); string s1 = SelKey();
            Check("1: nearest of two adjacent buildings", s1 == kA, $"sel={s1}");
            Buildings.Reverse(); Interact.Reset(); string s1b = SelKey(); Buildings.Reverse();
            Check("1b: selection independent of collection order", s1b == kA, $"sel={s1b}");
            // 2) устойчивость: небольшое смещение не переключает цель, заметное — переключает
            Interact.Reset(); At(cx, belowY); SelKey();
            At(cx + 16, belowY); string s2 = SelKey(); Check("2a: small move keeps target (hysteresis)", s2 == kA, $"sel={s2}");
            At(cx + 24, belowY); string s2b = SelKey(); Check("2b: clearly closer object takes over", s2b == kB, $"sel={s2b}");
            // детерминированная ничья: игрок строго посередине над стыком зданий
            Interact.Reset(); At(ax * 16 + 16, belowY); string tie1 = SelKey(); Interact.Reset(); Buildings.Reverse(); string tie2 = SelKey(); Buildings.Reverse();
            Check("2c: tie broken deterministically", tie1 == tie2 && tie1 != null, $"{tie1} / {tie2}");
            // 3) отход от здания сбрасывает выбор
            At(cx, belowY + 70); string s3 = SelKey(); Check("3: moving away clears selection", s3 == null, $"sel={s3}");
            // 4) мышь: клик по дальнему зданию выбирает его, пока оно доступно
            Interact.Reset(); At(cx - 4, belowY); SelKey();
            Mouse(ax * 16 + 16 + 8, ay * 16 + 8); OverUi = false; FindIx();
            Check("4a: hover detects building under cursor", Interact.Hover?.Key == kB, $"hover={Interact.Hover?.Key}");
            ClickWorld(); string s4 = SelKey(); Check("4b: click pins the clicked building", s4 == kB && Interact.Pinned, $"sel={s4}");
            OverUi = true; Interact.Reset(); At(cx - 4, belowY); Mouse(ax * 16 + 16 + 8, ay * 16 + 8); FindIx(); Check("4c: mouse over UI gives no hover", Interact.Hover == null);
            OverUi = false;
            Interact.Reset(); At(cx - 4, belowY); Mouse(ax * 16 + 16 + 8, ay * 16 + 8); FindIx(); ClickWorld(); SelKey();
            At(cx, belowY + 90); SelKey(); At(cx - 4, belowY); string s4d = SelKey(); Check("4d: pin dropped after leaving range", s4d == kA && !Interact.Pinned, $"sel={s4d}");
            // 5) E действует на выбранное здание, U — улучшает именно его
            TestAim(new Vector2(900, 900)); Interact.Reset(); A.Local[It.Wood] = 5; B.Local[It.Wood] = 7; Array.Clear(Stock);
            At(cx - 4, belowY); FindIx(); int wood0 = Inv[It.Wood]; Cur.E?.Invoke();
            Check("5: E collects from selected building only", Inv[It.Wood] == wood0 + 5 && A.Local[It.Wood] == 0 && B.Local[It.Wood] == 7, $"inv+={Inv[It.Wood] - wood0} A={A.Local[It.Wood]} B={B.Local[It.Wood]}");
            Interact.Reset(); At(cx + 24, belowY); FindIx(); Check("6a: selected is B before upgrade", Interact.Selected?.Key == kB);
            Cur.U?.Invoke(); Check("6b: U upgrades only the selected building", B.Level == 2 && A.Level == 1, $"A={A.Level} B={B.Level}");
            Check("9: panel title names the selected instance", Cur.Title != null && Cur.Title.Contains("уровень"), Cur.Title ?? "");
            // 7) окно строительства имеет приоритет
            Window = Win.Build; FindIx(); Check("7: build window disables selection", Interact.Selected == null && Cur.E == null && Cur.U == null); Window = Win.None;
            // 8) удаление объекта очищает выбор
            Interact.Reset(); At(cx + 24, belowY); FindIx(); Check("8a: B selected", Interact.Selected?.Key == kB);
            Buildings.Remove(B); SetObj(B.X, B.Y, Tl.None); FindIx(); Check("8b: removed building is no longer selected", Interact.Selected?.Key != kB, $"sel={Interact.Selected?.Key}");
            // выгрузка чанка/телепорт вдаль: ссылки сбрасываются без ошибок
            Tp(TileCenter(World.IX0 + 40, 64)); UpdateChunks(0, true); FindIx(); Check("8c: far teleport clears selection", Interact.Selected == null && Interact.Hover == null);
            GD.Print($"[check] SUMMARY failures={checkFails}");
        });
        S(.2f, () =>
        {
            int ax = W.Spawn.X + 12, ay = W.Spawn.Y - 14; Tp(TileCenter(ax, ay + 2)); Inv[It.Wood] = 3;
            for (int x = ax - 2; x <= ax + 6; x++) SetObj(x, ay, Tl.None); Buildings.Clear();
            var A = PutB(Tl.Sawmill, ax, ay); var B = PutB(Tl.Mine, ax + 2, ay); var C = PutB(Tl.Warehouse, ax - 1, ay); A.Local[It.Wood] = 12;
            Mouse(ax * 16 + 40, ay * 16 + 8);
        });
        S(3f, () => { });
        S(.2f, () => Shot("x1_interact"));
    }

    void BuildInteractLoadScript()
    {
        S(.4f, () =>
        {
            var wh = Buildings.Find(b => b.Type == Tl.Warehouse);
            Check("10: save loaded with buildings", wh != null, $"n={Buildings.Count}");
            if (wh != null)
            {
                Interact.Reset(); PPos = wh.Pos + new Vector2(0, 22); TestAim(new Vector2(0, 0) + PPos + new Vector2(900, 900)); FindIx();
                Check("10: interaction works after load", Interact.Selected?.Key == $"b:{wh.X}:{wh.Y}" && Cur.Title != null, $"sel={Interact.Selected?.Key} title={Cur.Title}");
            }
            GD.Print($"[check] SUMMARY failures={checkFails}");
        });
    }
}
