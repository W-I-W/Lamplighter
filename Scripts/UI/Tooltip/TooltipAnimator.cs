using Godot;

namespace Lamplighter;

/// <summary>Состояние анимации подсказки: появление (fade + подъём), плавное исчезновение, пульс клавиши, вспышка при появлении возможности.
/// Не рисует и не знает об игре: получает ключ объекта и готовую модель.</summary>
public sealed class TooltipAnimator
{
    public float Alpha, Flash, Time;                // Alpha 0..1; Time — секунды с появления этого объекта
    public TipModel Shown; public Rect2 Anchor;      // последняя показанная модель (держится на время затухания)
    string key; bool lastOk;

    /// <summary>Пока объект выбран — fade in; когда пропал — fade out по сохранённой модели.</summary>
    public void Update(float dt, string objKey, TipModel model, Rect2 worldRect, TooltipStyle st)
    {
        Time += dt; Flash = Mathf.Max(0, Flash - dt);
        if (model != null)
        {
            if (objKey != key) { key = objKey; Time = 0; Alpha = Mathf.Min(Alpha, .35f); lastOk = model.PrimaryOk; }
            else if (model.PrimaryOk && !lastOk && model.HasAction) { Flash = st.FlashTime; }
            lastOk = model.PrimaryOk; Shown = model; Anchor = worldRect;
            Alpha = Mathf.Min(1, Alpha + dt / Mathf.Max(.01f, st.FadeIn));
        }
        else
        {
            Alpha = Mathf.Max(0, Alpha - dt / Mathf.Max(.01f, st.FadeOut));
            if (Alpha <= 0) { Shown = null; key = null; }
        }
    }

    /// <summary>Подъём: 0 → 1 за время появления (ease-out), для сдвига панели вверх на пару пикселей.</summary>
    public float Rise(TooltipStyle st) { float k = Mathf.Clamp(Time / Mathf.Max(.01f, st.FadeIn * 1.4f), 0, 1); return 1 - (1 - k) * (1 - k); }

    public float Pulse(float speed) => .5f + .5f * Mathf.Sin(Time * speed);
}
