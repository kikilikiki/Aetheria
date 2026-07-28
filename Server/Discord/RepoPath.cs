namespace Aetheria.Server.Discord;

/// <summary>
/// Localise un fichier d'état à la racine du dépôt (dossier <c>.git</c>) en remontant depuis le
/// répertoire courant — partagé par les différents mécanismes liés aux annonces Discord
/// (<see cref="GitChangelogAnnouncer"/>, <see cref="PendingChangesLog"/>,
/// <see cref="DailyDigestScheduler"/>) pour ne pas dupliquer la même recherche trois fois.
/// </summary>
internal static class RepoPath
{
    public static string Resolve(string fileName)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());

        for (var depth = 0; depth < 8 && dir is not null; depth++, dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return Path.Combine(dir.FullName, fileName);
            }
        }

        // Dépôt introuvable (ex. exécutable publié sans .git) : à côté du binaire, mieux que planter.
        return Path.Combine(Directory.GetCurrentDirectory(), fileName);
    }
}
