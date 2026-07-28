namespace Aetheria.Server.Discord;

/// <summary>
/// Fichier accumulant les changements du jour avant l'envoi groupé quotidien (voir GDD/demande
/// utilisateur — "les modifications sont notées dans un fichier puis tous les jours à 23h elles
/// sont postées, après l'envoi le fichier se retransforme en rien du tout, et si le fichier est
/// vide le message ne doit pas être transmis"). Remplace l'annonce immédiate à chaque démarrage
/// par une annonce unique par jour — voir <see cref="DailyDigestScheduler"/>.
/// </summary>
public static class PendingChangesLog
{
    private const string FileName = ".discord-pending-changes.txt";

    public static void Append(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        File.AppendAllLines(RepoPath.Resolve(FileName), lines);
    }

    /// <summary>Lit le contenu actuel puis vide le fichier (voir demande utilisateur — "le fichier se retransforme en rien du tout" après l'envoi).</summary>
    public static IReadOnlyList<string> ReadAndClear()
    {
        var path = RepoPath.Resolve(FileName);
        if (!File.Exists(path))
        {
            return [];
        }

        var lines = File.ReadAllLines(path).Where(l => l.Trim().Length > 0).ToList();
        File.WriteAllText(path, string.Empty);
        return lines;
    }
}
