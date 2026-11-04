//! Détail d'un profil : contenu installé (mods, packs, datapacks, shaders) et explorateur Modrinth / CurseForge.

use super::{browser, dialogs, images, lancer, maj_ligne, modele, modele_textes, App};
use crate::services::catalogue::{ProjetDistant, MODRINTH};
use crate::services::content::{self, ContenuInstalle};
use crate::services::instances::{self, Instance};
use crate::services::{loc, packs, system_info};
use crate::{BadgeItem, ContentItem, InstanceState};
use slint::{Color, ComponentHandle, ModelRc};
use std::rc::Rc;

#[derive(Default)]
pub struct Etat {
    pub instance: Option<Instance>,
    contenus: Vec<ContenuInstalle>,
    /// Index (dans `contenus`) des lignes affichées
    visibles: Vec<usize>,
    filtre_type: String,
    types: Vec<String>,
    modele: Option<ModelRc<ContentItem>>,
}

fn s(app: &App) -> InstanceState<'_> {
    app.ui.global::<InstanceState>()
}

fn instance(app: &App) -> Option<Instance> {
    app.instance.borrow().instance.clone()
}

pub fn installer(app: &Rc<App>) {
    let st = s(app);
    let a = app.clone();
    st.on_back(move || super::profiles::rafraichir(&a));
    let a = app.clone();
    st.on_play(move || {
        if let Some(i) = instance(&a) {
            super::profiles::jouer(&a, i);
        }
    });
    let a = app.clone();
    st.on_folder(move || {
        if let Some(i) = instance(&a) {
            system_info::ouvrir_dossier(&i.dossier());
        }
    });
    let a = app.clone();
    st.on_change_icon(move || {
        let a = a.clone();
        lancer(async move { changer_icone(&a).await });
    });
    let a = app.clone();
    st.on_rename(move || {
        if let Some(i) = instance(&a) {
            s(&a).set_rename_text(i.nom.into());
            s(&a).set_renaming(true);
        }
    });
    let a = app.clone();
    st.on_rename_ok(move || {
        let nom = s(&a).get_rename_text().trim().to_string();
        if !nom.is_empty() {
            let mut i = a.instance.borrow_mut();
            if let Some(inst) = i.instance.as_mut() {
                inst.nom = nom;
                let _ = instances::sauver(inst);
            }
        }
        s(&a).set_renaming(false);
        maj_en_tete(&a);
    });
    let a = app.clone();
    st.on_delete(move || {
        let a = a.clone();
        lancer(async move {
            let Some(i) = instance(&a) else { return };
            if super::profiles::supprimer(&a, &i).await {
                super::profiles::rafraichir(&a);
            }
        });
    });
    let a = app.clone();
    st.on_tab_changed(move |t| {
        if t == 1 {
            if let Some(i) = instance(&a) {
                browser::configurer_instance(&a, i);
            }
        } else {
            let a = a.clone();
            lancer(async move { charger_contenu(&a).await });
        }
    });
    let a = app.clone();
    st.on_filter_changed(move |i| {
        let t = a.instance.borrow().types.get(i as usize).cloned().unwrap_or_default();
        a.instance.borrow_mut().filtre_type = t;
        appliquer_filtre(&a);
    });
    let a = app.clone();
    st.on_filter_text_changed(move |_| appliquer_filtre(&a));
    let a = app.clone();
    st.on_toggle(move |i| {
        let a = a.clone();
        lancer(async move { basculer(&a, i as usize).await });
    });
    let a = app.clone();
    st.on_remove(move |i| {
        let a = a.clone();
        lancer(async move { supprimer_contenu(&a, i as usize).await });
    });
}

/// Affiche un profil (appelé à chaque ouverture de la page).
pub fn ouvrir(app: &Rc<App>, inst: Instance) {
    app.instance.borrow_mut().instance = Some(inst);
    let st = s(app);
    st.set_renaming(false);
    st.set_status("".into());
    st.set_tab(0);
    st.set_filter_text("".into());
    maj_en_tete(app);
    construire_filtres(app);
    browser::oublier_instance(app); // l'explorateur sera reconfiguré à la première ouverture de l'onglet
    super::naviguer(app, super::page::INSTANCE);
    let a = app.clone();
    lancer(async move { charger_contenu(&a).await });
}

pub fn langue_changee(app: &Rc<App>) {
    if instance(app).is_none() {
        return;
    }
    construire_filtres(app);
    appliquer_filtre(app);
}

fn maj_en_tete(app: &Rc<App>) {
    let Some(i) = instance(app) else { return };
    let st = s(app);
    st.set_name(i.nom.to_uppercase().into());

    let mut badges = vec![
        BadgeItem { text: i.version_mc.to_uppercase().into(), color: Color::from_rgb_u8(0x1C, 0x23, 0x35) },
        BadgeItem {
            text: format!("{}{}", i.loader, i.loader_version.as_ref().map(|v| format!(" {v}")).unwrap_or_default()).to_uppercase().into(),
            color: Color::from_rgb_u8(0x13, 0xB2, 0x4C),
        },
    ];
    if i.est_modpack() {
        badges.push(BadgeItem {
            text: format!("MODPACK {}", i.modpack_version.clone().unwrap_or_default()).to_uppercase().into(),
            color: Color::from_rgb_u8(0x2F, 0x8C, 0xFF),
        });
    }
    st.set_badges(modele(badges));

    st.set_has_icon(false);
    let (fichier, url, id) = (i.chemin_icone(), i.icone_url.clone(), i.id.clone());
    let a = app.clone();
    lancer(async move {
        let image = match images::depuis_fichier(&fichier, 160) {
            Some(img) => Some(img),
            None => match url {
                Some(u) => images::depuis_url(&a, &u).await,
                None => None,
            },
        };
        if instance(&a).map(|i| i.id) == Some(id) {
            if let Some(img) = image {
                s(&a).set_icon(img);
                s(&a).set_has_icon(true);
            }
        }
    });
}

/// Filtres par type (reconstruits au changement de langue).
fn construire_filtres(app: &Rc<App>) {
    let mut types = vec![String::new()];
    let mut libelles = vec![loc::t("Instance_Tout")];
    for (t, l) in content::types() {
        types.push(t);
        libelles.push(l);
    }
    let actuel = app.instance.borrow().filtre_type.clone();
    let index = types.iter().position(|t| *t == actuel).unwrap_or(0);
    s(app).set_filters(modele_textes(&libelles));
    s(app).set_filter_index(index as i32);
    let mut e = app.instance.borrow_mut();
    e.types = types;
    if index == 0 {
        e.filtre_type.clear();
    }
}

// ───────────────────────── Contenu installé ─────────────────────────

async fn charger_contenu(app: &Rc<App>) {
    let Some(i) = instance(app) else { return };
    let inst = i.clone();
    let contenus = app.bloquant(move || content::lister(&inst)).await;
    let a_identifier = contenus.iter().any(|c| c.meta.is_none());
    app.instance.borrow_mut().contenus = contenus.clone();
    appliquer_filtre(app);

    // Identification des fichiers ajoutés à la main (nom + icône depuis Modrinth / CurseForge)
    if a_identifier {
        s(app).set_status(loc::t("Instance_Identification").into());
        let inst = i.clone();
        let resultat = app.bg(async move { content::identifier(&inst, contenus).await }).await;
        // hors-ligne : on garde les noms de fichiers
        if instance(app).map(|x| x.id) == Some(i.id.clone()) {
            if let Ok(c) = resultat {
                app.instance.borrow_mut().contenus = c;
                appliquer_filtre(app);
            }
            s(app).set_status("".into());
        }
    }
}

fn appliquer_filtre(app: &Rc<App>) {
    let texte = s(app).get_filter_text().trim().to_lowercase();
    let (visibles, items, nb) = {
        let e = app.instance.borrow();
        let visibles: Vec<usize> = (0..e.contenus.len())
            .filter(|&i| {
                let c = &e.contenus[i];
                (e.filtre_type.is_empty() || c.type_ == e.filtre_type)
                    && (texte.is_empty() || c.titre().to_lowercase().contains(&texte) || c.nom_fichier().to_lowercase().contains(&texte))
            })
            .collect();
        let items: Vec<(ContentItem, Option<String>)> = visibles
            .iter()
            .map(|&i| {
                let c = &e.contenus[i];
                (
                    ContentItem {
                        title: c.titre().into(),
                        details: c.details().into(),
                        icon: images::vide(),
                        has_icon: false,
                        active: c.actif(),
                    },
                    c.icone_url(),
                )
            })
            .collect();
        (visibles, items, e.contenus.len())
    };

    let urls: Vec<Option<String>> = items.iter().map(|(_, u)| u.clone()).collect();
    let m = modele(items.into_iter().map(|(c, _)| c).collect());
    s(app).set_items(m.clone());
    s(app).set_content_tab_text(if nb > 0 { format!("{} ({nb})", loc::t("Instance_Contenu")) } else { loc::t("Instance_Contenu") }.into());
    {
        let mut e = app.instance.borrow_mut();
        e.visibles = visibles;
        e.modele = Some(m.clone());
    }

    for (index, url) in urls.into_iter().enumerate() {
        let Some(url) = url else { continue };
        let m = m.clone();
        let a = app.clone();
        lancer(async move {
            if let Some(img) = images::depuis_url(&a, &url).await {
                maj_ligne(&m, index, |c| {
                    c.icon = img;
                    c.has_icon = true;
                });
            }
        });
    }
}

fn contenu_affiche(app: &App, ligne: usize) -> Option<(usize, ContenuInstalle)> {
    let e = app.instance.borrow();
    let i = *e.visibles.get(ligne)?;
    Some((i, e.contenus.get(i)?.clone()))
}

async fn basculer(app: &Rc<App>, ligne: usize) {
    let Some((i, contenu)) = contenu_affiche(app, ligne) else { return };
    match content::basculer(&contenu) {
        Ok(nouveau) => {
            let actif = {
                let mut e = app.instance.borrow_mut();
                e.contenus[i].chemin_fichier = nouveau;
                e.contenus[i].actif()
            };
            let details = app.instance.borrow().contenus[i].details();
            if let Some(m) = app.instance.borrow().modele.clone() {
                maj_ligne(&m, ligne, |c| {
                    c.active = actif;
                    c.details = details.into();
                });
            }
        }
        Err(e) => {
            dialogs::erreur(app, &loc::t("Instance_ModifImpossible"), &format!("{e}\n\n{}", loc::t("Commun_JeuOuvert"))).await;
        }
    }
}

async fn supprimer_contenu(app: &Rc<App>, ligne: usize) {
    let Some((i, contenu)) = contenu_affiche(app, ligne) else { return };
    let Some(inst) = instance(app) else { return };
    let ok = dialogs::confirmer(
        app,
        &loc::t("Commun_Supprimer"),
        &loc::tf("Instance_SupprimerContenu", &[&contenu.titre()]),
        Some(&loc::t("Commun_Supprimer")),
        None,
        true,
    )
    .await;
    if !ok {
        return;
    }
    match content::supprimer(&inst, &contenu) {
        Ok(()) => {
            app.instance.borrow_mut().contenus.remove(i);
            appliquer_filtre(app);
        }
        Err(e) => {
            dialogs::erreur(app, &loc::t("Commun_SuppressionImpossible"), &format!("{e}\n\n{}", loc::t("Commun_JeuOuvert"))).await;
        }
    }
}

/// Le projet est-il installé dans le profil affiché ?
pub fn est_installe(app: &App, projet: &ProjetDistant) -> bool {
    content::est_installe(&app.instance.borrow().contenus, &projet.source, &projet.id)
}

// ───────────────────────── Installation depuis l'explorateur ─────────────────────────

/// Installation (avec les cas particuliers datapacks / shaders). Renvoie true si elle a réussi.
pub async fn installer_projet(app: &Rc<App>, projet: ProjetDistant, type_: String) -> bool {
    let Some(inst) = instance(app) else { return false };
    let progression = app.progression(|ui, texte| ui.global::<InstanceState>().set_status(texte.into()));

    let mut monde = None;
    if type_ == content::DATAPACK {
        let mondes = content::mondes(&inst);
        if mondes.is_empty() {
            dialogs::info(app, &loc::t("Instance_AucunMonde"), &loc::t("Instance_AucunMonde_Texte")).await;
            return false;
        }
        monde = if mondes.len() == 1 {
            Some(mondes[0].clone())
        } else {
            dialogs::choisir(app, &loc::t("Instance_ChoisirMonde"), &loc::tf("Instance_ChoisirMonde_Texte", &[&projet.titre]), &mondes)
                .await
                .map(|i| mondes[i].clone())
        };
        if monde.is_none() {
            return false;
        }
    }

    if type_ == content::SHADER {
        match preparer_shaders(app, &inst, progression.clone()).await {
            Ok(true) => {}
            Ok(false) => return false,
            Err(e) => {
                s(app).set_status("".into());
                dialogs::erreur(app, &loc::t("Instance_InstallationImpossible"), &format!("{e:#}")).await;
                return false;
            }
        }
    }

    let (i2, p2, t2) = (inst.clone(), projet.clone(), type_.clone());
    let resultat = app
        .bg(async move { content::installer(&i2, &p2.source, &p2.id, &t2, monde.as_deref(), Some(progression)).await })
        .await;
    match resultat {
        Ok(installes) => {
            s(app).set_status(
                if installes.len() > 1 {
                    loc::tf("Instance_InstalleDeps", &[&projet.titre, &(installes.len() - 1)])
                } else {
                    loc::tf("Instance_Installe", &[&projet.titre])
                }
                .into(),
            );
            let i3 = inst.clone();
            let contenus = app.bloquant(move || content::lister(&i3)).await;
            app.instance.borrow_mut().contenus = contenus;
            appliquer_filtre(app);
            true
        }
        Err(e) => {
            s(app).set_status("".into());
            dialogs::erreur(app, &loc::t("Instance_InstallationImpossible"), &format!("{e:#}")).await;
            false
        }
    }
}

/// Les shaders ont besoin d'Iris (Fabric/Quilt/NeoForge), Oculus (Forge) ou OptiFine.
async fn preparer_shaders(app: &Rc<App>, inst: &Instance, progression: crate::services::Progress) -> anyhow::Result<bool> {
    let Some(prerequis) = content::prerequis_shaders(inst, &content::lister(inst)) else { return Ok(true) };

    if prerequis.is_empty() {
        return Ok(dialogs::confirmer(
            app,
            &loc::t("Shaders_NonPris"),
            &loc::tf("Shaders_NonPris_Texte", &[&inst.loader]),
            Some(&loc::t("Shaders_QuandMeme")),
            None,
            false,
        )
        .await);
    }

    let nom = if prerequis == content::ID_IRIS { "Iris" } else { "Oculus" };
    let installer = dialogs::confirmer(
        app,
        &loc::t("Shaders_ModRequis"),
        &loc::tf("Shaders_ModRequis_Texte", &[&nom]),
        Some(&loc::tf("Shaders_Installer", &[&nom])),
        Some(&loc::t("Commun_Ignorer")),
        false,
    )
    .await;
    if !installer {
        return Ok(true);
    }
    let i = inst.clone();
    app.bg(async move { content::installer(&i, MODRINTH, prerequis, content::MOD, None, Some(progression)).await }).await?;
    Ok(true)
}

// ───────────────────────── Actions du profil ─────────────────────────

async fn changer_icone(app: &Rc<App>) {
    let Some(i) = instance(app) else { return };
    let Some(fichier) = dialogs::choisir_fichier(&loc::t("Instance_IconeTitre"), "Images", &["png", "jpg", "jpeg", "bmp", "gif"]).await else {
        return;
    };
    // Réencodé en PNG 128 px pour garder un fichier léger
    let destination = i.chemin_icone();
    let resultat = app.bloquant(move || packs::enregistrer_icone(&fichier, &destination)).await;
    match resultat {
        Ok(()) => {
            let _ = instances::sauver(&i);
            maj_en_tete(app);
        }
        Err(e) => dialogs::erreur(app, &loc::t("Instance_ImageIllisible"), &e.to_string()).await,
    }
}
