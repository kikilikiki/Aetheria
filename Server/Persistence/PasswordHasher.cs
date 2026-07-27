namespace Aetheria.Server.Persistence;

/// <summary>
/// Hachage de mot de passe via BCrypt (sel intégré, coût de calcul volontairement élevé
/// pour ralentir les attaques par force brute) — jamais de mot de passe en clair côté serveur.
/// </summary>
public static class PasswordHasher
{
    private const int WorkFactor = 12;

    public static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public static bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}
