# Misty

Launcher Minecraft pour Windows, écrit en **Rust** avec l'interface [Slint](https://slint.dev). Un seul fichier `Misty.exe`, sans runtime à installer.

Interface « verre dépoli » sur fond ciel, fenêtre aux coins arrondis, titres en police pixel, animations fluides (navigation, boutons, progression).

## Fonctionnalités

- **Toutes les versions de Minecraft** : releases, avec en option les snapshots, les bêtas et les alphas/indev
- **Profils** (comme les instances de Modrinth App) : chacun a sa version, son mod loader et son propre dossier (mods, packs, shaders, mondes, options). Les versions, bibliothèques et assets restent partagés
- **Contenu Modrinth et CurseForge** (au choix) : recherche et installation de mods, packs de ressources, datapacks (dans le monde choisi) et shaders, filtrés selon la version et le loader du profil, avec les dépendances requises. Activation/désactivation (`.disabled`) et suppression ; les fichiers ajoutés à la main sont identifiés via leur hash (Modrinth) ou leur empreinte (CurseForge)
- **Modpacks Modrinth** (`.mrpack`) **et CurseForge** : un clic crée un profil prêt à jouer. Les fichiers CurseForge dont l'auteur interdit le téléchargement par d'autres applications sont listés, avec leur page à ouvrir
- **Mod loaders** : Forge, NeoForge, Fabric, Quilt, OptiFine et LiteLoader (seuls les loaders disponibles pour la version choisie sont proposés)
- **Éditeur de packs de textures** (expérimental) : catégories avec compteurs, outils de dessin, couleurs récentes, icône du pack, export ; la texture modifiée dans un éditeur externe (Paint.NET…) est rechargée automatiquement
- **Comptes** : connexion Microsoft (premium) ou comptes hors-ligne (plusieurs pseudos enregistrés)
- **Langues** : français et anglais, le changement s'applique immédiatement
- **RAM allouée** réglable, avec détection de la RAM de la machine ; le launcher lui-même consomme très peu de mémoire
- **Discord Rich Presence** (désactivable) : affiche « Dans le launcher » ou la version en cours de jeu
- **Mise à jour automatique** depuis les releases GitHub
- **Raccourci** Windows créé et mis à jour automatiquement
- Icône personnalisable : il suffit de poser un fichier `Icon.ico` à côté de `Misty.exe`

## Installation (utilisateur)

**Recommandé :** télécharger `Misty-Setup-<version>.exe` depuis la [dernière release](https://github.com/Misty-MC/Misty/releases/latest) et le lancer.

- Pas besoin de droits administrateur : Misty s'installe dans `%LOCALAPPDATA%\Programs\Misty`.
- Rien d'autre à installer : `Misty.exe` contient tout (polices, traductions, images), et Java est téléchargé automatiquement quand le jeu en a besoin.
- Raccourci dans le menu Démarrer (et sur le bureau en option), désinstallation depuis « Applications installées ». Les données de jeu ne sont supprimées que si tu le demandes.

**Version portable :** télécharger le zip, l'extraire et lancer `Misty-<version>/Misty.exe`.

Dans les deux cas, les mises à jour suivantes s'installent toutes seules.

## Développement

### Prérequis

- Windows 10/11
- [Rust](https://rustup.rs) (toolchain stable, cible MSVC) avec les outils de build C++ de Visual Studio

### Clé API CurseForge

L'API CurseForge exige une clé personnelle (gratuite, à demander sur [console.curseforge.com](https://console.curseforge.com)). Elle doit rester **privée** :

1. Colle-la dans `misty-rust/curseforge.key` (fichier ignoré par git).
2. Recompile : elle est intégrée à l'exe au build.

Alternative : la variable d'environnement `MISTY_CURSEFORGE_KEY`. Sans clé, l'onglet CurseForge explique comment la configurer ; Modrinth fonctionne normalement.

### Compiler et lancer

```powershell
cd misty-rust
cargo run              # version de développement
cargo build --release  # exe final : target\release\Misty.exe
```

Tests (ils utilisent Internet et ton dossier Misty, mais ne lancent jamais le jeu) :

```powershell
cargo test -- --ignored --nocapture
```

### Publier une release

1. Mettre à jour `VERSION` dans [misty-rust/src/services/app_info.rs](misty-rust/src/services/app_info.rs) et `version` dans [misty-rust/Cargo.toml](misty-rust/Cargo.toml), puis ajouter l'entrée dans [CHANGELOG.md](CHANGELOG.md). Ce fichier est intégré à l'exe et affiché dans la page « Notes de version ».
2. Lancer le script de release (il faut [Inno Setup 6](https://jrsoftware.org/isdl.php) : `winget install JRSoftware.InnoSetup`) :

   ```powershell
   powershell -ExecutionPolicy Bypass -File installer\build.ps1
   ```

   Il produit dans `publish\` :
   - `Misty-<version>\Misty.exe` : l'application ;
   - `Misty-<version>.zip` : le dossier `Misty-<version>` à sa racine, c'est ce qu'attend le système de mise à jour ;
   - `Misty-Setup-<version>.exe` : l'installateur (script : [installer/Misty.iss](installer/Misty.iss)).

   `-SansInstallateur` ne génère que le dossier et le zip.
3. Créer une release GitHub avec le tag `<version>` et y joindre **le zip et l'installateur** (la mise à jour automatique prend le `.zip`).

> ⚠️ `VERSION`, le tag GitHub (sans le `v`) et le nom du dossier `Misty-<version>` doivent être identiques. Sinon le launcher reproposera la mise à jour en boucle.

## Structure du projet

```
CHANGELOG.md                Notes de version (intégrées à l'exe)
installer/                  Script de release (build.ps1) + installateur Inno Setup (Misty.iss)
misty-rust/
├── Cargo.toml
├── build.rs                Compile l'interface, intègre l'icône, la clé CurseForge et le changelog
├── assets/                 Icône, polices pixel Silkscreen (licence OFL), drapeaux
├── lang/                   Traductions (fr.json, en.json), intégrées à l'exe
├── ui/                     Interface Slint
│   ├── theme.slint         Couleurs, polices, traductions (Tr.t("Cle"))
│   ├── widgets.slint       Boutons, listes déroulantes, interrupteurs, curseurs…
│   ├── state.slint         État de chaque page, partagé avec Rust
│   ├── main.slint          Fenêtre sans bordure, barre latérale, boîtes de dialogue
│   └── home / profiles / instance / browser / settings / changelog / packs / editor .slint
└── src/
    ├── main.rs             Démarrage
    ├── ms_login.rs         Fenêtre de connexion Microsoft (sous-processus « Misty.exe --ms-login »)
    ├── ui/                 Le code derrière chaque page
    ├── minecraft/          Installation et lancement du jeu
    │   ├── install.rs      JSON de version, jar, bibliothèques, assets
    │   ├── java.rs         Java fourni par Mojang
    │   ├── launch.rs       Ligne de commande Java
    │   └── loaders/        Forge, NeoForge, Fabric/Quilt, OptiFine, LiteLoader
    └── services/           La logique, sans interface
        ├── paths.rs        Tous les chemins (dossier de données, exe, icône…)
        ├── settings.rs     Lecture/écriture de data.txt
        ├── pseudos.rs      Comptes hors-ligne (listpseudo.txt)
        ├── account.rs      Session Microsoft partagée entre les pages
        ├── instances.rs    Profils : création, liste, suppression, dossier de jeu
        ├── content.rs      Contenu d'un profil : installation + dépendances, liste, activation, identification
        ├── modpack.rs      Installation des modpacks (.mrpack et CurseForge)
        ├── modrinth.rs     Client de l'API Modrinth v2
        ├── curseforge.rs   Client de l'API CurseForge v1 (clé API, empreintes MurmurHash2)
        ├── catalogue.rs    Recherche commune aux deux plateformes
        ├── packs.rs        Packs de textures (éditeur)
        ├── version_compat.rs  Plages de versions supportées par chaque loader
        ├── update.rs       Mise à jour via GitHub
        ├── shortcut.rs     Création du raccourci Misty.lnk
        ├── discord.rs      Rich Presence
        ├── loc.rs          Traductions
        └── download.rs, http.rs, system_info.rs …
src/Misty/                  Ancienne version C# / WPF (conservée pour référence, plus maintenue)
```

### Données utilisateur

Tout est stocké dans `%LOCALAPPDATA%\Misty\` :

| Fichier / dossier | Contenu |
|---|---|
| `data.txt` | Configuration `cle=valeur` : dernier pseudo, dernière version, RAM, langue, premium, raccourci, snapshot/beta/alpha, Rich Presence, dernier profil utilisé |
| `listpseudo.txt` | Comptes hors-ligne (`listpseudo=a,b,c`) |
| `msa.json` | Compte Microsoft connecté |
| `instances/<id>/` | Un dossier par profil : `instance.json`, `mods/`, `resourcepacks/`, `shaderpacks/`, `saves/`, `misty-contenu.json` (métadonnées du contenu installé) |
| `versions/`, `libraries/`, `assets/`, Java… | Installation Minecraft |

Si une clé est ajoutée ou retirée de `data.txt`, il faut incrémenter `DATA_VERSION` dans `app_info.rs` et ajouter la clé dans `services/settings.rs`. Le fichier est alors réécrit au démarrage sans perdre les valeurs existantes.

## Dépendances principales

| Crate | Rôle |
|---|---|
| slint | Interface graphique |
| tokio, reqwest | Asynchrone et réseau (API Modrinth / CurseForge, téléchargements) |
| wry, tao | Fenêtre web de connexion Microsoft |
| discord-rich-presence | Statut Discord |
| zip, sha1, image | Archives, vérification des fichiers, images |
| windows-sys, winreg, mslnk | Intégration Windows (coins arrondis, raccourcis) |

## Licence

[MIT](LICENSE.txt)
