# Déploiement du portail web (`Aetheria.Web`)

Le portail web (site vitrine + connexion au compte + candidatures bêta + administration) est
déployé **séparément** du serveur de jeu, sur Render (offre gratuite), avec une base PostgreSQL
Neon partagée.

```
Navigateur ──► Render (Docker, Aetheria.Web) ──┐
                                               ├──► PostgreSQL Neon (base partagée)
Launcher / Client ──► serveur de jeu (chez toi) ┘
```

Le serveur de jeu **TCP ne peut pas** tourner sur Render (un seul port HTTP exposé, pas de TCP,
mise en veille après 15 min). Il reste auto-hébergé ; les deux processus pointent vers la même
base Neon via `AETHERIA_DB_CONNECTION`, donc partagent comptes / mots de passe / grades.

## 1. Base de données Neon

1. La base est déjà créée. Récupérer la **chaîne de connexion "pooler"** dans le tableau de bord
   Neon (format `postgresql://user:pass@ep-xxxx-pooler.region.aws.neon.tech/neondb?sslmode=require&channel_binding=require`).
2. `Aetheria.Web` accepte cette URL telle quelle (`Web/Services/NeonConnectionString.cs` la
   convertit en chaîne Npgsql). Le serveur de jeu attend le format clé-valeur :
   `Host=ep-xxxx-pooler.region.aws.neon.tech;Database=neondb;Username=user;Password=pass;SSL Mode=Require;Channel Binding=Require`

## 2. Basculer le serveur de jeu sur Neon

Dans le `.env` (racine du dépôt, gitignoré) du serveur de jeu :

```
AETHERIA_DB_CONNECTION=Host=ep-xxxx-pooler.region.aws.neon.tech;Database=neondb;Username=user;Password=pass;SSL Mode=Require;Channel Binding=Require
```

Redémarrer le serveur : `Server/Program.cs` applique les migrations et `AdminAccountSeeder`
recrée le compte `admin` sur la base vierge. Les données de `aetheria-prod.db` ne sont **pas**
reprises (choix assumé, pré-lancement).

## 3. Déployer sur Render

1. Render → **New → Blueprint** → sélectionner ce dépôt. Le fichier `render.yaml` (racine) décrit
   le service `aetheria-web` (Docker, plan gratuit, `Web/Dockerfile`).
2. Renseigner les variables marquées `sync: false` :

   | Variable | Valeur |
   |---|---|
   | `AETHERIA_DB_CONNECTION` | l'URL Neon (`postgresql://…`) |
   | `DISCORD_BOT_TOKEN` | le token du bot (le même que le serveur de jeu) |
   | `DISCORD_BETA_GUILD_ID` | l'ID du serveur Discord |
   | `AETHERIA_ADMIN_BOOTSTRAP_PASSWORD` | *(facultatif)* mot de passe admin si la base est vierge |
   | `GAME_SERVER_HEALTH_URL` | *(facultatif)* `http://<ip-publique>:7778/api/health` |

3. Premier build ≈ 5 min. L'URL est `https://aetheria-web.onrender.com` (ou le nom choisi).
4. Mettre à jour `Shared/GameInfo.cs` (`WebsiteUrl` / `TermsOfServiceUrl`) si l'URL diffère, puis
   recompiler le Launcher.

## 4. Permissions du bot Discord

Le bot (déjà utilisé pour `/link` et les annonces) doit en plus avoir **« Gérer les salons »**
(`MANAGE_CHANNELS`) dans la guilde, pour créer les tickets `beta-test-<pseudo>`. Le réinviter si
besoin avec cette permission, et vérifier que son rôle est au-dessus des rôles staff
(`1531571205805707385`, `1516429803442671626`) dans la hiérarchie.

Catégorie des tickets : `1531565847125164123` (surchargeable via `DISCORD_BETA_CATEGORY_ID`).

## 5. Limites de l'offre gratuite Render

- Mise en veille après 15 min d'inactivité → première requête suivante ≈ 50 s (réveil).
- Pas de disque persistant → sans effet ici (tout est en base Neon).
- 750 heures d'instance / mois.
- Passer au palier payant (7 $/mois) supprime la mise en veille si nécessaire.

## Développement local

Sans `AETHERIA_DB_CONNECTION`, `Aetheria.Web` utilise un fichier SQLite local
(`aetheria-web-local.db`, gitignoré) et applique les migrations automatiquement.

```
cd Web
dotnet run
```

Compte admin local : définir `AETHERIA_ADMIN_BOOTSTRAP_PASSWORD` avant le premier lancement, ou
créer un compte via `/inscription` puis le passer admin en base.

Build de l'image comme Render :

```
docker build -f Web/Dockerfile -t aetheria-web .
docker run -e PORT=8080 -e AETHERIA_DB_CONNECTION="postgresql://…" -e DISCORD_BOT_TOKEN=… -p 8080:8080 aetheria-web
```
