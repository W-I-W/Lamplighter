using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Каталог визуальных настроек и материалов шейдеров. Единственная точка подключения ресурсов к игре.</summary>
[GlobalClass]
public partial class VisualCatalog : Resource
{
    public const string Path = "res://Resources/VisualConfigs/VisualCatalog.tres";
    [Export] public Godot.Collections.Array<EntityVisualDef> Entities = new();
    [Export] public Godot.Collections.Array<VfxDef> Effects = new();
    [Export] public ShaderMaterial Darkness;
    [Export] public ShaderMaterial Glow;

    static VisualCatalog inst; static bool tried;
    readonly Dictionary<string, EntityVisualDef> ents = new(); readonly Dictionary<string, VfxDef> fx = new();

    public static VisualCatalog Get()
    {
        if (tried) return inst; tried = true;
        if (ResourceLoader.Exists(Path)) inst = ResourceLoader.Load<VisualCatalog>(Path);
        if (inst != null) { foreach (var e in inst.Entities) if (e != null) inst.ents[e.Id] = e; foreach (var f in inst.Effects) if (f != null) inst.fx[f.Id] = f; }
        return inst;
    }

    public EntityVisualDef Entity(string id) => ents.TryGetValue(id, out var e) ? e : null;
    public VfxDef Effect(string id) => fx.TryGetValue(id, out var e) ? e : null;
}
