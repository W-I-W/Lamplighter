using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Доступ к описаниям внешнего вида (Resources/VisualConfigs/Entities) и единый способ нарисовать кадр листа спрайтов.</summary>
public partial class Game
{
    readonly Dictionary<string, EntityVisualDef> defCache = new();

    /// <summary>Описание по id; если каталога нет (например, не выполнен экспорт графики) — временное описание на процедурной текстуре.</summary>
    EntityVisualDef Def(string id)
    {
        if (defCache.TryGetValue(id, out var d)) return d;
        d = VisualCatalog.Get()?.Entity(id);
        if (d == null)
        {
            d = new EntityVisualDef { Id = id, Offset = id == "player" || id == "villager" || id == "trader" || id == "traveler" ? new Vector2(-8, -11) : new Vector2(-8, -10) };
            foreach (var s in SpriteLibrary.Sheets) if (s.id == id) d.Sheet = s.get();
        }
        defCache[id] = d; return d;
    }

    EntityVisualDef EnemyDef(EK k) => Def(k.ToString().ToLowerInvariant());

    /// <summary>Рисует кадр листа в текущей трансформации; height позволяет обрезать нижнюю часть (персонаж по пояс в воде).</summary>
    static void DrawFrame(EntityLayer c, EntityVisualDef d, int frame, Color mod, float height = -1, Vector2 shift = default)
    {
        float h = height < 0 ? d.FrameSize : height; var sh = d.Sheet; if (sh == null) return;
        c.DrawTextureRectRegion(sh, new Rect2(d.Offset + shift, new Vector2(d.FrameSize, h)), new Rect2(frame * d.FrameSize, 0, d.FrameSize, h), mod);
    }
}
