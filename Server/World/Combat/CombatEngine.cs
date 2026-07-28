namespace Aetheria.Server.World.Combat;

/// <summary>
/// Résolution pure du combat tactique sur grille (voir <c>Docs/GameDesign.md</c> — section
/// Combats) : ordre de jeu par vitesse, déplacement borné, dégâts Attaque-Défense, IA simple
/// pour le camp adverse. Pas encore de terrain/obstacles/zones d'effet/combos — une grille
/// vide 7x7 pour cette première version (voir limites documentées dans <c>Docs/README.md</c>).
/// </summary>
internal static class CombatEngine
{
    public static void Initialize(CombatSession session)
    {
        session.Combatants = [.. session.Combatants.OrderByDescending(c => c.Speed)];
        session.TurnIndex = 0;
        CheckEndCondition(session);
    }

    public static void ResolveMove(CombatSession session, Combatant actor, int targetX, int targetY)
    {
        if (!IsWithinGrid(targetX, targetY))
        {
            throw new InvalidOperationException("Case hors de la grille.");
        }

        if (Distance(actor.X, actor.Y, targetX, targetY) > actor.MovementRange)
        {
            throw new InvalidOperationException("Case hors de portée de déplacement.");
        }

        if (session.Combatants.Any(c => c.IsAlive && c != actor && c.X == targetX && c.Y == targetY))
        {
            throw new InvalidOperationException("Case déjà occupée.");
        }

        actor.X = targetX;
        actor.Y = targetY;
        session.LastMessage = $"{actor.Name} se déplace en ({targetX}, {targetY}).";
    }

    public static void ResolveAttack(CombatSession session, Combatant actor, int targetX, int targetY)
    {
        var target = session.Combatants.FirstOrDefault(c => c.IsAlive && c.X == targetX && c.Y == targetY)
            ?? throw new InvalidOperationException("Aucune cible sur cette case.");

        if (target.Team == actor.Team)
        {
            throw new InvalidOperationException("Impossible d'attaquer un allié.");
        }

        if (Distance(actor.X, actor.Y, targetX, targetY) > actor.AttackRange)
        {
            throw new InvalidOperationException("Cible hors de portée d'attaque.");
        }

        // La Défense n'est mitigée qu'à moitié (pas une soustraction complète) : avec l'Attaque
        // du joueur (10) et des créatures dont la Défense de base atteint 15 (voir
        // MonsterCatalogSeeder), une soustraction complète plafonnait presque tous les coups à 1
        // dégât — un combat contre un monstre de 26-36 PV prenait alors 20-30+ tours. Corrigé
        // suite à un retour utilisateur ("les monstres ont beaucoup trop de vie").
        var damage = Math.Max(2, actor.Attack - target.Defense / 2);
        target.CurrentHealth = Math.Max(0, target.CurrentHealth - damage);
        session.LastMessage = target.IsAlive
            ? $"{actor.Name} inflige {damage} dégâts à {target.Name}."
            : $"{actor.Name} inflige {damage} dégâts à {target.Name} et le met K.O. !";

        CheckEndCondition(session);
    }

    public static void AdvanceTurn(CombatSession session)
    {
        if (session.IsFinished)
        {
            return;
        }

        for (var i = 0; i < session.Combatants.Count; i++)
        {
            session.TurnIndex = (session.TurnIndex + 1) % session.Combatants.Count;
            if (session.CurrentCombatant!.IsAlive)
            {
                return;
            }
        }
    }

    /// <summary>Joue automatiquement les tours du camp adverse jusqu'à ce que ce soit à nouveau au joueur d'agir.</summary>
    public static void RunAiTurnsUntilPlayerTurn(CombatSession session)
    {
        var safety = 0;
        while (!session.IsFinished && session.CurrentCombatant is { IsPlayerControlled: false } ai && safety++ < 100)
        {
            RunAiTurn(session, ai);
            AdvanceTurn(session);
        }
    }

    private static void RunAiTurn(CombatSession session, Combatant actor)
    {
        var target = session.Combatants.Where(c => c.IsAlive && c.Team != actor.Team)
            .OrderBy(c => Distance(actor.X, actor.Y, c.X, c.Y))
            .FirstOrDefault();

        if (target is null)
        {
            return;
        }

        if (Distance(actor.X, actor.Y, target.X, target.Y) <= actor.AttackRange)
        {
            ResolveAttack(session, actor, target.X, target.Y);
            return;
        }

        // Pas de pathfinding : un pas naïf vers la cible, en évitant les cases occupées.
        var stepX = Math.Sign(target.X - actor.X);
        var stepY = Math.Sign(target.Y - actor.Y);
        var newX = Math.Clamp(actor.X + stepX, 0, CombatSession.GridWidth - 1);
        var newY = Math.Clamp(actor.Y + stepY, 0, CombatSession.GridHeight - 1);

        if (!session.Combatants.Any(c => c.IsAlive && c != actor && c.X == newX && c.Y == newY))
        {
            actor.X = newX;
            actor.Y = newY;
            session.LastMessage = $"{actor.Name} se rapproche.";
        }
    }

    private static void CheckEndCondition(CombatSession session)
    {
        var aliveTeams = session.Combatants.Where(c => c.IsAlive).Select(c => c.Team).Distinct().ToList();
        if (aliveTeams.Count > 1)
        {
            return;
        }

        session.IsFinished = true;
        session.WinningTeam = aliveTeams.Count == 1 ? aliveTeams[0] : null;
    }

    private static bool IsWithinGrid(int x, int y) => x >= 0 && x < CombatSession.GridWidth && y >= 0 && y < CombatSession.GridHeight;

    private static int Distance(int x1, int y1, int x2, int y2) => Math.Abs(x1 - x2) + Math.Abs(y1 - y2);
}
