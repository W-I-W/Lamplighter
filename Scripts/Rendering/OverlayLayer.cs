using Godot;

namespace Lamplighter;

/// <summary>Слой выше крон деревьев: область ломания (рамка клетки, трещины, полоса прогресса) всегда видна поверх дерева, которое игрок ломает.</summary>
public partial class OverlayLayer : EntityLayer
{
    public override void _Draw() => G?.DrawBreakOverlay(this);
}
