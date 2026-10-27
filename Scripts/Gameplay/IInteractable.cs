using Godot;

namespace Lamplighter;

/// <summary>Единый контракт интерактивного объекта мира: здания, склады, сундуки, фонари, NPC, алтари и т.д.
/// Объект сам описывает свои действия (Describe); поиск и выбор выполняет только InteractionController.</summary>
public interface IInteractable
{
    string Key { get; }            // стабильный идентификатор (не зависит от порядка в коллекциях)
    string Title { get; }          // название для подсветки и панели
    Rect2 Bounds { get; }          // область взаимодействия в мировых координатах
    int Priority { get; }          // при равном расстоянии побеждает меньшее значение
    void Describe(Ix ix);          // заполняет строки и действия [E]/[U]
}
