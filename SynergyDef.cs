using Godot;

namespace Lamplighter;

/// <summary>Синергия как данные Resource: условия активации (Requires) и награда (Grants).
/// Условия: "tag:fire>=1", "class:sword", "ability:pulse". Награды: "Burn:2", "Flag:FireTrail", "LampRadius:0.15".</summary>
[GlobalClass]
public partial class SynergyDef : Resource
{
    [Export] public string Id = "";
    [Export] public string Title = "";
    [Export] public string Path = "";
    [Export(PropertyHint.MultilineText)] public string Desc = "";
    [Export] public string[] Requires = System.Array.Empty<string>();
    [Export] public string[] Grants = System.Array.Empty<string>();
    [Export] public string Hint = "";
}
