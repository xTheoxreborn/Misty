# Changelog

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