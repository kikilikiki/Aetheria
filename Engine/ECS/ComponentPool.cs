using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Aetheria.Engine.ECS;

/// <summary>
/// Stocke toutes les instances d'un même type de composant <typeparamref name="T"/>,
/// indexées par identifiant d'entité.
/// </summary>
internal sealed class ComponentPool<T> : IComponentPool where T : struct
{
    private readonly Dictionary<int, T> _components = new();

    public void Set(Entity entity, T component) => _components[entity.Id] = component;

    public bool Has(Entity entity) => _components.ContainsKey(entity.Id);

    public void Remove(Entity entity) => _components.Remove(entity.Id);

    /// <summary>
    /// Retourne une référence modifiable vers le composant de <paramref name="entity"/>.
    /// Lève une exception si l'entité ne possède pas ce composant.
    /// </summary>
    public ref T GetRef(Entity entity)
    {
        ref var value = ref CollectionsMarshal.GetValueRefOrNullRef(_components, entity.Id);
        if (Unsafe.IsNullRef(ref value))
        {
            throw new KeyNotFoundException(
                $"{entity} ne possède pas de composant '{typeof(T).Name}'.");
        }

        return ref value;
    }

    /// <summary>
    /// Copie des identifiants d'entités possédant ce composant, utilisée par les requêtes
    /// de <see cref="World"/>. C'est une copie (et non la collection vive) afin qu'un système
    /// puisse ajouter/retirer des composants pendant qu'il itère sur une requête.
    /// </summary>
    public int[] GetEntityIds() => _components.Keys.ToArray();
}
