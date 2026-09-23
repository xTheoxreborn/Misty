# Changelog

## Non publié
- Nouvelle interface en WPF : fond ciel, cartes en verre dépoli, titres en police pixel, animations fluides
- Navigation latérale Jouer / Options / Notes de version, boîtes de dialogue intégrées
- Options repensées : profils en pastilles, RAM au curseur, interrupteurs pour les versions et le raccourci
- Tête du skin affichée à côté du compte, progression du téléchargement dans le bouton JOUER
- La mise à jour n'est plus proposée quand la release GitHub est plus ancienne que la version installée
- Réorganisation du projet : `src/Misty` avec `Forms/` et `Services/`, formulaires renommés (`MainForm`, `SettingsForm`, `ChangelogForm`), contrôles renommés
- Correctifs : le raccourci est bien recréé après une mise à jour, les options snapshot/beta/alpha sont appliquées même avec un compte premium, la migration de `C:\TEXT` ne supprime plus l'ancien dossier si la copie a échoué
- Ajout du README et du CHANGELOG

## 0.2.3.9.5.1
- Calcul de la taille du dossier, possibilité de le vider

## 0.2.3.9.5
- Changement du dossier de sauvegarde (`C:\TEXT` → `%LOCALAPPDATA%\Misty`)