using System.Text;
using Aetheria.Database.Context;
using Aetheria.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

Console.OutputEncoding = Encoding.UTF8;

var builder = WebApplication.CreateBuilder(args);

// Render (et la plupart des PaaS) fournit le port d'écoute via la variable PORT.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Base de données partagée avec le serveur de jeu (voir Docs/Deploiement-Web.md) : même variable
// AETHERIA_DB_CONNECTION que Server/Program.cs. Accepte une URL Neon (postgres://…), convertie en
// chaîne Npgsql par NeonConnectionString. Absente ⇒ fichier SQLite local (développement).
var rawConnection = Environment.GetEnvironmentVariable("AETHERIA_DB_CONNECTION");
var connectionString = string.IsNullOrWhiteSpace(rawConnection)
    ? "Data Source=aetheria-web-local.db"
    : NeonConnectionString.Normalize(rawConnection);

var usingSqlite = connectionString.TrimStart().StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase);

builder.Services.AddDbContext<AetheriaDbContext>(options =>
{
    if (usingSqlite)
    {
        options.UseSqlite(connectionString);
        // Faux positif du fournisseur SQLite face à des migrations générées pour Npgsql
        // (voir Server/Program.cs pour l'explication complète). Ignoré pour SQLite uniquement.
        options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
    }
    else
    {
        options.UseNpgsql(connectionString);
    }
});

builder.Services.AddScoped<WebAccountService>();
builder.Services.AddSingleton<DiscordTicketService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/connexion";
        options.LogoutPath = "/deconnexion";
        options.AccessDeniedPath = "/connexion";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.Cookie.Name = "aetheria.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAuthorization(options =>
{
    // Même règle que Server/Persistence/AdminAuthService : compte IsAdmin ou grade Fondateur.
    options.AddPolicy("Staff", policy => policy.RequireClaim("is_staff", "true"));
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", "Staff");
    options.Conventions.AuthorizePage("/Beta");
    options.Conventions.AuthorizePage("/Compte");
});

var app = builder.Build();

// Applique les migrations au démarrage (idempotent) + recrée le compte admin si la base est
// vierge (Neon neuve). Le serveur de jeu fait de même de son côté sur la même base.
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AetheriaDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    await db.Database.MigrateAsync();
    await WebAdminSeeder.SeedAsync(db, logger);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Erreur");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
