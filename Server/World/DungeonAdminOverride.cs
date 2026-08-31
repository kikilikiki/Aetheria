namespace Aetheria.Server.World;

/// <summary>
/// Voir demande utilisateur — "ajoute une commande admin et un bouton dans le panel pour faire
/// apparaître un donjon spécifique" : 3ᵉ portail temporaire, visible par tous, jusqu'à la
/// rotation horaire suivante. En mémoire uniquement (repart à zéro au redémarrage), comme les
/// autres états globaux éphémères de cette échelle (voir <see cref="GlobalEventService"/>).
/// </summary>
public static class DungeonAdminOverride
{
    private static int? _forcedDungeonId;
    private static long _forcedUntilHourBucket = -1;

    private static long CurrentHourBucket => DateTime.UtcNow.Ticks / TimeSpan.FromHours(1).Ticks;

    /// <summary>Force ce donjon comme 3ᵉ portail jusqu'à la fin de l'heure UTC courante incluse.</summary>
    public static void SetForCurrentHour(int dungeonId)
    {
        _forcedDungeonId = dungeonId;
        _forcedUntilHourBucket = CurrentHourBucket;
    }

    /// <summary>Id du donjon forcé s'il est encore valide pour l'heure demandée, sinon <c>null</c>.</summary>
    public static int? ActiveDungeonId(long hourBucket) =>
        _forcedDungeonId is { } id && hourBucket <= _forcedUntilHourBucket ? id : null;
}
