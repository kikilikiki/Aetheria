namespace Aetheria.Engine.ECS;

/// <summary>
/// Vue non générique d'un <see cref="ComponentPool{T}"/>, utilisée en interne par
/// <see cref="World"/> pour manipuler tous les types de composants de façon uniforme
/// (par exemple lors de la destruction d'une entité).
/// </summary>
internal interface IComponentPool
{
    bool Has(Entity entity);

    void Remove(Entity entity);
}
