# Aetheria — Site web de téléchargement

Site statique (HTML/CSS pur, aucune dépendance, aucune build) présentant le jeu et proposant
le téléchargement direct de l'installateur. Volontairement hors de la solution .NET
(`Aetheria.sln`) car il ne partage aucun code avec le jeu — voir `Docs/README.md`.

## Contenu

- `index.html` — page d'accueil : présentation, fonctionnalités, royaumes, bouton de
  téléchargement, footer (copyright + lien vers les CGU).
- `conditions-generales.html` — conditions générales d'utilisation.
- `downloads/AetheriaSetup.zip` — paquet réel téléchargeable : `AetheriaInstaller.exe` +
  son dossier `Payload/` (Launcher + Client compilés en configuration Release). Construit
  manuellement pour cette version (voir ci-dessous) ; pas encore de chaîne de publication
  automatisée.

## Bouton de téléchargement

Le bouton "Installer le Launcher" est un lien direct (`download`) vers
`downloads/AetheriaSetup.zip` — **pas** une redirection vers GitHub Releases. L'utilisateur
télécharge un vrai fichier, l'extrait, lance `AetheriaInstaller.exe` (qui copie `Payload/`
vers le dossier choisi et propose un raccourci bureau), puis lance le Launcher installé.

Nécessite le runtime .NET 10 Desktop sur la machine de l'utilisateur (builds "framework-
dependent", pas autonomes/self-contained — ce qui garde le paquet léger, ~2 Mo compressé,
mais suppose le runtime déjà installé).

**Reconstruire le paquet** après un changement du Launcher/Client/Installer :

```powershell
dotnet build Aetheria.sln -c Release
# Copier Launcher + Client (Release) dans Sites/downloads/Payload/
# Copier Installer (Release) dans Sites/downloads/
Compress-Archive -Path "Sites/downloads/*" -DestinationPath "Sites/downloads/AetheriaSetup.zip" -Force
# Puis supprimer les fichiers non-zippés (Payload/, AetheriaInstaller.exe, ...) pour ne garder que le zip.
```

**Limite assumée** : ce paquet est reconstruit et commité manuellement dans ce dépôt plutôt
que publié par une CI. Pour un vrai produit, ce zip (ou mieux, un artefact self-contained par
plateforme) devrait être hébergé sur un CDN/serveur de distribution dédié, pas dans le
contrôle de version — voir `Docs/README.md`.

## Déploiement

N'importe quel hébergeur de site statique convient (GitHub Pages, Netlify, Cloudflare Pages...).
Exemple avec GitHub Pages : activer Pages sur le dépôt en pointant vers le dossier `Sites/`
(ou publier son contenu sur une branche `gh-pages`).
