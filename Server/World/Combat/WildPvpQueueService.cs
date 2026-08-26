namespace Aetheria.Server.World.Combat;

/// <summary>
/// Voir Docs/Idees.md — "PvP sauvage" : file d'attente en mémoire pour les duels en zone à
/// risque (voir <c>Server/Program.cs</c>, vérification de zone à l'inscription). Même mécanique
/// que <see cref="KingdomWarQueueService"/> mais sans distinction de royaume — n'importe quels
/// deux joueurs déjà dans une zone à risque forment un duel dès que le second rejoint la file
/// (voir <c>CombatService.StartWildPvpDuelAsync</c>). Volontairement basé sur la file plutôt
/// qu'une attaque directe non consentie : couvre "combat PvP direct sans passer par l'arène"
/// tout en évitant le harcèlement d'un joueur qui ne veut pas se battre (aucun système de
/// consentement/notification n'existe encore pour une vraie embuscade — voir Docs/Idees.md).
/// </summary>
public sealed class WildPvpQueueService
{
    public sealed record WildTicket(Guid CharacterId, Guid UserId);

    private readonly object _lock = new();
    private readonly List<WildTicket> _waiting = [];
    private readonly Dictionary<Guid, Guid> _matchedCombatByCharacter = new();

    /// <summary>Ajoute un ticket et retourne la paire si un autre joueur attendait déjà, sinon <c>null</c> (le ticket reste en attente).</summary>
    public List<WildTicket>? EnqueueAndTryMatch(WildTicket ticket)
    {
        lock (_lock)
        {
            if (_waiting.Any(t => t.CharacterId == ticket.CharacterId))
            {
                return null;
            }

            var opponent = _waiting.FirstOrDefault();
            if (opponent is null)
            {
                _waiting.Add(ticket);
                return null;
            }

            _waiting.Remove(opponent);
            return [opponent, ticket];
        }
    }

    public void RecordMatch(IEnumerable<Guid> characterIds, Guid combatId)
    {
        lock (_lock)
        {
            foreach (var id in characterIds)
            {
                _matchedCombatByCharacter[id] = combatId;
            }
        }
    }

    public bool TryConsumeMatch(Guid characterId, out Guid combatId)
    {
        lock (_lock)
        {
            return _matchedCombatByCharacter.Remove(characterId, out combatId);
        }
    }

    public void Cancel(Guid characterId)
    {
        lock (_lock)
        {
            _waiting.RemoveAll(t => t.CharacterId == characterId);
        }
    }
}
