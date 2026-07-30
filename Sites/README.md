# Aetheria — Site web de téléchargement

Site statique (HTML/CSS pur, aucune dépendance, aucune build) présentant le jeu et proposant
le téléchargement direct de l'installateur. Volontairement hors de la solution .NET
(`Aetheria.sln`) car il ne partage aucun code avec le jeu — voir `Docs/README.md`.

## Contenu

- `index.html` — page d'accueil : présentation, fonctionnalités, royaumes, bouton de
  téléchargement, footer (copyright + lien vers les CGU).
- `conditions-generales.html` — conditions générales d'utilisation.
- `downloads/AetheriaSetup.exe` — installateur Windows, **un seul fichier exécutable**
  (`PublishSingleFile` — voir GDD/demande utilisateur "au lieu d'un zip, un seul exécutable
  pour télécharger le Launcher avec le jeu"). Le Payload (Launcher + Client) est embarqué
  comme ressource dans l'exécutable lui-même (voir
  `Installer/Services/EmbeddedPayloadExtractor.cs`), plus besoin d'un dossier `Payload/` à
  côté. **Framework-dependent** (pas self-contained) : nécessite toujours le
  [runtime .NET 10 Desktop](https://dotnet.microsoft.com/download/dotnet/10.0) — la version
  self-contained testée d'abord pesait ~140 Mo et dépassait la limite de 100 Mo par fichier
  de GitHub (dépôt sans Git LFS configuré), d'où ce compromis assumé (~2,7 Mo, même
  contrainte "runtime déjà installé" que l'ancien zip).
- `downloads/linux/` — voir "Paquet Linux" ci-dessous : scaffolding .deb/AppImage du Client
  Linux, **pas encore lié depuis le bouton de téléchargement du site** (limite assumée, voir
  plus bas).

## Bouton de téléchargement

Le bouton "Installer le Launcher" est un lien direct (`download`) vers
`downloads/AetheriaSetup.exe` — **pas** une redirection vers GitHub Releases. L'utilisateur
télécharge un vrai fichier unique, le lance directement (pas d'extraction, pas de runtime à
installer) : `AetheriaSetup.exe` installe le Launcher + Client dans le dossier choisi et
propose un raccourci bureau, puis l'utilisateur lance le Launcher installé.

**Reconstruire le paquet Windows** après un changement du Launcher/Client/Installer :

```powershell
dotnet build Aetheria.sln -c Release

# Zipper Launcher + Client (Release) en Payload.zip pour l'embarquer dans l'installateur.
Compress-Archive -Path "build/bin/Aetheria.Launcher/Release/net10.0-windows/*",
                        "build/bin/Aetheria.Client/Release/net10.0/*" `
    -DestinationPath "Installer/Resources/Payload.zip" -Force

# Publier l'installateur en fichier unique (framework-dependent — voir plus haut pourquoi pas
# self-contained ; Payload.zip est alors embarqué, voir Aetheria.Installer.csproj —
# condition Exists('Resources\Payload.zip')).
dotnet publish Installer/Aetheria.Installer.csproj -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -o build/publish/installer-win-x64

Copy-Item "build/publish/installer-win-x64/AetheriaInstaller.exe" "Sites/downloads/AetheriaSetup.exe" -Force
```

**Limite assumée** : ce paquet est reconstruit et commité manuellement dans ce dépôt plutôt
que publié par une CI. Pour un vrai produit, cet exécutable (ou mieux, un artefact par
plateforme) devrait être hébergé sur un CDN/serveur de distribution dédié, pas dans le
contrôle de version — voir `Docs/README.md`.

## Paquet Linux (scaffolding — non publié sur le site)

Voir GDD/demande utilisateur — "un installateur pour Linux (.deb, .appimage etc)". **Limite
architecturale découverte en préparant ceci** : le moteur de jeu (`Aetheria.Client`, rendu
OpenGL via Silk.NET) est multiplateforme et se compile nativement pour Linux (`dotnet publish
-r linux-x64`, testé et fonctionnel depuis Windows sans machine Linux). Le **Launcher**
(création de compte, connexion, sélection de personnage, mise à jour automatique), en
revanche, est écrit en WPF — une interface **réservée à Windows**. Sans Launcher, le Client
seul démarre en mode démo hors-ligne (voir `Client/LaunchOptions.cs`) : impossible de se
connecter à un vrai compte sur Linux pour l'instant.

`downloads/linux/` contient donc un scaffolding honnête plutôt qu'un faux support complet :

- `aetheria-client-deb/` — arborescence `.deb` prête (binaire Linux x64 self-contained déjà
  copié dans `opt/aetheria/`, `control`, `.desktop`, script `usr/bin/aetheria`).
- `Aetheria.AppDir/` — arborescence AppImage prête (même binaire dans `usr/bin/`, `AppRun`,
  `.desktop` — il manque encore une icône `aetheria.png`, voir le TODO dans
  `build-appimage.sh`).
- `build-deb.sh` / `build-appimage.sh` — scripts qui terminent l'empaquetage. **Ne
  fonctionnent pas sur cet environnement Windows** (ni `dpkg-deb` ni `appimagetool` n'y sont
  disponibles) : à exécuter sur une vraie machine Linux, WSL avec une distribution installée,
  ou un conteneur Docker.

**Porter le Launcher vers une interface multiplateforme (Avalonia UI serait le choix le plus
direct, même stack CommunityToolkit.Mvvm) pour un vrai support Linux avec connexion/inscription
est un chantier séparé, non commencé.**

## Déploiement

N'importe quel hébergeur de site statique convient (GitHub Pages, Netlify, Cloudflare Pages...).
Exemple avec GitHub Pages : activer Pages sur le dépôt en pointant vers le dossier `Sites/`
(ou publier son contenu sur une branche `gh-pages`).
