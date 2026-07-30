using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;

namespace Aetheria.Server.World.Combat;

/// <summary>
/// Résolution pure du combat tactique sur grille (voir <c>Docs/GameDesign.md</c> — section
/// Combats) : ordre de jeu par vitesse, déplacement borné, dégâts Attaque-Défense, IA simple
/// pour le camp adverse. Pas encore de terrain/obstacles/zones d'effet/combos — une grille
/// vide 7x7 pour cette première version (voir limites documentées dans <c>Docs/README.md</c>).
/// </summary>
internal static class CombatEngine
{
    /// <summary>Voir GDD/demande utilisateur — "ajoute un cooldown pour le spécial" : nombre de tours du combattant avant de pouvoir la réutiliser.</summary>
    private const int SpecialAbilityCooldownTurns = 3;

    /// <summary>Voir GDD/demande utilisateur — "l'attaque ultime en plus de l'attaque spéciale" : cooldown propre à <c>ResolveUltimateAbility</c>, séparé de <see cref="SpecialAbilityCooldownTurns"/>.</summary>
    private const int UltimateAbilityCooldownTurns = 3;

    private const int DestructibleTileHealth = 15;

    /// <summary>Voir GDD/demande utilisateur — "poser des bloques" (capacité du Contrôleur) : portée depuis laquelle une case vide peut recevoir un nouvel obstacle.</summary>
    private const int PlacedBlockRange = 3;

    public static void Initialize(CombatSession session)
    {
        session.Combatants = [.. session.Combatants.OrderByDescending(c => c.Speed)];
        session.TurnIndex = 0;
        session.TurnStartedAtUtc = DateTime.UtcNow;
        ScatterTileEffects(session);
        CheckEndCondition(session);
    }

    /// <summary>
    /// Voir GDD/demande utilisateur — "cases destructibles, cases de lave, cases glacées, pièges
    /// posés sur les cases" : quelques cases spéciales par combat PvE (jamais en PvP/Arène, pour
    /// garder les duels entre joueurs équitables et simples). Un piège n'apparaît que si un
    /// monstre sauvage de type Assassin/Contrôleur est engagé (voir GDD — "monstres qui peuvent
    /// [poser des] pièges").
    /// </summary>
    private static void ScatterTileEffects(CombatSession session)
    {
        if (session.IsPvp)
        {
            return;
        }

        var occupied = session.Combatants.Where(c => c.IsAlive).Select(c => (c.X, c.Y)).ToHashSet();
        var freeTiles = new List<(int X, int Y)>();
        for (var x = 0; x < CombatSession.GridWidth; x++)
        {
            for (var y = 0; y < CombatSession.GridHeight; y++)
            {
                if (!occupied.Contains((x, y)))
                {
                    freeTiles.Add((x, y));
                }
            }
        }

        for (var i = freeTiles.Count - 1; i > 0; i--)
        {
            var swapIndex = Random.Shared.Next(i + 1);
            (freeTiles[i], freeTiles[swapIndex]) = (freeTiles[swapIndex], freeTiles[i]);
        }

        var cursor = 0;
        void PlaceTiles(TileEffect effect, int count)
        {
            for (var i = 0; i < count && cursor < freeTiles.Count; i++, cursor++)
            {
                session.TileEffects[freeTiles[cursor]] = effect;
                if (effect == TileEffect.Destructible)
                {
                    session.DestructibleHealth[freeTiles[cursor]] = DestructibleTileHealth;
                }
            }
        }

        PlaceTiles(TileEffect.Lave, 2);
        PlaceTiles(TileEffect.Glace, 2);
        PlaceTiles(TileEffect.Destructible, 2);

        var hasTrapper = session.Combatants.Any(c => c.Team == 1 && c.Type is MonsterType.Assassin or MonsterType.Controleur);
        if (hasTrapper)
        {
            PlaceTiles(TileEffect.Piege, 1);
        }
    }

    public static void ResolveMove(CombatSession session, Combatant actor, int targetX, int targetY)
    {
        if (!IsWithinGrid(targetX, targetY))
        {
            throw new InvalidOperationException("Case hors de la grille.");
        }

        // Voir GDD/demande utilisateur — "cases glacées" : +1 case de portée de déplacement quand
        // on part d'une case de glace (glisse plus loin que prévu).
        var effectiveRange = actor.MovementRange
            + (session.TileEffects.GetValueOrDefault((actor.X, actor.Y)) == TileEffect.Glace ? 1 : 0);
        if (Distance(actor.X, actor.Y, targetX, targetY) > effectiveRange)
        {
            throw new InvalidOperationException("Case hors de portée de déplacement.");
        }

        if (session.Combatants.Any(c => c.IsAlive && c != actor && c.X == targetX && c.Y == targetY))
        {
            throw new InvalidOperationException("Case déjà occupée.");
        }

        // Voir GDD/demande utilisateur — "cases destructibles" : infranchissables tant qu'elles
        // n'ont pas été détruites (voir ResolveAttack, qui accepte de cibler une case vide si elle
        // porte ce statut).
        if (session.TileEffects.GetValueOrDefault((targetX, targetY)) == TileEffect.Destructible)
        {
            throw new InvalidOperationException("Cette case est bloquée par un obstacle — détruisez-le d'abord.");
        }

        actor.X = targetX;
        actor.Y = targetY;
        session.LastMessage = $"{actor.Name} se déplace en ({targetX}, {targetY}).";

        // Voir GDD/demande utilisateur — "des monstres qui peuvent [poser des] pièges posés sur
        // les cases" : déclenché une seule fois (retiré après usage), dégâts fixes.
        if (session.TileEffects.GetValueOrDefault((targetX, targetY)) == TileEffect.Piege)
        {
            session.TileEffects.Remove((targetX, targetY));
            var trapDamage = Math.Max(2, actor.MaxHealth / 8);
            actor.CurrentHealth = Math.Max(0, actor.CurrentHealth - trapDamage);
            session.LastMessage = $"{session.LastMessage} {actor.Name} déclenche un piège et subit {trapDamage} dégâts !";
            CheckEndCondition(session);
        }
    }

    public static void ResolveAttack(CombatSession session, Combatant actor, int targetX, int targetY)
    {
        var target = session.Combatants.FirstOrDefault(c => c.IsAlive && c.X == targetX && c.Y == targetY);
        if (target is null)
        {
            // Voir GDD/demande utilisateur — "cases destructibles" : une case vide portant ce
            // statut peut être attaquée directement (pas de combattant requis) pour la détruire.
            if (session.TileEffects.GetValueOrDefault((targetX, targetY)) != TileEffect.Destructible)
            {
                throw new InvalidOperationException("Aucune cible sur cette case.");
            }

            if (Distance(actor.X, actor.Y, targetX, targetY) > actor.AttackRange)
            {
                throw new InvalidOperationException("Cible hors de portée d'attaque.");
            }

            var remainingHealth = session.DestructibleHealth.GetValueOrDefault((targetX, targetY), DestructibleTileHealth) - actor.Attack;
            if (remainingHealth <= 0)
            {
                session.TileEffects.Remove((targetX, targetY));
                session.DestructibleHealth.Remove((targetX, targetY));
                session.LastMessage = $"{actor.Name} détruit l'obstacle en ({targetX}, {targetY}).";
            }
            else
            {
                session.DestructibleHealth[(targetX, targetY)] = remainingHealth;
                session.LastMessage = $"{actor.Name} endommage l'obstacle en ({targetX}, {targetY}) ({remainingHealth} PV restants).";
            }

            return;
        }

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
        damage = ApplyAffinityBonus(session, actor, damage);
        damage = ApplyComboBonus(session, actor, target, damage, out var comboSuffix);
        damage = ApplyPassiveDamageModifiers(actor, target, damage);
        target.CurrentHealth = Math.Max(0, target.CurrentHealth - damage);
        var suffix = ElementalSuffix(multiplier);
        session.LastMessage = target.IsAlive
            ? $"{actor.Name} inflige {damage} dégâts à {target.Name}{suffix}.{comboSuffix}"
            : $"{actor.Name} inflige {damage} dégâts à {target.Name}{suffix} et le met K.O. !{comboSuffix}";
        ApplyPostDamagePassives(session, actor, target, damage);

        CheckEndCondition(session);
    }

    /// <summary>
    /// Voir GDD/demande utilisateur — "Affinité entre monstres" : +10% de dégâts quand un allié du
    /// même élément (autre que Neutre) est adjacent — récompense un positionnement groupé par
    /// élément plutôt qu'un simple bonus passif de plus.
    /// </summary>
    private static int ApplyAffinityBonus(CombatSession session, Combatant actor, int rawDamage)
    {
        var hasAffinityAlly = session.Combatants.Any(c =>
            c.IsAlive && c != actor && c.Team == actor.Team && c.Element == actor.Element
            && c.Element != Element.Neutre && Distance(c.X, c.Y, actor.X, actor.Y) == 1);

        return hasAffinityAlly ? Math.Max(1, (int)(rawDamage * 1.1)) : rawDamage;
    }

    /// <summary>
    /// Voir GDD/demande utilisateur — "Combo entre monstres", "Combo entre joueurs", "Ultime
    /// d'équipe" : un même système couvre les trois — enchaîner des coups d'ATTAQUANTS DIFFÉRENTS
    /// (monstres et/ou personnage joueur, peu importe) sur la MÊME cible sans qu'un autre
    /// combattant ne l'attaque entre-temps augmente les dégâts, jusqu'à un bonus maximal quand
    /// TOUTE l'équipe a frappé la même cible d'affilée ("ultime d'équipe"). La chaîne se
    /// réinitialise dès qu'une cible différente est attaquée.
    /// </summary>
    private static int ApplyComboBonus(CombatSession session, Combatant actor, Combatant target, int rawDamage, out string suffix)
    {
        if (session.ComboTargetId != target.Id)
        {
            session.ComboTargetId = target.Id;
            session.ComboAttackerIds.Clear();
        }

        if (session.ComboAttackerIds.Contains(actor.Id))
        {
            suffix = "";
            return rawDamage;
        }

        var hitNumber = session.ComboAttackerIds.Count + 1;
        session.ComboAttackerIds.Add(actor.Id);

        var bonusMultiplier = hitNumber switch
        {
            1 => 1.0,
            2 => 1.2,
            3 => 1.35,
            _ => 1.6,
        };

        suffix = hitNumber switch
        {
            >= 4 => " ULTIME D'EQUIPE !",
            2 => " Combo x2 !",
            3 => " Combo x3 !",
            _ => "",
        };

        return bonusMultiplier > 1.0 ? Math.Max(1, (int)(rawDamage * bonusMultiplier)) : rawDamage;
    }

    /// <summary>
    /// Voir GDD/demande utilisateur — "Compétences passives" (voir PassiveTalentCatalog) :
    /// modificateurs appliqués AVANT que les dégâts ne touchent la cible — Acharnement côté
    /// attaquant (PV bas), Bouclier côté défenseur.
    /// </summary>
    private static int ApplyPassiveDamageModifiers(Combatant actor, Combatant target, int rawDamage)
    {
        var damage = rawDamage;
        if (actor.PassiveTalent == PassiveTalentCatalog.Acharnement && actor.CurrentHealth < actor.MaxHealth * 0.3)
        {
            damage = (int)(damage * 1.3);
        }

        if (target.PassiveTalent == PassiveTalentCatalog.Bouclier)
        {
            damage = (int)(damage * 0.85);
        }

        return Math.Max(1, damage);
    }

    /// <summary>Voir GDD/demande utilisateur — "Compétences passives" : effets déclenchés APRÈS l'application des dégâts — Vol de vie côté attaquant, Contre-attaque côté défenseur (s'il a survécu).</summary>
    private static void ApplyPostDamagePassives(CombatSession session, Combatant actor, Combatant target, int damageDealt)
    {
        if (actor.PassiveTalent == PassiveTalentCatalog.VolDeVie)
        {
            var heal = Math.Max(1, damageDealt / 5);
            actor.CurrentHealth = Math.Min(actor.MaxHealth, actor.CurrentHealth + heal);
        }

        if (target.IsAlive && target.PassiveTalent == PassiveTalentCatalog.ContreAttaque && Random.Shared.NextDouble() < 0.2)
        {
            var counterDamage = Math.Max(1, damageDealt / 2);
            actor.CurrentHealth = Math.Max(0, actor.CurrentHealth - counterDamage);
            session.LastMessage = $"{session.LastMessage} {target.Name} contre-attaque pour {counterDamage} dégâts !";
        }
    }

    /// <summary>
    /// Capacité spéciale selon le type du combattant (voir GDD/demande utilisateur — "ajoute des
    /// capacités spéciales" et "il doit y avoir l'attaque spéciale (poser des bloques, soigner un
    /// allié, une grosse attaque etc) en plus de l'attaque ultime") : Soigneur soigne l'allié le
    /// plus affaibli (aucune cible à viser), Contrôleur pose un bloc sur une case vide (voir
    /// <see cref="ResolveBlockPlacement"/>), Archer transperce en ignorant la défense (portée +1),
    /// Guerrier (et tout autre type) déclenche un coup puissant à dégâts majorés. Toujours
    /// utilisable quel que soit le niveau — voir <see cref="ResolveUltimateAbility"/> pour
    /// l'attaque ultime, une action SÉPARÉE (cooldown propre) débloquée au niveau max, en plus de
    /// celle-ci et non à sa place.
    /// </summary>
    public static void ResolveSpecialAbility(CombatSession session, Combatant actor, int targetX, int targetY)
    {
        if (actor.SpecialAbilityCooldownRemaining > 0)
        {
            throw new InvalidOperationException(
                $"Capacité spéciale en recharge ({actor.SpecialAbilityCooldownRemaining} tour(s) restant(s)).");
        }

        if (actor.Type == MonsterType.Soigneur)
        {
            var lowest = session.Combatants
                .Where(c => c.IsAlive && c.Team == actor.Team)
                .OrderBy(c => (float)c.CurrentHealth / c.MaxHealth)
                .FirstOrDefault();

            if (lowest is null || lowest.CurrentHealth >= lowest.MaxHealth)
            {
                session.LastMessage = $"{actor.Name} ne trouve personne à soigner.";
                actor.SpecialAbilityCooldownRemaining = SpecialAbilityCooldownTurns;
                return;
            }

            var healAmount = Math.Max(1, lowest.MaxHealth * 3 / 10);
            lowest.CurrentHealth = Math.Min(lowest.MaxHealth, lowest.CurrentHealth + healAmount);
            session.LastMessage = $"{actor.Name} soigne {lowest.Name} de {healAmount} PV.";
            actor.SpecialAbilityCooldownRemaining = SpecialAbilityCooldownTurns;
            return;
        }

        if (actor.Type == MonsterType.Controleur)
        {
            ResolveBlockPlacement(session, actor, targetX, targetY);
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

        actor.SpecialAbilityCooldownRemaining = SpecialAbilityCooldownTurns;

        var multiplier = ElementalMultiplier(actor.Element, target.Element);
        int damage;
        string verb;
        if (actor.Type == MonsterType.Archer)
        {
            // Tir perçant : ignore entièrement la Défense (contrepartie de la portée +1).
            damage = Math.Max(2, (int)(actor.Attack * multiplier));
            verb = "transperce";
        }
        else if (actor.Type == MonsterType.Mage)
        {
            // Voir GDD/demande utilisateur — "capacités élémentaires (lave/glace) pour d'autres
            // types de monstres, en plus des ultimes" : même formule que le coup puissant
            // générique, mais laisse en plus une case de lave/glace sous la cible (voir
            // TryPlaceElementalTileUnderTarget) selon l'Element du Mage lui-même.
            damage = Math.Max(3, (int)((actor.Attack - target.Defense / 2) * 1.8 * multiplier));
            verb = "frappe d'un sort élémentaire";
        }
        else
        {
            damage = Math.Max(3, (int)((actor.Attack - target.Defense / 2) * 1.8 * multiplier));
            verb = "frappe (coup puissant)";
        }

        damage = ApplyAffinityBonus(session, actor, damage);
        damage = ApplyComboBonus(session, actor, target, damage, out var comboSuffix);
        damage = ApplyPassiveDamageModifiers(actor, target, damage);
        target.CurrentHealth = Math.Max(0, target.CurrentHealth - damage);
        var suffix = ElementalSuffix(multiplier);
        session.LastMessage = target.IsAlive
            ? $"{actor.Name} {verb} {target.Name} pour {damage} dégâts{suffix}.{comboSuffix}"
            : $"{actor.Name} {verb} {target.Name} pour {damage} dégâts{suffix} et le met K.O. !{comboSuffix}";
        ApplyPostDamagePassives(session, actor, target, damage);

        if (actor.Type == MonsterType.Mage)
        {
            TryPlaceElementalTileUnderTarget(session, actor, target);
        }

        CheckEndCondition(session);
    }

    /// <summary>
    /// Voir GDD/demande utilisateur — "capacités élémentaires (lave/glace) pour d'autres types de
    /// monstres, en plus des ultimes" : réutilise les cases Lave/Glace déjà générées au hasard par
    /// <see cref="ScatterTileEffects"/> (brûlure à chaque tour passé dessus / +1 case de
    /// déplacement en partant de la case), mais posées volontairement sous la cible par un Mage,
    /// selon son propre Element (Feu -&gt; Lave, Glace -&gt; Glace). Aucun effet pour les autres
    /// Elements — pas de case "élémentaire" correspondante — et ne remplace jamais une case qui
    /// porte déjà un effet (pour ne pas écraser un bloc du Contrôleur, par ex.). Purement en plus
    /// des dégâts déjà infligés : échoue silencieusement (la cible est peut-être déjà K.O.).
    /// </summary>
    private static void TryPlaceElementalTileUnderTarget(CombatSession session, Combatant actor, Combatant target)
    {
        if (!target.IsAlive || session.TileEffects.ContainsKey((target.X, target.Y)))
        {
            return;
        }

        TileEffect? effect = actor.Element switch
        {
            Element.Feu => TileEffect.Lave,
            Element.Glace => TileEffect.Glace,
            _ => null,
        };

        if (effect is null)
        {
            return;
        }

        session.TileEffects[(target.X, target.Y)] = effect.Value;
        session.LastMessage += effect == TileEffect.Lave ? " Le sol s'embrase sous lui !" : " Le sol gèle sous lui !";
    }

    /// <summary>
    /// Voir GDD/demande utilisateur — "poser des bloques" (capacité du Contrôleur) : place un
    /// obstacle Destructible (même mécanique que les cases générées en PvE, voir
    /// <see cref="ScatterTileEffects"/>) sur une case vide à portée, infranchissable tant qu'il
    /// n'est pas détruit (voir <see cref="ResolveMove"/>/<see cref="ResolveAttack"/>).
    /// </summary>
    private static void ResolveBlockPlacement(CombatSession session, Combatant actor, int targetX, int targetY)
    {
        if (!IsWithinGrid(targetX, targetY))
        {
            throw new InvalidOperationException("Case hors de la grille.");
        }

        if (Distance(actor.X, actor.Y, targetX, targetY) > PlacedBlockRange)
        {
            throw new InvalidOperationException("Case hors de portée.");
        }

        if (session.Combatants.Any(c => c.IsAlive && c.X == targetX && c.Y == targetY))
        {
            throw new InvalidOperationException("Impossible de poser un bloc sur une case occupée.");
        }

        if (session.TileEffects.ContainsKey((targetX, targetY)))
        {
            throw new InvalidOperationException("Cette case porte déjà un effet.");
        }

        session.TileEffects[(targetX, targetY)] = TileEffect.Destructible;
        session.DestructibleHealth[(targetX, targetY)] = DestructibleTileHealth;
        session.LastMessage = $"{actor.Name} pose un bloc en ({targetX}, {targetY}).";
        actor.SpecialAbilityCooldownRemaining = SpecialAbilityCooldownTurns;
    }

    /// <summary>Cherche une case valide pour <see cref="ResolveBlockPlacement"/> autour de <paramref name="actor"/> — utilisé par l'IA (voir <see cref="RunAiTurn"/>), qui ne peut pas cibler la case de l'ennemi comme pour les autres capacités.</summary>
    private static (int X, int Y)? FindBlockPlacementTile(CombatSession session, Combatant actor)
    {
        var candidates = new List<(int X, int Y)>();
        for (var x = Math.Max(0, actor.X - PlacedBlockRange); x <= Math.Min(CombatSession.GridWidth - 1, actor.X + PlacedBlockRange); x++)
        {
            for (var y = Math.Max(0, actor.Y - PlacedBlockRange); y <= Math.Min(CombatSession.GridHeight - 1, actor.Y + PlacedBlockRange); y++)
            {
                if (Distance(actor.X, actor.Y, x, y) > PlacedBlockRange)
                {
                    continue;
                }

                if (session.Combatants.Any(c => c.IsAlive && c.X == x && c.Y == y) || session.TileEffects.ContainsKey((x, y)))
                {
                    continue;
                }

                candidates.Add((x, y));
            }
        }

        return candidates.Count == 0 ? null : candidates[Random.Shared.Next(candidates.Count)];
    }

    /// <summary>
    /// Voir GDD/demande utilisateur — "il doit y avoir l'attaque spéciale ... en plus de
    /// l'attaque ultime si le monstre est lvl max" : action séparée de
    /// <see cref="ResolveSpecialAbility"/> (pas un remplacement/relabel — les deux restent
    /// utilisables), débloquée uniquement à <c>MonsterProgressionService.MaxLevel</c>, avec son
    /// propre cooldown (<see cref="Combatant.UltimateAbilityCooldownRemaining"/>). Même
    /// flaveur par type que la capacité spéciale (Soigneur soigne, Archer transperce, le reste
    /// frappe fort) mais nettement amplifiée — y compris pour le Contrôleur, dont la capacité
    /// spéciale (pose de bloc) n'inflige elle-même aucun dégât.
    /// </summary>
    public static void ResolveUltimateAbility(CombatSession session, Combatant actor, int targetX, int targetY)
    {
        if (actor.Level < MonsterProgressionService.MaxLevel)
        {
            throw new InvalidOperationException("L'attaque ultime n'est débloquée qu'au niveau max.");
        }

        if (actor.UltimateAbilityCooldownRemaining > 0)
        {
            throw new InvalidOperationException(
                $"Attaque ultime en recharge ({actor.UltimateAbilityCooldownRemaining} tour(s) restant(s)).");
        }

        if (actor.Type == MonsterType.Soigneur)
        {
            var lowest = session.Combatants
                .Where(c => c.IsAlive && c.Team == actor.Team)
                .OrderBy(c => (float)c.CurrentHealth / c.MaxHealth)
                .FirstOrDefault();

            actor.UltimateAbilityCooldownRemaining = UltimateAbilityCooldownTurns;

            if (lowest is null || lowest.CurrentHealth >= lowest.MaxHealth)
            {
                session.LastMessage = $"{actor.Name} ne trouve personne à soigner (ultime).";
                return;
            }

            var healAmount = lowest.MaxHealth - lowest.CurrentHealth;
            lowest.CurrentHealth = lowest.MaxHealth;
            session.LastMessage = $"{actor.Name} soigne entièrement {lowest.Name} ({healAmount} PV, ultime) !";
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

        actor.UltimateAbilityCooldownRemaining = UltimateAbilityCooldownTurns;

        const double ultimateMultiplier = 1.6;
        var multiplier = ElementalMultiplier(actor.Element, target.Element);
        int damage;
        string verb;
        if (actor.Type == MonsterType.Archer)
        {
            damage = Math.Max(2, (int)(actor.Attack * multiplier * ultimateMultiplier));
            verb = "transperce (ultime)";
        }
        else if (actor.Type == MonsterType.Mage)
        {
            damage = Math.Max(3, (int)((actor.Attack - target.Defense / 2) * 1.8 * multiplier * ultimateMultiplier));
            verb = "frappe d'un sort élémentaire (ultime)";
        }
        else
        {
            damage = Math.Max(3, (int)((actor.Attack - target.Defense / 2) * 1.8 * multiplier * ultimateMultiplier));
            verb = "frappe (ultime)";
        }

        damage = ApplyAffinityBonus(session, actor, damage);
        damage = ApplyComboBonus(session, actor, target, damage, out var comboSuffix);
        damage = ApplyPassiveDamageModifiers(actor, target, damage);
        target.CurrentHealth = Math.Max(0, target.CurrentHealth - damage);
        var suffix = ElementalSuffix(multiplier);
        session.LastMessage = target.IsAlive
            ? $"{actor.Name} {verb} {target.Name} pour {damage} dégâts{suffix}.{comboSuffix}"
            : $"{actor.Name} {verb} {target.Name} pour {damage} dégâts{suffix} et le met K.O. !{comboSuffix}";
        ApplyPostDamagePassives(session, actor, target, damage);

        if (actor.Type == MonsterType.Mage)
        {
            TryPlaceElementalTileUnderTarget(session, actor, target);
        }

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

        // Voir GDD/demande utilisateur — bestiaire étendu (nouveaux "types"), même principe de
        // triangle simple qu'au-dessus plutôt qu'un tableau exhaustif entièrement recroisé.
        [Element.Poison] = [Element.Nature, Element.Eau],
        [Element.Metal] = [Element.Glace, Element.Cristal],
        [Element.Spectre] = [Element.Lumiere, Element.Metal],
        [Element.Dragon] = [Element.Poison, Element.Metal],
        [Element.Cristal] = [Element.Foudre, Element.Spectre],
        [Element.Arcane] = [Element.Dragon, Element.Cristal],
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
            var next = session.CurrentCombatant!;
            if (next.IsAlive)
            {
                session.TurnStartedAtUtc = DateTime.UtcNow;
                if (next.SpecialAbilityCooldownRemaining > 0)
                {
                    next.SpecialAbilityCooldownRemaining--;
                }

                if (next.UltimateAbilityCooldownRemaining > 0)
                {
                    next.UltimateAbilityCooldownRemaining--;
                }

                // Voir GDD/demande utilisateur — "Compétences passives" : Régénération, déclenchée
                // en tout début de tour plutôt que sur une action précise.
                if (next.PassiveTalent == PassiveTalentCatalog.Regeneration && next.CurrentHealth < next.MaxHealth)
                {
                    var regen = Math.Max(1, next.MaxHealth / 20);
                    next.CurrentHealth = Math.Min(next.MaxHealth, next.CurrentHealth + regen);
                }

                // Voir GDD/demande utilisateur — "cases de lave" : dégâts à chaque tour passé dessus.
                if (session.TileEffects.GetValueOrDefault((next.X, next.Y)) == TileEffect.Lave && next.IsAlive)
                {
                    var lavaDamage = Math.Max(2, next.MaxHealth / 12);
                    next.CurrentHealth = Math.Max(0, next.CurrentHealth - lavaDamage);
                    session.LastMessage = $"{next.Name} brûle sur la lave et subit {lavaDamage} dégâts !";
                    CheckEndCondition(session);
                }

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
        // Voir GDD/demande utilisateur — "cooldown pour le spécial" ET "certains mobs sont
        // invincibles" (deux demandes liées : un Soigneur sauvage seul sur son équipe se soignait
        // LUI-MÊME dès qu'il encaissait des dégâts, à chaque tour, sans jamais pouvoir mourir —
        // c'était la vraie cause des mobs "invincibles" signalés). Le cooldown throttle
        // maintenant ce comportement ; l'IA doit donc vérifier qu'elle n'est pas en recharge
        // avant de choisir la capacité spéciale, sous peine de faire planter la résolution du
        // tour (ResolveSpecialAbility lève une exception si en recharge).
        var canUseSpecial = actor.SpecialAbilityCooldownRemaining == 0;

        // IA Soigneur (voir GDD — types de monstres) : priorité à soigner un allié blessé plutôt
        // qu'attaquer, tant qu'il y en a un et que la capacité n'est pas en recharge — sinon se
        // rabat sur le comportement standard (attaquer/se rapprocher) comme les autres types.
        if (actor.Type == MonsterType.Soigneur && canUseSpecial
            && session.Combatants.Any(c => c.IsAlive && c.Team == actor.Team && c.CurrentHealth < c.MaxHealth))
        {
            ResolveSpecialAbility(session, actor, actor.X, actor.Y);
            return;
        }

        // IA Contrôleur : pose un bloc sur une case vide de temps en temps plutôt qu'à chaque
        // tour disponible (voir wantsSpecial plus bas pour les autres types) — cible une case
        // vide autour de lui (voir FindBlockPlacementTile), PAS la case de l'ennemi comme les
        // autres capacités spéciales (ResolveBlockPlacement refuserait une case occupée).
        if (actor.Type == MonsterType.Controleur && canUseSpecial && Random.Shared.NextDouble() < 0.4
            && FindBlockPlacementTile(session, actor) is { } blockTile)
        {
            ResolveSpecialAbility(session, actor, blockTile.X, blockTile.Y);
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
            // l'attaque de base totalement obsolète. Dans les deux cas, seulement si disponible.
            var mustUseSpecial = distance > actor.AttackRange;
            // Contrôleur exclu ici aussi (voir plus haut) : sa capacité spéciale cible une case
            // vide, pas l'ennemi — l'appeler avec target.X/target.Y planterait (case occupée).
            var wantsSpecial = actor.Type is not (MonsterType.Soigneur or MonsterType.Controleur) && Random.Shared.NextDouble() < 0.35;

            if (canUseSpecial && (mustUseSpecial || wantsSpecial))
            {
                ResolveSpecialAbility(session, actor, target.X, target.Y);
            }
            else if (distance <= actor.AttackRange)
            {
                ResolveAttack(session, actor, target.X, target.Y);
            }

            // Sinon (mustUseSpecial mais en recharge, hors de portée normale) : rien de valide
            // cette fois-ci, l'IA passe simplement son tour plutôt que de planter.
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
