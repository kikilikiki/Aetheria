@echo off
REM ==========================================================================
REM  Aetheria.Server - INSTANCE DE DEVELOPPEMENT / TEST
REM ==========================================================================
REM  Base SQLite locale (aetheria-dev.db) : donnees jetables, persistees entre
REM  redemarrages. Le compte admin y est recree automatiquement au 1er lancement
REM  (admin / voir Server\Persistence\AdminAccountSeeder.cs).
REM
REM  Differences avec start-server-prod.bat :
REM   - meme ports 7777/7778 que la prod (le Launcher les impose) -> on ARRETE
REM     d'abord toute instance qui les occupe (la prod, typiquement).
REM   - bot Discord DESACTIVE (DISCORD_BOT_TOKEN vide) : deux Gateway avec le
REM     meme token s'invalident, et on ne veut pas polluer le Discord en test.
REM   - beta fermee DESACTIVEE (AETHERIA_CLOSED_BETA=false) : n'importe quel
REM     compte peut se connecter pendant les tests.
REM ==========================================================================
cd /d "%~dp0"

set AETHERIA_DB_CONNECTION=Data Source=aetheria-dev.db
set DISCORD_BOT_TOKEN=
set AETHERIA_CLOSED_BETA=false

REM Le "dotnet" du PATH est un SDK 8.0 qui ne peut PAS compiler ce projet (.NET 10) : on prefere
REM le SDK installe par utilisateur (%USERPROFILE%\.dotnet, SDK 10.x) s'il est present.
set "DOTNET=dotnet"
if exist "%USERPROFILE%\.dotnet\dotnet.exe" (
    set "DOTNET=%USERPROFILE%\.dotnet\dotnet.exe"
    set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
    set "DOTNET_MULTILEVEL_LOOKUP=0"
)

REM Libere les ports 7777 (jeu) et 7778 (API compte) si une autre instance tourne
REM (sinon le demarrage echoue avec "address already in use").
for %%P in (7777 7778) do (
    for /f "tokens=5" %%I in ('netstat -ano ^| findstr /r /c:":%%P .*LISTENING"') do (
        echo Arret du processus %%I qui occupe le port %%P ...
        taskkill /F /PID %%I >nul 2>&1
    )
)

REM Recompile TOUJOURS avant de lancer (sinon on relance un vieux .dll en retard sur le code).
"%DOTNET%" build Server\Aetheria.Server.csproj -c Debug
if errorlevel 1 (
    echo.
    echo Compilation echouee, serveur non demarre.
    pause
    exit /b 1
)

echo.
echo === Aetheria DEV : http://localhost:7778/api/health  (jeu sur le port 7777) ===
echo === Compte admin : admin / voir AdminAccountSeeder.cs                       ===
echo.
"%DOTNET%" build\bin\Aetheria.Server\Debug\net10.0\Aetheria.Server.dll
pause
