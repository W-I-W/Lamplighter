using Godot;

namespace Lamplighter;

/// <summary>Реликвия как данные Resource: теги, редкость и эффекты строками вида "Burn:1", "LampRadius:0.1".</summary>
[GlobalClass]
public partial class RelicDef : Resource
{
    [Export] public string Id = "";
    [Export] public string Title = "";
    [Export(PropertyHint.MultilineText)] public string Desc = "";
    [Export] public string[] Tags = System.Array.Empty<string>();
    [Export] public int Rarity;
    [Export] public bool Permanent = true;
    [Export] public string[] Effects = System.Array.Empty<string>();
    [Export] public Color Tint = Colors.White;
}
