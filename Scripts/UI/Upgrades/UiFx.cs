using Godot;

namespace Lamplighter;

/// <summary>Мелкие пиксельные эффекты интерфейса: квадратики 2×2, разлетающиеся и гаснущие. Без размытия и градиентов.</summary>
public static class UiFx
{
    /// <summary>Точка src (его локальные координаты) в координатах parent — интерфейс масштабируется, поэтому через матрицы.</summary>
    public static Vector2 LocalPoint(Control parent, Control src, Vector2 srcLocal) => parent.GetGlobalTransform().AffineInverse() * (src.GetGlobalTransform() * srcLocal);

    /// <summary>Всплеск частиц вокруг точки (координаты родителя parent).</summary>
    public static void Burst(Control parent, Vector2 centre, Color col, int n = 12, float dist = 22f, float time = .5f)
    {
        if (parent == null || !GodotObject.IsInstanceValid(parent)) return;
        var rng = new RandomNumberGenerator(); rng.Randomize();
        for (int i = 0; i < n; i++)
        {
            var p = new ColorRect { Color = col, Size = new Vector2(2, 2), Position = centre, MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 50 };
            parent.AddChild(p);
            float a = rng.Randf() * Mathf.Tau, d = dist * (.45f + rng.Randf() * .75f); var to = (centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d).Round();
            var tw = p.CreateTween().SetParallel(true);
            tw.TweenProperty(p, "position", to, time * (.7f + rng.Randf() * .5f)).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(p, "modulate:a", 0f, time).SetDelay(time * .35f);
            tw.Chain().TweenCallback(Callable.From(p.QueueFree));
        }
    }
}
