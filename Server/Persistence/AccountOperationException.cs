namespace Aetheria.Server.Persistence;

/// <summary>
/// Échec attendu d'une opération de compte (identifiants invalides, pseudo déjà pris,
/// compte banni, ...) — distingué des exceptions techniques pour que l'API HTTP puisse
/// répondre avec un message utilisateur propre plutôt qu'une erreur 500.
/// </summary>
public sealed class AccountOperationException(string message) : Exception(message);
