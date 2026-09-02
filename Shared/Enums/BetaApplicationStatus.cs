namespace Aetheria.Shared.Enums;

/// <summary>
/// État d'une candidature bêta-testeur soumise depuis le portail web (voir
/// <c>Aetheria.Web</c> — formulaire <c>/beta</c> et page d'administration <c>/admin/candidatures</c>).
/// Même logique de modération à trois états que les signalements de joueurs
/// (<see cref="Aetheria.Database.Entities.ReportEntity"/>), mais avec un refus explicite en plus
/// du « traité ».
/// </summary>
public enum BetaApplicationStatus
{
    /// <summary>En attente de traitement par un membre du staff.</summary>
    Pending,

    /// <summary>Candidature acceptée — le candidat rejoint la bêta.</summary>
    Approved,

    /// <summary>Candidature refusée (voir <c>AdminNote</c> pour la raison).</summary>
    Rejected,
}
