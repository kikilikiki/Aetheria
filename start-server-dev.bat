@echo off
REM Demarre Aetheria.Server en base de developpement (fichier SQLite local, aetheria-dev.db).
REM Les donnees sont persistees entre redemarrages, contrairement au mode "base memoire" par
REM defaut (sans AETHERIA_DB_CONNECTION) qui perd tout a l'arret du serveur.
cd /d "%~dp0"
set AETHERIA_DB_CONNECTION=Data Source=aetheria-dev.db

REM Recompile TOUJOURS avant de lancer : sans ca, ce script relancait l'ancien .dll deja
REM present dans build\bin (jamais recompile automatiquement), qui pouvait rester en retard
REM sur GameInfo.Version apres un git pull/edition de code. Consequence vecue : le serveur
REM annoncait une version perimee via /api/health, le Launcher se croyait alors en
REM permanence en retard ("il nous dit de refaire la mise a jour a chaque fois") sans que
REM retelecharger le Launcher ne puisse jamais corriger un decalage cote SERVEUR.
dotnet build Server\Aetheria.Server.csproj -c Debug
if errorlevel 1 (
    echo Compilation echouee, serveur non demarre.
    pause
    exit /b 1
)

dotnet build\bin\Aetheria.Server\Debug\net10.0\Aetheria.Server.dll
pause
