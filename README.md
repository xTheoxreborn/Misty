# Misty

Launcher Minecraft pour Windows (WPF, .NET 10), basé sur [CmlLib.Core](https://github.com/CmlLib/CmlLib.Core).

Interface « verre dépoli » sur fond ciel, titres en police pixel, animations fluides (navigation, boutons, progression).

## Fonctionnalités

- **Toutes les versions de Minecraft** : releases, avec en option les snapshots, les bêtas et les alphas/indev
- **Profils** (comme les instances de Modrinth App) : chacun a sa version, son mod loader et son propre dossier (mods, packs, shaders, mondes, options). Les versions, bibliothèques et assets restent partagés
- **Contenu Modrinth et CurseForge** (au choix) : recherche et installation de mods, packs de ressources, datapacks (dans le monde choisi) et shaders, filtrés selon la version et le loader du profil, avec les dépendances requises. Activation/désactivation (`.disabled`) et suppression ; les fichiers ajoutés à la main sont identifiés via leur hash (Modrinth) ou leur empreinte (CurseForge)
- **Modpacks Modrinth** (`.mrpack`) **et CurseForge** : un clic crée un profil prêt à jouer. Les fichiers CurseForge dont l'auteur interdit le téléchargement par d'autres applications sont listés, avec leur page à ouvrir
- **Mod loaders** : Forge, NeoForge, Fabric, Quilt, OptiFine et LiteLoader (seuls les loaders disponibles pour la version choisie sont proposés)
- **Comptes** : connexion Microsoft (premium) ou comptes hors-ligne (plusieurs pseudos enregistrés)
- **RAM allouée** réglable, avec détection de la RAM de la machine
- **Discord Rich Presence** : affiche « Dans le launcher » ou la version en cours de jeu
- **Mise à jour automatique** depuis les releases GitHub
- **Raccourci** Windows créé et mis à jour automatiquement
- Icône personnalisable : il suffit de poser un fichier `Icon.ico` à côté de `Misty.exe`

## Installation (utilisateur)

1. Télécharger le zip de la [dernière release](https://github.com/xTheoxreborn/Misty/releases/latest).
2. L'extraire. Il contient un dossier `Misty-<version>`.
3. Lancer `Misty-<version>/Misty.exe`.

Les mises à jour suivantes s'installent toutes seules à côté de ce dossier.

## Développement

### Prérequis

- Windows 10/11
- [SDK .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2022 17.14+ / Visual Studio 2026 (pour ouvrir `Misty.slnx`), ou VS Code / Rider

### Clé API CurseForge

L'API CurseForge exige une clé personnelle (gratuite, à demander sur [console.curseforge.com](https://console.curseforge.com)). Elle doit rester **privée** :

1. Colle-la dans `src/Misty/curseforge.key` (fichier ignoré par git).
2. Recompile : elle est intégrée à l'exe au build.

Alternative : la variable d'environnement `MISTY_CURSEFORGE_KEY`. Sans clé, l'onglet CurseForge explique comment la configurer ; Modrinth fonctionne normalement.

### Compiler et lancer

```powershell
dotnet restore          # installe les dépendances NuGet
dotnet build            # compile
dotnet run --project src/Misty
```

### Publier une release

```powershell
dotnet publish src/Misty -c Release -r win-x64 --self-contained false -o publish/Misty-<version>
```

Puis :

1. Mettre à jour `AppInfo.Version` dans [src/Misty/AppInfo.cs](src/Misty/AppInfo.cs) et ajouter l'entrée dans [CHANGELOG.md](CHANGELOG.md). Ce fichier est intégré à l'exe et affiché dans la page « Notes de version ».
2. Zipper le dossier `Misty-<version>` : le zip doit contenir ce dossier à sa racine, c'est ce qu'attend le système de mise à jour.
3. Créer une release GitHub avec le tag `v<version>` et le zip comme **premier** asset.

> ⚠️ `AppInfo.Version`, le tag GitHub (sans le `v`) et le nom du dossier `Misty-<version>` doivent être identiques. Sinon le launcher reproposera la mise à jour en boucle.

## Structure du projet

```
Misty.slnx
src/Misty/
├── App.xaml                    Point d'entrée, charge le thème
├── AppInfo.cs                  Version de l'app, version de data.txt, IDs (GitHub, Discord)
├── Assets/
│   ├── icone.ico               Icône de l'exécutable
│   └── Fonts/                  Police pixel Silkscreen (licence OFL)
├── Themes/
│   ├── Colors.xaml             Palette, dégradés, polices
│   └── Controls.xaml           Styles : boutons, ComboBox, interrupteurs, slider, barre de progression…
├── Models/                     Instance (profil), contenu installé, DTO des API Modrinth et CurseForge
├── Controls/
│   ├── OutlinedText.cs         Texte pixel avec contour + ombre (titres)
│   └── PixelArt.cs             Bloc d'herbe généré en pixel art
├── Views/
│   ├── MainWindow              Cadre sans bordure, barre latérale, navigation, boîtes de dialogue
│   ├── HomeView                Accueil : compte, profil (ou jeu rapide), bouton JOUER
│   ├── ProfilesView            Liste des profils, création, installation de modpacks
│   ├── InstanceView            Détail d'un profil : contenu installé + onglet Découvrir
│   ├── ContentBrowser          Explorateur Modrinth / CurseForge réutilisable (recherche, tri, défilement infini)
│   ├── SettingsView            Options (comptes hors-ligne, Microsoft, RAM, raccourci, versions, stockage)
│   └── ChangelogView           Notes de version (lues depuis CHANGELOG.md)
└── Services/
    ├── AppPaths.cs             Tous les chemins (dossier de données, exe, icône…)
    ├── SettingsStore.cs        Lecture/écriture de data.txt
    ├── ProfileStore.cs         Liste des pseudos hors-ligne (listpseudo.txt)
    ├── ModLoaderService.cs     Détection + installation des mod loaders
    ├── VersionCompatibility.cs Plages de versions supportées par chaque loader
    ├── UpdateService.cs        Mise à jour via GitHub + migration de l'ancien dossier
    ├── ShortcutService.cs      Création du raccourci Misty.lnk
    ├── DiscordPresenceService.cs
    ├── AccountService.cs       Session Microsoft partagée entre les pages
    ├── Dialogs.cs              Boîtes de dialogue intégrées à la fenêtre
    ├── SystemInfo.cs           RAM totale, taille d'un dossier
    ├── MinecraftService.cs     Launcher CmlLib partagé + liste des versions
    ├── InstanceStore.cs        Profils : création, liste, suppression, dossier de jeu
    ├── ContentService.cs       Contenu d'un profil : installation (Modrinth / CurseForge) + dépendances, liste, activation, identification
    ├── ModpackService.cs       Installation des modpacks (.mrpack et CurseForge)
    ├── ModrinthApi.cs          Client de l'API Modrinth v2
    ├── CurseForgeApi.cs        Client de l'API CurseForge v1 (clé API, empreintes MurmurHash2)
    ├── Catalogue.cs            Recherche commune aux deux plateformes
    ├── Telechargement.cs       Téléchargement avec vérification SHA-1
    └── Images.cs               Chargement des icônes
```

### Données utilisateur

Tout est stocké dans `%LOCALAPPDATA%\Misty\` :

| Fichier / dossier | Contenu |
|---|---|
| `data.txt` | Configuration `cle=valeur` : dernier pseudo, dernière version, RAM, premium, raccourci, snapshot/beta/alpha, dossier à supprimer après une mise à jour, dernier profil utilisé |
| `listpseudo.txt` | Comptes hors-ligne (`listpseudo=a,b,c`) |
| `instances/<id>/` | Un dossier par profil : `instance.json`, `mods/`, `resourcepacks/`, `shaderpacks/`, `saves/`, `misty-contenu.json` (métadonnées Modrinth du contenu installé) |
| `versions/`, `libraries/`, `assets/`… | Installation Minecraft gérée par CmlLib |

Si une clé est ajoutée ou retirée de `data.txt`, il faut incrémenter `AppInfo.DataVersion` et ajouter la clé dans `SettingsStore`. Le fichier est alors réécrit au démarrage sans perdre les valeurs existantes.

## Dépendances

Les API Modrinth et CurseForge sont appelées directement (HttpClient + System.Text.Json), sans package dédié.

| Package | Rôle |
|---|---|
| CmlLib.Core | Téléchargement et lancement de Minecraft |
| CmlLib.Core.Auth.Microsoft | Connexion compte Microsoft |
| CmlLib.Core.Installer.Forge / .NeoForge | Installation Forge / NeoForge |
| Optifine.Installer | Installation OptiFine |
| DiscordRichPresence | Statut Discord |

## Licence

[MIT](LICENSE.txt)
