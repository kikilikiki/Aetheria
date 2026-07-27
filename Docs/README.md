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
2. 🔶 Moteur — fenêtre + boucle de jeu + ECS.
   - ✅ `Engine.ECS` : `World`, `Entity`, `ComponentPool<T>`, `ISystem` (voir `Engine/ECS/`).
   - ✅ `Engine.Core.GameHost` : fenêtre + contexte OpenGL via Silk.NET (voir `Engine/Core/`).
   - ⬜ Rendu de sprites (batching, textures, caméra 2D) dans `Engine/Rendering/`.
   - ⬜ Système d'input (clavier/souris) dans `Engine/Input/`.
3. ✅ Base de données — `AetheriaDbContext` EF Core, 12 tables du GDD + migration `InitialCreate`
   (voir `Database/`). PostgreSQL en production, base en mémoire en dev si
   `AETHERIA_DB_CONNECTION` n'est pas défini.
4. ✅ Serveur — API HTTP de compte (`/api/account/register`, `/api/account/login`, hash BCrypt,
   jetons de session) + serveur de jeu TCP (`Server/Networking`) avec framing de packets
   (Ping/Pong, EnterWorld, PlayerMove) vérifié de bout en bout sur un vrai socket.
   - ⬜ Monde partagé multi-joueurs (diffusion des positions, royaumes, instances de donjon) —
     arrive avec les systèmes de jeu (étape 7).
5. ⬜ Launcher (UI de login/inscription branchée sur l'API compte, téléchargement, mises à jour).
6. ⬜ Rendu de sprites + input (suite de l'étape 2).
7. ⬜ Boucle jouable de base côté Client (sélection de royaume, connexion, grille de combat).
8. ⬜ Systèmes de jeu ajoutés progressivement (combat tactique, capture, métiers, guildes,
   donjons procéduraux, succès, classements, saisons...).

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
