//! Liste des profils, création d'un profil, installation d'un modpack Modrinth / CurseForge.

use super::{browser, dialogs, images, lancer, maj_ligne, modele, modele_textes, App};
use crate::minecraft::{install, loaders};
use crate::services::catalogue::ProjetDistant;
use crate::services::instances::{self, Instance};
use crate::services::{loc, modpack, system_info};
use crate::{AppState, ProfileCard, ProfilesState};
use slint::{ComponentHandle, ModelRc};
use std::rc::Rc;

#[derive(Default)]
pub struct Etat {
    liste: Vec<Instance>,
    versions: Vec<String>,
    loaders: Vec<String>,
}

fn p(app: &App) -> ProfilesState<'_> {
    app.ui.global::<ProfilesState>()
}

pub fn installer(app: &Rc<App>) {
    let s = p(app);
    let a = app.clone();
    s.on_open(move |id| {
        if let Some(i) = trouver(&a, &id) {
            super::instance::ouvrir(&a, i);
        }
    });
    let a = app.clone();
    s.on_play(move |id| {
        if let Some(i) = trouver(&a, &id) {
            jouer(&a, i);
        }
    });
    let a = app.clone();
    s.on_delete(move |id| {
        let a = a.clone();
        lancer(async move {
            let Some(i) = trouver(&a, &id) else { return };
            if supprimer(&a, &i).await {
                rafraichir(&a);
            }
        });
    });
    let a = app.clone();
    s.on_new_profile(move || {
        let a = a.clone();
        lancer(async move { nouveau(&a).await });
    });
    let a = app.clone();
    s.on_modpacks(move || {
        p(&a).set_view(1);
        browser::configurer_modpacks(&a);
    });
    let a = app.clone();
    s.on_back(move || rafraichir(&a));
    let a = app.clone();
    s.on_create_version_selected(move |i| {
        let a = a.clone();
        lancer(async move { version_choisie(&a, i).await });
    });
    let a = app.clone();
    s.on_create(move || creer(&a));
    let a = app.clone();
    s.on_cancel_create(move || p(&a).set_creating(false));
}

fn trouver(app: &App, id: &str) -> Option<Instance> {
    app.profiles.borrow().liste.iter().find(|i| i.id == id).cloned().or_else(|| instances::trouver(id))
}

/// Recharge la liste (appelé à chaque ouverture de la page).
pub fn rafraichir(app: &Rc<App>) {
    p(app).set_view(0);
    rafraichir_liste(app);
    super::naviguer(app, super::page::PROFILS);
}

/// Cartes des profils (« Joué hier »… est recalculé, d'où l'appel au changement de langue).
pub fn rafraichir_liste(app: &Rc<App>) {
    let liste = instances::lister();
    let cartes: Vec<ProfileCard> = liste
        .iter()
        .map(|i| ProfileCard {
            id: i.id.clone().into(),
            name: i.nom.clone().into(),
            summary: i.resume().into(),
            last: i.dernier_lancement_texte().into(),
            icon: images::vide(),
            has_icon: false,
        })
        .collect();
    let m: ModelRc<ProfileCard> = modele(cartes);
    p(app).set_list(m.clone());

    // Icônes : icon.png du profil, sinon celle du modpack
    for (index, i) in liste.iter().enumerate() {
        let (fichier, url) = (i.chemin_icone(), i.icone_url.clone());
        let m = m.clone();
        let a = app.clone();
        lancer(async move {
            let image = match images::depuis_fichier(&fichier, 128) {
                Some(img) => Some(img),
                None => match url {
                    Some(u) => images::depuis_url(&a, &u).await,
                    None => None,
                },
            };
            if let Some(img) = image {
                maj_ligne(&m, index, |c| {
                    c.icon = img;
                    c.has_icon = true;
                });
            }
        });
    }
    app.profiles.borrow_mut().liste = liste;
}

/// Lance un profil : on revient sur l'accueil, qui affiche la progression.
pub fn jouer(app: &Rc<App>, instance: Instance) {
    app.ui.global::<AppState>().set_nav(super::page::ACCUEIL);
    super::naviguer(app, super::page::ACCUEIL);
    super::home::selectionner_et_jouer(app, instance);
}

/// Demande confirmation puis supprime le profil. Renvoie true s'il a été supprimé.
pub async fn supprimer(app: &Rc<App>, instance: &Instance) -> bool {
    let ok = dialogs::confirmer(
        app,
        &loc::t("Profil_Supprimer"),
        &loc::tf("Profil_Supprimer_Texte", &[&instance.nom]),
        Some(&loc::t("Commun_Supprimer")),
        None,
        true,
    )
    .await;
    if !ok {
        return false;
    }
    match instances::supprimer(instance) {
        Ok(()) => true,
        Err(e) => {
            dialogs::erreur(app, &loc::t("Commun_SuppressionImpossible"), &format!("{e}\n\n{}", loc::t("Commun_JeuOuvert"))).await;
            false
        }
    }
}

// ───────────────────────── Création ─────────────────────────

async fn nouveau(app: &Rc<App>) {
    let s = p(app);
    s.set_create_name("".into());
    s.set_create_info(loc::t("Home_ChargementVersions").into());
    s.set_create_enabled(false);
    s.set_create_loader_enabled(false);
    s.set_create_loaders(modele_textes(&[]));
    s.set_creating(true);

    match app.bg(install::versions_proposees()).await {
        Ok(versions) => {
            s.set_create_versions(modele_textes(&versions));
            let vide = versions.is_empty();
            app.profiles.borrow_mut().versions = versions;
            if !vide {
                s.set_create_version_index(0);
                version_choisie(app, 0).await;
            }
        }
        Err(e) => s.set_create_info(loc::tf("Home_VersionsErreur", &[&e]).into()),
    }
}

async fn version_choisie(app: &Rc<App>, i: i32) {
    let Some(version) = app.profiles.borrow().versions.get(i.max(0) as usize).cloned() else { return };
    let s = p(app);
    s.set_create_loader_enabled(false);
    s.set_create_enabled(false);
    s.set_create_info(loc::tf("Commun_RechercheLoaders", &[&version]).into());

    let v = version.clone();
    let modes = app.bg(async move { loaders::modes_disponibles(&v).await }).await;
    // Version changée entre-temps
    if app.profiles.borrow().versions.get(s.get_create_version_index().max(0) as usize) != Some(&version) {
        return;
    }
    // Un profil sert souvent à installer des mods : on propose Fabric par défaut s'il existe
    let defaut = modes.iter().position(|m| m == loaders::FABRIC).unwrap_or(0);
    s.set_create_loaders(modele_textes(&modes));
    s.set_create_loader_index(defaut as i32);
    s.set_create_loader_enabled(true);
    s.set_create_enabled(true);
    s.set_create_info(loc::t("Profil_InfoLoader").into());
    app.profiles.borrow_mut().loaders = modes;
}

fn creer(app: &Rc<App>) {
    let s = p(app);
    let (version, loader) = {
        let e = app.profiles.borrow();
        (
            e.versions.get(s.get_create_version_index().max(0) as usize).cloned(),
            e.loaders.get(s.get_create_loader_index().max(0) as usize).cloned(),
        )
    };
    let (Some(version), Some(loader)) = (version, loader) else { return };
    let mut nom = s.get_create_name().trim().to_string();
    if nom.is_empty() {
        nom = format!("{version} {loader}");
    }
    match instances::creer(&nom, &version, &loader, None) {
        Ok(instance) => {
            s.set_creating(false);
            super::instance::ouvrir(app, instance);
        }
        Err(e) => {
            let a = app.clone();
            lancer(async move { dialogs::erreur(&a, &loc::t("Erreur_Inattendue"), &e.to_string()).await });
        }
    }
}

// ───────────────────────── Modpacks ─────────────────────────

/// Le modpack est-il déjà installé dans un profil ?
pub fn modpack_installe(projet: &ProjetDistant) -> bool {
    instances::lister().iter().any(|i| {
        i.modpack_project_id.as_deref() == Some(projet.id.as_str())
            && i.modpack_source.as_deref().unwrap_or(crate::services::catalogue::MODRINTH) == projet.source
    })
}

/// Installation depuis l'explorateur de modpacks. Renvoie true si elle a réussi.
pub async fn installer_modpack(app: &Rc<App>, projet: ProjetDistant) -> bool {
    let s = p(app);

    // 1. Liste des versions du modpack
    s.set_wait_title(projet.titre.to_uppercase().into());
    s.set_wait_text(loc::t("Commun_Preparation").into());
    s.set_waiting(true);
    let a_lister = projet.clone();
    let versions = app.bg(async move { modpack::versions_disponibles(&a_lister).await }).await;
    s.set_waiting(false);
    let versions = match versions {
        Ok(v) if !v.is_empty() => v,
        Ok(_) => {
            dialogs::erreur(app, &loc::t("Profil_ModpackImpossible"), &loc::t("Erreur_ModpackSansVersion")).await;
            return false;
        }
        Err(e) => {
            dialogs::erreur(app, &loc::t("Profil_ModpackImpossible"), &format!("{e:#}")).await;
            return false;
        }
    };

    // 2. Choix de la version (la plus récente est en haut), puis confirmation
    let libelles: Vec<String> = versions.iter().map(|v| v.libelle.clone()).collect();
    let Some(choix) = dialogs::choisir(app, &loc::t("Profil_VersionPack_Titre"), &loc::tf("Profil_VersionPack_Texte", &[&projet.titre]), &libelles).await else {
        return false;
    };
    let Some(version) = versions.get(choix).cloned() else { return false };
    let ok = dialogs::confirmer(
        app,
        &loc::t("Profil_ConfirmerPack_Titre"),
        &loc::tf("Profil_ConfirmerPack_Texte", &[&projet.titre, &version.libelle]),
        Some(&loc::t("Profil_ConfirmerPack_Oui")),
        None,
        false,
    )
    .await;
    if !ok {
        return false;
    }
    let version_id = Some(version.id);

    s.set_wait_title(projet.titre.to_uppercase().into());
    s.set_wait_text(loc::t("Commun_Preparation").into());
    s.set_waiting(true);

    let progression = app.progression(|ui, texte| ui.global::<ProfilesState>().set_wait_text(texte.into()));
    let resultat = app.bg(async move { modpack::installer(&projet, version_id, progression).await }).await;
    s.set_waiting(false);

    match resultat {
        Ok(r) => {
            super::instance::ouvrir(app, r.instance.clone());
            if !r.bloques.is_empty() {
                proposer_telechargements_manuels(app, &r).await;
            }
            true
        }
        Err(e) => {
            dialogs::erreur(app, &loc::t("Profil_ModpackImpossible"), &format!("{e:#}")).await;
            false
        }
    }
}

/// Certains auteurs CurseForge interdisent le téléchargement par des applications tierces :
/// on liste ces fichiers et on propose d'ouvrir leur page pour les télécharger à la main.
async fn proposer_telechargements_manuels(app: &Rc<App>, r: &modpack::Resultat) {
    let mut liste: Vec<String> = r.bloques.iter().take(12).map(|b| format!("• {}", loc::tf("Manuel_Ligne", &[&b.nom, &b.dossier]))).collect();
    if r.bloques.len() > 12 {
        liste.push(loc::tf("Manuel_Autres", &[&(r.bloques.len() - 12)]));
    }
    let ouvrir = dialogs::confirmer(
        app,
        &loc::t("Manuel_Titre"),
        &loc::tf("Manuel_Texte", &[&r.bloques.len(), &liste.join("\n")]),
        Some(&loc::t("Manuel_Ouvrir")),
        Some(&loc::t("Commun_PlusTard")),
        false,
    )
    .await;
    if !ouvrir {
        return;
    }
    for b in &r.bloques {
        system_info::ouvrir(&b.page);
    }
    system_info::ouvrir_dossier(&r.instance.dossier());
}
