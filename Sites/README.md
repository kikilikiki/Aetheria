# Aetheria — Site web de téléchargement

Site statique (HTML/CSS pur, aucune dépendance, aucune build) présentant le jeu. Les boutons
de téléchargement pointent vers les **GitHub Releases** du dépôt — voir "Distribution" plus
bas. Volontairement hors de la solution .NET (`Aetheria.sln`) car il ne partage aucun code
avec le jeu — voir `Docs/README.md`.

## Contenu

- `index.html` — page d'accueil : présentation, fonctionnalités, royaumes, sélecteur de
  téléchargement par OS (Windows/Linux, détection automatique + bascule manuelle), footer.
- `conditions-generales.html` — conditions générales d'utilisation.
- `downloads/linux/` — scaffolding + scripts de build du paquet Linux (voir "Paquet Linux"
  ci-dessous). Les binaires/paquets finaux ne sont **pas** commités (voir `.gitignore`) : ils
  sont reconstruits localement puis publiés en Release.

## Distribution : GitHub Releases

Les installateurs (`AetheriaSetup.exe`, `.deb`, `.tar.gz` Linux) sont attachés en tant
qu'assets à une [Release GitHub](https://github.com/kikilikiki/Aetheria/releases), pas commités
dans le dépôt (évite de gonfler l'historique Git avec des binaires de plusieurs dizaines de
Mo à chaque itération). Le site s'y lie via l'URL stable
`releases/latest/download/<nom-du-fichier>`, qui redirige toujours vers l'asset de la
dernière release publiée — pas besoin de changer les liens du site à chaque nouvelle version.

**Publier une nouvelle release** (remplacer `X.Y.Z` par la version) :

```powershell
# 1. Build Windows (voir "Reconstruire le paquet Windows" ci-dessous) ->
#    build/publish/installer-win-x64/AetheriaInstaller.exe

# 2. Build Linux (voir "Paquet Linux" ci-dessous) ->
#    downloads/linux/aetheria_X.Y.Z_amd64.deb
#    downloads/linux/aetheria-linux-x64.tar.gz

# 3. Créer la release et uploader les assets (nécessite `gh` CLI authentifié,
#    ou l'équivalent via l'API REST GitHub avec un token si `gh` n'est pas installé)
#
#    IMPORTANT : le .deb est uploadé sous un nom STABLE ("aetheria-amd64.deb", sans version) via
#    la syntaxe gh `fichier-local#nom-de-l'asset` — voir bug corrigé, le site liait
#    auparavant en dur "aetheria-client_0.2.0_amd64.deb" (nom qui change à chaque version) au
#    lieu de suivre `releases/latest/download/<nom-du-fichier>` (voir "Distribution" ci-dessus,
#    même logique déjà appliquée à AetheriaSetup.exe) : le lien du site cassait dès qu'une
#    nouvelle version changeait le nom du fichier dans la dernière release.
gh release create vX.Y.Z `
    build/publish/installer-win-x64/AetheriaInstaller.exe#AetheriaSetup.exe `
    Sites/downloads/linux/aetheria_X.Y.Z_amd64.deb#aetheria-amd64.deb `
    Sites/downloads/linux/aetheria-linux-x64.tar.gz `
    --title "Aetheria vX.Y.Z" --notes "..."
```

Sans `gh` CLI disponible, les mêmes étapes se font via l'API REST GitHub
(`POST /repos/kikilikiki/Aetheria/releases` puis upload sur `uploads.github.com` — un token
OAuth avec accès au dépôt suffit, par exemple celui déjà stocké par le credential manager Git
pour les push HTTPS : `git credential fill` avec `protocol=https` / `host=github.com`).

## Reconstruire le paquet Windows

```powershell
dotnet build Aetheria.sln -c Release

# Zipper Launcher + Client (Release) en Payload.zip pour l'embarquer dans l'installateur.
Compress-Archive -Path "build/bin/Aetheria.Launcher/Release/net10.0-windows/*",
                        "build/bin/Aetheria.Client/Release/net10.0/*" `
    -DestinationPath "Installer/Resources/Payload.zip" -Force

# Publier l'installateur en fichier unique (framework-dependent : la version self-contained
# testée pesait ~140 Mo et dépassait la limite de 100 Mo par fichier de GitHub, d'où ce
# compromis assumé — ~2,7 Mo, nécessite le runtime .NET 10 Desktop déjà installé).
dotnet publish Installer/Aetheria.Installer.csproj -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -o build/publish/installer-win-x64
```

Le résultat (`build/publish/installer-win-x64/AetheriaInstaller.exe`) est l'asset à uploader
en Release sous le nom `AetheriaSetup.exe` (voir "Distribution" ci-dessus) — plus besoin de le
copier dans `Sites/downloads/`.

## Paquet Linux

Voir GDD/demande utilisateur — "un installateur pour Linux (.deb, .appimage etc)". Le moteur de
jeu (`Aetheria.Client`, rendu OpenGL via Silk.NET) est multiplateforme et se compile nativement
pour Linux (`dotnet publish -r linux-x64`, testé et fonctionnel depuis Windows sans machine
Linux). Le **Launcher** (création de compte, connexion, mise à jour automatique) a été porté de
WPF (réservé à Windows) vers **Avalonia UI** (`Launcher/`, même stack CommunityToolkit.Mvvm) —
il est donc lui aussi multiplateforme désormais, packagé avec le Client dans les mêmes
`.deb`/`.tar.gz`/AppImage : `aetheria` (le wrapper/raccourci) lance le Launcher, qui lance
ensuite `Aetheria.Client` à côté de lui. Le mode manuel du Client seul reste disponible via
`--host=`/`--token=`/`--characterId=` (voir `Client/LaunchOptions.cs`, `README-linux.txt` inclus
dans chaque paquet) pour les cas sans Launcher (script, compte de test).

`downloads/linux/` contient :

- `aetheria-client-deb/` — arborescence `.deb` (Launcher + Client Linux x64 self-contained à
  copier dans `opt/aetheria/`, `control`, `.desktop`, script `usr/bin/aetheria`).
- `build-deb.py` — **construit un vrai `.deb` sans `dpkg-deb`** (indisponible sur cette
  machine Windows, ni `wsl` avec une distribution installée) : réimplémente le format à la
  main (archive `ar` + `control.tar.gz` + `data.tar.gz`, permissions/`md5sums` corrects) avec
  uniquement la stdlib Python. Usage : `python3 build-deb.py [version]`. Testé : parsing manuel
  de l'archive `ar` généré, `control`/`md5sums` valides, permissions 755 sur les exécutables.
- `build-deb.sh` — équivalent utilisant `dpkg-deb` directement, pour une vraie machine Linux.
- `aetheria-linux-x64.tar.gz` — alternative portable (extraire, lancer `./Aetheria.Launcher`
  ou directement `./Aetheria.Client`), pas de paquet système requis.
- `Aetheria.AppDir/` — arborescence AppImage prête (Launcher + Client + `AppRun` + `.desktop`),
  mais **pas de `.AppImage` généré pour l'instant** : contrairement au `.deb`, le format
  AppImage a besoin de `mksquashfs`/`appimagetool` (vrais binaires Linux), qu'on ne peut pas
  réimplémenter simplement en Python pur — nécessite une vraie machine Linux, WSL avec une
  distribution installée, ou un conteneur Docker (`build-appimage.sh`). Il manque aussi une
  icône `aetheria.png` (TODO dans le script).

Régénérer les binaires Linux avant de packager (Launcher **et** Client — le Launcher est
self-contained, il embarque le runtime .NET + SkiaSharp/HarfBuzzSharp d'Avalonia, aucune
dépendance à installer sur la machine cible) :

```powershell
dotnet publish Launcher/Aetheria.Launcher.csproj -c Release -r linux-x64 --self-contained true `
    -o build/publish/launcher-linux-x64
dotnet publish Client/Aetheria.Client.csproj -c Release -r linux-x64 --self-contained true `
    -o build/publish/client-linux-x64

Copy-Item build/publish/launcher-linux-x64/* Sites/downloads/linux/aetheria-client-deb/opt/aetheria/ -Recurse -Force
Copy-Item build/publish/client-linux-x64/Aetheria.Client, build/publish/client-linux-x64/libglfw.so.3 `
    Sites/downloads/linux/aetheria-client-deb/opt/aetheria/ -Force
```

## Déploiement

N'importe quel hébergeur de site statique convient (GitHub Pages, Netlify, Cloudflare Pages...).
Exemple avec GitHub Pages : activer Pages sur le dépôt en pointant vers le dossier `Sites/`
(ou publier son contenu sur une branche `gh-pages`).
