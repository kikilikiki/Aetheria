namespace Aetheria.Engine.ECS;

/// <summary>
/// Identifiant léger d'une entité du monde ECS. Une entité ne contient aucune donnée ni
/// comportement : ce n'est qu'une clé utilisée par <see cref="World"/> pour associer des
/// composants entre eux.
/// </summary>
public readonly struct Entity : IEquatable<Entity>
{
    public int Id { get; }

    internal Entity(int id) => Id = id;

    public bool Equals(Entity other) => Id == other.Id;

    public override bool Equals(object? obj) => obj is Entity other && Equals(other);

    public override int GetHashCode() => Id;

    public static bool operator ==(Entity left, Entity right) => left.Equals(right);

    public static bool operator !=(Entity left, Entity right) => !left.Equals(right);

    public override string ToString() => $"Entity#{Id}";
}
