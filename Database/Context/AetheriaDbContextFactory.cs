using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Aetheria.Database.Context;

/// <summary>
/// Permet aux outils EF Core (<c>dotnet ef migrations add</c>, <c>dotnet ef database update</c>)
/// de construire un <see cref="AetheriaDbContext"/> sans dépendre du démarrage complet du
/// serveur. La chaîne de connexion réelle (production) est fournie par Server via injection
/// de dépendances ; celle-ci ne sert qu'aux outils en ligne de commande et au développement local.
/// </summary>
public sealed class AetheriaDbContextFactory : IDesignTimeDbContextFactory<AetheriaDbContext>
{
    public AetheriaDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("AETHERIA_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=aetheria;Username=aetheria;Password=aetheria_dev_only";

        var optionsBuilder = new DbContextOptionsBuilder<AetheriaDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AetheriaDbContext(optionsBuilder.Options);
    }
}
