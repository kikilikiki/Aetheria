# Aetheria — Documentation technique

MMORPG tactique (combats sur grille façon Dofus + capture/collection façon Pokémon +
donjons procéduraux rogue-lite) développé entièrement en C#, avec un moteur de jeu maison
(aucun moteur tiers type Unity/Unreal).

Pour la vision complète du jeu (univers, royaumes, monstres, combats, donjons, métiers,
guildes, succès, classements, saisons...), voir le
[Document de conception](GameDesign.md).

## Architecture du dépôt

```
Aetheria/
├── Aetheria.sln                  Solution Visual Studio
├── Directory.Build.props         Configuration MSBuild commune à tous les projets
├── Engine/                       Aetheria.Engine   — moteur de jeu maison
│   ├── Core/                     Boucle de jeu, fenêtre, temps
│   ├── Rendering/                Rendu graphique (sprites, shaders, caméra)
│   ├── ECS/                      Système Entité-Composant-Système
│   ├── Physics/                  Collisions, portée, grille tactique
│   ├── Input/                    Clavier / souris / manette
│   └── Audio/                    Musiques et effets sonores
├── Shared/                       Aetheria.Shared   — modèles et protocole communs
│   ├── Network/                  Packets, opcodes, sérialisation
│   ├── Models/                   DTO (Personnage, Créature, Objet, ...)
│   └── Enums/                    Éléments, raretés, variantes, classes
├── Server/                       Aetheria.Server   — serveur MMORPG (monde persistant)
│   ├── World/                    Royaumes, donjons, instances
│   ├── Networking/                Connexions joueurs, dispatch des packets
│   └── Persistence/              Sauvegarde via Aetheria.Database
├── Client/                       Aetheria.Client   — jeu joueur
│   ├── UI/                       Interface (HUD, inventaire, bestiaire, HDV)
│   ├── Combat/                   Combat tactique sur grille
│   ├── Exploration/              Déplacement, royaumes, donjons
│   └── Networking/               Connexion au serveur
├── Launcher/                     Aetheria.Launcher — launcher Windows (login, MAJ)
├── MapEditor/                    Aetheria.MapEditor — éditeur de cartes/donjons
├── MonsterEditor/                Aetheria.MonsterEditor — éditeur de créatures
├── AdminPanel/                   Aetheria.AdminPanel — outils d'administration
├── Database/                     Aetheria.Database — Entity Framework Core
│   ├── Entities/                 Users, Characters, Monsters, Guilds, ...
│   ├── Context/                  DbContext
│   └── Migrations/               Migrations EF Core (générées automatiquement)
└── Docs/                         Documentation (ce dossier)
```

> **Planifié (hors solution .NET) :** `Sites/` — site web public permettant de télécharger le
> launcher (`AetheriaInstaller.exe`). Ce sera un projet séparé (site statique ou ASP.NET),
> non inclus dans `Aetheria.sln` car il ne partage pas de code avec le jeu. À construire une
> fois le Launcher fonctionnel.

## Graphe de dépendances entre projets

```
Engine  ────────────┐
                     ├──> Client
Shared  ─────────────┤
                     ├──> MapEditor
                     │
Shared  ────> Database ────> Server
                     └──────> AdminPanel

Shared ──> Launcher
Shared ──> MonsterEditor
```

Règles :
- `Engine` ne dépend de rien : c'est un moteur de jeu générique, réutilisable en dehors d'Aetheria.
- `Shared` ne dépend de rien : uniquement des modèles/contrats, aucune logique métier lourde.
- `Server` ne référence jamais `Engine` (pas de rendu côté serveur).
- `Client` et `MapEditor` référencent `Engine` car ils ont besoin d'affichage.
- `Database` encapsule tout l'accès EF Core ; seuls `Server` et `AdminPanel` en dépendent directement.

## Cible de compilation

Tous les projets ciblent **.NET 10** et produisent des exécutables Windows (`.exe`) pour les
projets de type application (`Client`, `Server`, `Launcher`, `MapEditor`, `MonsterEditor`,
`AdminPanel`). `Engine`, `Shared` et `Database` sont des bibliothèques de classes (`.dll`).

## Feuille de route (suivie étape par étape)

Priorité choisie : consolider les fondations techniques (moteur + serveur) avant les systèmes
de gameplay.

1. ✅ Solution Visual Studio + tous les projets C# + architecture.
2. ✅ Moteur — fenêtre + boucle de jeu + ECS + rendu + input.
   - ✅ `Engine.ECS` : `World`, `Entity`, `ComponentPool<T>`, `ISystem` (voir `Engine/ECS/`).
   - ✅ `Engine.Core.GameHost` : fenêtre + contexte OpenGL via Silk.NET (voir `Engine/Core/`).
   - ✅ `Engine.Rendering` : `Shader`, `Texture2D` (via StbImageSharp), `Camera2D` orthographique,
     `SpriteBatch` (batching par texture, VAO/VBO/EBO dynamiques) — voir `Engine/Rendering/`.
   - ✅ `Engine.Input` : `KeyboardState` (polling + détection "vient d'être pressée"),
     `MouseState`, exposés via `GameHost.Input` — voir `Engine/Input/`.
3. ✅ Base de données — `AetheriaDbContext` EF Core, 12 tables du GDD + migration `InitialCreate`
   (voir `Database/`). PostgreSQL en production, base en mémoire en dev si
   `AETHERIA_DB_CONNECTION` n'est pas défini.
4. ✅ Serveur — API HTTP de compte (`/api/account/register`, `/api/account/login`, hash BCrypt,
   jetons de session) + serveur de jeu TCP (`Server/Networking`) avec framing de packets
   (Ping/Pong, EnterWorld, PlayerMove) vérifié de bout en bout sur un vrai socket.
   - ⬜ Monde partagé multi-joueurs (diffusion des positions, royaumes, instances de donjon) —
     arrive avec les systèmes de jeu (étape 7).
5. ✅ Launcher — WPF (`net10.0-windows`, MVVM via CommunityToolkit.Mvvm) : écran connexion/
   inscription branché sur l'API compte, sélection de personnage, bouton Jouer qui lance
   `Aetheria.Client.exe` avec le jeton de session en argument.
   - ⬜ Téléchargement/mise à jour/réparation de fichiers : nécessite un serveur de
     distribution de contenu qui n'existe pas encore — non implémenté plutôt que simulé.
6. ✅ Boucle jouable de base côté Client — `Client/LaunchOptions` (parsing `--token`/
   `--characterId`/`--host`/`--port`) + `Client/Networking/GameConnection` (TCP vers Server,
   thread de réception dédié). Deux modes :
   - Sans jeton (lancement direct, dev) : démo hors-ligne, déplacement libre continu.
   - Avec jeton (lancé par le Launcher) : connexion réelle au Server, `EnterWorldRequest`,
     déplacement case par case confirmé par le serveur (autoritaire).
   - **Vérifié de bout en bout sur un vrai serveur**, refus ET acceptation : inscription →
     connexion → création de personnage → lancement du Client avec `--token`/`--characterId`
     → connexion TCP → `EnterWorldAccepted` reçu et affiché (`Entrée dans le monde acceptée
     en (0, 0)`). La boucle complète Launcher→Server→Database→Client fonctionne réellement.
7. 🔶 Systèmes de jeu (voir `Server/Persistence` et `Server/World`) :
   - ✅ Création de personnage (`CharacterService`, `POST /api/characters`) — débloque le
     test ci-dessus.
   - ✅ Catalogue de monstres + capture (`MonsterSpeciesEntity`, `CaptureService`,
     `POST /api/monsters/capture`, `GET /api/monsters/species`) : 5 espèces de démarrage
     (une par royaume + une légendaire), objet "Sphère de capture" offert à la création de
     personnage. La formule de réussite dépend de la vie restante simulée du monstre
     (`TargetHealthPercent`) et de sa rareté — **le combat tactique lui-même n'existe pas
     encore**, ce endpoint prend son résultat en entrée plutôt que de le simuler.
     Vérifié de bout en bout : capture échouée à haute vie, réussie à vie basse, objet de
     capture bien consommé (409 une fois l'inventaire épuisé).
   - ⬜ Combat tactique, métiers, guildes, donjons procéduraux, succès, classements, saisons.

> **Piège rencontré et corrigé :** `ComplexProperty` (EF Core 8+, utilisé pour mapper
> `StatBlock` en un seul bloc) fait planter le fournisseur InMemory sur certaines requêtes
> (`KeyNotFoundException` interne). Corrigé en aplatissant `StatBlock` en colonnes scalaires
> (`Base*`/`StatBonus*`) avec une propriété `[NotMapped]` de confort — voir `ItemEntity` et
> `MonsterSpeciesEntity`. Non re-testé sur PostgreSQL réel (indisponible dans cet
> environnement), mais l'approche par colonnes scalaires est de toute façon la plus
> largement compatible entre fournisseurs EF Core.

> **Limite de vérification connue :** je peux confirmer par les logs et l'absence de plantage
> qu'un rendu s'exécute sans erreur, mais je n'ai pas d'outil pour capturer une image de la
> fenêtre native et vérifier visuellement le résultat pixel par pixel — à valider par un humain
> en lançant `Aetheria.Client.exe`.

Dépendance graphique du moteur : **Silk.NET** (Windowing + OpenGL + Input + Maths), choisie
pour ses bindings modernes activement maintenus par la communauté .NET et son bon écosystème
de documentation en C#. Voir `Engine/Aetheria.Engine.csproj` pour les versions exactes.

### Configuration du Serveur

| Variable d'environnement    | Rôle                                                              |
|------------------------------|--------------------------------------------------------------------|
| `AETHERIA_DB_CONNECTION`     | Chaîne de connexion PostgreSQL. Absente ⇒ base en mémoire (dev).   |

Ports par défaut (voir `Shared/GameInfo.cs`) : `7777` (TCP jeu), `7778` (HTTP compte).

## Compiler le projet

Voir les commandes dans le message de suivi de l'étape 1, ou simplement :

```powershell
dotnet build Aetheria.sln
```

Pour lancer un module précis (exemple : le futur launcher) :

```powershell
dotnet run --project Launcher/Aetheria.Launcher.csproj
```
