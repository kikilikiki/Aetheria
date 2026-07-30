using System.Text;
using System.Text.Json.Serialization;
using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Discord;
using Aetheria.Server.Networking;
using Aetheria.Server.Persistence;
using Aetheria.Server.World;
using Aetheria.Server.World.Combat;
using Aetheria.Shared;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Aetheria.Shared.Models.Account;
using Aetheria.Shared.Models.Admin;
using Aetheria.Shared.Models.BattlePass;
using Aetheria.Shared.Models.Combat;
using Aetheria.Shared.Models.Premium;
using Aetheria.Shared.Models.WorldBoss;
using Aetheria.Shared.Network;
using Aetheria.Shared.Network.Packets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

Console.OutputEncoding = Encoding.UTF8;

DotEnv.LoadIfPresent();

var builder = WebApplication.CreateBuilder(args);

// Voir GDD/demande utilisateur — "crée une base de donné prod et une base de donné dev" :
// AETHERIA_DB_CONNECTION accepte soit une chaîne Npgsql classique (PostgreSQL, déploiement réel),
// soit une chaîne SQLite ("Data Source=..."), reconnue au préfixe — SQLite choisi ici comme base
// fichier zéro-installation pour dev/prod (voir Tools/start-server-dev.bat et
// Tools/start-server-prod.bat, chacun pointant vers un fichier .db distinct) tant qu'aucun
// serveur PostgreSQL n'est disponible sur la machine hébergeant le serveur.
//
// Voir GDD/demande utilisateur — "à chaque redémarrage la base de données ne doit pas être
// reset" : une base en mémoire pure a longtemps servi de valeur par défaut quand cette variable
// n'était pas définie (ex. serveur lancé sans passer par les scripts .bat) — tout redisparaissait
// au moindre redémarrage. Remplacée par un fichier SQLite local par défaut, tout aussi zéro-
// installation mais réellement persisté entre deux lancements.
var connectionString = Environment.GetEnvironmentVariable("AETHERIA_DB_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    connectionString = "Data Source=aetheria-local.db";
}

var usingSqlite = connectionString.TrimStart().StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase);

builder.Services.AddPooledDbContextFactory<AetheriaDbContext>(options =>
{
    if (usingSqlite)
    {
        options.UseSqlite(connectionString);

        // Les migrations sont générées avec le fournisseur Npgsql par défaut (voir outillage EF
        // Core Design) : leurs annotations de génération de valeur (identité PostgreSQL)
        // diffèrent de ce que le fournisseur SQLite attend, ce qui déclenche un faux positif
        // "PendingModelChangesWarning" au démarrage alors qu'aucune migration ne manque
        // réellement (vérifié : le schéma appliqué correspond bien au modèle). Ignoré
        // uniquement pour SQLite — pas pour Npgsql, où ce warning reste un vrai signal utile.
        options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
    }
    else
    {
        options.UseNpgsql(connectionString);
    }
});

builder.Services.AddSingleton<SessionTokenStore>();
builder.Services.AddSingleton<WorldSessionRegistry>();
builder.Services.AddSingleton<CombatSessionStore>();
builder.Services.AddSingleton<DuelInviteService>();
builder.Services.AddSingleton<LootSessionStore>();
builder.Services.AddSingleton<ArenaQueueService>();
builder.Services.AddSingleton<KingdomWarQueueService>();
builder.Services.AddSingleton<DiscordAnnouncer>();
// Voir GDD/demande utilisateur — "laisse allumé le serveur de prod et allume aussi le serveur de
// dev" : les deux ne peuvent pas partager les mêmes ports sur la même machine, d'où ces
// surcharges optionnelles (non définies = ports par défaut habituels, utilisés par la prod/le
// paquet d'installation public — voir GameInfo).
var accountApiPort = int.TryParse(Environment.GetEnvironmentVariable("AETHERIA_ACCOUNT_PORT"), out var configuredAccountPort) ? configuredAccountPort : GameInfo.DefaultAccountApiPort;
var gamePort = int.TryParse(Environment.GetEnvironmentVariable("AETHERIA_GAME_PORT"), out var configuredGamePort) ? configuredGamePort : GameInfo.DefaultGamePort;
builder.WebHost.UseUrls($"http://0.0.0.0:{accountApiPort}");

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

if (usingSqlite)
{
    app.Logger.LogInformation("Base SQLite : {ConnectionString}", connectionString);
}

var dbFactory = app.Services.GetRequiredService<IDbContextFactory<AetheriaDbContext>>();
await using (var db = await dbFactory.CreateDbContextAsync())
{
    await db.Database.MigrateAsync();

    await DatabaseSeeder.SeedAsync(db);
    await AdminAccountSeeder.SeedAsync(db);
    await MonsterCatalogSeeder.SeedAsync(db);
    await DungeonSeeder.SeedAsync(db);
    await ProfessionCatalogSeeder.SeedAsync(db);
    await EquipmentCatalogSeeder.SeedAsync(db);
    await TerritorySeeder.SeedAsync(db);
    await SeasonSeeder.SeedAsync(db);
    await QuestCatalogSeeder.SeedAsync(db);
}

// Journalise les nouveaux commits Git à chaque démarrage (voir GDD/demande utilisateur — aucune
// étape manuelle : relancer le serveur EST la mise à jour). Ne poste plus directement sur
// Discord : accumulé dans PendingChangesLog, envoyé une fois par jour à 23h par
// DailyDigestScheduler (voir plus bas) — fire-and-forget, GitChangelogAnnouncer journalise déjà
// ses propres erreurs, jamais d'exception non gérée ici.
_ = GitChangelogAnnouncer.LogNewCommitsAsync(app.Logger);

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", version = GameInfo.Version }));

// Voir GDD/demande utilisateur — "mise à jour obligatoire du Launcher" : sert le même paquet que
// le site (Payload Launcher+Client en Release) pour que le Launcher puisse se mettre à jour tout
// seul au lieu de se contenter de bloquer JOUER en renvoyant vers un téléchargement manuel.
app.MapGet("/api/updates/launcher-package", (IWebHostEnvironment env) =>
{
    var path = Path.Combine(env.ContentRootPath, "Sites", "downloads", "AetheriaSetup.zip");
    return File.Exists(path)
        ? Results.File(path, "application/zip", "AetheriaSetup.zip")
        : Results.NotFound();
});

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

app.MapPost("/api/account/login", async (LoginRequest request, HttpContext httpContext) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var accountService = new AccountService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        // Voir GDD/demande utilisateur — "ban ip" : l'IP appelante est vérifiée contre la liste
        // des IP bannies puis mémorisée sur le compte (indépendamment du bannissement de compte).
        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
        var response = await accountService.LoginAsync(request, remoteIp);
        return Results.Ok(response);
    }
    catch (AccountOperationException ex)
    {
        return Results.Json(new ApiError { Message = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
    }
});

// Revalidation légère d'un jeton de session persisté (voir GDD/demande utilisateur — "rester
// connecté jusqu'à la déconnexion") : le Launcher l'appelle à son démarrage plutôt que de
// redemander les identifiants, tant que le serveur (dont le SessionTokenStore vit en mémoire)
// n'a pas redémarré et que le compte n'a pas été banni/supprimé entre-temps.
app.MapGet("/api/account/session", async (string sessionToken) =>
{
    if (!app.Services.GetRequiredService<SessionTokenStore>().TryValidate(sessionToken, out var userId))
    {
        return Results.Json(new ApiError { Message = "Session invalide ou expirée." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    await using var db = await dbFactory.CreateDbContextAsync();
    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
    if (user is null || user.IsDeleted || user.IsBanned)
    {
        return Results.Json(new ApiError { Message = "Compte introuvable, supprimé ou banni." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    return Results.Ok(new SessionInfoResponse { UserId = userId, IsAdmin = user.IsAdmin, Rank = user.Rank });
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
        Type = species.Type,
        BaseRarity = species.BaseRarity,
        Habitat = species.Habitat,
        Lore = species.Lore,
        BaseStats = species.BaseStats,
        EvolvesIntoSpeciesId = species.EvolvesIntoSpeciesId,
        EvolutionLevel = species.EvolutionLevel,
        IsCosmetic = species.IsCosmetic,
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
    existing.Type = updated.Type;
    existing.BaseRarity = updated.BaseRarity;
    existing.Habitat = updated.Habitat;
    existing.Lore = updated.Lore;
    existing.BaseStats = updated.BaseStats;
    existing.EvolvesIntoSpeciesId = updated.EvolvesIntoSpeciesId;
    existing.EvolutionLevel = updated.EvolutionLevel;
    existing.IsCosmetic = updated.IsCosmetic;

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
    // Voir GDD/demande utilisateur — "10 choix de starter, pas 18" : IsStarter (pas seulement
    // BaseRarity==Commun, le bestiaire étendu a ajouté d'autres espèces communes qui ne sont que
    // des rencontres sauvages).
    var species = await db.MonsterSpecies.Where(s => s.IsStarter).OrderBy(s => s.Id).ToListAsync();
    return Results.Ok(species.Select(ToSpeciesData));
});

app.MapGet("/api/characters/{id:guid}/monsters", async (Guid id) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    // Voir GDD/demande utilisateur — "/monster-lvl pseudo (n° où est son monstre) lvl" : ordre
    // explicite et stable (voir PlayerSession.SetMonsterLevelByIndex, qui suppose le même tri).
    var monsters = await db.Monsters.Where(m => m.OwnerCharacterId == id).OrderBy(m => m.CapturedAtUtc).ToListAsync();

    // Voir GDD/demande utilisateur — équipement affiché par nom (pas seulement par ID) dans le
    // panneau Monstres côté client.
    var equippedItemIds = monsters
        .SelectMany(m => new[] { m.EquippedWeaponItemId, m.EquippedArmorItemId, m.EquippedAccessoryItemId })
        .Where(i => i is not null)
        .Select(i => i!.Value)
        .Distinct()
        .ToList();
    var itemNames = equippedItemIds.Count == 0
        ? new Dictionary<int, string>()
        : await db.Items.Where(i => equippedItemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name);

    return Results.Ok(monsters.Select(m => ToMonsterInstanceData(m, itemNames)));
});

// UI de gestion des créatures (voir GDD — "monter de niveau, objet à donner").
app.MapPost("/api/monsters/{monsterId:guid}/give-item", async (Guid monsterId, GiveItemToMonsterRequest request) =>
{
    if (monsterId != request.MonsterId)
    {
        return Results.BadRequest(new ApiError { Message = "Identifiant de créature incohérent." });
    }

    await using var db = await dbFactory.CreateDbContextAsync();
    var careService = new MonsterCareService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        return Results.Ok(await careService.GiveItemAsync(request));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

// Voir GDD/demande utilisateur — bâtiment "où l'on peut voir tout nos monstres et déplacer ce
// que l'on a dans notre team" (panneau Monstres, touche T).
app.MapPost("/api/monsters/{monsterId:guid}/set-active-team", async (Guid monsterId, SetMonsterActiveTeamRequest request) =>
{
    if (monsterId != request.MonsterId)
    {
        return Results.BadRequest(new ApiError { Message = "Identifiant de créature incohérent." });
    }

    await using var db = await dbFactory.CreateDbContextAsync();
    var careService = new MonsterCareService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        return Results.Ok(await careService.SetActiveTeamAsync(request.SessionToken, request.MonsterId, request.IsInActiveTeam));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

// Voir GDD/demande utilisateur — "Prestige après niveau maximum".
app.MapPost("/api/monsters/{monsterId:guid}/prestige", async (Guid monsterId, PrestigeMonsterRequest request) =>
{
    if (monsterId != request.MonsterId)
    {
        return Results.BadRequest(new ApiError { Message = "Identifiant de créature incohérent." });
    }

    await using var db = await dbFactory.CreateDbContextAsync();
    var prestigeService = new PrestigeService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        return Results.Ok(await prestigeService.PrestigeAsync(request.SessionToken, request.CharacterId, request.MonsterId));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

// Voir GDD/demande utilisateur — "les items équipés peuvent donner des avantages à nos monstres".
app.MapPost("/api/monsters/{monsterId:guid}/equip", async (Guid monsterId, EquipItemRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var equipmentService = new MonsterEquipmentService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        return Results.Ok(await equipmentService.EquipAsync(monsterId, request));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

app.MapPost("/api/monsters/{monsterId:guid}/unequip", async (Guid monsterId, UnequipItemRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var equipmentService = new MonsterEquipmentService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        return Results.Ok(await equipmentService.UnequipAsync(monsterId, request));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

// Voir GDD/demande utilisateur — bâtiment Fusion ("leur niveau sera leur 2 niveaux additionnés
// puis divisé par 2") et bâtiment Couvée ("reproduction avec heritage de statistiques... des
// monstres que l'on peut avoir que en reproduction").
app.MapPost("/api/monsters/fuse", async (FuseMonstersRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var fusionService = new FusionService(db, app.Services.GetRequiredService<SessionTokenStore>());
    try
    {
        return Results.Ok(await fusionService.FuseAsync(request.SessionToken, request.CharacterId, request.SurvivorMonsterId, request.ConsumedMonsterId));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

app.MapPost("/api/monsters/breed", async (BreedMonstersRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var breedingService = new BreedingService(db, app.Services.GetRequiredService<SessionTokenStore>());
    try
    {
        return Results.Ok(await breedingService.BreedAsync(request.SessionToken, request.CharacterId, request.ParentMonsterId1, request.ParentMonsterId2));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

// Inventaire (voir GDD — bouton Inventaire en jeu).
app.MapGet("/api/characters/{id:guid}/inventory", async (Guid id) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var inventory = await db.InventoryItems
        .Include(inv => inv.Item)
        .Where(inv => inv.CharacterId == id)
        .ToListAsync();

    return Results.Ok(inventory.Where(inv => inv.Item is not null).Select(inv => new InventoryItemSummary
    {
        ItemId = inv.ItemId,
        Name = inv.Item!.Name,
        Description = inv.Item.Description,
        ItemType = inv.Item.ItemType,
        Rarity = inv.Item.Rarity,
        Quantity = inv.Quantity,
    }));
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

    var anyPositionChanged = false;
    foreach (var dungeon in dungeons)
    {
        anyPositionChanged |= DungeonWorldService.EnsureCurrentPosition(dungeon);
    }

    if (anyPositionChanged)
    {
        await db.SaveChangesAsync();
    }

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
        MinLevel = Math.Max(1, dungeon.MinLevel),
        IsHardcore = dungeon.IsHardcore,
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
    existing.MinLevel = Math.Max(1, updated.MinLevel);
    existing.IsHardcore = updated.IsHardcore;

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
    // Voir GDD/demande utilisateur — "liste des items que l'on peut craft et ce qu'il faut" :
    // inclut les noms d'objets (résultat + ingrédients) pour que le client n'ait pas besoin d'un
    // second aller-retour vers le catalogue pour les afficher.
    var recipes = await db.Recipes
        .Include(r => r.ResultItem)
        .Include(r => r.Ingredients).ThenInclude(i => i.Item)
        .ToListAsync();
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

// Voir GDD/demande utilisateur — "un UI avec un bouton pour voir les métiers, les niveaux de
// chaque métier etc" : un par ProfessionType, y compris ceux jamais pratiqués (niveau 1).
app.MapGet("/api/professions/{characterId:guid}", async (Guid characterId) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var professionService = new ProfessionService(db, app.Services.GetRequiredService<SessionTokenStore>());
    return Results.Ok(await professionService.GetSummaryAsync(characterId));
});

// Voir GDD/demande utilisateur — "un tutoriel qui force le joueur à faire des quêtes qui lui
// expliquent le jeu" et "une histoire avec des dialogues cohérents". Une seule quête active à la
// fois (voir QuestService), déclenchée par les points d'ancrage existants côté client plutôt
// qu'un vrai système de conditions serveur (voir Docs/README.md pour cette limite assumée).
app.MapGet("/api/quests/active", async (Guid characterId) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var questService = new QuestService(db, app.Services.GetRequiredService<SessionTokenStore>());
    return Results.Ok(await questService.GetActiveQuestAsync(characterId));
});

app.MapPost("/api/quests/complete", async (CompleteQuestRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var questService = new QuestService(db, app.Services.GetRequiredService<SessionTokenStore>());
    await questService.CompleteIfActiveAsync(request.SessionToken, request.CharacterId, request.QuestName);
    return Results.Ok();
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

app.MapGet("/api/guilds/mine", async (Guid characterId) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var guildService = new GuildService(db, app.Services.GetRequiredService<SessionTokenStore>());
    var guild = await guildService.GetForCharacterAsync(characterId);
    return guild is null ? Results.NoContent() : Results.Ok(guild);
});

// Recherche de guildes (voir GDD — panneau Guilde : rejoindre/rechercher/créer).
app.MapGet("/api/guilds", async (string? search) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var guildService = new GuildService(db, app.Services.GetRequiredService<SessionTokenStore>());
    return Results.Ok(await guildService.SearchAsync(search));
});

// Banque de guilde (voir GDD/demande utilisateur — dépôt d'or, fait aussi monter le niveau de guilde).
app.MapPost("/api/guilds/{guildId:guid}/deposit-gold", async (Guid guildId, GuildDepositGoldRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var guildService = new GuildService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        return Results.Ok(await guildService.DepositGoldAsync(guildId, request));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

// Coffre partagé de guilde (voir GDD/demande utilisateur).
app.MapGet("/api/guilds/{guildId:guid}/chest", async (Guid guildId) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var guildService = new GuildService(db, app.Services.GetRequiredService<SessionTokenStore>());
    return Results.Ok(await guildService.GetChestAsync(guildId));
});

app.MapPost("/api/guilds/{guildId:guid}/chest/deposit", async (Guid guildId, GuildChestActionRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var guildService = new GuildService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        return Results.Ok(await guildService.DepositItemAsync(guildId, request));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

app.MapPost("/api/guilds/{guildId:guid}/chest/withdraw", async (Guid guildId, GuildChestActionRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var guildService = new GuildService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        return Results.Ok(await guildService.WithdrawItemAsync(guildId, request));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

// Classement des guildes (voir GDD/demande utilisateur).
app.MapGet("/api/guilds/leaderboard", async (int? limit) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var guildService = new GuildService(db, app.Services.GetRequiredService<SessionTokenStore>());
    return Results.Ok(await guildService.GetLeaderboardAsync(limit ?? 10));
});

// Groupes (voir GDD — visibilité globale des joueurs, XP partagée en groupe).
app.MapPost("/api/parties", async (CreatePartyRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var partyService = new PartyService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        return Results.Ok(await partyService.CreateAsync(request));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

// Identifie le groupe par son code à 5 chiffres (voir GDD/demande utilisateur), pas par son GUID
// interne dans l'URL comme avant.
app.MapPost("/api/parties/join", async (JoinPartyRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var partyService = new PartyService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        return Results.Ok(await partyService.JoinAsync(request));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

app.MapPost("/api/parties/leave", async (LeavePartyRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var partyService = new PartyService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        await partyService.LeaveAsync(request);
        return Results.Ok();
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

app.MapGet("/api/parties/mine", async (Guid characterId) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var partyService = new PartyService(db, app.Services.GetRequiredService<SessionTokenStore>());
    var party = await partyService.GetForCharacterAsync(characterId);
    return party is null ? Results.NoContent() : Results.Ok(party);
});

// Boutique (voir GDD — bouton Boutique en jeu).
app.MapGet("/api/shop/catalog", async () =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var shopService = new ShopService(db, app.Services.GetRequiredService<SessionTokenStore>());
    return Results.Ok(await shopService.GetCatalogAsync());
});

app.MapPost("/api/shop/buy", async (ShopPurchaseRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var shopService = new ShopService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        return Results.Ok(await shopService.BuyAsync(request));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

// Voir GDD/demande utilisateur — "un UI pour l'achat/vente d'objet [chez la marchande] mais tu
// gagnes un peu moins que si tu les mets à l'HDV" : vente à prix réduit, immédiate (voir
// ShopService.SellAsync), par opposition à AuctionService (dépôt réel, vente différée).
app.MapPost("/api/shop/sell", async (ShopSellRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var shopService = new ShopService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        return Results.Ok(await shopService.SellAsync(request));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

// Voir GDD/demande utilisateur — "ajoute des consommables pour booster la luck l'xp la money".
app.MapPost("/api/inventory/use", async (UseItemRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var consumableService = new ConsumableService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        var message = await consumableService.UseAsync(request.SessionToken, request.CharacterId, request.ItemId);
        return Results.Ok(new { message });
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

// Économie premium (voir GDD/demande utilisateur — "shop avec des gems") : palier de grade
// (bonus XP/or) et palier de pass d'emplacement de personnage, tous deux achetés en gemmes.
// Aucune passerelle de paiement réel branchée pour le moment (voir GDD, "bloque la page pour le
// moment") — les gemmes ne sont créditées que manuellement (/givegems, Fondateur) ou converties
// depuis des pièces (voir /api/shop/gems/exchange-gold).
app.MapGet("/api/shop/premium/status", async (string sessionToken) =>
{
    var tokenStore = app.Services.GetRequiredService<SessionTokenStore>();
    if (!tokenStore.TryValidate(sessionToken, out var userId))
    {
        return Results.Json(new ApiError { Message = "Session invalide ou expirée." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    await using var db = await dbFactory.CreateDbContextAsync();
    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
    return user is null
        ? Results.Conflict(new ApiError { Message = "Compte introuvable." })
        : Results.Ok(PremiumService.ToStatus(user));
});

app.MapPost("/api/shop/gems/exchange-gold", async (ExchangeGoldForGemsRequest request) =>
{
    var tokenStore = app.Services.GetRequiredService<SessionTokenStore>();
    if (!tokenStore.TryValidate(request.SessionToken, out var userId))
    {
        return Results.Json(new ApiError { Message = "Session invalide ou expirée." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    if (request.GoldAmount <= 0 || request.GoldAmount % PremiumService.GoldPerGemBlock != 0)
    {
        return Results.Conflict(new ApiError { Message = $"Le montant doit être un multiple de {PremiumService.GoldPerGemBlock:N0} pièces." });
    }

    await using var db = await dbFactory.CreateDbContextAsync();
    var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == request.CharacterId && c.UserId == userId);
    if (character is null)
    {
        return Results.Conflict(new ApiError { Message = "Personnage introuvable pour ce compte." });
    }

    if (character.Gold < request.GoldAmount)
    {
        return Results.Conflict(new ApiError { Message = "Pas assez de pièces." });
    }

    var user = await db.Users.FirstAsync(u => u.Id == userId);
    var blocks = request.GoldAmount / PremiumService.GoldPerGemBlock;
    character.Gold -= request.GoldAmount;
    user.Gems += blocks * PremiumService.GemsPerGemBlock;
    await db.SaveChangesAsync();

    return Results.Ok(PremiumService.ToStatus(user));
});

app.MapPost("/api/shop/premium/grade/upgrade", async (PurchasePremiumTierRequest request) =>
{
    var tokenStore = app.Services.GetRequiredService<SessionTokenStore>();
    if (!tokenStore.TryValidate(request.SessionToken, out var userId))
    {
        return Results.Json(new ApiError { Message = "Session invalide ou expirée." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    await using var db = await dbFactory.CreateDbContextAsync();
    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
    if (user is null)
    {
        return Results.Conflict(new ApiError { Message = "Compte introuvable." });
    }

    var cost = PremiumService.NextGradeTierCost(user);
    if (cost is null)
    {
        return Results.Conflict(new ApiError { Message = "Palier de grade déjà maximum." });
    }

    if (user.Gems < cost)
    {
        return Results.Conflict(new ApiError { Message = $"Pas assez de gemmes (coût : {cost} gemmes)." });
    }

    user.Gems -= cost.Value;
    user.PremiumGradeTier++;
    await db.SaveChangesAsync();

    return Results.Ok(PremiumService.ToStatus(user));
});

// Passe de Niveau (voir GDD/demande utilisateur — "un pass de niveaux de joueur ou chaque xp que
// tu gagne est ajouté dedans aussi ou chaque passage te fait gagner quelque chose ... si il paie
// le pass premium alors il auront accès à des trucs plus exclusif") : voir BattlePassService.
app.MapGet("/api/battlepass/{characterId:guid}", async (Guid characterId) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId);
    return character is null
        ? Results.Conflict(new ApiError { Message = "Personnage introuvable." })
        : Results.Ok(BattlePassService.ToStatus(character));
});

app.MapPost("/api/battlepass/premium/purchase", async (PurchaseBattlePassPremiumRequest request) =>
{
    var tokenStore = app.Services.GetRequiredService<SessionTokenStore>();
    if (!tokenStore.TryValidate(request.SessionToken, out var userId))
    {
        return Results.Json(new ApiError { Message = "Session invalide ou expirée." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    await using var db = await dbFactory.CreateDbContextAsync();
    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
    var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == request.CharacterId && c.UserId == userId);
    if (user is null || character is null)
    {
        return Results.Conflict(new ApiError { Message = "Compte ou personnage introuvable." });
    }

    try
    {
        return Results.Ok(await BattlePassService.PurchasePremiumAsync(db, user, character));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

// Hôtel des ventes entre joueurs (voir GDD/demande utilisateur — "ajoute un bâtiment (un HDV) où
// les joueurs mettent en vente et achètent, moins cher que chez la marchande").
app.MapGet("/api/auction/listings", async (Guid? viewerCharacterId) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var auctionService = new AuctionService(db, app.Services.GetRequiredService<SessionTokenStore>());
    return Results.Ok(await auctionService.GetActiveListingsAsync(viewerCharacterId));
});

app.MapPost("/api/auction/list", async (CreateAuctionListingRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var auctionService = new AuctionService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        return Results.Ok(await auctionService.CreateListingAsync(request));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

app.MapPost("/api/auction/buy", async (AuctionActionRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var auctionService = new AuctionService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        return Results.Ok(await auctionService.BuyAsync(request));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

app.MapPost("/api/auction/cancel", async (AuctionActionRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var auctionService = new AuctionService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        return Results.Ok(await auctionService.CancelAsync(request));
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

// Voir GDD/demande utilisateur — "classement de team (le meilleur de la team ombre etc), on peut
// voir le classement des joueurs seulement si on est dans la même équipe" : le royaume est dérivé
// du personnage authentifié (SessionToken+CharacterId), jamais reçu tel quel du client — un
// membre des Ombres ne peut donc pas demander le classement des Glaces en falsifiant un paramètre.
app.MapGet("/api/leaderboard/{category}/kingdom", async (LeaderboardCategory category, string sessionToken, Guid characterId, int limit) =>
{
    var tokenStore = app.Services.GetRequiredService<SessionTokenStore>();
    if (!tokenStore.TryValidate(sessionToken, out var userId))
    {
        return Results.Json(new ApiError { Message = "Session invalide ou expirée." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    await using var db = await dbFactory.CreateDbContextAsync();
    var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId && c.UserId == userId);
    if (character is null)
    {
        return Results.Conflict(new ApiError { Message = "Personnage introuvable pour ce compte." });
    }

    var leaderboardService = new LeaderboardService(db);
    var top = await leaderboardService.GetTopByKingdomAsync(category, character.Kingdom, limit <= 0 ? 10 : limit);
    return Results.Ok(top);
});

// Boss mondial (voir GDD/demande utilisateur — "un boss monde... barre de vie... leaderboard du
// boss actuel et de toujours"). Voir WorldBossService.
app.MapGet("/api/worldboss/status", async () =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var status = await new WorldBossService(db, app.Services.GetRequiredService<SessionTokenStore>()).GetStatusAsync();
    return status is null ? Results.NoContent() : Results.Ok(status);
});

app.MapPost("/api/worldboss/attack", async (WorldBossAttackRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var worldBossService = new WorldBossService(db, app.Services.GetRequiredService<SessionTokenStore>());
    try
    {
        return Results.Ok(await worldBossService.AttackAsync(request));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

app.MapGet("/api/worldboss/leaderboard", async (string scope, int limit) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var worldBossService = new WorldBossService(db, app.Services.GetRequiredService<SessionTokenStore>());
    var rows = scope == "alltime"
        ? await worldBossService.GetAllTimeLeaderboardAsync(limit <= 0 ? 10 : limit)
        : await worldBossService.GetCurrentLeaderboardAsync(limit <= 0 ? 10 : limit);
    return Results.Ok(rows);
});

// Voir GDD/demande utilisateur — "un endroit pour modifier son profil".
app.MapGet("/api/profile/{characterId:guid}", async (Guid characterId) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var profile = await new ProfileService(db, app.Services.GetRequiredService<SessionTokenStore>()).GetAsync(characterId);
    return profile is null ? Results.NotFound(new ApiError { Message = "Personnage introuvable." }) : Results.Ok(profile);
});

app.MapPost("/api/profile/update", async (UpdateProfileRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    try
    {
        var profile = await new ProfileService(db, app.Services.GetRequiredService<SessionTokenStore>()).UpdateAsync(request);
        return Results.Ok(profile);
    }
    catch (AccountOperationException ex)
    {
        return Results.Json(new ApiError { Message = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
    }
});

// Voir GDD/demande utilisateur — "ajouter les amis".
FriendService CreateFriendService(AetheriaDbContext friendDb) =>
    new(friendDb, app.Services.GetRequiredService<SessionTokenStore>(), app.Services.GetRequiredService<WorldSessionRegistry>());

app.MapPost("/api/friends/request", async (FriendActionRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    try
    {
        return Results.Ok(new AdminGameActionResponse { Success = true, Message = await CreateFriendService(db).SendRequestAsync(request) });
    }
    catch (AccountOperationException ex)
    {
        return Results.Json(new ApiError { Message = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
    }
});

app.MapPost("/api/friends/respond", async (FriendRespondRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    try
    {
        return Results.Ok(new AdminGameActionResponse { Success = true, Message = await CreateFriendService(db).RespondAsync(request) });
    }
    catch (AccountOperationException ex)
    {
        return Results.Json(new ApiError { Message = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
    }
});

app.MapPost("/api/friends/remove", async (FriendActionRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    try
    {
        return Results.Ok(new AdminGameActionResponse { Success = true, Message = await CreateFriendService(db).RemoveAsync(request) });
    }
    catch (AccountOperationException ex)
    {
        return Results.Json(new ApiError { Message = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
    }
});

app.MapGet("/api/friends/{characterId:guid}", async (Guid characterId) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    return Results.Ok(await CreateFriendService(db).GetFriendsAsync(characterId));
});

app.MapGet("/api/friends/{characterId:guid}/pending", async (Guid characterId) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    return Results.Ok(await CreateFriendService(db).GetPendingRequestsAsync(characterId));
});

app.MapPost("/api/combat/start", async (StartCombatRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var combatService = new CombatService(db, app.Services.GetRequiredService<SessionTokenStore>(), app.Services.GetRequiredService<CombatSessionStore>(), app.Services.GetRequiredService<LootSessionStore>());

    try
    {
        return Results.Ok(await combatService.StartAsync(request));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

// Rencontre sauvage hors donjon (voir GDD — mobs sauvages scalés sur le niveau du chef de groupe).
app.MapPost("/api/combat/start-wild", async (StartWildEncounterRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var combatService = new CombatService(db, app.Services.GetRequiredService<SessionTokenStore>(), app.Services.GetRequiredService<CombatSessionStore>(), app.Services.GetRequiredService<LootSessionStore>());

    try
    {
        return Results.Ok(await combatService.StartWildEncounterAsync(request));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

app.MapPost("/api/combat/{combatId:guid}/action", async (Guid combatId, CombatActionRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var combatService = new CombatService(db, app.Services.GetRequiredService<SessionTokenStore>(), app.Services.GetRequiredService<CombatSessionStore>(), app.Services.GetRequiredService<LootSessionStore>());

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
    var combatService = new CombatService(db, app.Services.GetRequiredService<SessionTokenStore>(), app.Services.GetRequiredService<CombatSessionStore>(), app.Services.GetRequiredService<LootSessionStore>());

    try
    {
        return Results.Ok(await combatService.StartFromDungeonAsync(dungeonId, floorNumber, roomIndex, request));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

// Voir GDD/demande utilisateur — "faire en sorte que l'on voie les ennemis avant de les
// combattre, comme Pokémon Épée" : lecture seule, même tirage que /engage (graine stable), pour
// afficher la créature qui sera affrontée avant que le joueur n'engage le combat.
app.MapGet("/api/dungeons/{dungeonId:int}/floors/{floorNumber:int}/rooms/{roomIndex:int}/encounter-preview",
    async (int dungeonId, int floorNumber, int roomIndex) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var combatService = new CombatService(db, app.Services.GetRequiredService<SessionTokenStore>(), app.Services.GetRequiredService<CombatSessionStore>(), app.Services.GetRequiredService<LootSessionStore>());

    try
    {
        return Results.Ok(await combatService.GetDungeonEncounterPreviewAsync(dungeonId, floorNumber, roomIndex));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

// Salle Coffre (voir GDD — exploration en couloir linéaire, "loot au fil du chemin").
app.MapPost("/api/dungeons/{dungeonId:int}/floors/{floorNumber:int}/rooms/{roomIndex:int}/loot-chest",
    async (int dungeonId, int floorNumber, int roomIndex, OpenChestRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var roomService = new DungeonRoomService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        var goldGained = await roomService.OpenChestAsync(dungeonId, floorNumber, roomIndex, request);
        return Results.Ok(new { goldGained });
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

app.MapGet("/api/combat/{combatId:guid}", async (Guid combatId) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var combatService = new CombatService(db, app.Services.GetRequiredService<SessionTokenStore>(), app.Services.GetRequiredService<CombatSessionStore>(), app.Services.GetRequiredService<LootSessionStore>());
    return combatService.TryGetState(combatId, out var state)
        ? Results.Ok(state)
        : Results.NotFound(new ApiError { Message = "Combat introuvable ou terminé." });
});

// Butin de victoire (voir GDD — 4 objets à départager, tirage aléatoire en cas d'égalité).
app.MapPost("/api/loot/{lootId:guid}/claim", async (Guid lootId, LootClaimRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var lootService = new Aetheria.Server.World.Combat.LootService(
        db, app.Services.GetRequiredService<LootSessionStore>(), new PartyService(db, app.Services.GetRequiredService<SessionTokenStore>()));

    try
    {
        return Results.Ok(await lootService.ClaimAsync(lootId, request, app.Services.GetRequiredService<SessionTokenStore>()));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

app.MapGet("/api/loot/{lootId:guid}", async (Guid lootId) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var lootService = new Aetheria.Server.World.Combat.LootService(
        db, app.Services.GetRequiredService<LootSessionStore>(), new PartyService(db, app.Services.GetRequiredService<SessionTokenStore>()));

    return lootService.TryGetState(lootId, out var state)
        ? Results.Ok(state)
        : Results.NotFound(new ApiError { Message = "Butin introuvable ou déjà réparti." });
});

app.MapPost("/api/pvp/team-challenge", async (StartFriendlyTeamDuelRequest request) =>
{
    var tokenStore = app.Services.GetRequiredService<SessionTokenStore>();
    if (!tokenStore.TryValidate(request.SessionToken, out var userId))
    {
        return Results.Json(new ApiError { Message = "Session invalide ou expirée." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    if (!request.ChallengerTeamCharacterIds.Contains(request.CharacterId))
    {
        return Results.Conflict(new ApiError { Message = "Vous ne faites pas partie de l'équipe qui défie." });
    }

    await using var db = await dbFactory.CreateDbContextAsync();
    var caller = await db.Characters.FirstOrDefaultAsync(c => c.Id == request.CharacterId && c.UserId == userId);
    if (caller is null)
    {
        return Results.Conflict(new ApiError { Message = "Personnage introuvable pour ce compte." });
    }

    var combatService = new CombatService(db, tokenStore, app.Services.GetRequiredService<CombatSessionStore>(), app.Services.GetRequiredService<LootSessionStore>());

    try
    {
        var state = await combatService.StartFriendlyTeamDuelAsync(request.ChallengerTeamCharacterIds, request.TargetTeamCharacterIds);

        // Voir GDD/demande utilisateur — "propose un pvp, si la personne est en team tout les
        // membres doivent accepter" : notifie tous les autres participants (des deux équipes) que
        // le combat a bien été créé, avec son ID, pour qu'ils puissent le récupérer eux aussi.
        var registry = app.Services.GetRequiredService<WorldSessionRegistry>();
        foreach (var characterId in request.ChallengerTeamCharacterIds.Concat(request.TargetTeamCharacterIds).Distinct())
        {
            if (characterId == request.CharacterId)
            {
                continue;
            }

            registry.FindByCharacterId(characterId)?.SendPacket(new DuelStartedPacket { CombatId = state.CombatId });
        }

        return Results.Ok(state);
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

// Arènes classées (voir GDD — formats 1v1/2v2/3v3/4v4, ligues ELO). File d'attente en mémoire
// (ArenaQueueService) plutôt qu'un vrai lobby : POST met en file et forme le combat dès que le
// format a assez de joueurs distincts, GET permet à chaque joueur déjà en file de savoir s'il a
// été appairé entre-temps par la requête d'un autre joueur (voir ArenaQueueService.TryConsumeMatch).
app.MapPost("/api/pvp/arena/queue", async (QueueForArenaRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var tokenStore = app.Services.GetRequiredService<SessionTokenStore>();

    if (!tokenStore.TryValidate(request.SessionToken, out var userId))
    {
        return Results.Json(new ApiError { Message = "Session invalide ou expirée." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == request.CharacterId && c.UserId == userId);
    if (character is null)
    {
        return Results.Conflict(new ApiError { Message = "Personnage introuvable pour ce compte." });
    }

    var arenaQueue = app.Services.GetRequiredService<ArenaQueueService>();
    var ticket = new ArenaTicket { UserId = userId, CharacterId = character.Id, MonsterIds = request.MonsterIds };
    var matched = arenaQueue.EnqueueAndTryMatch(request.Format, ticket);

    if (matched is not null)
    {
        var combatService = new CombatService(db, tokenStore, app.Services.GetRequiredService<CombatSessionStore>(), app.Services.GetRequiredService<LootSessionStore>());
        var combatId = await combatService.StartArenaMatchAsync(request.Format, matched);
        arenaQueue.RecordMatch(matched.Select(t => t.CharacterId), combatId);
    }

    return Results.Ok(new { queued = true });
});

app.MapGet("/api/pvp/arena/status", (Guid characterId) =>
{
    var arenaQueue = app.Services.GetRequiredService<ArenaQueueService>();
    return arenaQueue.TryConsumeMatch(characterId, out var combatId)
        ? Results.Ok(new ArenaQueueStatus { IsMatched = true, CombatId = combatId })
        : Results.Ok(new ArenaQueueStatus { IsMatched = false, CombatId = null });
});

app.MapPost("/api/pvp/arena/cancel", (Guid characterId) =>
{
    app.Services.GetRequiredService<ArenaQueueService>().Cancel(characterId);
    return Results.Ok();
});

app.MapGet("/api/kingdoms", async () =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var kingdoms = await db.Kingdoms.ToListAsync();
    var territories = await db.Territories.ToListAsync();
    var characters = await db.Characters.ToListAsync();

    // Voir GDD/demande utilisateur — "ajoute un UI pour les kingdom" : points de guerre, bonus de
    // rendement et nombre de membres, pour construire le panneau Royaume en un seul appel.
    return Results.Ok(kingdoms.Select(k => new KingdomData
    {
        Id = k.Id,
        Type = k.Type,
        Name = k.Name,
        CapitalName = k.CapitalName,
        ControlledTerritoryIds = territories.Where(t => t.ControllingKingdomId == k.Id).Select(t => t.Id).ToList(),
        WarPoints = k.WarPoints,
        BonusTerritoryCount = k.BonusTerritoryCount,
        MemberCount = characters.Count(c => c.Kingdom == k.Type),
    }));
});

app.MapGet("/api/territories", async () =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var territories = await db.Territories.Include(t => t.ControllingKingdom).ToListAsync();
    return Results.Ok(territories.Select(t => new TerritorySummary
    {
        Id = t.Id,
        Name = t.Name,
        TerritoryType = t.TerritoryType,
        ControllingKingdomId = t.ControllingKingdomId,
        ControllingKingdomName = t.ControllingKingdom?.Name ?? "?",
    }));
});

// Voir GDD/demande utilisateur — "guerre de territoire... quêtes de minage" : la première
// ressource brute du catalogue (voir ProfessionCatalogSeeder — "Minerai de fer" pour cette
// première version, une seule mine "type" plutôt qu'une par territoire).
app.MapGet("/api/items/gatherable", async () =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var item = await db.Items.FirstOrDefaultAsync(i => i.ItemType == ItemType.Ressource);
    return item is null ? Results.NotFound() : Results.Ok(new ShopItem { ItemId = item.Id, Name = item.Name, Description = item.Description, ItemType = item.ItemType, Rarity = item.Rarity, Price = item.Price });
});

// Voir GDD/demande utilisateur — "ajoute des bâtiments dans les villes (mine, champs etc) pour
// avoir des objets" : ressource du Champ (voir WorldMap), pendant du Minerai de fer de la Mine
// (voir /api/items/gatherable ci-dessus) — identifiée par nom plutôt que "premier Ressource" pour
// ne pas entrer en collision avec elle.
app.MapGet("/api/items/gatherable-crop", async () =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var item = await db.Items.FirstOrDefaultAsync(i => i.ItemType == ItemType.Ressource && i.Name == "Blé");
    return item is null ? Results.NotFound() : Results.Ok(new ShopItem { ItemId = item.Id, Name = item.Name, Description = item.Description, ItemType = item.ItemType, Rarity = item.Rarity, Price = item.Price });
});

app.MapGet("/api/kingdoms/wars/standings", async () =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    return Results.Ok(await new KingdomWarService(db).GetStandingsAsync());
});

// Voir GDD/demande utilisateur — bâtiment "Guerre", UI "prêt" : matchmaking contre un personnage
// d'un AUTRE royaume (voir KingdomWarQueueService) — le combat lui-même est un duel amical 1v1
// classique (voir CombatService.StartFriendlyTeamDuelAsync), dont la victoire alimente déjà les
// points de guerre du royaume vainqueur via ApplyArenaResultAsync (aucune logique dupliquée ici).
app.MapPost("/api/kingdoms/wars/queue", async (QueueForWarRequest request) =>
{
    var tokenStore = app.Services.GetRequiredService<SessionTokenStore>();
    if (!tokenStore.TryValidate(request.SessionToken, out var userId))
    {
        return Results.Json(new ApiError { Message = "Session invalide ou expirée." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    await using var db = await dbFactory.CreateDbContextAsync();
    var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == request.CharacterId && c.UserId == userId);
    if (character is null)
    {
        return Results.Conflict(new ApiError { Message = "Personnage introuvable pour ce compte." });
    }

    var warQueue = app.Services.GetRequiredService<KingdomWarQueueService>();
    var ticket = new KingdomWarQueueService.WarTicket(character.Id, userId, character.Kingdom);
    var matched = warQueue.EnqueueAndTryMatch(ticket);

    if (matched is not null)
    {
        var combatService = new CombatService(db, tokenStore, app.Services.GetRequiredService<CombatSessionStore>(), app.Services.GetRequiredService<LootSessionStore>());
        var state = await combatService.StartFriendlyTeamDuelAsync([matched[0].CharacterId], [matched[1].CharacterId]);
        warQueue.RecordMatch(matched.Select(t => t.CharacterId), state.CombatId);
    }

    return Results.Ok(new { queued = true });
});

app.MapGet("/api/kingdoms/wars/queue/status", (Guid characterId) =>
{
    var warQueue = app.Services.GetRequiredService<KingdomWarQueueService>();
    return warQueue.TryConsumeMatch(characterId, out var combatId)
        ? Results.Ok(new ArenaQueueStatus { IsMatched = true, CombatId = combatId })
        : Results.Ok(new ArenaQueueStatus { IsMatched = false, CombatId = null });
});

app.MapPost("/api/kingdoms/wars/queue/cancel", (Guid characterId) =>
{
    app.Services.GetRequiredService<KingdomWarQueueService>().Cancel(characterId);
    return Results.Ok();
});

app.MapPost("/api/kingdoms/wars/resolve", async () =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var message = await new KingdomWarService(db).ResolveWeeklyWarAsync();
    return Results.Ok(new { message });
});

// Voir GDD/demande utilisateur — "Fonctionnalités de royaume avancées" (élections du roi, taxes, construction).
app.MapGet("/api/kingdoms/politics", async (Guid characterId) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var politicsService = new KingdomPoliticsService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        return Results.Ok(await politicsService.GetStatusAsync(characterId));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

app.MapPost("/api/kingdoms/vote", async (VoteForKingRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var politicsService = new KingdomPoliticsService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        await politicsService.VoteAsync(request);
        return Results.Ok();
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
});

app.MapPost("/api/kingdoms/elections/resolve", async () =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var message = await new KingdomPoliticsService(db, app.Services.GetRequiredService<SessionTokenStore>()).ResolveElectionsAsync();
    return Results.Ok(new { message });
});

app.MapPost("/api/kingdoms/construct", async (ConstructKingdomBuildingRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    var politicsService = new KingdomPoliticsService(db, app.Services.GetRequiredService<SessionTokenStore>());

    try
    {
        return Results.Ok(await politicsService.ConstructBuildingAsync(request));
    }
    catch (AccountOperationException ex)
    {
        return Results.Conflict(new ApiError { Message = ex.Message });
    }
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
        Rank = u.Rank,
        IsMuted = u.IsMuted,
        LastKnownIp = u.LastKnownIp,
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

// Grade communautaire (voir GDD/demande utilisateur — "le grade peut être donné par l'admin").
app.MapPost("/api/admin/users/{userId:guid}/set-rank", async (Guid userId, AdminSetRankRequest request) =>
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

    user.Rank = request.Rank;
    await db.SaveChangesAsync();
    return Results.Ok();
});

// Mute (voir GDD/demande utilisateur — "mute pour ne pas qu'il parle dans le tchat") : messages
// silencieusement refusés côté serveur, voir PlayerSession.HandleChatMessage.
app.MapPost("/api/admin/users/{userId:guid}/set-mute", async (Guid userId, AdminSetMuteRequest request) =>
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

    user.IsMuted = request.IsMuted;
    await db.SaveChangesAsync();
    return Results.Ok();
});

// Ban IP (voir GDD/demande utilisateur — "ban ip") : bannit la dernière IP connue du compte,
// distinct du bannissement de compte — bloque la connexion depuis cette IP quel que soit le
// compte utilisé ensuite (voir AccountService.LoginAsync).
app.MapPost("/api/admin/users/{userId:guid}/ban-ip", async (Guid userId, AdminSessionRequest request) =>
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

    if (string.IsNullOrWhiteSpace(user.LastKnownIp))
    {
        return Results.Conflict(new ApiError { Message = "Aucune IP connue pour ce compte." });
    }

    if (!await db.BannedIps.AnyAsync(b => b.IpAddress == user.LastKnownIp))
    {
        db.BannedIps.Add(new BannedIpEntity { Id = Guid.NewGuid(), IpAddress = user.LastKnownIp, Reason = $"Banni via le compte {user.Username}." });
        await db.SaveChangesAsync();
    }

    return Results.Ok();
});

// Réinitialise le profil de jeu (voir GDD/demande utilisateur — "possibilité de reset le profil
// en jeu de quelqu'un") : supprime tous les personnages (et leurs dépendances en cascade —
// monstres, inventaire, professions) sans toucher au compte/login lui-même.
app.MapPost("/api/admin/users/{userId:guid}/reset-profile", async (Guid userId, AdminSessionRequest request) =>
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

    var characters = await db.Characters.Where(c => c.UserId == userId).ToListAsync();
    var characterIds = characters.Select(c => c.Id).ToList();

    // Retire ces personnages des groupes/guildes avant suppression (GuildMemberEntity et
    // PartyMemberEntity référencent CharacterId en DeleteBehavior.Restrict) : transfère le lead
    // au membre suivant ou supprime le groupe/la guilde s'il ne reste plus personne — même
    // logique que PartyService.LeaveAsync (pas encore d'équivalent "quitter la guilde" à
    // réutiliser côté guildes, voir Docs/README.md).
    foreach (var characterId in characterIds)
    {
        var partyMembership = await db.PartyMembers.FirstOrDefaultAsync(m => m.CharacterId == characterId);
        if (partyMembership is not null)
        {
            var party = await db.Parties.FirstAsync(p => p.Id == partyMembership.PartyId);
            db.PartyMembers.Remove(partyMembership);

            var remaining = await db.PartyMembers
                .Where(m => m.PartyId == party.Id && m.Id != partyMembership.Id)
                .OrderBy(m => m.JoinedAtUtc)
                .ToListAsync();

            if (remaining.Count == 0)
            {
                db.Parties.Remove(party);
            }
            else if (party.LeaderCharacterId == characterId)
            {
                party.LeaderCharacterId = remaining[0].CharacterId;
            }
        }

        var guildMembership = await db.GuildMembers.FirstOrDefaultAsync(m => m.CharacterId == characterId);
        if (guildMembership is not null)
        {
            var guild = await db.Guilds.FirstAsync(g => g.Id == guildMembership.GuildId);
            if (guild.LeaderCharacterId == characterId)
            {
                var allMembers = await db.GuildMembers.Where(m => m.GuildId == guild.Id).ToListAsync();
                db.GuildMembers.RemoveRange(allMembers);
                db.Guilds.Remove(guild);
            }
            else
            {
                db.GuildMembers.Remove(guildMembership);
            }
        }
    }

    db.Characters.RemoveRange(characters);
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

// Annonce de mise à jour dans le salon Discord du projet (voir DiscordAnnouncer). Réservé aux
// comptes IsAdmin comme les autres actions sensibles ci-dessus.
app.MapPost("/api/admin/discord/announce", async (DiscordAnnounceRequest request) =>
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

    var announcer = app.Services.GetRequiredService<DiscordAnnouncer>();
    var posted = await announcer.PostUpdateAsync(request.Title, request.Description, request.Changes);
    return Results.Ok(new { posted });
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

// Voir GDD/demande utilisateur — "panel admin en jeu... peuvent afficher un message en haut de
// l'écran en gros à tous les joueurs, donner des items, transformer le skin de tous les joueurs
// en panneau pendant 5min, kick" : diffusé via AdminEffectPacket/WorldSessionRegistry, distinct
// du panel admin du Launcher (comptes hors-jeu) mais réutilisant le même AdminAuthService.
app.MapPost("/api/admin/game/broadcast", async (AdminBroadcastRequest request) =>
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

    app.Services.GetRequiredService<WorldSessionRegistry>().BroadcastAll(new AdminEffectPacket
    {
        Kind = AdminEffectKind.Broadcast,
        Message = request.Message,
    });

    return Results.Ok(new AdminGameActionResponse { Success = true, Message = "Message diffusé." });
});

app.MapPost("/api/admin/game/sign-mode", async (AdminSignModeRequest request) =>
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

    var duration = Math.Clamp(request.DurationSeconds, 5, 3600);
    app.Services.GetRequiredService<WorldSessionRegistry>().BroadcastAll(new AdminEffectPacket
    {
        Kind = AdminEffectKind.SignMode,
        DurationSeconds = duration,
    });

    return Results.Ok(new AdminGameActionResponse { Success = true, Message = $"Mode panneau activé pour {duration}s." });
});

app.MapPost("/api/admin/game/give-item", async (AdminGiveItemRequest request) =>
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

    var target = (await db.Characters.ToListAsync()).FirstOrDefault(c => TextMatching.NamesMatch(c.Name, request.TargetCharacterName));
    if (target is null)
    {
        return Results.NotFound(new ApiError { Message = "Personnage introuvable." });
    }

    var item = await db.Items.FirstOrDefaultAsync(i => i.Id == request.ItemId);
    if (item is null)
    {
        return Results.NotFound(new ApiError { Message = "Objet introuvable." });
    }

    // Voir GDD/demande utilisateur — "limite de stack d'item à 99 par item dans l'inventaire".
    var quantity = Math.Max(1, request.Quantity);
    await InventoryStackingService.AddQuantityAsync(db, target.Id, item.Id, quantity, item.MaxStackSize);

    await db.SaveChangesAsync();
    return Results.Ok(new AdminGameActionResponse { Success = true, Message = $"{quantity}x {item.Name} donné(s) à {target.Name}." });
});

app.MapPost("/api/admin/game/kick", async (AdminKickRequest request) =>
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

    var session = app.Services.GetRequiredService<WorldSessionRegistry>().FindByCharacterName(request.TargetCharacterName);
    if (session is null)
    {
        return Results.Ok(new AdminGameActionResponse { Success = false, Message = "Ce joueur n'est pas connecté." });
    }

    session.Kick();
    return Results.Ok(new AdminGameActionResponse { Success = true, Message = $"{request.TargetCharacterName} a été expulsé." });
});

// Voir GDD/demande utilisateur — "le fonda[teur] ajoute un bouton que seul eux peuvent voir" :
// bascule le flag IsAdmin d'un joueur, réservé au grade Fondateur spécifiquement (pas seulement
// IsAdmin) — accorder des droits admin à d'autres est un cran au-dessus des autres actions.
app.MapPost("/api/admin/game/toggle-admin", async (AdminToggleAdminRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();

    if (!app.Services.GetRequiredService<SessionTokenStore>().TryValidate(request.SessionToken, out var callerUserId))
    {
        return Results.Json(new ApiError { Message = "Session invalide ou expirée." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var caller = await db.Users.FirstOrDefaultAsync(u => u.Id == callerUserId);
    if (caller is not { Rank: UserRank.Fondateur })
    {
        return Results.Json(new ApiError { Message = "Action réservée au grade Fondateur." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var target = await db.Characters.Include(c => c.User).FirstOrDefaultAsync(c => c.Name == request.TargetCharacterName);
    if (target?.User is null)
    {
        return Results.NotFound(new ApiError { Message = "Personnage introuvable." });
    }

    target.User.IsAdmin = !target.User.IsAdmin;
    await db.SaveChangesAsync();

    return Results.Ok(new AdminGameActionResponse
    {
        Success = true,
        Message = $"{request.TargetCharacterName} est {(target.User.IsAdmin ? "maintenant" : "n'est plus")} administrateur.",
    });
});

// Voir GDD/demande utilisateur — "il n'y a pas la touche pour les admin abuse et faire des choses
// (kick/ban/transformer en panneau etc) réservé au fonda et au admin" : le kick existait déjà
// (EXPULSER), ceci ajoute le bannissement complet du compte depuis le panel en jeu (même logique
// que la commande de tchat <c>/ban</c>, voir PlayerSession.HandleChatCommand, exposée ici via HTTP
// pour le panel F2 plutôt que dupliquée).
app.MapPost("/api/admin/game/ban", async (AdminBanRequest request) =>
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

    var target = await db.Characters.Include(c => c.User).FirstOrDefaultAsync(c => c.Name == request.TargetCharacterName);
    if (target?.User is null)
    {
        return Results.NotFound(new ApiError { Message = "Personnage introuvable." });
    }

    target.User.IsBanned = true;
    target.User.BanReason = string.IsNullOrWhiteSpace(request.Reason) ? "Banni via le panel admin en jeu." : request.Reason;
    await db.SaveChangesAsync();

    // Voir GDD/demande utilisateur — un compte banni doit aussi être déconnecté immédiatement
    // (le /ban de tchat, lui, ne le faisait pas — voir Docs/README.md sur cette limite).
    app.Services.GetRequiredService<WorldSessionRegistry>().FindByCharacterName(request.TargetCharacterName)?.Kick();

    return Results.Ok(new AdminGameActionResponse { Success = true, Message = $"{request.TargetCharacterName} a été banni." });
});

// Voir GDD/demande utilisateur — "transformer en panneau" ciblé sur un seul joueur (par
// opposition au mode panneau global existant, voir /api/admin/game/sign-mode) : réutilise le même
// AdminEffectPacket/AdminEffectKind.SignMode côté client (aucun changement client nécessaire),
// envoyé uniquement à la session visée plutôt que diffusé à tout le monde.
app.MapPost("/api/admin/game/transform", async (AdminTransformRequest request) =>
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

    var session = app.Services.GetRequiredService<WorldSessionRegistry>().FindByCharacterName(request.TargetCharacterName);
    if (session is null)
    {
        return Results.Ok(new AdminGameActionResponse { Success = false, Message = "Ce joueur n'est pas connecté." });
    }

    var duration = Math.Clamp(request.DurationSeconds, 5, 3600);
    session.SendPacket(new AdminEffectPacket { Kind = AdminEffectKind.SignMode, DurationSeconds = duration });

    return Results.Ok(new AdminGameActionResponse { Success = true, Message = $"{request.TargetCharacterName} transformé en panneau pour {duration}s." });
});

// Voir GDD/demande utilisateur — "ajoute au admin la possibilité d'augmenter le niveau de ces
// monstres" : agit directement sur MonsterEntity.Level, sans passer par la courbe d'XP normale.
app.MapPost("/api/admin/game/level-up-monster", async (AdminLevelUpMonsterRequest request) =>
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

    var monster = await db.Monsters.FirstOrDefaultAsync(m => m.Id == request.MonsterId);
    if (monster is null)
    {
        return Results.NotFound(new ApiError { Message = "Créature introuvable." });
    }

    // Voir GDD/demande utilisateur — "limite de niveau à 1000 pour les monstres".
    monster.Level = Math.Clamp(monster.Level + Math.Max(1, request.Levels), 1, MonsterProgressionService.MaxLevel);
    await db.SaveChangesAsync();
    return Results.Ok(new AdminGameActionResponse { Success = true, Message = $"{monster.Nickname} est maintenant niveau {monster.Level}." });
});

// Voir GDD/demande utilisateur — "ajoute que les admin et le fonda peuvent aussi donner 1 monstre".
app.MapPost("/api/admin/game/give-monster", async (AdminGiveMonsterRequest request) =>
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

    // Voir GDD/demande utilisateur — "je n'arrive pas à me give de monstre" : recherche insensible
    // à la casse et aux accents (voir TextMatching) plutôt qu'une correspondance exacte.
    var target = (await db.Characters.ToListAsync()).FirstOrDefault(c => TextMatching.NamesMatch(c.Name, request.TargetCharacterName));
    if (target is null)
    {
        return Results.NotFound(new ApiError { Message = "Personnage introuvable." });
    }

    var species = (await db.MonsterSpecies.ToListAsync()).FirstOrDefault(s => TextMatching.NamesMatch(s.Name, request.SpeciesName));
    if (species is null)
    {
        return Results.NotFound(new ApiError { Message = "Espèce introuvable." });
    }

    var monster = new MonsterEntity
    {
        Id = Guid.NewGuid(),
        OwnerCharacterId = target.Id,
        SpeciesId = species.Id,
        Variant = MonsterVariant.Normal,
        Nickname = species.Name,
        Level = 1,
    };

    db.Monsters.Add(monster);
    await db.SaveChangesAsync();

    return Results.Ok(new AdminGameActionResponse { Success = true, Message = $"{species.Name} donné à {target.Name}." });
});

// Voir GDD/demande utilisateur — "une touche pour mettre niveau max toute son équipe ou celle
// d'un joueur" : agit sur TOUTES les créatures possédées par le personnage ciblé (pas seulement
// les 4 de l'équipe active en combat), lecture la plus utile pour une action admin "tout maxer".
app.MapPost("/api/admin/game/max-level-team", async (AdminMaxLevelTeamRequest request) =>
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

    var target = await db.Characters.FirstOrDefaultAsync(c => c.Name == request.TargetCharacterName);
    if (target is null)
    {
        return Results.NotFound(new ApiError { Message = "Personnage introuvable." });
    }

    var monsters = await db.Monsters.Where(m => m.OwnerCharacterId == target.Id).ToListAsync();
    foreach (var monster in monsters)
    {
        monster.Level = MonsterProgressionService.MaxLevel;
        monster.Experience = 0;
    }

    await db.SaveChangesAsync();
    return Results.Ok(new AdminGameActionResponse { Success = true, Message = $"{monsters.Count} créature(s) de {target.Name} au niveau {MonsterProgressionService.MaxLevel}." });
});

// Voir retour utilisateur — "il manque des commandes dans les commandes admin (F2)" : ces cinq
// actions existaient déjà comme commandes de tchat (/givemoney, /givexp, /setlevel, /unban,
// /givegems) mais pas dans le panel en jeu (F2) — équivalents HTTP dédiés, comme le reste du
// panel (voir /api/admin/game/ban ci-dessus, même choix de ne pas dupliquer PlayerSession).
app.MapPost("/api/admin/game/give-money", async (AdminGiveMoneyRequest request) =>
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

    var target = await db.Characters.FirstOrDefaultAsync(c => c.Name == request.TargetCharacterName);
    if (target is null)
    {
        return Results.NotFound(new ApiError { Message = "Personnage introuvable." });
    }

    target.Gold = Math.Max(0, target.Gold + request.Amount);
    await db.SaveChangesAsync();
    return Results.Ok(new AdminGameActionResponse { Success = true, Message = $"{target.Name} a maintenant {target.Gold} pièces." });
});

app.MapPost("/api/admin/game/give-xp", async (AdminGiveXpRequest request) =>
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

    var target = await db.Characters.FirstOrDefaultAsync(c => c.Name == request.TargetCharacterName);
    if (target is null)
    {
        return Results.NotFound(new ApiError { Message = "Personnage introuvable." });
    }

    CharacterProgressionService.GrantExperience(target, request.Amount);
    await db.SaveChangesAsync();
    return Results.Ok(new AdminGameActionResponse { Success = true, Message = $"{target.Name} est maintenant niveau {target.Level}." });
});

app.MapPost("/api/admin/game/set-level", async (AdminSetLevelRequest request) =>
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

    var target = await db.Characters.FirstOrDefaultAsync(c => c.Name == request.TargetCharacterName);
    if (target is null)
    {
        return Results.NotFound(new ApiError { Message = "Personnage introuvable." });
    }

    target.Level = Math.Max(1, request.Level);
    target.Experience = 0;
    await db.SaveChangesAsync();
    return Results.Ok(new AdminGameActionResponse { Success = true, Message = $"{target.Name} est maintenant niveau {target.Level}." });
});

app.MapPost("/api/admin/game/unban", async (AdminUnbanCharacterRequest request) =>
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

    var target = await db.Characters.Include(c => c.User).FirstOrDefaultAsync(c => c.Name == request.TargetCharacterName);
    if (target?.User is null)
    {
        return Results.NotFound(new ApiError { Message = "Personnage introuvable." });
    }

    target.User.IsBanned = false;
    target.User.BanReason = null;
    await db.SaveChangesAsync();
    return Results.Ok(new AdminGameActionResponse { Success = true, Message = $"{request.TargetCharacterName} a été débanni." });
});

// Voir GDD/demande utilisateur — "/givegems" est réservé au Fondateur (économie premium) : même
// restriction que la commande de tchat, revérifiée ici (pas seulement IsAdmin).
app.MapPost("/api/admin/game/give-gems", async (AdminGiveGemsRequest request) =>
{
    await using var db = await dbFactory.CreateDbContextAsync();
    if (!app.Services.GetRequiredService<SessionTokenStore>().TryValidate(request.SessionToken, out var callerUserId))
    {
        return Results.Json(new ApiError { Message = "Session invalide ou expirée." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var caller = await db.Users.FirstOrDefaultAsync(u => u.Id == callerUserId);
    if (caller is not { Rank: UserRank.Fondateur })
    {
        return Results.Json(new ApiError { Message = "Action réservée au grade Fondateur." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var target = await db.Characters.Include(c => c.User).FirstOrDefaultAsync(c => c.Name == request.TargetCharacterName);
    if (target?.User is null)
    {
        return Results.NotFound(new ApiError { Message = "Personnage introuvable." });
    }

    target.User.Gems = Math.Max(0, target.User.Gems + request.Amount);
    await db.SaveChangesAsync();
    return Results.Ok(new AdminGameActionResponse { Success = true, Message = $"{request.TargetCharacterName} a maintenant {target.User.Gems} gemmes." });
});

// Voir GDD/demande utilisateur — "boss geant mondial (un pnj qui apparait a notre royaume ou tout
// le monde doit le combattre)" : fait apparaître un nouveau boss mondial (voir WorldBossService),
// annoncé à tous les joueurs connectés comme les autres effets admin globaux.
app.MapPost("/api/admin/game/spawn-world-boss", async (SpawnWorldBossRequest request) =>
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

    var boss = await new WorldBossService(db, app.Services.GetRequiredService<SessionTokenStore>()).SpawnAsync(request.Name, request.MaxHealth);

    app.Services.GetRequiredService<WorldSessionRegistry>().BroadcastAll(new AdminEffectPacket
    {
        Kind = AdminEffectKind.Broadcast,
        Message = $"Un boss mondial est apparu : {boss.Name} ({boss.MaxHealth} PV) !",
    });

    return Results.Ok(new AdminGameActionResponse { Success = true, Message = $"{boss.Name} a été invoqué avec {boss.MaxHealth} PV." });
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
    app.Services.GetRequiredService<ILoggerFactory>(),
    app.Services.GetRequiredService<WorldSessionRegistry>(),
    app.Services.GetRequiredService<DuelInviteService>());

var tcpTask = tcpGameServer.RunAsync(gamePort, shutdownCts.Token);
var httpTask = app.RunAsync(shutdownCts.Token);

// Récapitulatif Discord toutes les heures (voir GDD/demande utilisateur — "au lieu de 23h, tout
// les heures") — tourne en tâche de fond pendant toute la durée de vie du serveur, voir DigestScheduler.
var digestScheduler = new DigestScheduler(
    app.Services.GetRequiredService<DiscordAnnouncer>(),
    app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<DigestScheduler>());
var dailyDigestTask = digestScheduler.RunAsync(shutdownCts.Token);

// Timers de combat/butin (voir GDD/demande utilisateur — "timer de 10 secondes entre chaque
// tour" et "pour le choix des gains") — tourne en tâche de fond pendant toute la durée de vie
// du serveur, voir CombatTimeoutScheduler.
var combatTimeoutScheduler = new Aetheria.Server.World.Combat.CombatTimeoutScheduler(
    app.Services.GetRequiredService<CombatSessionStore>(),
    app.Services.GetRequiredService<LootSessionStore>(),
    app.Services.GetRequiredService<SessionTokenStore>(),
    dbFactory,
    app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<Aetheria.Server.World.Combat.CombatTimeoutScheduler>());
var combatTimeoutTask = combatTimeoutScheduler.RunAsync(shutdownCts.Token);

// Voir GDD/demande utilisateur — "guerre de territoire" résolue automatiquement chaque semaine
// (voir KingdomWarScheduler), plutôt que de dépendre d'un appel manuel à l'endpoint.
var kingdomWarScheduler = new KingdomWarScheduler(dbFactory, app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<KingdomWarScheduler>(), app.Services.GetRequiredService<SessionTokenStore>());
var kingdomWarTask = kingdomWarScheduler.RunAsync(shutdownCts.Token);

await Task.WhenAll(tcpTask, httpTask, dailyDigestTask, combatTimeoutTask, kingdomWarTask);

return;

static MonsterSpeciesData ToSpeciesData(MonsterSpeciesEntity entity) => new()
{
    Id = entity.Id,
    Name = entity.Name,
    Element = entity.Element,
    Type = entity.Type,
    BaseRarity = entity.BaseRarity,
    Habitat = entity.Habitat,
    Lore = entity.Lore,
    BaseStats = entity.BaseStats,
    EvolvesIntoSpeciesId = entity.EvolvesIntoSpeciesId,
    EvolutionLevel = entity.EvolutionLevel,
    IsCosmetic = entity.IsCosmetic,
};

static MonsterInstanceData ToMonsterInstanceData(MonsterEntity entity, IReadOnlyDictionary<int, string>? itemNames = null) => new()
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
    EquippedWeaponItemId = entity.EquippedWeaponItemId,
    EquippedWeaponName = entity.EquippedWeaponItemId is { } weaponId ? itemNames?.GetValueOrDefault(weaponId) : null,
    EquippedArmorItemId = entity.EquippedArmorItemId,
    EquippedArmorName = entity.EquippedArmorItemId is { } armorId ? itemNames?.GetValueOrDefault(armorId) : null,
    EquippedAccessoryItemId = entity.EquippedAccessoryItemId,
    EquippedAccessoryName = entity.EquippedAccessoryItemId is { } accessoryId ? itemNames?.GetValueOrDefault(accessoryId) : null,
    CapturedAtUtc = entity.CapturedAtUtc,
    PrestigeLevel = entity.PrestigeLevel,
};

static DungeonData ToDungeonData(DungeonEntity entity) => new()
{
    Id = entity.Id,
    Name = entity.Name,
    KingdomId = entity.KingdomId,
    Description = entity.Description,
    Seed = entity.Seed,
    WorldX = entity.WorldX,
    WorldY = entity.WorldY,
    MinLevel = entity.MinLevel,
    IsHardcore = entity.IsHardcore,
};
