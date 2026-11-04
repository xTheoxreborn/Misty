# Misty — version Rust

Le launcher Misty réécrit entièrement en Rust, avec l'interface en [Slint](https://slint.dev).
Il reprend toutes les fonctions de la version C# / WPF et **les mêmes fichiers de données**
(`%LOCALAPPDATA%\Misty` : `data.txt`, `listpseudo.txt`, `instances\…\instance.json`,
`misty-contenu.json`, versions, bibliothèques, Java…). Rien n'est à réinstaller.

## Compiler et lancer

```powershell
cargo run              # version de développement (avec console pour voir les erreurs)
cargo build --release  # exe final : target\release\misty.exe
```

La clé API CurseForge est lue au moment de la compilation depuis `misty-rust\curseforge.key`
ou `src\Misty\curseforge.key` (comme avant), ou à l'exécution depuis la variable
d'environnement `MISTY_CURSEFORGE_KEY`.

Tests (ils utilisent Internet et votre dossier Misty, mais ne lancent jamais le jeu) :

```powershell
cargo test -- --ignored --nocapture
```

## Organisation

```
ui/                 Interface Slint (l'équivalent des .xaml)
  theme.slint       couleurs, polices, traductions (Tr.t("Cle"))
  widgets.slint     boutons, listes déroulantes, interrupteurs, curseurs…
  state.slint       état de chaque page, partagé avec Rust (les « globals »)
  main.slint        fenêtre, barre latérale, boîte de dialogue
  home / profiles / instance / browser / settings / changelog / packs / editor .slint

src/
  main.rs           démarrage
  ms_login.rs       fenêtre de connexion Microsoft (sous-processus « Misty.exe --ms-login »)
  services/         la logique, sans interface (un fichier = un service de la version C#)
  minecraft/        installation et lancement du jeu (remplace CmlLib)
    install.rs      JSON de version, jar, bibliothèques, assets
    java.rs         Java fourni par Mojang
    launch.rs       ligne de commande Java
    loaders/        Forge, NeoForge, Fabric/Quilt, OptiFine, LiteLoader
  ui/               le code derrière chaque page (l'équivalent des .xaml.cs)
lang/               traductions (fr.json, en.json), intégrées à l'exe
assets/             polices, drapeaux, icône
```

## Correspondance avec la version C#

| C#                              | Rust                                   |
|---------------------------------|----------------------------------------|
| `async void` + `Dispatcher`     | `ui::lancer(async { … })` (thread de l'interface) |
| `await Task.Run(…)` / HttpClient | `app.bg(async { … })` (runtime tokio)  |
| `Loc.T("Cle")`, `{loc:Tr Cle}`  | `loc::t("Cle")`, `Tr.t("Cle")` en Slint |
| `Dialogs.ConfirmerAsync`        | `dialogs::confirmer(&app, …).await`    |
| `ObservableCollection`          | `VecModel` / `ModelRc`                 |
| CmlLib.Core                     | `minecraft/`                           |
| CmlLib.Core.Auth.Microsoft      | `services/account.rs` + `ms_login.rs`  |

## Ce qui a changé

- Le compte Microsoft : le cache de CmlLib n'est pas réutilisable, il faut se reconnecter une fois
  (le compte est ensuite gardé dans `msa.json`).
- La fenêtre est transparente et son contenu est découpé en coins arrondis (identique sur Windows 10 et 11).
- L'éditeur externe (Paint.NET…) : la texture est rechargée dès que son fichier change.
