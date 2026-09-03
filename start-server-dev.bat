@echo off
REM Demarre Aetheria.Server en base de developpement (fichier SQLite local, aetheria-dev.db).
REM Les donnees sont persistees entre redemarrages, contrairement au mode "base memoire" par
REM defaut (sans AETHERIA_DB_CONNECTION) qui perd tout a l'arret du serveur.
cd /d "%~dp0"
set AETHERIA_DB_CONNECTION=Data Source=aetheria-dev.db

REM Le "dotnet" du PATH est un SDK 8.0 qui ne peut PAS compiler ce projet (.NET 10) : on prefere
REM le SDK installe par utilisateur (%USERPROFILE%\.dotnet, SDK 10.x) s'il est present.
set "DOTNET=dotnet"
if exist "%USERPROFILE%\.dotnet\dotnet.exe" (
    set "DOTNET=%USERPROFILE%\.dotnet\dotnet.exe"
    set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
    set "DOTNET_MULTILEVEL_LOOKUP=0"
)

REM Recompile TOUJOURS avant de lancer : sans ca, ce script relancait l'ancien .dll deja
REM present dans build\bin (jamais recompile automatiquement), qui pouvait rester en retard
REM sur GameInfo.Version apres un git pull/edition de code. Consequence vecue : le serveur
REM annoncait une version perimee via /api/health, le Launcher se croyait alors en
REM permanence en retard ("il nous dit de refaire la mise a jour a chaque fois") sans que
REM retelecharger le Launcher ne puisse jamais corriger un decalage cote SERVEUR.
"%DOTNET%" build Server\Aetheria.Server.csproj -c Debug
if errorlevel 1 (
    echo Compilation echouee, serveur non demarre.
    pause
    exit /b 1
)

"%DOTNET%" build\bin\Aetheria.Server\Debug\net10.0\Aetheria.Server.dll
pause
