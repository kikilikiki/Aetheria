@echo off
REM Demarre Aetheria.Server en base de developpement (fichier SQLite local, aetheria-dev.db).
REM Les donnees sont persistees entre redemarrages, contrairement au mode "base memoire" par
REM defaut (sans AETHERIA_DB_CONNECTION) qui perd tout a l'arret du serveur.
cd /d "%~dp0"
set AETHERIA_DB_CONNECTION=Data Source=aetheria-dev.db
dotnet build\bin\Aetheria.Server\Debug\net10.0\Aetheria.Server.dll
pause
