namespace Aetheria.Server.Discord;

/// <summary>
/// Charge un fichier <c>.env</c> (paires <c>CLE=valeur</c>, lignes <c>#</c> ignorées) dans les
/// variables d'environnement du processus, sans dépendance NuGet supplémentaire pour un besoin
/// aussi simple. Cherche <c>.env</c> dans le répertoire courant puis ses parents (jusqu'à la
/// racine du dépôt) car le répertoire de travail au lancement varie selon la méthode de
/// démarrage (<c>dotnet run</c> vs exécutable publié). Ne remplace jamais une variable déjà
/// définie dans l'environnement réel (permet de surcharger `.env` sans le modifier).
/// </summary>
public static class DotEnv
{
    public static void LoadIfPresent()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        for (var depth = 0; depth < 6 && directory is not null; depth++, directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, ".env");
            if (!File.Exists(path))
            {
                continue;
            }

            foreach (var rawLine in File.ReadAllLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = line[..separatorIndex].Trim();
                var value = line[(separatorIndex + 1)..].Trim().Trim('"');

                if (Environment.GetEnvironmentVariable(key) is null)
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }

            return;
        }
    }
}
