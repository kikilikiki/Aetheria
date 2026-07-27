using System.Text;
using System.Text.Json.Serialization;
using Aetheria.Database.Context;
using Aetheria.Server.Networking;
using Aetheria.Server.Persistence;
using Aetheria.Server.World;
using Aetheria.Server.World.Combat;
using Aetheria.Shared;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Aetheria.Shared.Models.Account;
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
    return Results.Ok(species);
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

app.MapGet("/api/dungeons", async () =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var dungeons = await db.Dungeons.ToListAsync();
    return Results.Ok(dungeons);
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
