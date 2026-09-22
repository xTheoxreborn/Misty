# Misty

Launcher Minecraft pour Windows (WinForms, .NET 10), basé sur [CmlLib.Core](https://github.com/CmlLib/CmlLib.Core).

## Fonctionnalités

- **Toutes les versions de Minecraft** : releases, avec en option les snapshots, les bêtas et les alphas/indev
- **Mod loaders** : Forge, NeoForge, Fabric, OptiFine et LiteLoader (seuls les loaders disponibles pour la version choisie sont proposés)
- **Comptes** : connexion Microsoft (premium) ou profils hors-ligne (plusieurs pseudos enregistrés)
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
- Visual Studio 2022 17.14+ / Visual Studio 2026 (pour ouvrir `Misty.slnx` et le designer WinForms), ou VS Code / Rider

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

1. Mettre à jour `AppInfo.Version` dans [src/Misty/AppInfo.cs](src/Misty/AppInfo.cs), ajouter l'entrée dans [CHANGELOG.md](CHANGELOG.md) et dans `ChangelogForm`.
2. Zipper le dossier `Misty-<version>` : le zip doit contenir ce dossier à sa racine, c'est ce qu'attend le système de mise à jour.
3. Créer une release GitHub avec le tag `v<version>` et le zip comme **premier** asset.

> ⚠️ `AppInfo.Version`, le tag GitHub (sans le `v`) et le nom du dossier `Misty-<version>` doivent être identiques. Sinon le launcher reproposera la mise à jour en boucle.

## Structure du projet

```
Misty.slnx
src/Misty/
├── Program.cs                  Point d'entrée
├── AppInfo.cs                  Version de l'app, version de data.txt, IDs (GitHub, Discord)
├── Assets/icone.ico            Icône de l'exécutable
├── Forms/
│   ├── MainForm                Fenêtre principale (compte, version, loader, lancement)
│   ├── SettingsForm            Options (profils, RAM, raccourci, types de versions, dossier)
│   └── ChangelogForm           Notes de version (clic sur le numéro de version)
└── Services/
    ├── AppPaths.cs             Tous les chemins (dossier de données, exe, icône…)
    ├── SettingsStore.cs        Lecture/écriture de data.txt
    ├── ProfileStore.cs         Liste des pseudos hors-ligne (listpseudo.txt)
    ├── ModLoaderService.cs     Détection + installation des mod loaders
    ├── VersionCompatibility.cs Plages de versions supportées par chaque loader
    ├── UpdateService.cs        Mise à jour via GitHub + migration de l'ancien dossier
    ├── ShortcutService.cs      Création du raccourci Misty.lnk
    ├── DiscordPresenceService.cs
    ├── SystemInfo.cs           RAM totale, taille d'un dossier
    └── ModrinthService.cs      Recherche de mods Modrinth (en cours, pas encore dans l'interface)
```

### Données utilisateur

Tout est stocké dans `%LOCALAPPDATA%\Misty\` :

| Fichier / dossier | Contenu |
|---|---|
| `data.txt` | Configuration `cle=valeur` : dernier pseudo, dernière version, RAM, premium, raccourci, snapshot/beta/alpha, dossier à supprimer après une mise à jour |
| `listpseudo.txt` | Profils hors-ligne (`listpseudo=a,b,c`) |
| `versions/`, `libraries/`, `assets/`… | Installation Minecraft gérée par CmlLib |

Si une clé est ajoutée ou retirée de `data.txt`, il faut incrémenter `AppInfo.DataVersion` et ajouter la clé dans `SettingsStore`. Le fichier est alors réécrit au démarrage sans perdre les valeurs existantes.

## Dépendances

| Package | Rôle |
|---|---|
| CmlLib.Core | Téléchargement et lancement de Minecraft |
| CmlLib.Core.Auth.Microsoft | Connexion compte Microsoft |
| CmlLib.Core.Installer.Forge / .NeoForge | Installation Forge / NeoForge |
| Optifine.Installer | Installation OptiFine |
| DiscordRichPresence | Statut Discord |
| Modrinth.Net | API Modrinth |

## Licence

[MIT](LICENSE.txt)
