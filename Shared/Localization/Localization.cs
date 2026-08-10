using System.Linq;
using Aetheria.Shared.Settings;

namespace Aetheria.Shared.Localization;

/// <summary>
/// Voir GDD/demande utilisateur — "ajoute un parametre pour changer la langue... traduit tout
/// les dialogue". Le français reste la langue source : chaque chaîne statique affichée dans le
/// jeu peut être cherchée ici pour obtenir sa traduction anglaise. Les chaînes absentes du
/// dictionnaire (notamment celles construites par interpolation avec du contenu dynamique — nom
/// de joueur, nombres, etc., voir Client/Program.cs) retombent silencieusement sur le français
/// plutôt que d'afficher un texte manquant — couverture partielle mais fonctionnelle, voir
/// Client/Program.cs (DrawText/DrawTextCentered/MeasureTextWidth) pour comment ce dictionnaire
/// est branché. Le japonais (voir <see cref="Language.Japonais"/>) n'a volontairement aucune
/// entrée : reste désactivé côté UI tant qu'aucun vrai système de police ne supporte les glyphes
/// japonais (voir retour utilisateur explicite à ce sujet).
/// </summary>
public static class Localization
{
    public static string Translate(string text, Language language)
    {
        if (language != Language.Anglais)
        {
            return text;
        }

        if (English.TryGetValue(text, out var exact))
        {
            return exact;
        }

        // Voir retour utilisateur — "quand on choisis anglais il n'y a quasiment rien en
        // anglais, remplace tout les texte" : beaucoup de textes du jeu sont construits par
        // interpolation ($"...{valeurDynamique}...") — leur forme finale ne correspond jamais
        // exactement à une entrée du dictionnaire ci-dessus (voir Client/Program.cs). Repli sur
        // un remplacement de sous-chaînes : chaque fragment français statique connu (voir
        // EnglishFragments, longueur décroissante pour ne pas laisser un fragment court amputer
        // un fragment plus long qui le contient) est remplacé où qu'il apparaisse dans le texte,
        // le contenu dynamique entre les fragments reste inchangé.
        var result = text;
        foreach (var fragment in FragmentKeysByDescendingLength)
        {
            if (result.Contains(fragment, StringComparison.Ordinal))
            {
                result = result.Replace(fragment, EnglishFragments[fragment]);
            }
        }

        return result;
    }

    private static readonly Dictionary<string, string> English = new()
    {
        // Panneaux / titres.
        ["PARAMETRES"] = "SETTINGS",
        ["AMIS"] = "FRIENDS",
        ["ARENE CLASSEE"] = "RANKED ARENA",
        ["BOSS MONDIAL"] = "WORLD BOSS",
        ["BOUTIQUE GEMMES"] = "GEM SHOP",
        ["CLASSEMENT"] = "LEADERBOARD",
        ["CLASSEMENT DES GUILDES"] = "GUILD LEADERBOARD",
        ["CLASSEMENT DES ROYAUMES"] = "KINGDOM LEADERBOARD",
        ["COFFRE PARTAGE"] = "SHARED CHEST",
        ["DEFIS"] = "CHALLENGES",
        ["DONJONS"] = "DUNGEONS",
        ["ENCYCLOPEDIE"] = "ENCYCLOPEDIA",
        ["FUSION"] = "FUSION",
        ["GROUPE"] = "PARTY",
        ["GUERRE DE GUILDES"] = "GUILD WAR",
        ["GUERRE DE ROYAUMES"] = "KINGDOM WAR",
        ["GUILDE"] = "GUILD",
        ["INVENTAIRE"] = "INVENTORY",
        ["METIERS"] = "PROFESSIONS",
        ["MONSTRES"] = "MONSTERS",
        ["MONTURES"] = "MOUNTS",
        ["OPTIONS"] = "OPTIONS",
        ["PANEL ADMIN"] = "ADMIN PANEL",
        ["PASSE DE NIVEAU"] = "BATTLE PASS",
        ["PROFIL"] = "PROFILE",
        ["REPRODUCTION"] = "BREEDING",
        ["ROYAUME"] = "KINGDOM",
        ["ROYAUMES"] = "KINGDOMS",
        ["SIGNALEMENTS"] = "REPORTS",
        ["TCHAT"] = "CHAT",
        ["TELEPORTEUR"] = "TELEPORTER",

        // Messages "vide"/statut.
        ["AUCUN BOSS MONDIAL ACTIF POUR LE MOMENT"] = "NO WORLD BOSS ACTIVE RIGHT NOW",
        ["AUCUN DEFI DISPONIBLE"] = "NO CHALLENGE AVAILABLE",
        ["AUCUN DONJON DANS CE ROYAUME"] = "NO DUNGEON IN THIS KINGDOM",
        ["AUCUN OBJET RECLAME"] = "NO ITEM CLAIMED",
        ["AUCUN SIGNALEMENT"] = "NO REPORTS",
        ["AUCUNE ANNONCE POUR L'INSTANT"] = "NO ANNOUNCEMENT RIGHT NOW",
        ["AUCUNE ARME/ARMURE/ACCESSOIRE EN INVENTAIRE"] = "NO WEAPON/ARMOR/ACCESSORY IN INVENTORY",
        ["AUCUNE CREATURE POUR L'INSTANT"] = "NO CREATURE YET",
        ["AUCUNE DONNEE POUR CE CLASSEMENT"] = "NO DATA FOR THIS LEADERBOARD",
        ["AUCUNE GUILDE TROUVEE"] = "NO GUILD FOUND",
        ["AUCUNE QUETE EN COURS"] = "NO QUEST IN PROGRESS",
        ["Aucun rang PvP enregistre pour le moment."] = "No PvP rank recorded yet.",
        ["Aucune donnée pour ce classement."] = "No data for this leaderboard.",
        ["Aucun ami pour l'instant."] = "No friends yet.",
        ["BIENTOT DISPONIBLE"] = "COMING SOON",
        ["CHARGEMENT DU COMBAT..."] = "LOADING COMBAT...",
        ["CHARGEMENT..."] = "LOADING...",
        ["Chargement..."] = "Loading...",
        ["CONNEXION EN COURS..."] = "CONNECTING...",
        ["IL FAUT AU MOINS 2 CREATURES"] = "AT LEAST 2 CREATURES REQUIRED",
        ["INVENTAIRE VIDE"] = "EMPTY INVENTORY",
        ["LE COFFRE EST VIDE"] = "THE CHEST IS EMPTY",
        ["PRET"] = "READY",
        ["PRET - RECHERCHE D'UNE GUILDE ADVERSE..."] = "READY - SEARCHING FOR AN OPPONENT GUILD...",
        ["PRET A NAITRE ! (ENTREE POUR RECUPERER)"] = "READY TO HATCH! (ENTER TO COLLECT)",
        ["Pass Premium déjà actif — merci !"] = "Premium Pass already active — thank you!",
        ["QUETES EN COURS"] = "QUESTS IN PROGRESS",
        ["RECHERCHE D'ADVERSAIRES..."] = "SEARCHING FOR OPPONENTS...",
        ["RECHERCHE D'UN ADVERSAIRE..."] = "SEARCHING FOR AN OPPONENT...",
        ["TOUR ADVERSE..."] = "OPPONENT'S TURN...",
        ["UN VIEUX GARDIEN VOUS ACCUEILLE"] = "AN OLD GUARDIAN WELCOMES YOU",
        ["VOUS N'APPARTENEZ A AUCUNE GUILDE"] = "YOU DO NOT BELONG TO ANY GUILD",
        ["VOUS N'ETES DANS AUCUN GROUPE"] = "YOU ARE NOT IN ANY PARTY",
        ["FUSION PRETE ! (ENTREE POUR RECUPERER)"] = "FUSION READY! (ENTER TO COLLECT)",
        ["LES DEUX PARENTS SURVIVENT"] = "BOTH PARENTS SURVIVE",
        ["LE FORGERON PROPOSE :"] = "THE BLACKSMITH OFFERS:",
        ["QUITTER LE DONJON ?"] = "LEAVE THE DUNGEON?",
        ["CONFIRMER LA FUSION ?"] = "CONFIRM FUSION?",
        ["CONFIRMER LA REPRODUCTION ?"] = "CONFIRM BREEDING?",

        // Écrans de connexion / création de personnage.
        ["+ NOUVEAU PERSONNAGE"] = "+ NEW CHARACTER",
        ["CHOISIS TON COMPAGNON"] = "CHOOSE YOUR COMPANION",
        ["CHOISIS TON PERSONNAGE"] = "CHOOSE YOUR CHARACTER",
        ["PERSONNALISE TON APPARENCE"] = "CUSTOMIZE YOUR APPEARANCE",
        ["QUEL EST TON NOM ?"] = "WHAT IS YOUR NAME?",
        ["TAPEZ VOTRE NOM (3 LETTRES MIN.) - ENTREE POUR CONTINUER"] = "TYPE YOUR NAME (3 LETTERS MIN.) - ENTER TO CONTINUE",

        // Prompts / raccourcis (bas d'écran).
        ["APPUYEZ SUR E POUR PARLER"] = "PRESS E TO TALK",
        ["APPUYEZ SUR ECHAP POUR SORTIR"] = "PRESS ESCAPE TO EXIT",
        ["APPUYEZ SUR ENTREE"] = "PRESS ENTER",
        ["C : CREER UNE GUILDE"] = "C: CREATE A GUILD",
        ["CLIC OU ENTREE : VALIDER - TAB : ACHAT/VENTE - ECHAP : FERMER"] = "CLICK OR ENTER: CONFIRM - TAB: BUY/SELL - ESCAPE: CLOSE",
        ["CLIC OU ENTREE : VOYAGER - HAUT/BAS : CHOISIR - ECHAP : ANNULER"] = "CLICK OR ENTER: TRAVEL - UP/DOWN: CHOOSE - ESCAPE: CANCEL",
        ["CODE D'INVITATION (5 CHIFFRES) :"] = "INVITE CODE (5 DIGITS):",
        ["CODE DU GROUPE A REJOINDRE (5 CHIFFRES) :"] = "PARTY CODE TO JOIN (5 DIGITS):",
        ["D : BANQUE   K : COFFRE   L : CLASSEMENT   W : GUERRE"] = "D: BANK   K: CHEST   L: LEADERBOARD   W: WAR",
        ["D : description - GAUCHE/DROITE : titre - HAUT/BAS : monture - TAB : ailes - ECHAP : fermer"] = "D: description - LEFT/RIGHT: title - UP/DOWN: mount - TAB: wings - ESCAPE: close",
        ["DEFIER EN DUEL"] = "CHALLENGE TO DUEL",
        ["ECHAP : FERMER"] = "ESCAPE: CLOSE",
        ["ECHAP : fermer"] = "Escape: close",
        ["ECHAP POUR ANNULER"] = "ESCAPE TO CANCEL",
        ["ECHAP POUR FERMER"] = "ESCAPE TO CLOSE",
        ["ECHAP POUR REVENIR"] = "ESCAPE TO GO BACK",
        ["EN LIGNE"] = "ONLINE",
        ["ENTREE : CONFIRMER - ECHAP : RETOUR"] = "ENTER: CONFIRM - ESCAPE: BACK",
        ["ENTREE : CREER UN GROUPE"] = "ENTER: CREATE A PARTY",
        ["ENTREE : DEFIER - ECHAP : ANNULER"] = "ENTER: CHALLENGE - ESCAPE: CANCEL",
        ["ENTREE : ENVOYER - ECHAP : ANNULER"] = "ENTER: SEND - ESCAPE: CANCEL",
        ["ENTREE : EQUIPER - ECHAP : ANNULER"] = "ENTER: EQUIP - ESCAPE: CANCEL",
        ["ENTREE : REJOINDRE - ECHAP : NOUVELLE RECHERCHE"] = "ENTER: JOIN - ESCAPE: NEW SEARCH",
        ["ENTREE : VALIDER - ECHAP : ANNULER"] = "ENTER: CONFIRM - ESCAPE: CANCEL",
        ["ENTREE : VALIDER - ECHAP : ANNULER LA SAISIE"] = "ENTER: CONFIRM - ESCAPE: CANCEL INPUT",
        ["ENTREE OU CLIC : EPINGLER/DESEPINGLER - ECHAP : FERMER"] = "ENTER OR CLICK: PIN/UNPIN - ESCAPE: CLOSE",
        ["ENTREE POUR CONTINUER"] = "ENTER TO CONTINUE",
        ["ENTREE POUR RECHERCHER - ECHAP POUR ANNULER"] = "ENTER TO SEARCH - ESCAPE TO CANCEL",
        ["ENTREE POUR REJOINDRE - ECHAP POUR ANNULER"] = "ENTER TO JOIN - ESCAPE TO CANCEL",
        ["ENTREE POUR VALIDER - ECHAP POUR ANNULER"] = "ENTER TO CONFIRM - ESCAPE TO CANCEL",
        ["F9 POUR CHANGER"] = "F9 TO CHANGE",
        ["FLECHES : CHOISIR - ENTREE : REJOINDRE LA FILE"] = "ARROWS: CHOOSE - ENTER: JOIN QUEUE",
        ["FLECHES : NAVIGUER - ECHAP OU F1 : FERMER"] = "ARROWS: NAVIGATE - ESCAPE OR F1: CLOSE",
        ["FLECHES OU CLIC : CHOISIR - ENTREE : RECLAMER"] = "ARROWS OR CLICK: CHOOSE - ENTER: CLAIM",
        ["FLECHES POUR CHOISIR - ENTREE POUR VALIDER"] = "ARROWS TO CHOOSE - ENTER TO CONFIRM",
        ["GAUCHE/DROITE : categorie - ECHAP : fermer"] = "LEFT/RIGHT: category - ESCAPE: close",
        ["GAUCHE/DROITE : classement - ENTREE : attaquer - ECHAP : fermer"] = "LEFT/RIGHT: leaderboard - ENTER: attack - ESCAPE: close",
        ["HAUT/BAS : CHAMP - GAUCHE/DROITE : VALEUR - ENTREE : CONTINUER"] = "UP/DOWN: FIELD - LEFT/RIGHT: VALUE - ENTER: CONTINUE",
        ["HAUT/BAS : CHOISIR - ENTREE : VALIDER - ECHAP : FERMER (F2)"] = "UP/DOWN: CHOOSE - ENTER: CONFIRM - ESCAPE: CLOSE (F2)",
        ["HAUT/BAS : DEFILER - ECHAP : FERMER"] = "UP/DOWN: SCROLL - ESCAPE: CLOSE",
        ["HAUT/BAS : ROYAUME - ENTREE : CONTINUER"] = "UP/DOWN: KINGDOM - ENTER: CONTINUE",
        ["HAUT/BAS : choisir - C OU CLIC : fabriquer - ECHAP : fermer"] = "UP/DOWN: choose - C OR CLICK: craft - ESCAPE: close",
        ["HAUT/BAS : choisir - ENTREE : MP/accepter - SUPPR : retirer - A : ajouter - ECHAP : fermer"] = "UP/DOWN: choose - ENTER: whisper/accept - DEL: remove - A: add - ESCAPE: close",
        ["HAUT/BAS : choisir - ENTREE : reclamer - ECHAP : fermer"] = "UP/DOWN: choose - ENTER: claim - ESCAPE: close",
        ["HAUT/BAS : choisir - TAB : hardcore/normal - ENTREE : entrer - ECHAP : fermer"] = "UP/DOWN: choose - TAB: hardcore/normal - ENTER: enter - ESCAPE: close",
        ["HAUT/BAS : parcourir - ECHAP : FERMER"] = "UP/DOWN: browse - ESCAPE: CLOSE",
        ["HAUT/BAS : parcourir - TAB : especes/montures - ECHAP : fermer"] = "UP/DOWN: browse - TAB: species/mounts - ESCAPE: close",
        ["HAUT/BAS : parcourir la route - ECHAP : FERMER"] = "UP/DOWN: browse the track - ESCAPE: CLOSE",
        ["I : DEPOSER 1ER OBJET DE L'INVENTAIRE - O : RETIRER LA SELECTION"] = "I: DEPOSIT 1ST INVENTORY ITEM - O: WITHDRAW SELECTION",
        ["J : REJOINDRE PAR IDENTIFIANT"] = "J: JOIN BY ID",
        ["L : QUITTER LE GROUPE"] = "L: LEAVE PARTY",
        ["MEILLEURS JOUEURS DE VOTRE ROYAUME (PVP)"] = "TOP PLAYERS OF YOUR KINGDOM (PVP)",
        ["MONTANT A DEPOSER A LA BANQUE :"] = "AMOUNT TO DEPOSIT AT THE BANK:",
        ["NOM A RECHERCHER (VIDE = TOUTES) :"] = "NAME TO SEARCH (EMPTY = ALL):",
        ["NOM DE LA NOUVELLE GUILDE :"] = "NAME OF THE NEW GUILD:",
        ["NOM DU CANDIDAT (VOTRE ROYAUME) :"] = "CANDIDATE NAME (YOUR KINGDOM):",
        ["R : RECHERCHER / REJOINDRE UNE GUILDE"] = "R: SEARCH / JOIN A GUILD",
        ["R OU CLIC : RECOLTER - ECHAP : FERMER"] = "R OR CLICK: GATHER - ESCAPE: CLOSE",
        ["T : TP VERS LE RAPPORTEUR - Y : TP VERS LE SIGNALE - ENTREE : MARQUER TRAITE - ECHAP : FERMER"] = "T: TP TO REPORTER - Y: TP TO REPORTED - ENTER: MARK RESOLVED - ESCAPE: CLOSE",
        ["TAB : CANAL - ENTREE : ENVOYER - ECHAP : FERMER"] = "TAB: CHANNEL - ENTER: SEND - ESCAPE: CLOSE",
        ["BUTIN - CHOISISSEZ UN OBJET"] = "LOOT - CHOOSE AN ITEM",
        ["Une victoire rapporte des points de guerre a votre guilde."] = "A victory earns war points for your guild.",
        ["Une victoire rapporte des points de guerre a votre royaume."] = "A victory earns war points for your kingdom.",
        ["Affrontez un joueur d'un autre royaume."] = "Fight a player from another kingdom.",
        ["Affrontez un membre d'une autre guilde en duel amical."] = "Fight a member of another guild in a friendly duel.",

        // Amis / guilde / bannissement / divers (DrawText non centré).
        ["AMIS :"] = "FRIENDS:",
        ["Acheter des gemmes avec de l'argent reel :"] = "Buy gems with real money:",
        ["DEMANDES RECUES :"] = "RECEIVED REQUESTS:",
        ["Description (200 caracteres max) :"] = "Description (200 characters max):",
        ["Description :"] = "Description:",
        ["ECHAP : QUITTER LE DONJON - ZQSD/FLECHES : SE DEPLACER"] = "ESCAPE: LEAVE DUNGEON - WASD/ARROWS: MOVE",
        ["GRATUIT"] = "FREE",
        ["HEBDOMADAIRES"] = "WEEKLY",
        ["MEMBRES :"] = "MEMBERS:",
        ["MENSUELS"] = "MONTHLY",
        ["Nom du personnage a ajouter :"] = "Name of the character to add:",
        ["PALIER"] = "TIER",
        ["PREMIUM"] = "PREMIUM",
        ["Pseudo du joueur a defier :"] = "Username of the player to challenge:",
        ["Si son groupe (ou le votre) compte plusieurs joueurs,"] = "If their party (or yours) has multiple players,",
        ["tous ses membres devront accepter pour lancer le combat."] = "all its members will need to accept to start the fight.",

        // Tutoriel (voir TutorialPages) : titres.
        ["BIENVENUE DANS AETHERIA"] = "WELCOME TO AETHERIA",
        ["SE DEPLACER"] = "MOVING AROUND",
        ["INTERAGIR"] = "INTERACTING",
        ["PANNEAUX EN JEU"] = "IN-GAME PANELS",
        ["COMBAT"] = "COMBAT",
        ["TYPES ELEMENTAIRES"] = "ELEMENT TYPES",

        // Tutoriel : lignes purement informatives (Action vide).
        ["Ce tutoriel explique les bases du jeu."] = "This tutorial explains the basics of the game.",
        ["Approchez-vous d'un PNJ, d'un bâtiment ou d'un donjon :"] = "Approach an NPC, a building or a dungeon:",
        ["un message apparaît en bas de l'écran."] = "a message appears at the bottom of the screen.",
        ["Boutique, Hôtel des ventes, Forge, Mine, Pension et"] = "Shop, Auction House, Forge, Mine, Inn and",
        ["Téléporteur s'ouvrent en visitant leur bâtiment en ville."] = "Teleporter open by visiting their building in town.",
        ["Un donjon apparaît à un endroit aléatoire de la carte"] = "A dungeon appears at a random spot on the map",
        ["et change de position toutes les heures."] = "and changes position every hour.",
        ["(combats, coffres d'or, et autres événements)."] = "(fights, gold chests, and other events).",
        ["Feu > Nature, Glace   Eau > Feu, Terre"] = "Fire > Nature, Ice   Water > Fire, Earth",
        ["Nature > Eau, Terre   Glace > Nature, Air"] = "Nature > Water, Earth   Ice > Nature, Air",
        ["Foudre > Eau, Air     Terre > Foudre, Feu"] = "Lightning > Water, Air   Earth > Lightning, Fire",
        ["Air > Terre, Nature   Lumière > Ombre"] = "Air > Earth, Nature   Light > Shadow",
        ["Ombre > Lumière       Neutre : sans avantage"] = "Shadow > Light   Neutral: no advantage",
        ["'>' = 1.5x dégâts infligés, 0.67x dégâts subis en retour."] = "'>' = 1.5x damage dealt, 0.67x damage taken in return.",

        // Tutoriel : touches d'action (voir "[{step.Action}]" dans DrawTutorialOverlay).
        ["[FLECHES G/D OU ENTREE]"] = "[ARROWS L/R OR ENTER]",
        ["[ECHAP]"] = "[ESCAPE]",
        ["[W A S D (OU Z Q S D)]"] = "[W A S D]",
        ["[CLIC SUR LA CARTE]"] = "[CLICK THE MAP]",
        ["[F9]"] = "[F9]",
        ["[E]"] = "[E]",
        ["[I]"] = "[I]",
        ["[M]"] = "[M]",
        ["[P]"] = "[P]",
        ["[G]"] = "[G]",
        ["[V]"] = "[V]",
        ["[1]"] = "[1]",
        ["[2]"] = "[2]",
        ["[3]"] = "[3]",
        ["[4]"] = "[4]",
        ["[FLECHES + ENTREE OU CLIC]"] = "[ARROWS + ENTER OR CLICK]",
        ["[ENTREE]"] = "[ENTER]",

        // Tutoriel : effets d'action (voir " {step.Effect}" dans DrawTutorialOverlay — espace initial inclus).
        [" avancer dans ce tutoriel."] = " to move through this tutorial.",
        [" fermer ce tutoriel (F1 pour le rouvrir à tout moment)."] = " to close this tutorial (F1 to reopen it any time).",
        [" se déplacer sur la carte."] = " to move around the map.",
        [" tracer un chemin jusqu'à la case cliquée."] = " to trace a path to the clicked tile.",
        [" changer la disposition clavier détectée."] = " to change the detected keyboard layout.",
        [" parler au PNJ ou entrer dans le bâtiment/donjon."] = " to talk to the NPC or enter the building/dungeon.",
        [" ouvrir l'Inventaire."] = " to open the Inventory.",
        [" ouvrir la liste de vos Monstres."] = " to open your Monster list.",
        [" ouvrir le Groupe."] = " to open the Party.",
        [" ouvrir la Guilde."] = " to open the Guild.",
        [" ouvrir l'Arène classée."] = " to open the Ranked Arena.",
        [" Déplacer votre créature sur la grille."] = " Move your creature on the grid.",
        [" Attaquer une cible à portée."] = " Attack a target in range.",
        [" Passer votre tour."] = " Pass your turn.",
        [" Capturer (nécessite une Carte de capture)."] = " Capture (requires a Capture Card).",
        [" viser une case en surbrillance."] = " to aim at a highlighted tile.",
        [" avancer de salle en salle une fois la salle nettoyée."] = " to move room to room once the room is cleared.",

        // Panneau Paramètres (voir DrawSettingsPanel).
        ["HAUT/BAS : CHOISIR - GAUCHE/DROITE : CHANGER - ECHAP : FERMER"] = "UP/DOWN: CHOOSE - LEFT/RIGHT: CHANGE - ESCAPE: CLOSE",
        ["JAPONAIS : BIENTOT (police non supportee)"] = "JAPANESE: COMING SOON (font not supported)",

        // Complément (voir retour utilisateur — "quasiment rien en anglais, traduit tout").
        ["ECHAP : ANNULER"] = "ESCAPE: CANCEL",
        ["ENTREE : PRET - ECHAP : FERMER"] = "ENTER: READY - ESCAPE: CLOSE",
        ["ESPECE INCONNUE"] = "UNKNOWN SPECIES",
        ["BOUTIQUE - VENTE"] = "SHOP - SELL",
        ["BOUTIQUE - ACHAT"] = "SHOP - BUY",
        ["HOTEL DES VENTES - DEPOSER"] = "AUCTION HOUSE - DEPOSIT",
        ["HOTEL DES VENTES"] = "AUCTION HOUSE",
        ["APPUYEZ SUR E POUR CONTINUER"] = "PRESS E TO CONTINUE",
        ["APPUYEZ SUR E POUR FERMER"] = "PRESS E TO CLOSE",
        ["APPUYEZ SUR ENTREE POUR AFFRONTER UN MONSTRE SAUVAGE"] = "PRESS ENTER TO FIGHT A WILD MONSTER",
        ["VICTOIRE !"] = "VICTORY!",
        ["DEFAITE..."] = "DEFEAT...",
        ["CHOISISSEZ LA PREMIERE CREATURE (ENTREE)"] = "CHOOSE THE FIRST CREATURE (ENTER)",
        ["CHOISISSEZ LE PREMIER PARENT (ENTREE)"] = "CHOOSE THE FIRST PARENT (ENTER)",
        ["CONTENU END-GAME DEBLOQUE (donjon mythique Sanctuaire Ultime)"] = "END-GAME CONTENT UNLOCKED (Ultimate Sanctuary mythic dungeon)",
        ["Grade au palier maximum (Légende)."] = "Grade at maximum tier (Legend).",
        ["APPUYEZ SUR E POUR ENTRER DANS LE DONJON"] = "PRESS E TO ENTER THE DUNGEON",
        ["ECHAP : RETOUR"] = "ESCAPE: BACK",
        ["P : PRESTIGE (reinitialise le niveau, +5% de stats permanent) - ECHAP : RETOUR"] = "P: PRESTIGE (resets level, +5% permanent stats) - ESCAPE: BACK",
        ["HAUT/BAS : CHOISIR - GAUCHE/DROITE : CHANGER"] = "UP/DOWN: CHOOSE - LEFT/RIGHT: CHANGE",
        ["RETOUR (ECHAP)"] = "BACK (ESCAPE)",

        // Quêtes du fil narratif (voir Server/Persistence/QuestCatalogSeeder.cs) — envoyées par le
        // serveur mais la valeur exacte est fixe/connue, donc traduisible comme n'importe quel
        // autre texte statique (voir retour utilisateur — "des champs comme les quetes ne sont
        // pas traduit").
        ["Une arrivée remarquée"] = "A Remarkable Arrival",
        ["Le Garde royal semble méfiant. Va lui parler pour te présenter."] = "The Royal Guard seems wary. Go talk to him to introduce yourself.",
        ["Faire ses preuves"] = "Proving Yourself",
        ["Des créatures rôdent aux abords de la capitale. Remporte ton premier combat."] = "Creatures are prowling near the capital. Win your first battle.",
        ["Un allié à quatre pattes"] = "A Four-Legged Ally",
        ["Une créature affaiblie ne demande qu'à te suivre. Capture-en une avec une Carte de capture."] = "A weakened creature is eager to follow you. Capture one with a Capture Card.",
        ["Le forgeron a besoin de bras"] = "The Blacksmith Needs a Hand",
        ["Rends-toi à la Forge, parle à l'Apprenti forgeron et fabrique ton premier objet."] = "Head to the Forge, talk to the Blacksmith's Apprentice and craft your first item.",
        ["Les rouages du commerce"] = "The Wheels of Commerce",
        ["La Marchande t'apprendra à acheter et vendre. Fais affaire avec elle une première fois."] = "The Merchant will teach you to buy and sell. Do business with her for the first time.",
        ["Les échos du donjon"] = "Echoes of the Dungeon",
        ["Des bruits inquiétants viennent du donjon voisin. Vas-y voir de plus près."] = "Worrying noises are coming from the nearby dungeon. Go take a closer look.",
        ["Q OU CLIC POUR MASQUER"] = "Q OR CLICK TO HIDE",

        // Dialogues des PNJ (voir Client/World/NpcDialogues.cs).
        ["Halte, voyageur. Je ne te reconnais pas."] = "Halt, traveler. I don't recognize you.",
        ["Les créatures qui sortent des donjons sont de plus en plus nombreuses,"] = "The creatures coming out of the dungeons are growing more numerous,",
        ["et de plus en plus agressives. Le royaume a besoin de gens capables."] = "and more aggressive. The kingdom needs capable people.",
        ["Si tu comptes rester, prouve ta valeur. Fais bon voyage."] = "If you intend to stay, prove your worth. Safe travels.",
        ["Bienvenue a l'Hotel des ventes !"] = "Welcome to the Auction House!",
        ["J'ai les meilleurs prix du royaume."] = "I have the best prices in the kingdom.",
        ["Reviens quand tu auras des objets a vendre."] = "Come back when you have items to sell.",
        ["Le feu de la forge ne s'eteint jamais."] = "The forge's fire never goes out.",
        ["Avec ce qui rôde près des donjons ces temps-ci,"] = "With what's lurking near the dungeons these days,",
        ["tout le monde a besoin d'un bon équipement."] = "everyone needs good equipment.",
        ["La vie au village était paisible, avant."] = "Life in the village used to be peaceful.",
        ["Maintenant on entend des bruits, la nuit, du côté du donjon..."] = "Now we hear noises, at night, coming from the dungeon...",
        ["Le garde dit que ça vient de plus en plus près."] = "The guard says it's getting closer and closer.",
        ["Bienvenue au château, voyageur."] = "Welcome to the castle, traveler.",
        ["Sa Majesté ne reçoit personne aujourd'hui — trop occupée"] = "Her Majesty is not receiving anyone today — too busy",
        ["à débattre de ce qu'il faut faire au sujet des donjons."] = "debating what to do about the dungeons.",
        ["Assieds-toi, la soupe est chaude."] = "Sit down, the soup is hot.",
        ["Les chambres sont à l'étage, si le cœur t'en dit."] = "The rooms are upstairs, if you're interested.",
        ["Les voyageurs se font rares depuis que les créatures"] = "Travelers have become scarce since the creatures",
        ["des donjons rôdent plus près des routes. Sois prudent."] = "from the dungeons prowl closer to the roads. Be careful.",
        ["L'Hôtel des ventes n'a jamais été aussi actif."] = "The Auction House has never been busier.",
        ["Reviens voir le catalogue régulièrement."] = "Come check the catalog regularly.",
        ["Le maître forgeron est occupé avec l'enclume."] = "The master blacksmith is busy with the anvil.",
        ["Reviens plus tard, il te fera peut-être une arme."] = "Come back later, he might make you a weapon.",
        ["Toutes les guildes du royaume sont répertoriées ici."] = "All the guilds of the kingdom are listed here.",
        ["Fonde la tienne, et ton nom y figurera aussi."] = "Found your own, and your name will appear here too.",

        // Voir GDD/demande utilisateur — "traduit le launcher en anglais aussi" (Launcher/MainWindow.xaml, voir loc:Tr).
        ["ACTUALITÉS"] = "NEWS",
        ["ADRESSE DU SERVEUR"] = "SERVER ADDRESS",
        ["Accueil"] = "Home",
        ["Banni"] = "Banned",
        ["Bannir la dernière IP"] = "Ban last IP",
        ["Bannir le compte"] = "Ban account",
        ["Bannissement"] = "Ban",
        ["Basculer mute (tchat)"] = "Toggle mute (chat)",
        ["Boutique (bientôt disponible ici)"] = "Shop (coming soon here)",
        ["Classement"] = "Leaderboard",
        ["Code par feelsman"] = "Code by feelsman",
        ["Communauté (administration)"] = "Community (administration)",
        ["Communauté — Administration"] = "Community — Administration",
        ["Compte connecté"] = "Account connected",
        ["Conditions générales d'utilisation"] = "Terms of Service",
        ["Connexion"] = "Login",
        ["Créer un compte"] = "Create an account",
        ["Créé par feelsman"] = "Created by feelsman",
        ["DISPOSITION CLAVIER"] = "KEYBOARD LAYOUT",
        ["Date (UTC)"] = "Date (UTC)",
        ["Derniere IP"] = "Last IP",
        ["Débannir le compte"] = "Unban account",
        ["Déconnexion"] = "Logout",
        ["Définir le grade"] = "Set rank",
        ["EMAIL (INSCRIPTION UNIQUEMENT)"] = "EMAIL (REGISTRATION ONLY)",
        ["Email"] = "Email",
        ["Enregistrer"] = "Save",
        ["Entrez dans le monde d'Aetheria"] = "Enter the world of Aetheria",
        ["Fermer"] = "Close",
        ["Grade"] = "Rank",
        ["Graphisme par feelsman"] = "Art by feelsman",
        ["IP publique ou nom de domaine du serveur — fonctionne en local comme depuis un autre réseau."] = "Public IP or domain name of the server — works locally as well as from another network.",
        ["JOUER"] = "PLAY",
        ["Japonais bientôt disponible (police non supportée pour l'instant)."] = "Japanese coming soon (font not supported yet).",
        ["LANGUE / LANGUAGE"] = "LANGUAGE",
        ["La sélection et la création de personnage se font en jeu, juste après le lancement."] = "Character selection and creation happen in-game, right after launch.",
        ["Logo par Korbak"] = "Logo by Korbak",
        ["MOT DE PASSE"] = "PASSWORD",
        ["Marquer traité"] = "Mark resolved",
        ["Modération"] = "Moderation",
        ["Muet"] = "Muted",
        ["Musique par Konss"] = "Music by Konss",
        ["Nouveau pseudo (laisser vide si inchangé)"] = "New username (leave blank if unchanged)",
        ["Nouvel email (laisser vide si inchangé)"] = "New email (leave blank if unchanged)",
        ["PSEUDO OU EMAIL"] = "USERNAME OR EMAIL",
        ["Paramètres"] = "Settings",
        ["Pseudo"] = "Username",
        ["Raison"] = "Reason",
        ["Rechercher"] = "Search",
        ["Copier tous les emails"] = "Copy all emails",
        ["Copie les emails ci-dessous, séparés par des virgules — à coller dans le champ Cci de Gmail pour écrire à tout le monde"] = "Copies the emails below, comma-separated — paste into Gmail's Bcc field to email everyone",
        ["Renommer / changer l'email"] = "Rename / change email",
        ["Réglable aussi en jeu avec la touche F9."] = "Also adjustable in-game with the F9 key.",
        ["Réinitialiser le profil de jeu"] = "Reset game profile",
        ["Réservé au compte Fondateur."] = "Reserved for the Founder account.",
        ["Se connecter"] = "Log in",
        ["Signalements de joueurs"] = "Player reports",
        ["Signalé"] = "Reported",
        ["Signalé par"] = "Reported by",
        ["TOUTES LES ACTUALITÉS"] = "ALL NEWS",
        ["Traité"] = "Resolved",
        ["Un MMORPG tactique de créatures et de royaumes."] = "A tactical MMORPG of creatures and kingdoms.",
        ["Voir toutes les actualités"] = "See all news",
        ["À propos"] = "About",

        // Voir GDD/demande utilisateur — "traduit le launcher en anglais aussi" (Launcher/ViewModels/MainViewModel.cs, messages assignés en C#, voir MainViewModel.Tr).
        ["Vérification du serveur..."] = "Checking server...",
        ["Serveur en ligne"] = "Server online",
        ["Serveur hors ligne"] = "Server offline",
        ["Mise à jour en cours"] = "Updating",
        ["Mise à jour"] = "Update",
        ["Détecté sur cette machine"] = "Detected on this machine",
        ["L'email doit être au format exemple@domaine.com."] = "Email must be in the format example@domain.com.",
        ["Compte créé. Vous pouvez maintenant vous connecter."] = "Account created. You can now log in.",
        ["Une nouvelle version est disponible : mettez à jour avant de jouer."] = "A new version is available: update before playing.",

        // Voir GDD/demande utilisateur — "Système d'échange (trade) entre joueurs" (panneau Echange).
        ["ECHANGE"] = "TRADE",
        ["Joueur;indexCreature;orOffert;orDemande"] = "Player;creatureIndex;goldOffered;goldRequested",
        ["VOS CREATURES :"] = "YOUR CREATURES:",
        ["OFFRES RECUES :"] = "INCOMING OFFERS:",
        ["OFFRES ENVOYEES :"] = "OUTGOING OFFERS:",
        ["Aucune offre en attente."] = "No pending offers.",
        ["HAUT/BAS : choisir - ENTREE : accepter - N : refuser - SUPPR : annuler - A : proposer - ECHAP : fermer"] = "UP/DOWN: select - ENTER: accept - N: decline - DEL: cancel - A: propose - ESC: close",

        // Voir GDD/demande utilisateur — "Raids de guilde (boss coopératif nécessitant plusieurs joueurs)".
        ["RAID DE GUILDE"] = "GUILD RAID",
        ["INVOQUER (ENTREE)"] = "SUMMON (ENTER)",
        ["ATTAQUER (ENTREE)"] = "ATTACK (ENTER)",
        ["DEGATS INFLIGES :"] = "DAMAGE DEALT:",

        // Voir GDD/demande utilisateur — "Housing/décoration de guilde ou de royaume".
        ["DECORATIONS DE GUILDE"] = "GUILD DECORATIONS",
        ["[AFFICHEE]"] = "[DISPLAYED]",
        ["[POSSEDEE - ENTREE POUR AFFICHER]"] = "[OWNED - ENTER TO DISPLAY]",
        ["Bannière Écarlate"] = "Scarlet Banner",
        ["Trophée de Dragon"] = "Dragon Trophy",
        ["Fontaine Dorée"] = "Golden Fountain",
        ["Statue du Fondateur"] = "Founder's Statue",
        ["Jardin Suspendu"] = "Hanging Garden",
        ["Tapis Royal"] = "Royal Carpet",
        ["Vitrail Ancien"] = "Ancient Stained Glass",
        ["Blason Légendaire"] = "Legendary Crest",

        // Voir GDD/demande utilisateur — "a la fin des 10 etage termine le dongon [...] ajoute un cooldown de 1h".
        ["Donjon termine."] = "Dungeon complete.",
        ["Donjon termine..."] = "Dungeon complete...",
    };

    /// <summary>
    /// Voir GDD/demande utilisateur — couverture des textes construits par interpolation (voir
    /// <see cref="Translate"/>) : fragments français statiques extraits des chaînes $"..." du
    /// client (portions entre/autour des {valeurs dynamiques}), avec leur traduction anglaise.
    /// Volontairement limité aux fragments d'au moins quelques caractères significatifs — les
    /// fragments trop courts/génériques (ponctuation seule, une lettre...) sont exclus : un
    /// remplacement de sous-chaîne sur un fragment aussi court que "s" ou ")" corromprait la
    /// quasi-totalité du texte du jeu (voir Translate, Contains/Replace).
    /// </summary>
    private static readonly Dictionary<string, string> EnglishFragments = new()
    {
        [" (NIV. "] = " (LVL. ",
        [" (Nv."] = " (Lvl.",
        [" (TAB POUR ANNULER)"] = " (TAB TO CANCEL)",
        [" - NIV. "] = " - LVL. ",
        [" - Nv."] = " - Lvl.",
        [" : SE DEPLACER - F9 : CLAVIER - F1 : AIDE"] = ": MOVE - F9: KEYBOARD - F1: HELP",
        [" ESPECES DECOUVERTES"] = " SPECIES DISCOVERED",
        [" EST EN INCUBATION"] = " IS INCUBATING",
        [" FUSIONNE AVEC "] = " FUSED WITH ",
        [" JOUEURS"] = " PLAYERS",
        [" MEMBRES)"] = " MEMBERS)",
        [" MONTURES POSSEDEES"] = " MOUNTS OWNED",
        [" OR (GAUCHE/DROITE POUR AJUSTER)"] = " GOLD (LEFT/RIGHT TO ADJUST)",
        [" POINTS DE GUERRE CETTE SEMAINE"] = " WAR POINTS THIS WEEK",
        [" SERA CONSOMMEE"] = " WILL BE CONSUMED",
        [" SURVIVRA (NIV. "] = " WILL SURVIVE (LVL. ",
        [" VOUS DEFIE EN DUEL !"] = " CHALLENGES YOU TO A DUEL!",
        [" XP DE GUILDE"] = " GUILD XP",
        [" degats"] = " damage",
        [" joueurs) doit accepter."] = " players) must accept.",
        [" possedee(s))"] = " owned)",
        ["Ailes actives : "] = "Active wings: ",
        ["CONTROLE PAR : "] = "CONTROLLED BY: ",
        ["CONTROLEE PAR : "] = "CONTROLLED BY: ",
        ["EN COURS... ("] = "IN PROGRESS... (",
        ["EN FILE : "] = "IN QUEUE: ",
        ["ENCHERE ACTUELLE : "] = "CURRENT BID: ",
        ["ENCHERIR SUR "] = "BID ON ",
        ["EQUIPER "] = "EQUIP ",
        ["ETAGE "] = "FLOOR ",
        ["GUILDE PRIVEE - CODE D'INVITATION : "] = "PRIVATE GUILD - INVITE CODE: ",
        ["Gemmes : "] = "Gems: ",
        ["MESSAGE PRIVE A "] = "PRIVATE MESSAGE TO ",
        ["Monture active : "] = "Active mount: ",
        ["NIVEAU FINAL : "] = "FINAL LEVEL: ",
        ["NIVEAU "] = "LEVEL ",
        ["Niveau "] = "Level ",
        ["Or : "] = "Gold: ",
        ["RAISON : "] = "REASON: ",
        ["ROYAUME VAINQUEUR : "] = "WINNING KINGDOM: ",
        ["S RESTANT)"] = "S LEFT)",
        ["SALLES NETTOYEES : "] = "ROOMS CLEARED: ",
        ["VAINCU PAR "] = "DEFEATED BY ",
        ["VOTRE OFFRE : "] = "YOUR BID: ",
        ["Votre groupe entier ("] = "Your entire party (",
        [" PV"] = " HP",
        [" OR"] = " GOLD",
        ["CODE : "] = "CODE: ",
        ["Discord : "] = "Discord: ",
        ["Twitch : "] = "Twitch: ",
        ["YouTube : "] = "YouTube: ",
        ["TAB : "] = "TAB: ",
        ["PAGE "] = "PAGE ",
        ["VERSION "] = "VERSION ",

        // Complément (voir retour utilisateur — "quasiment rien en anglais, traduit tout").
        ["PREMIERE : "] = "FIRST: ",
        [" - CHOISISSEZ LA SECONDE"] = " - CHOOSE THE SECOND",
        ["PREMIER PARENT : "] = "FIRST PARENT: ",
        [" - CHOISISSEZ LE SECOND"] = " - CHOOSE THE SECOND",
        ["END-GAME : "] = "END-GAME: ",
        [" especes niv. max - "] = " max-level species - ",
        [" succes"] = " achievements",
        ["[G] Passer "] = "[G] Move to ",
        [" gemmes"] = " gems",
        ["APPUYEZ SUR E POUR PARLER A "] = "PRESS E TO TALK TO ",
        ["APPUYEZ SUR E POUR ENTRER : "] = "PRESS E TO ENTER: ",
        ["Tour de "] = "Turn: ",
        [" (vous)"] = " (you)",
        ["DISPOSITION CLAVIER : "] = "KEYBOARD LAYOUT: ",

        // Voir GDD/demande utilisateur — Echange/Raid de guilde/Decoration/fin de donjon (fragments interpolés).
        [" propose : "] = " offers: ",
        [" contre "] = " for ",
        [" or (ENTREE : accepter, N : refuser)"] = " gold (ENTER: accept, N: decline)",
        [" or (SUPPR : annuler)"] = " gold (DEL: cancel)",
        [" VAINCU PAR "] = " DEFEATED BY ",
        ["Cout d'invocation : "] = "Summon cost: ",
        [" or (banque de guilde : "] = " gold (guild bank: ",
        ["BANQUE : "] = "BANK: ",
        [" OR - ENTREE POUR ACHETER]"] = " GOLD - ENTER TO BUY]",
        ["DONJON TERMINE ! +"] = "DUNGEON COMPLETE! +",
        [" OR. Revient dans 1h."] = " GOLD. Back in 1h.",
        [". Revient dans 1h."] = ". Back in 1h.",
        ["Ce donjon recharge encore ("] = "This dungeon is still on cooldown (",
        [" min)."] = " min).",
        [" [RECHARGE "] = " [COOLDOWN ",
    };

    private static readonly List<string> FragmentKeysByDescendingLength =
        [.. EnglishFragments.Keys.OrderByDescending(k => k.Length)];
}
