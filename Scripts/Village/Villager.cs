using Godot;
using System.Collections.Generic;

namespace Lamplighter;

public enum VState : byte { Idle, Walking, Working, Interacting }

/// <summary>Житель деревни. Id постоянный: у спасённых в местах интереса это 1000 + номер места, у путников 5000 + порядковый номер — не зависит от порядка загрузки чанков.
/// Хозяин состояния — хост (или одиночная игра); клиенты только получают позицию и состояние и сглаживают движение (NetPos).</summary>
public sealed class Villager
{
    public int Id; public string Name = ""; public string Prof = ""; public string Src = "";
    public int Work = -1;                     // ключ (World.Key) здания-рабочего места, −1 — нет
    public int Home = -1;                     // ключ дома, −1 — живёт у маяка
    public int Talk;                          // маска уже услышанных реплик (сохраняется)
    // состояние показа и поведения (в сохранение не входит: после загрузки житель появляется у рабочего места или у маяка)
    public Vector2 Pos; public int Face = 1; public float Walk, Stuck; public bool Working; public VState State;
    public float Think, Rest, WorkT, Cool, Anim, Fx; public int Fails;
    public List<Vector2> Path = new(); public int PathI; public Vector2I Goal = new(-1, -1);
    public Vector2 Wander; public float WanderT;
    public Vector2 NetPos; public bool NetInit;
}
