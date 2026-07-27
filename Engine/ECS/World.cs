namespace Aetheria.Engine.ECS;

/// <summary>
/// Conteneur central de l'ECS : crée/détruit les entités, stocke leurs composants et
/// exécute les systèmes enregistrés. Un <see cref="World"/> est indépendant du rendu et du
/// réseau — il peut être utilisé aussi bien côté Client (prédiction/affichage) que côté
/// Server (simulation autoritaire).
/// </summary>
public sealed class World
{
    private readonly HashSet<int> _aliveEntities = new();
    private readonly Dictionary<Type, IComponentPool> _pools = new();
    private readonly List<ISystem> _systems = new();
    private int _nextEntityId;

    public Entity CreateEntity()
    {
        var entity = new Entity(_nextEntityId++);
        _aliveEntities.Add(entity.Id);
        return entity;
    }

    public void DestroyEntity(Entity entity)
    {
        if (!_aliveEntities.Remove(entity.Id))
        {
            return;
        }

        foreach (var pool in _pools.Values)
        {
            pool.Remove(entity);
        }
    }

    public bool IsAlive(Entity entity) => _aliveEntities.Contains(entity.Id);

    public void AddComponent<T>(Entity entity, T component) where T : struct
        => GetOrCreatePool<T>().Set(entity, component);

    public ref T GetComponent<T>(Entity entity) where T : struct
        => ref GetOrCreatePool<T>().GetRef(entity);

    public bool HasComponent<T>(Entity entity) where T : struct
        => TryGetPool<T>(out var pool) && pool.Has(entity);

    public void RemoveComponent<T>(Entity entity) where T : struct
    {
        if (TryGetPool<T>(out var pool))
        {
            pool.Remove(entity);
        }
    }

    /// <summary>Toutes les entités vivantes possédant un composant <typeparamref name="T"/>.</summary>
    public IEnumerable<Entity> Query<T>() where T : struct
    {
        if (!TryGetPool<T>(out var pool))
        {
            yield break;
        }

        foreach (var id in pool.GetEntityIds())
        {
            if (_aliveEntities.Contains(id))
            {
                yield return new Entity(id);
            }
        }
    }

    /// <summary>Toutes les entités vivantes possédant à la fois <typeparamref name="T1"/> et <typeparamref name="T2"/>.</summary>
    public IEnumerable<Entity> Query<T1, T2>()
        where T1 : struct
        where T2 : struct
    {
        foreach (var entity in Query<T1>())
        {
            if (HasComponent<T2>(entity))
            {
                yield return entity;
            }
        }
    }

    public void AddSystem(ISystem system) => _systems.Add(system);

    /// <summary>Exécute tous les systèmes enregistrés, dans leur ordre d'ajout.</summary>
    public void Update(float deltaTime)
    {
        foreach (var system in _systems)
        {
            system.Update(this, deltaTime);
        }
    }

    private ComponentPool<T> GetOrCreatePool<T>() where T : struct
    {
        var type = typeof(T);
        if (!_pools.TryGetValue(type, out var pool))
        {
            pool = new ComponentPool<T>();
            _pools[type] = pool;
        }

        return (ComponentPool<T>)pool;
    }

    private bool TryGetPool<T>(out ComponentPool<T> pool) where T : struct
    {
        if (_pools.TryGetValue(typeof(T), out var raw))
        {
            pool = (ComponentPool<T>)raw;
            return true;
        }

        pool = null!;
        return false;
    }
}
