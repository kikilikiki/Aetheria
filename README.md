# Aetheria

**Aetheria** est un MMORPG tactique développé entièrement en C#, avec un moteur de jeu maison
(aucun moteur tiers type Unity ou Unreal). Le projet mélange plusieurs genres :

- **MMORPG** — comptes joueurs, personnages, guildes, métiers, économie, classements, saisons
- **Tactical RPG** — combats tour par tour sur grille, placement, terrain, éléments, synergies
- **Collection / Monster Capture** — créatures capturables, bestiaire, évolutions, variantes
- **Rogue-lite** — donjons générés procéduralement, exploration différente à chaque aventure
- **PvP compétitif** — guerres de royaumes, contrôle de territoires

## Aperçu du projet

Le monde d'Aetheria est composé de plusieurs royaumes, chacun avec sa capitale, ses villages,
ses créatures exclusives et ses ressources. Le personnage se crée directement en jeu (pas dans
le launcher) avec un aperçu animé de son apparence, avant de rejoindre le monde ouvert en vue
isométrique 2D : bâtiments visitables, PNJ avec dialogues, portail de donjon, et un premier
compagnon à choisir parmi une dizaine de créatures communes.

Voir le [document de conception complet](Docs/GameDesign.md) pour la vision détaillée (royaumes,
combats, donjons dynamiques, métiers, guildes, succès, saisons...), et le
[journal technique](Docs/README.md) pour l'état d'avancement réel, les choix d'architecture et
les limites assumées à chaque étape.

## Structure du dépôt

```
Aetheria/
├── Engine/          Moteur de jeu maison (rendu OpenGL, ECS, input, rendu de texte, ...)
├── Client/           Application jouée par le joueur (Silk.NET / OpenGL)
├── Server/           Serveur MMORPG (API HTTP + protocole TCP temps réel)
├── Launcher/          Launcher Windows (compte, mises à jour, lancement du jeu)
├── Installer/         Installateur Windows (AetheriaInstaller.exe)
├── Shared/            Modèles, protocole et enums communs à tous les projets
├── Database/           Entités et contexte Entity Framework Core
├── MapEditor/         Outil WPF de création de cartes/donjons
├── MonsterEditor/     Outil WPF de création du bestiaire
├── AdminPanel/         Outil WPF d'administration (comptes joueurs)
├── Sites/              Site web de présentation et de téléchargement
└── Docs/               Documentation (conception + journal technique)
```

## Stack technique

- **.NET 10 / C# 13**, solution Visual Studio classique (`Aetheria.sln`)
- **Silk.NET** (fenêtrage, OpenGL, input) pour le moteur de rendu du Client
- **WPF** + **CommunityToolkit.Mvvm** pour le Launcher et les outils (MapEditor, MonsterEditor,
  AdminPanel, Installer)
- **ASP.NET Core** (API minimale) + **TCP brut** pour le Server
- **Entity Framework Core** avec PostgreSQL (bascule automatique sur une base en mémoire si
  `AETHERIA_DB_CONNECTION` n'est pas défini, pratique pour le développement local)

## Démarrage rapide

Prérequis : [SDK .NET 10](https://dotnet.microsoft.com/) et Windows (le Launcher, les outils et
l'installateur sont des applications WPF).

```powershell
# Compiler toute la solution
dotnet build Aetheria.sln

# Lancer le serveur (voir Docs/README.md : passer par le .dll, pas le .exe natif)
dotnet build/bin/Aetheria.Server/Debug/net10.0/Aetheria.Server.dll

# Lancer le Launcher (inscription/connexion, puis lance le Client)
build/bin/Aetheria.Launcher/Debug/net10.0-windows/Aetheria.Launcher.exe
```

### Compte administrateur (AdminPanel)

Le fichier `Server/Persistence/AdminAccountSeeder.cs` crée un compte administrateur au premier
démarrage du serveur. Il **n'est pas versionné** (voir `.gitignore`) pour ne pas publier
d'identifiants, même par défaut — seul `Server/Persistence/AdminAccountSeeder.exemple` (un
modèle, sans vrai secret) est dans le dépôt. Avant de lancer le serveur pour la première fois,
deux étapes obligatoires :

1. **Renommez** `Server/Persistence/AdminAccountSeeder.exemple` en
   `Server/Persistence/AdminAccountSeeder.cs` :

   ```powershell
   Rename-Item Server/Persistence/AdminAccountSeeder.exemple AdminAccountSeeder.cs
   ```

2. **Modifiez** ce nouveau fichier `AdminAccountSeeder.cs` : changez `DefaultUsername`,
   `DefaultEmail` et surtout `DefaultPassword` pour vos propres identifiants avant de
   compiler/lancer le serveur.

Sans ce fichier, le projet ne compile pas (`Server/Program.cs` référence `AdminAccountSeeder`).

## Licence

Code sous licence [MIT](LICENSE). L'utilisation du service en ligne (comptes, serveur hébergé)
reste soumise à ses propres conditions générales (`Sites/conditions-generales.html`).
