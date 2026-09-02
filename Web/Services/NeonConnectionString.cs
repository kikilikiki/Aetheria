using System.Text;

namespace Aetheria.Web.Services;

/// <summary>
/// Neon (comme la plupart des fournisseurs PostgreSQL managés) donne sa chaîne de connexion sous
/// forme d'URL <c>postgresql://user:pass@host/db?sslmode=require&amp;channel_binding=require</c>,
/// alors que Npgsql attend un format clé-valeur <c>Host=…;Username=…;Password=…;Database=…</c>.
/// Ce helper convertit l'un vers l'autre pour qu'on puisse coller la chaîne Neon telle quelle
/// dans <c>AETHERIA_DB_CONNECTION</c>. Une chaîne déjà au format clé-valeur (ou une chaîne SQLite
/// <c>Data Source=…</c>) est renvoyée inchangée.
/// </summary>
public static class NeonConnectionString
{
    public static string Normalize(string raw)
    {
        raw = raw.Trim();

        if (!raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return raw;
        }

        var uri = new Uri(raw);
        var userInfo = uri.UserInfo.Split(':', 2);

        var builder = new StringBuilder();
        builder.Append("Host=").Append(uri.Host).Append(';');
        if (!uri.IsDefaultPort && uri.Port > 0)
        {
            builder.Append("Port=").Append(uri.Port).Append(';');
        }

        builder.Append("Database=").Append(uri.AbsolutePath.Trim('/')).Append(';');
        builder.Append("Username=").Append(Uri.UnescapeDataString(userInfo[0])).Append(';');
        if (userInfo.Length > 1)
        {
            builder.Append("Password=").Append(Uri.UnescapeDataString(userInfo[1])).Append(';');
        }

        var query = ParseQuery(uri.Query);

        var sslMode = query.GetValueOrDefault("sslmode");
        if (!string.IsNullOrEmpty(sslMode))
        {
            // Npgsql : "require" -> "Require" (majuscule). Neon exige SSL.
            builder.Append("SSL Mode=")
                .Append(char.ToUpperInvariant(sslMode[0]))
                .Append(sslMode.AsSpan(1))
                .Append(';');
        }
        else
        {
            builder.Append("SSL Mode=Require;");
        }

        if (string.Equals(query.GetValueOrDefault("channel_binding"), "require", StringComparison.OrdinalIgnoreCase))
        {
            builder.Append("Channel Binding=Require;");
        }

        return builder.ToString();
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            result[Uri.UnescapeDataString(kv[0])] = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
        }

        return result;
    }
}
