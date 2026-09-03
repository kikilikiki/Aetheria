@echo off
REM Demarre Aetheria.Server en PRODUCTION.
REM
REM La base de donnees de prod est desormais PostgreSQL (Neon), PARTAGEE avec le portail web
REM (Aetheria.Web, deploye sur Render) : memes comptes / mots de passe / grades, et c'est ce
REM serveur qui traite les candidatures beta soumises sur le site (voir BetaTicketProcessor).
REM La chaine de connexion vient du fichier .env (AETHERIA_DB_CONNECTION=Host=...neon.tech;...),
REM charge automatiquement au demarrage. Voir Docs/Deploiement-Web.md.
REM
REM (Avant, ce script forcait une base SQLite locale aetheria-prod.db : ce n'est plus le cas,
REM sinon le serveur de jeu et le site ne verraient pas les memes comptes.)
cd /d "%~dp0"

REM Le "dotnet" du PATH est un SDK 8.0 qui ne peut PAS compiler ce projet (.NET 10) : on prefere
REM le SDK installe par utilisateur (%USERPROFILE%\.dotnet, SDK 10.x) s'il est present.
set "DOTNET=dotnet"
if exist "%USERPROFILE%\.dotnet\dotnet.exe" (
    set "DOTNET=%USERPROFILE%\.dotnet\dotnet.exe"
    set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
    set "DOTNET_MULTILEVEL_LOOKUP=0"
)

findstr /b /c:"AETHERIA_DB_CONNECTION=" .env >nul 2>&1
if errorlevel 1 (
    echo.
    echo ATTENTION : AETHERIA_DB_CONNECTION est absent de .env.
    echo Le serveur va utiliser une base SQLite locale au lieu de Neon, et ne partagera donc
    echo PAS les comptes avec le site. Ajoute la ligne dans .env puis relance ce script.
    echo.
    pause
)

REM Recompile TOUJOURS avant de lancer : sinon ce script relance l'ancien .dll deja present
REM dans build\bin, potentiellement en retard sur GameInfo.Version (le Launcher se croirait
REM alors en permanence en retard de mise a jour).
"%DOTNET%" build Server\Aetheria.Server.csproj -c Debug
if errorlevel 1 (
    echo Compilation echouee, serveur non demarre.
    pause
    exit /b 1
)

"%DOTNET%" build\bin\Aetheria.Server\Debug\net10.0\Aetheria.Server.dll
pause
