# Changelog
## 0.3.3
- Misty now uses the new GitHub repository (Misty-MC/Misty) to look for updates: this version keeps your updates going without reinstalling
- New "Check for updates" button in the settings (About card)
- Several Microsoft accounts: add as many as you want, switch from the home page or from the settings, and forget one without losing the others
- Modpacks: Misty now lets you pick the version to install (Modrinth and CurseForge) and asks for a confirmation before creating the profile
- English is now the default language of the launcher (you can change it in the settings: English, French, Spanish, Italian, Russian)
- New cloud logo, used for the app icon and the sidebar
- The settings button moved to the bottom of the sidebar
- Skin tab: a full 3D Steve is shown when you are not signed in, instead of just a head
- The account head on the home page now uses your real skin when you are signed in with Microsoft
- Fix: dropdown lists no longer open on top of their own button when there is no room below, they open above it
- Fix: the dark background behind the new profile, new texture pack and modpack loading dialogs now covers the whole window
- The empty profile list is now centered, without an icon
- Removed the "experimental" popup when opening texture packs
- The installer license now shows the correct year and name

## 0.3.2
- New SKIN tab on the home page, next to PLAY (vertical text on the far right): change your skin and your cape from the launcher
- The character preview is now a real 3D model: drag it to turn it around and see your cape from behind
- Skin change: pick a PNG file (64x64 or 64x32), choose classic (Steve) or slim (Alex) arms, and it is sent straight to your Microsoft account; the chosen skin is previewed before sending
- Capes: every cape on your account is shown with a clear preview, 4 per row, one click equips it and None removes it
- The account badge on the home page shows the new head as soon as the skin is changed
- The tab needs a Microsoft account (otherwise a message offers to sign in) and is translated in every language
- Added new languages: Spanish, Italian and Russian, with their flags in the language selector

## 0.3.1
- Fenêtre aux coins arrondis : le fond de l'application épouse maintenant des angles arrondis, avec un affichage net sur tous les bords
- Amélioration : le sélecteur de version (menu général) s'ouvre directement sur la version choisie au lieu de revenir tout en haut de la liste, ce qui permet de choisir facilement la version juste avant ou après
- Correction : le même comportement est appliqué à toutes les listes déroulantes (profil, mod loader, …)
- Correction de bugs mineurs d'affichage
- Correction de Optifine dans les dernières versions

## 0.3.0
- Baisse considérable de l'utilisation de la RAM du launcher

## 0.2.4.3
- Séparer les textures modifié avec le template
- Ajout du sélecteur des langues
- Toute l'interface est traduite (français / anglais), y compris les messages et les boîtes de dialogue ; le changement de langue s'applique immédiatement
- Installateur Windows (Misty-Setup.exe) : sans droits administrateur, installe .NET 10 si besoin, raccourcis menu Démarrer / bureau
- Éditeur de packs de textures repensé : catégories avec compteurs, outils et couleurs plus lisibles, couleurs récentes, icône du pack, export depuis l'éditeur
- Éditeur : les traits rapides n'ont plus de trous, une seule annulation par trait, raccourcis (Ctrl+Z/Y/S, B, E, G, I), zoom Ctrl + molette
- Éditeur : la texture modifiée dans Paint.NET est rechargée automatiquement, la texture en cours est enregistrée en quittant la page
- Packs : pack_format correct pour les versions avant 1.14 et après 1.21.9, message clair pour les versions trop anciennes
- Correctifs : Rich Presence Discord (un seul client, l'option est respectée partout), bouton JOUER qui restait en français, avertissement « expérimental » affiché une seule fois

## 0.2.4.2
- Amélioration du modificateur de pack de textures

## 0.2.4.1
- Possibilité de supprimer un profil directement où il y a la liste des profils
- Fix curseforge key 
- Ajout de la modification de pack de ressources (Expérimentale)
- Possibilité de désactiver le rich presence

## 0.2.4
- CurseForge en plus de Modrinth : choix de la plateforme dans l'explorateur (mods, packs de ressources, datapacks, shaders, modpacks)
- Modpacks CurseForge : les fichiers que leurs auteurs réservent au site CurseForge sont listés, avec leur page à ouvrir
- Profils (instances) : chacun a sa version, son mod loader et son propre dossier de jeu ; sélection du profil depuis l'accueil
- Explorateur Modrinth dans chaque profil : mods, packs de ressources, datapacks et shaders, avec installation des dépendances
- Contenu d'un profil : activation / désactivation, suppression, identification des fichiers ajoutés à la main
- Installation de modpacks Modrinth (.mrpack) en un clic
- Les pseudos hors-ligne deviennent des « comptes hors-ligne »
- Nouvelle interface en WPF : fond ciel, cartes en verre dépoli, titres en police pixel, animations fluides
- Navigation latérale Jouer / Options / Notes de version, boîtes de dialogue intégrées
- Options repensées : profils en pastilles, RAM au curseur, interrupteurs pour les versions et le raccourci
- Tête du skin affichée à côté du compte, progression du téléchargement dans le bouton JOUER
- La mise à jour n'est plus proposée quand la release GitHub est plus ancienne que la version installée
- Ajout du modloader quilt
- Réorganisation du projet : `src/Misty` avec `Forms/` et `Services/`, formulaires renommés (`MainForm`, `SettingsForm`, `ChangelogForm`), contrôles renommés
- Correctifs : le raccourci est bien recréé après une mise à jour, les options snapshot/beta/alpha sont appliquées même avec un compte premium, la migration de `C:\TEXT` ne supprime plus l'ancien dossier si la copie a échoué
- Ajout du README et du CHANGELOG

## 0.2.3.9.5.1
- Calcul de la taille du dossier, possibilité de le vider

## 0.2.3.9.5
- Changement du dossier de sauvegarde (`C:\TEXT` → `%LOCALAPPDATA%\Misty`)