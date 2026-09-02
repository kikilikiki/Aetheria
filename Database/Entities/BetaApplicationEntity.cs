using Aetheria.Shared.Enums;

namespace Aetheria.Database.Entities;

/// <summary>
/// Candidature bêta-testeur soumise depuis le portail web (table <c>BetaApplications</c>) — voir
/// <c>Aetheria.Web</c>, formulaire <c>/beta</c>. À la soumission, un salon Discord privé
/// (« ticket ») est créé pour le candidat et le staff ; un membre du staff valide ou refuse
/// ensuite la candidature depuis <c>/admin/candidatures</c>.
///
/// Champs de contexte dénormalisés (<see cref="Username"/>) pour un affichage immédiat côté admin
/// sans jointure, même approche que <see cref="ReportEntity"/>.
/// </summary>
public sealed class BetaApplicationEntity
{
    public Guid Id { get; set; }

    /// <summary>Compte Aetheria auteur de la candidature.</summary>
    public Guid UserId { get; set; }
    public UserEntity? User { get; set; }

    /// <summary>Pseudo du compte au moment de la candidature (dénormalisé).</summary>
    public required string Username { get; set; }

    // --- Réponses du formulaire ---

    /// <summary>Pseudo Discord saisi par le candidat (ou récupéré du compte lié).</summary>
    public string DiscordHandle { get; set; } = string.Empty;

    /// <summary>Email de contact (prérempli depuis le compte, modifiable).</summary>
    public string ContactEmail { get; set; } = string.Empty;

    /// <summary>Pseudo souhaité / utilisé en jeu.</summary>
    public string InGamePseudo { get; set; } = string.Empty;

    /// <summary>Plateforme de jeu (« Windows » / « Linux »).</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>Configuration matérielle décrite par le candidat (processeur, carte graphique, RAM…).</summary>
    public string HardwareSpecs { get; set; } = string.Empty;

    /// <summary>Remarques libres (facultatif).</summary>
    public string? Notes { get; set; }

    // --- Résolution Discord ---

    /// <summary>Identifiant Discord (snowflake) résolu via le bot au moment de la soumission.</summary>
    public string? ResolvedDiscordUserId { get; set; }

    /// <summary>Identifiant du salon Discord (« ticket ») créé pour cette candidature, si la création a réussi.</summary>
    public string? DiscordTicketChannelId { get; set; }

    /// <summary>
    /// Identifiant du message de récapitulatif posté dans le ticket (celui qui porte les boutons
    /// Accepter / Refuser) — sert à désactiver les boutons une fois la décision prise.
    /// </summary>
    public string? DiscordTicketMessageId { get; set; }

    /// <summary>
    /// Renseigné par le serveur de jeu (voir <c>Server/Discord/BetaTicketProcessor</c>) une fois la
    /// candidature traitée : vérification de la présence Discord + création du salon, ou refus
    /// automatique si le pseudo Discord est introuvable. <c>null</c> = pas encore traitée.
    /// (Le portail web ne parle jamais à Discord — l'IP partagée de Render est rate-limitée.)
    /// </summary>
    public DateTime? ProcessedAtUtc { get; set; }

    /// <summary>
    /// Dernier <see cref="Status"/> déjà répercuté sur le salon Discord par le serveur de jeu.
    /// Quand il diffère de <see cref="Status"/>, le serveur de jeu poste la mise à jour (accepté /
    /// refusé) dans le ticket puis réaligne cette valeur.
    /// </summary>
    public BetaApplicationStatus? SyncedStatus { get; set; }

    // --- Modération ---

    public BetaApplicationStatus Status { get; set; } = BetaApplicationStatus.Pending;

    /// <summary>Raison / commentaire du staff (surtout en cas de refus).</summary>
    public string? AdminNote { get; set; }

    public string? ReviewedByUsername { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
