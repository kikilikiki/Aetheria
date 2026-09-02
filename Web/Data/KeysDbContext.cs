using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Web.Data;

/// <summary>
/// Contexte EF Core minimal dédié au stockage des clés Data Protection (une seule table,
/// <c>DataProtectionKeys</c>), sur la même base que <c>AetheriaDbContext</c>. Volontairement séparé
/// pour ne pas toucher au modèle partagé du jeu : la table est créée à la main au démarrage
/// (<c>CREATE TABLE IF NOT EXISTS</c>, voir <c>Program.cs</c>), pas via une migration.
/// </summary>
public sealed class KeysDbContext(DbContextOptions<KeysDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
}
