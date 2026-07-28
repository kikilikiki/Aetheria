# Aetheria — Site web de téléchargement

Site statique (HTML/CSS pur, aucune dépendance, aucune build) présentant le jeu et pointant
vers le téléchargement du Launcher. Volontairement hors de la solution .NET (`Aetheria.sln`)
car il ne partage aucun code avec le jeu — voir `Docs/README.md`.

## Contenu

- `index.html` — page unique : présentation, fonctionnalités, royaumes, lien de téléchargement.

## Lien de téléchargement

Le bouton "Télécharger le Launcher" pointe vers
[github.com/kikilikiki/Aetheria/releases](https://github.com/kikilikiki/Aetheria/releases).
**Aucune version n'y est encore publiée** — le lien est réel (pas une URL inventée) mais mène
pour l'instant à une page de releases vide. Publier une release nécessite :

1. Une chaîne de publication (`dotnet publish` en mode autonome pour Launcher/Client/Server)
   qui n'existe pas encore dans ce dépôt.
2. Un choix de packaging (zip simple, ou `AetheriaInstaller.exe` avec son dossier `Payload/`
   rempli par cette même chaîne de publication — voir `Installer/`).

## Déploiement

N'importe quel hébergeur de site statique convient (GitHub Pages, Netlify, Cloudflare Pages...).
Exemple avec GitHub Pages : activer Pages sur le dépôt en pointant vers le dossier `Sites/`
(ou publier son contenu sur une branche `gh-pages`).
