# Changelog

## Non publié
- Réorganisation du projet : `src/Misty` avec `Forms/` et `Services/`, formulaires renommés (`MainForm`, `SettingsForm`, `ChangelogForm`), contrôles renommés
- Forge passe par le package NuGet `CmlLib.Core.Installer.Forge` au lieu d'une DLL compilée en local
- Correctifs : le raccourci est bien recréé après une mise à jour, les options snapshot/beta/alpha sont appliquées même avec un compte premium, la migration de `C:\TEXT` ne supprime plus l'ancien dossier si la copie a échoué
- Ajout du README et du CHANGELOG

## 0.2.3.9.5.1
- Calcul de la taille du dossier, possibilité de le vider

## 0.2.3.9.5
- Changement du dossier de sauvegarde (`C:\TEXT` → `%LOCALAPPDATA%\Misty`)

## 0.2.3.9.4.3
- Ajout de la nouvelle version MC

## 0.2.3.9.2
- 0.2.3.9.2 : possibilité de déconnecter le compte si la session expire et que la connexion n'est plus possible
- v2 : rechargement des versions + ajout des snapshots, bêtas, alphas et indev
- v1 : gestion des icônes (un fichier `icon.ico` change l'icône et est conservé après une mise à jour) + amélioration du système de raccourci

## 0.2.3.9.1
- Ajout de la gestion des raccourcis

## 0.2.3.9
- Correctif sur la suppression de l'ancien dossier de version + correction de l'affichage de la version

## 0.2.3.8
- 0.2.3.8.3 : correction d'un bug du sélecteur de RAM
- 0.2.3.8.2 : mise à jour de data.txt sans perte de données + reconnexion des comptes premium
- 0.2.3.8.1 : bug du pseudo avec OptiFine
- v2 : correction du pseudo qui ne s'affichait pas
- v1 : fix taille de la fenêtre des paramètres + gros fix sur la gestion d'OptiFine + bug de RAM au premier lancement

## 0.2.3.7
- 0.2.3.7.2 : fix mise à jour
- 0.2.3.7.1 : résolution de bugs liés à la détection des mises à jour
- Détection automatique des mises à jour, changement du fichier data

## 0.2.3.1 → 0.2.3.6
- 0.2.3.6 : détection de nouvelles mises à jour
- 0.2.3.5 : RAM allouée modifiable
- 0.2.3.4 : suppression de Quilt (problèmes), ajout d'OptiFine
- 0.2.3.3 : ouverture du dossier Minecraft
- 0.2.3.2 : ajout de Fabric, Quilt, LiteLoader
- 0.2.3.1 : nouveaux logos et titres de fenêtre, modification du Rich Presence

## 0.2.3
- v2 : corrections de bugs d'interface
- v1 : nouveau menu Options, création/suppression de profils, réglage de la RAM déplacé dans les options

## 0.2.2
- 0.2.2.9 : Rich Presence v2 + nouveau formulaire
- 0.2.2.8 : le Rich Presence affiche le loader
- 0.2.2.7 : ajout de NeoForge et du Discord Rich Presence
- 0.2.2.5 / 0.2.2.6 : refonte de l'interface
- 0.2.2.3 : modification d'une dépendance
- 0.2.2.2 : nouvelle taille de fenêtre, règles d'activation des boutons de déconnexion
- 0.2.2.1 : bouton « autre » renommé « Déconnexion totale »
- Ajout de Forge, fix de l'enregistrement de la RAM

## 0.2.1
- 0.2.1.1 : suppression d'un package NuGet
- RAM allouée modifiable, prise en compte de la dernière version sélectionnée

## 0.2.0
- 0.2.0.1 : changement du titre de la déconnexion rapide
- Refonte complète de la gestion et du téléchargement des versions

## 0.1.x
- 0.1.6 : déconnexion totale du compte Microsoft + mémorisation de la dernière version
- 0.1.5 : fix du chemin du JDK
- 0.1.4 : interface de connexion/déconnexion Microsoft
- 0.1.3 : bouton de déconnexion
- 0.1.2 : connexion premium
- 0.1.1 : correction du JDK utilisé, fix des versions 1.19+
- 0.1.0 : toutes les releases, gestion JDK 25 et 21

## 0.0.x
- 0.0.5 : sélection des versions 1.20.6 et 1.21.11
- 0.0.4 : textes des boutons
- 0.0.3 : sauvegarde du pseudo
- 0.0.2 : modification du pseudo
- 0.0.1 : première release
