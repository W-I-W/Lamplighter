using Godot;

namespace Lamplighter;

/// <summary>Один узел рисует всех персонажей и частицы — без Node на каждую сущность.</summary>
public partial class EntityLayer : Node2D
{
    public Game G;
    public override void _Draw() => G?.DrawEntities(this);
}
