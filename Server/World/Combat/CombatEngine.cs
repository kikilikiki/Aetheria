using Aetheria.Shared.Enums;

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
        session.TurnStartedAtUtc = DateTime.UtcNow;
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
        var multiplier = ElementalMultiplier(actor.Element, target.Element);
        var damage = Math.Max(2, (int)((actor.Attack - target.Defense / 2) * multiplier));
        target.CurrentHealth = Math.Max(0, target.CurrentHealth - damage);
        var suffix = ElementalSuffix(multiplier);
        session.LastMessage = target.IsAlive
            ? $"{actor.Name} inflige {damage} dégâts à {target.Name}{suffix}."
            : $"{actor.Name} inflige {damage} dégâts à {target.Name}{suffix} et le met K.O. !";

        CheckEndCondition(session);
    }

    /// <summary>
    /// Capacité spéciale selon le type du combattant (voir GDD/demande utilisateur — "ajoute des
    /// capacités spéciales") : Soigneur soigne l'allié le plus affaibli (aucune cible à viser),
    /// Archer transperce en ignorant la défense (portée +1), Guerrier (et tout autre type)
    /// déclenche un coup puissant à dégâts majorés. Un seul palier de capacités par type pour
    /// cette première version — pas d'arbre de compétences ni de coût de ressource dédié.
    /// </summary>
    public static void ResolveSpecialAbility(CombatSession session, Combatant actor, int targetX, int targetY)
    {
        if (actor.Type == MonsterType.Soigneur)
        {
            var lowest = session.Combatants
                .Where(c => c.IsAlive && c.Team == actor.Team)
                .OrderBy(c => (float)c.CurrentHealth / c.MaxHealth)
                .FirstOrDefault();

            if (lowest is null || lowest.CurrentHealth >= lowest.MaxHealth)
            {
                session.LastMessage = $"{actor.Name} ne trouve personne à soigner.";
                return;
            }

            var healAmount = Math.Max(1, lowest.MaxHealth * 3 / 10);
            lowest.CurrentHealth = Math.Min(lowest.MaxHealth, lowest.CurrentHealth + healAmount);
            session.LastMessage = $"{actor.Name} soigne {lowest.Name} de {healAmount} PV.";
            return;
        }

        var target = session.Combatants.FirstOrDefault(c => c.IsAlive && c.X == targetX && c.Y == targetY)
            ?? throw new InvalidOperationException("Aucune cible sur cette case.");

        if (target.Team == actor.Team)
        {
            throw new InvalidOperationException("Impossible d'attaquer un allié.");
        }

        var range = actor.Type == MonsterType.Archer ? actor.AttackRange + 1 : actor.AttackRange;
        if (Distance(actor.X, actor.Y, targetX, targetY) > range)
        {
            throw new InvalidOperationException("Cible hors de portée.");
        }

        var multiplier = ElementalMultiplier(actor.Element, target.Element);
        int damage;
        string verb;
        if (actor.Type == MonsterType.Archer)
        {
            // Tir perçant : ignore entièrement la Défense (contrepartie de la portée +1).
            damage = Math.Max(2, (int)(actor.Attack * multiplier));
            verb = "transperce";
        }
        else
        {
            damage = Math.Max(3, (int)((actor.Attack - target.Defense / 2) * 1.8 * multiplier));
            verb = "frappe (coup puissant)";
        }

        target.CurrentHealth = Math.Max(0, target.CurrentHealth - damage);
        var suffix = ElementalSuffix(multiplier);
        session.LastMessage = target.IsAlive
            ? $"{actor.Name} {verb} {target.Name} pour {damage} dégâts{suffix}."
            : $"{actor.Name} {verb} {target.Name} pour {damage} dégâts{suffix} et le met K.O. !";

        CheckEndCondition(session);
    }

    /// <summary>
    /// Avantages/faiblesses de type (voir GDD/demande utilisateur — "avantage/faiblesse de
    /// type"), inspirés d'un triangle façon jeu de rôle plutôt que d'un tableau exhaustif :
    /// chaque élément est fort contre 1-2 autres (dégâts x1.5), et réciproquement faible contre
    /// l'élément qui le contre (dégâts x0.67). Neutre n'a ni avantage ni faiblesse.
    /// </summary>
    private static readonly Dictionary<Element, Element[]> StrongAgainst = new()
    {
        [Element.Feu] = [Element.Nature, Element.Glace],
        [Element.Eau] = [Element.Feu, Element.Terre],
        [Element.Nature] = [Element.Eau, Element.Terre],
        [Element.Glace] = [Element.Nature, Element.Air],
        [Element.Foudre] = [Element.Eau, Element.Air],
        [Element.Terre] = [Element.Foudre, Element.Feu],
        [Element.Air] = [Element.Terre, Element.Nature],
        [Element.Lumiere] = [Element.Ombre],
        [Element.Ombre] = [Element.Lumiere],
        [Element.Neutre] = [],
    };

    private static float ElementalMultiplier(Element attacker, Element defender)
    {
        if (attacker == Element.Neutre || defender == Element.Neutre)
        {
            return 1f;
        }

        if (StrongAgainst.TryGetValue(attacker, out var strongList) && strongList.Contains(defender))
        {
            return 1.5f;
        }

        if (StrongAgainst.TryGetValue(defender, out var defenderStrongList) && defenderStrongList.Contains(attacker))
        {
            return 0.67f;
        }

        return 1f;
    }

    private static string ElementalSuffix(float multiplier) => multiplier switch
    {
        > 1f => " (efficace !)",
        < 1f => " (peu efficace...)",
        _ => "",
    };

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
                session.TurnStartedAtUtc = DateTime.UtcNow;
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
        // IA Soigneur (voir GDD — types de monstres) : priorité à soigner un allié blessé plutôt
        // qu'attaquer, tant qu'il y en a un — sinon se rabat sur le comportement standard.
        if (actor.Type == MonsterType.Soigneur
            && session.Combatants.Any(c => c.IsAlive && c.Team == actor.Team && c.CurrentHealth < c.MaxHealth))
        {
            ResolveSpecialAbility(session, actor, actor.X, actor.Y);
            return;
        }

        var target = session.Combatants.Where(c => c.IsAlive && c.Team != actor.Team)
            .OrderBy(c => Distance(actor.X, actor.Y, c.X, c.Y))
            .FirstOrDefault();

        if (target is null)
        {
            return;
        }

        var distance = Distance(actor.X, actor.Y, target.X, target.Y);
        var specialRange = actor.Type == MonsterType.Archer ? actor.AttackRange + 1 : actor.AttackRange;

        if (distance <= specialRange)
        {
            // Hors de la portée d'attaque normale mais dans la portée étendue de l'Archer : la
            // capacité spéciale est alors la SEULE option valide (ResolveAttack refuserait cette
            // distance). Sinon, l'IA l'utilise aussi environ un tour sur trois pour ne pas rendre
            // l'attaque de base totalement obsolète.
            var mustUseSpecial = distance > actor.AttackRange;
            var wantsSpecial = actor.Type != MonsterType.Soigneur && Random.Shared.NextDouble() < 0.35;

            if (mustUseSpecial || wantsSpecial)
            {
                ResolveSpecialAbility(session, actor, target.X, target.Y);
            }
            else
            {
                ResolveAttack(session, actor, target.X, target.Y);
            }

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
