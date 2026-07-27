namespace Aetheria.Engine.ECS;

/// <summary>
/// Une logique de jeu qui s'exécute à chaque frame/tick sur les entités d'un <see cref="World"/>
/// (déplacement, IA, résolution de combat, etc.). Les systèmes concrets vivront dans
/// Client/Server selon qu'ils sont côté présentation ou côté simulation.
/// </summary>
public interface ISystem
{
    void Update(World world, float deltaTime);
}
