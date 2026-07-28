<#
.SYNOPSIS
    Poste une annonce de mise à jour dans le salon Discord du projet, via l'API du serveur
    (voir Server/Discord/DiscordAnnouncer.cs et POST /api/admin/discord/announce).

.DESCRIPTION
    Se connecte d'abord avec un compte administrateur (IsAdmin=true) pour obtenir un jeton de
    session, puis appelle l'endpoint d'annonce avec ce jeton. Le serveur (Aetheria.Server) doit
    être en cours d'exécution et DISCORD_BOT_TOKEN doit être configuré (voir .env.exemple),
    sinon l'annonce est journalisée côté serveur mais pas envoyée.

.EXAMPLE
    ./Tools/discord-announce.ps1 -Title "Mise a jour" -Description "Corrections diverses" `
        -Changes @("Police bitmap : N distinct de H/K", "Position dynamique des donjons (backend)") `
        -AdminUsername admin -AdminPassword ChangeMoi123!
#>
param(
    [Parameter(Mandatory)] [string]$Title,
    [string]$Description = "",
    [string[]]$Changes = @(),
    [Parameter(Mandatory)] [string]$AdminUsername,
    [Parameter(Mandatory)] [string]$AdminPassword,
    [string]$ServerHost = "localhost",
    [int]$Port = 7778
)

$ErrorActionPreference = "Stop"
$baseUrl = "http://${ServerHost}:${Port}"

$loginBody = @{ usernameOrEmail = $AdminUsername; password = $AdminPassword } | ConvertTo-Json
$login = Invoke-RestMethod -Uri "$baseUrl/api/account/login" -Method Post -Body $loginBody -ContentType "application/json"

$announceBody = @{
    sessionToken = $login.sessionToken
    title        = $Title
    description  = $Description
    changes      = $Changes
} | ConvertTo-Json

$result = Invoke-RestMethod -Uri "$baseUrl/api/admin/discord/announce" -Method Post -Body $announceBody -ContentType "application/json"

if ($result.posted) {
    Write-Host "Annonce Discord postee." -ForegroundColor Green
} else {
    Write-Host "Annonce non envoyee (DISCORD_BOT_TOKEN absent cote serveur ? voir logs)." -ForegroundColor Yellow
}
