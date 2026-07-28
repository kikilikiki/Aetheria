using System.Text;
using System.Text.Json.Serialization;
using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Networking;
using Aetheria.Server.Persistence;
using Aetheria.Server.World;
using Aetheria.Server.World.Combat;
using Aetheria.Shared;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Aetheria.Shared.Models.Account;
using Aetheria.Shared.Models.Admin;
using Aetheria.Shared.Models.Combat;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

Console.OutputEncoding = Encoding.UTF8;

var builder = WebApplication.CreateBuilder(args);

var connectionString = Environment.GetEnvironmentVariable("AETHERIA_DB_CONNECTION");
var usingInMemoryDatabase = string.IsNullOrWhiteSpace(connectionString);

builder.Services.AddPooledDbContextFactory<AetheriaDbContext>(options =>
{
    if (usingInMemoryDatabase)
    {
        options.UseInMemoryDatabase("aetheria-dev");
    }
    else
    {
        options.UseNpgsql(connectionString);
    }
});

builder.Services.AddSingleton<SessionTokenStore>();
builder.Services.AddSingleton<CombatSessionStore>();
builder.WebHost.UseUrls($"http://0.0.0.0:{GameInfo.DefaultAccountApiPort}");

// Enums échangés en toutes lettres ("Guerrier", "Feu", ...) plutôt qu'en entiers opaques :
// plus lisible pour tout client de l'API (Launcher, outils d'admin, tests manuels).
// IgnoreCycles : les catalogues (Recipes -> Ingredients -> Recipe -> ...) exposent des
// entités EF Core directement plutôt que des DTO dédiés ; à terme ces endpoints de lecture
// devraient projeter vers des DTO Shared/Models pour ne pas dépendre de la forme des entités.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

var app = builder.Build();

app.Logger.LogInformation("{Name} Server v{Version}", GameInfo.Name, GameInfo.Version);

if (usingInMemoryDatabase)
{
    app.Logger.LogWarning(
        "AETHERIA_DB_CONNECTION non défini : utilisation d'une base PostgreSQL en mémoire, " +
        "réservée au développement local. Les données seront perdues à l'arrêt du serveur.");
}

var dbFactory = app.Services.GetRequiredService<IDbContextFactory<AetheriaDbContext>>();
await using (var db = await dbFactory.CreateDbContextAsync())
{
    if (usingInMemoryDatabase)
    {
        await db.Database.EnsureCreatedAsync();
    }
    else
    {
        await db.Database.MigrateAsync();
    }

    await DatabaseSeeder.SeedAsync(db);
    await AdminAccountSeeder.SeedAsync(db);
    await MonsterCatalogSeeder.SeedAsync(db);
    await DungeonSeeder.SeedAsync(db);
    await ProfessionCatalogSeeder.SeedAsync(db);
    await TerritorySeeder.SeedAsync(db);
    await SeasonSeeder.SeedAsync(db);
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", version = GameInfo.Version }));

app.MapPost("/api/account/register", async (RegisterRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var accountService = new AccountService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        var userId = await accountService.RegisterAsync(request);
        return Results.Ok(new { userId });
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

app.MapPost("/api/account/login", async (LoginRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var accountService = new AccountService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        var response = await accountService.LoginAsync(request);
        return Results.Ok(response);
    }
    catch (AccountOperationException ex)
    {
        return Results.Json(new ApiError { Message = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
    }
});

// Liste des personnages du compte authentifié — utilisé par le Client pour l'écran de
// sélection/création en jeu (voir GDD : la création ne se fait plus dans le Launcher).
app.MapGet("/api/characters/mine", async (string sessionToken) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var characterService = new CharacterService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        var characters = await characterService.GetMineAsync(sessionToken);
        return Results.Ok(characters);
    }
    catch (AccountOperationException ex)
    {
        return Results.Json(new ApiError { Message = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
    }
});

app.MapPost("/api/characters", async (CreateCharacterRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var characterService = new CharacterService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        var summary = await characterService.CreateAsync(request);
        return Results.Ok(summary);
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

app.MapGet("/api/monsters/species", async () =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var species = await db.MonsterSpecies.ToListAsync();
    return Results.Ok(species.Select(ToSpeciesData));
});

// CRUD destiné au MonsterEditor. Expose Shared.Models.MonsterSpeciesData (pas l'entité EF Core)
// pour que l'outil n'ait besoin de référencer que Shared, pas Database — voir Docs/README.md
// pour le graphe de dépendances. Pas d'authentification admin dédiée pour cette première
// version (outil interne supposé lancé contre un serveur de confiance) — à sécuriser avant
// tout déploiement réel.
app.MapPost("/api/monsters/species", async (MonsterSpeciesData species) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var entity = new MonsterSpeciesEntity
    {
        Name = species.Name,
        Element = species.Element,
        BaseRarity = species.BaseRarity,
        Habitat = species.Habitat,
        Lore = species.Lore,
        BaseStats = species.BaseStats,
        EvolvesIntoSpeciesId = species.EvolvesIntoSpeciesId,
        EvolutionLevel = species.EvolutionLevel,
    };

    db.MonsterSpecies.Add(entity);
    await db.SaveChangesAsync();
    return Results.Ok(ToSpeciesData(entity));
});

app.MapPut("/api/monsters/species/{id:int}", async (int id, MonsterSpeciesData updated) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var existing = await db.MonsterSpecies.FirstOrDefaultAsync(s => s.Id == id);
    if (existing is null)
    {
        return Results.NotFound(new ApiError { Message = "Espèce introuvable." });
    }

    existing.Name = updated.Name;
    existing.Element = updated.Element;
    existing.BaseRarity = updated.BaseRarity;
    existing.Habitat = updated.Habitat;
    existing.Lore = updated.Lore;
    existing.BaseStats = updated.BaseStats;
    existing.EvolvesIntoSpeciesId = updated.EvolvesIntoSpeciesId;
    existing.EvolutionLevel = updated.EvolutionLevel;

    await db.SaveChangesAsync();
    return Results.Ok(ToSpeciesData(existing));
});

app.MapDelete("/api/monsters/species/{id:int}", async (int id) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var existing = await db.MonsterSpecies.FirstOrDefaultAsync(s => s.Id == id);
    if (existing is null)
    {
        return Results.NotFound(new ApiError { Message = "Espèce introuvable." });
    }

    db.MonsterSpecies.Remove(existing);
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapPost("/api/monsters/capture", async (CaptureAttemptRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var captureService = new CaptureService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        var result = await captureService.AttemptCaptureAsync(request);
        return Results.Ok(result);
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

// Espèces communes proposées comme premier compagnon (voir GDD — scène d'introduction du
// starter). Filtré côté serveur pour que le client n'ait pas à connaître la règle de rareté.
app.MapGet("/api/monsters/species/starters", async () =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var species = await db.MonsterSpecies.Where(s => s.BaseRarity == Rarity.Commun).OrderBy(s => s.Id).ToListAsync();
    return Results.Ok(species.Select(ToSpeciesData));
});

app.MapGet("/api/characters/{id:guid}/monsters", async (Guid id) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var monsters = await db.Monsters.Where(m => m.OwnerCharacterId == id).ToListAsync();
    return Results.Ok(monsters.Select(ToMonsterInstanceData));
});

app.MapPost("/api/characters/{id:guid}/starter", async (Guid id, StarterChoiceRequest request) =>
{
    if (id != request.CharacterId)
    {
        return Results.BadRequest(new ApiError { Message = "Identifiant de personnage incohérent." });
    }

    await using var db = await dbFactory.CreateDbContextAsync();
    var starterService = new StarterService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        var result = await starterService.ChooseStarterAsync(request);
        return Results.Ok(result);
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

app.MapGet("/api/dungeons", async () =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var dungeons = await db.Dungeons.ToListAsync();
    return Results.Ok(dungeons.Select(ToDungeonData));
});

// CRUD destiné au MapEditor. Mêmes limites que le CRUD d'espèces : pas d'authentification
// admin dédiée pour cette première version (voir Docs/README.md).
app.MapPost("/api/dungeons", async (DungeonData dungeon) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var entity = new DungeonEntity
    {
        Name = dungeon.Name,
        KingdomId = dungeon.KingdomId,
        Description = dungeon.Description,
        Seed = dungeon.Seed,
    };

    db.Dungeons.Add(entity);
    await db.SaveChangesAsync();
    return Results.Ok(ToDungeonData(entity));
});

app.MapPut("/api/dungeons/{id:int}", async (int id, DungeonData updated) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var existing = await db.Dungeons.FirstOrDefaultAsync(d => d.Id == id);
    if (existing is null)
    {
        return Results.NotFound(new ApiError { Message = "Donjon introuvable." });
    }

    existing.Name = updated.Name;
    existing.KingdomId = updated.KingdomId;
    existing.Description = updated.Description;
    existing.Seed = updated.Seed;

    await db.SaveChangesAsync();
    return Results.Ok(ToDungeonData(existing));
});

app.MapDelete("/api/dungeons/{id:int}", async (int id) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var existing = await db.Dungeons.FirstOrDefaultAsync(d => d.Id == id);
    if (existing is null)
    {
        return Results.NotFound(new ApiError { Message = "Donjon introuvable." });
    }

    db.Dungeons.Remove(existing);
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapGet("/api/dungeons/{dungeonId:int}/floors/{floorNumber:int}", async (int dungeonId, int floorNumber) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var dungeon = await db.Dungeons.FirstOrDefaultAsync(d => d.Id == dungeonId);
    if (dungeon is null)
    {
        return Results.NotFound(new ApiError { Message = "Donjon introuvable." });
    }

    if (floorNumber <= 0)
    {
        return Results.BadRequest(new ApiError { Message = "Le numéro d'étage doit être positif." });
    }

    var floor = DungeonFloorGenerator.GenerateFloor(dungeon.Seed, floorNumber);
    return Results.Ok(floor);
});

app.MapGet("/api/professions/recipes", async () =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var recipes = await db.Recipes.Include(r => r.Ingredients).ToListAsync();
    return Results.Ok(recipes);
});

app.MapPost("/api/professions/gather", async (GatherRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var professionService = new ProfessionService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        return Results.Ok(await professionService.GatherAsync(request));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

app.MapPost("/api/professions/craft", async (CraftRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var professionService = new ProfessionService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        return Results.Ok(await professionService.CraftAsync(request));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

app.MapPost("/api/guilds", async (CreateGuildRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var guildService = new GuildService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        return Results.Ok(await guildService.CreateAsync(request));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

app.MapPost("/api/guilds/{guildId:guid}/join", async (Guid guildId, JoinGuildRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var guildService = new GuildService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        return Results.Ok(await guildService.JoinAsync(guildId, request));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

app.MapGet("/api/achievements/catalog", () => Results.Ok(AchievementCatalog.All));

app.MapGet("/api/achievements", async (string sessionToken) =>
{
    if (!app.Services.GetRequiredService<SessionTokenStore>().TryValidate(sessionToken, out var userId))
    {
        return Results.Json(new ApiError { Message = "Session invalide ou expirée." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    await using var db = await dbFactory.CreateDbContextAsync();
    var unlockedKeys = await new AchievementService(db).GetUnlockedKeysAsync(userId);
    var unlocked = unlockedKeys.Select(AchievementCatalog.Find).Where(a => a is not null);
    return Results.Ok(unlocked);
});

app.MapPost("/api/leaderboard/{category}/refresh", async (LeaderboardCategory category) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var leaderboardService = new LeaderboardService(db);

    try
    {
        await leaderboardService.RefreshAsync(category);
        return Results.Ok();
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

app.MapGet("/api/leaderboard/{category}", async (LeaderboardCategory category, int limit) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var leaderboardService = new LeaderboardService(db);
    var top = await leaderboardService.GetTopAsync(category, limit <= 0 ? 10 : limit);
    return Results.Ok(top);
});

app.MapPost("/api/combat/start", async (StartCombatRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var combatService = new CombatService(db, app.Services.GetRequiredService<SessionTokenStore>(), app.Services.GetRequiredService<CombatSessionStore>());

    try
    {
        return Results.Ok(await combatService.StartAsync(request));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

app.MapPost("/api/combat/{combatId:guid}/action", async (Guid combatId, CombatActionRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var combatService = new CombatService(db, app.Services.GetRequiredService<SessionTokenStore>(), app.Services.GetRequiredService<CombatSessionStore>());

    try
    {
        return Results.Ok(await combatService.SubmitActionAsync(combatId, request));
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

app.MapPost("/api/dungeons/{dungeonId:int}/floors/{floorNumber:int}/rooms/{roomIndex:int}/engage",
    async (int dungeonId, int floorNumber, int roomIndex, StartDungeonCombatRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var combatService = new CombatService(db, app.Services.GetRequiredService<SessionTokenStore>(), app.Services.GetRequiredService<CombatSessionStore>());

    try
    {
        return Results.Ok(await combatService.StartFromDungeonAsync(dungeonId, floorNumber, roomIndex, request));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

app.MapGet("/api/combat/{combatId:guid}", async (Guid combatId) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var combatService = new CombatService(db, app.Services.GetRequiredService<SessionTokenStore>(), app.Services.GetRequiredService<CombatSessionStore>());
    return combatService.TryGetState(combatId, out var state)
        ? Results.Ok(state)
        : Results.NotFound(new ApiError { Message = "Combat introuvable ou terminé." });
});

app.MapPost("/api/pvp/challenge", async (StartPvpCombatRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var combatService = new CombatService(db, app.Services.GetRequiredService<SessionTokenStore>(), app.Services.GetRequiredService<CombatSessionStore>());

    try
    {
        return Results.Ok(await combatService.StartPvpAsync(request));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

app.MapGet("/api/kingdoms", async () =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var kingdoms = await db.Kingdoms.ToListAsync();
    var territories = await db.Territories.ToListAsync();

    return Results.Ok(kingdoms.Select(k => new KingdomData
    {
        Id = k.Id,
        Type = k.Type,
        Name = k.Name,
        CapitalName = k.CapitalName,
        ControlledTerritoryIds = territories.Where(t => t.ControllingKingdomId == k.Id).Select(t => t.Id).ToList(),
    }));
});

app.MapGet("/api/territories", async () =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var territories = await db.Territories.ToListAsync();
    return Results.Ok(territories);
});

app.MapGet("/api/kingdoms/wars/standings", async () =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    return Results.Ok(await new KingdomWarService(db).GetStandingsAsync());
});

app.MapPost("/api/kingdoms/wars/resolve", async () =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var message = await new KingdomWarService(db).ResolveWeeklyWarAsync();
    return Results.Ok(new { message });
});

app.MapGet("/api/seasons/current", async () =>
{
    await using var db = await dbFactory.CreateDbContextAsync();

    try
    {
        return Results.Ok(await new SeasonService(db).GetCurrentAsync());
    }
    catch (AccountOperationException ex)
    {
        return Results.NotFound(new ApiError { Message = ex.Message });
    }
});

app.MapPost("/api/seasons/next", async () =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    return Results.Ok(await new SeasonService(db).StartNextSeasonAsync());
});

// Endpoints AdminPanel. Pas d'authentification admin dédiée pour cette première version
// (outil interne supposé lancé contre un serveur de confiance) — à sécuriser avant tout
// déploiement réel (voir Docs/README.md).
app.MapGet("/api/admin/users", async (string? search) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var query = db.Users.AsQueryable();

    if (!string.IsNullOrWhiteSpace(search))
    {
        query = query.Where(u => u.Username.Contains(search) || u.Email.Contains(search));
    }

    var users = await query.Include(u => u.Characters).ToListAsync();

    return Results.Ok(users.Select(u => new AdminUserSummary
    {
        Id = u.Id,
        Username = u.Username,
        Email = u.Email,
        IsBanned = u.IsBanned,
        BanReason = u.BanReason,
        IsAdmin = u.IsAdmin,
        IsDeleted = u.IsDeleted,
        CreatedAtUtc = u.CreatedAtUtc,
        CharacterCount = u.Characters.Count,
    }));
});

// Suppression/restauration/modification/permissions de compte : actions destructives, réservées
// aux comptes IsAdmin (voir AdminAuthService) — contrairement au ban/unban ci-dessus qui datent
// d'avant l'ajout du rôle admin et restent ouverts (voir Docs/README.md pour cette incohérence
// assumée). Suppression en "soft delete" (IsDeleted) plutôt qu'un retrait réel de la ligne, pour
// permettre une restauration (voir GDD — "restaurer un compte").
app.MapPost("/api/admin/users/{userId:guid}/delete", async (Guid userId, AdminSessionRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    try
    {
        await AdminAuthService.RequireAdminAsync(db, app.Services.GetRequiredService<SessionTokenStore>(), request.SessionToken);
    }
    catch (AccountOperationException ex)
    {
        return Results.Json(new ApiError { Message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
    }

    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
    if (user is null)
    {
        return Results.NotFound(new ApiError { Message = "Compte introuvable." });
    }

    if (user.IsAdmin)
    {
        return Results.Conflict(new ApiError { Message = "Impossible de supprimer un compte administrateur." });
    }

    user.IsDeleted = true;
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapPost("/api/admin/users/{userId:guid}/restore", async (Guid userId, AdminSessionRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    try
    {
        await AdminAuthService.RequireAdminAsync(db, app.Services.GetRequiredService<SessionTokenStore>(), request.SessionToken);
    }
    catch (AccountOperationException ex)
    {
        return Results.Json(new ApiError { Message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
    }

    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
    if (user is null)
    {
        return Results.NotFound(new ApiError { Message = "Compte introuvable." });
    }

    user.IsDeleted = false;
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapPost("/api/admin/users/{userId:guid}/set-admin", async (Guid userId, AdminSetPermissionRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    try
    {
        await AdminAuthService.RequireAdminAsync(db, app.Services.GetRequiredService<SessionTokenStore>(), request.SessionToken);
    }
    catch (AccountOperationException ex)
    {
        return Results.Json(new ApiError { Message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
    }

    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
    if (user is null)
    {
        return Results.NotFound(new ApiError { Message = "Compte introuvable." });
    }

    user.IsAdmin = request.IsAdmin;
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapPost("/api/admin/users/{userId:guid}/modify", async (Guid userId, AdminModifyUserRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    try
    {
        await AdminAuthService.RequireAdminAsync(db, app.Services.GetRequiredService<SessionTokenStore>(), request.SessionToken);
    }
    catch (AccountOperationException ex)
    {
        return Results.Json(new ApiError { Message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
    }

    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
    if (user is null)
    {
        return Results.NotFound(new ApiError { Message = "Compte introuvable." });
    }

    if (!string.IsNullOrWhiteSpace(request.NewUsername))
    {
        user.Username = request.NewUsername.Trim();
    }

    if (!string.IsNullOrWhiteSpace(request.NewEmail))
    {
        user.Email = request.NewEmail.Trim();
    }

    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapPost("/api/admin/users/{userId:guid}/ban", async (Guid userId, BanUserRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
    if (user is null)
    {
        return Results.NotFound(new ApiError { Message = "Compte introuvable." });
    }

    user.IsBanned = true;
    user.BanReason = request.Reason;
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapPost("/api/admin/users/{userId:guid}/unban", async (Guid userId) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
    if (user is null)
    {
        return Results.NotFound(new ApiError { Message = "Compte introuvable." });
    }

    user.IsBanned = false;
    user.BanReason = null;
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapGet("/api/admin/stats", async () =>
{
    await using var db = await dbFactory.CreateDbContextAsync();

    var totalUsers = await db.Users.CountAsync();
    var bannedUsers = await db.Users.CountAsync(u => u.IsBanned);
    var totalCharacters = await db.Characters.CountAsync();
    var totalMonstersCaptured = await db.Monsters.CountAsync();
    var totalGuilds = await db.Guilds.CountAsync();
    var activeSeason = await db.Seasons.FirstOrDefaultAsync(s => s.IsActive);

    return Results.Ok(new AdminGlobalStats
    {
        TotalUsers = totalUsers,
        BannedUsers = bannedUsers,
        TotalCharacters = totalCharacters,
        TotalMonstersCaptured = totalMonstersCaptured,
        TotalGuilds = totalGuilds,
        ActiveSeasonNumber = activeSeason?.Number ?? 0,
    });
});

using var shutdownCts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdownCts.Cancel();
};

var tcpGameServer = new TcpGameServer(
    app.Services.GetRequiredService<SessionTokenStore>(),
    dbFactory,
    app.Services.GetRequiredService<ILoggerFactory>());

var tcpTask = tcpGameServer.RunAsync(GameInfo.DefaultGamePort, shutdownCts.Token);
var httpTask = app.RunAsync(shutdownCts.Token);

await Task.WhenAll(tcpTask, httpTask);

return;

static MonsterSpeciesData ToSpeciesData(MonsterSpeciesEntity entity) => new()
{
    Id = entity.Id,
    Name = entity.Name,
    Element = entity.Element,
    BaseRarity = entity.BaseRarity,
    Habitat = entity.Habitat,
    Lore = entity.Lore,
    BaseStats = entity.BaseStats,
    EvolvesIntoSpeciesId = entity.EvolvesIntoSpeciesId,
    EvolutionLevel = entity.EvolutionLevel,
};

static MonsterInstanceData ToMonsterInstanceData(MonsterEntity entity) => new()
{
    Id = entity.Id,
    SpeciesId = entity.SpeciesId,
    OwnerCharacterId = entity.OwnerCharacterId,
    Variant = entity.Variant,
    Nickname = entity.Nickname,
    Level = entity.Level,
    Experience = entity.Experience,
    Personality = entity.Personality,
    PassiveTalent = entity.PassiveTalent,
    IsInActiveTeam = entity.IsInActiveTeam,
    CapturedAtUtc = entity.CapturedAtUtc,
};

static DungeonData ToDungeonData(DungeonEntity entity) => new()
{
    Id = entity.Id,
    Name = entity.Name,
    KingdomId = entity.KingdomId,
    Description = entity.Description,
    Seed = entity.Seed,
};
