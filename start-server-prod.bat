@echo off
REM Demarre Aetheria.Server en base de production (fichier SQLite local, aetheria-prod.db),
REM distincte de la base de developpement (aetheria-dev.db, voir start-server-dev.bat) pour ne
REM jamais melanger donnees de test et donnees des vrais joueurs.
cd /d "%~dp0"
set AETHERIA_DB_CONNECTION=Data Source=aetheria-prod.db
dotnet build\bin\Aetheria.Server\Debug\net10.0\Aetheria.Server.dll
pause
