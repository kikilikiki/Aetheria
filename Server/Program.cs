using System.Text;
using Aetheria.Database.Context;
using Aetheria.Server.Networking;
using Aetheria.Server.Persistence;
using Aetheria.Shared;
using Aetheria.Shared.Models.Account;
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
builder.WebHost.UseUrls($"http://0.0.0.0:{GameInfo.DefaultAccountApiPort}");

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
