@echo off
REM Demarre Aetheria.Server en base de production (fichier SQLite local, aetheria-prod.db),
REM distincte de la base de developpement (aetheria-dev.db, voir start-server-dev.bat) pour ne
REM jamais melanger donnees de test et donnees des vrais joueurs.
cd /d "%~dp0"
set AETHERIA_DB_CONNECTION=Data Source=aetheria-prod.db

REM Recompile TOUJOURS avant de lancer (voir start-server-dev.bat pour le detail) : sinon ce
REM script relance l'ancien .dll deja present dans build\bin, potentiellement en retard sur
REM GameInfo.Version, ce qui coince le Launcher dans une boucle de mise a jour infinie.
dotnet build Server\Aetheria.Server.csproj -c Debug
if errorlevel 1 (
    echo Compilation echouee, serveur non demarre.
    pause
    exit /b 1
)

dotnet build\bin\Aetheria.Server\Debug\net10.0\Aetheria.Server.dll
pause
