using Godot;

namespace Lamplighter;

/// <summary>Слой области ломания (рамка клетки, подсветка и трещины): выше стволов, но ниже крон — дерево спереди закрывает область ломания дерева позади.</summary>
public partial class BreakAreaLayer : EntityLayer
{
    public override void _Draw() => G?.DrawBreakArea(this);
}
