//! Onglet « Skin » de l'accueil : aperçu du personnage, changement de skin, choix de la cape.

use super::{dialogs, images, lancer, modele, App};
use crate::services::skins::{self, Profil};
use crate::services::{account, loc};
use crate::{CapeItem, HomeState, SkinState};
use slint::{ComponentHandle, Image};
use std::rc::Rc;

/// Nombre de capes par ligne (doit correspondre à la largeur du panneau).
const COLONNES: usize = 4;


#[derive(Default)]
pub struct Etat {
    profil: Option<Profil>,
    /// PNG du skin actuellement porté par le compte
    skin_actuel: Vec<u8>,
    /// Image de chaque cape de `profil.capes`
    capes_img: Vec<Option<Vec<u8>>>,
    /// PNG choisi par l'utilisateur, pas encore envoyé
    fichier: Option<Vec<u8>>,
    /// Incremented at each 3D preview request, to drop outdated results
    generation: u64,
}

fn s(app: &App) -> SkinState<'_> {
    app.ui.global::<SkinState>()
}

pub fn installer(app: &Rc<App>) {
    let st = s(app);

    let a = app.clone();
    st.on_open(move || {
        // Premier affichage (ou compte changé) : on va chercher le skin et les capes
        if account::session().is_some() && a.skin.borrow().profil.is_none() {
            charger(&a);
        } else if account::session().is_none() {
            afficher_steve(&a);
        }
    });

    let a = app.clone();
    st.on_pick_file(move || {
        let a = a.clone();
        lancer(async move { choisir_fichier(&a).await });
    });

    let a = app.clone();
    st.on_slim_changed(move || afficher_personnage(&a));

    let a = app.clone();
    st.on_apply(move || {
        let a = a.clone();
        lancer(async move { appliquer(&a).await });
    });

    let a = app.clone();
    st.on_select_cape(move |id| {
        let a = a.clone();
        let id = id.to_string();
        lancer(async move { changer_cape(&a, id).await });
    });
}

/// Connexion / déconnexion : on repart de zéro.
pub fn session_changee(app: &Rc<App>) {
    *app.skin.borrow_mut() = Etat::default();
    let st = s(app);
    st.set_has_figure(false);
    st.set_frames(modele(Vec::new()));
    st.set_capes(modele(Vec::new()));
    st.set_has_file(false);
    st.set_file_name("".into());
    st.set_info("".into());
    if account::session().is_some() && st.get_tab() == 1 {
        charger(app);
    } else if account::session().is_none() {
        afficher_steve(app);
    }
}

/// Pas de compte connecté : on montre Steve en entier (skin par défaut de Minecraft).
fn afficher_steve(app: &Rc<App>) {
    let generation = {
        let mut e = app.skin.borrow_mut();
        e.generation += 1;
        e.generation
    };
    let a = app.clone();
    lancer(async move {
        let vues = a.bloquant(|| skins::rendre_tour(include_bytes!("../../assets/steve.png"), false, None)).await;
        if a.skin.borrow().generation != generation || account::session().is_some() {
            return;
        }
        let st = s(&a);
        st.set_has_figure(vues.is_some());
        let images: Vec<Image> = vues.unwrap_or_default().iter().map(|v| Image::from_rgba8(images::tampon(v))).collect();
        st.set_frames(modele(images));
    });
}

pub fn langue_changee(app: &Rc<App>) {
    afficher_capes(app);
}

// ───────────────────────── Chargement ─────────────────────────

struct Charge {
    profil: Profil,
    skin: Vec<u8>,
    capes: Vec<Option<Vec<u8>>>,
}

async fn recuperer() -> anyhow::Result<Charge> {
    let profil = skins::profil().await?;
    let skin = if profil.skin_url.is_empty() { Vec::new() } else { skins::telecharger(&profil.skin_url).await.unwrap_or_default() };
    let telechargements = profil.capes.iter().map(|c| async move {
        skins::telecharger(&c.url).await.ok()
    });
    let capes = futures::future::join_all(telechargements).await;
    Ok(Charge { profil, skin, capes })
}

fn charger(app: &Rc<App>) {
    let a = app.clone();
    lancer(async move {
        let st = s(&a);
        st.set_loading(true);
        st.set_info("".into());
        match a.bg(recuperer()).await {
            Ok(c) => {
                st.set_slim(c.profil.fin);
                {
                    let mut e = a.skin.borrow_mut();
                    e.skin_actuel = c.skin;
                    e.capes_img = c.capes;
                    e.profil = Some(c.profil);
                }
                afficher_personnage(&a);
                afficher_capes(&a);
            }
            Err(e) => st.set_info(loc::tf("Skin_ErreurChargement", &[&e]).into()),
        }
        st.set_loading(false);
    });
}


/// Preview in 3D: the chosen skin if any, otherwise the current skin, with the equipped cape.
fn afficher_personnage(app: &Rc<App>) {
    let fin = s(app).get_slim();
    let (png, cape, generation) = {
        let mut e = app.skin.borrow_mut();
        e.generation += 1;
        let png = e.fichier.clone().unwrap_or_else(|| e.skin_actuel.clone());
        let cape = e.profil.as_ref().and_then(|p| p.capes.iter().position(|c| c.active)).and_then(|i| e.capes_img.get(i).cloned().flatten());
        (png, cape, e.generation)
    };
    let a = app.clone();
    lancer(async move {
        let vues = a.bloquant(move || skins::rendre_tour(&png, fin, cape.as_deref())).await;
        if a.skin.borrow().generation != generation {
            return; // a newer preview was requested meanwhile
        }
        let st = s(&a);
        st.set_has_figure(vues.is_some());
        let images: Vec<Image> = vues.unwrap_or_default().iter().map(|v| Image::from_rgba8(images::tampon(v))).collect();
        st.set_frames(modele(images));
    });
}

fn afficher_capes(app: &Rc<App>) {
    let e = app.skin.borrow();
    let Some(profil) = &e.profil else { return };
    let aucune_active = !profil.capes.iter().any(|c| c.active);
    let mut elements = vec![CapeItem {
        id: "".into(),
        name: loc::t("Skin_AucuneCape").into(),
        image: Image::default(),
        has_image: false,
        active: aucune_active,
        col: 0,
        row: 0,
    }];
    for (i, cape) in profil.capes.iter().enumerate() {
        let image = e.capes_img.get(i).cloned().flatten().and_then(|o| skins::rendre_cape(&o)).map(|i| Image::from_rgba8(images::tampon(&i)));
        elements.push(CapeItem {
            id: cape.id.clone().into(),
            name: cape.nom.clone().into(),
            has_image: image.is_some(),
            image: image.unwrap_or_default(),
            active: cape.active,
            col: 0,
            row: 0,
        });
    }
    for (i, el) in elements.iter_mut().enumerate() {
        el.col = (i % COLONNES) as i32;
        el.row = (i / COLONNES) as i32;
    }
    let lignes = elements.len().div_ceil(COLONNES).max(1) as i32;
    drop(e);
    let st = s(app);
    st.set_cape_rows(lignes);
    st.set_capes(modele(elements));
}

// ───────────────────────── Actions ─────────────────────────

async fn choisir_fichier(app: &Rc<App>) {
    let Some(chemin) = dialogs::choisir_fichier(&loc::t("Skin_Fichier"), "PNG", &["png"]).await else { return };
    let octets = match std::fs::read(&chemin) {
        Ok(o) => o,
        Err(e) => return dialogs::erreur(app, &loc::t("Skin_Erreur"), &e.to_string()).await,
    };
    if !skins::skin_valide(&octets) {
        return dialogs::erreur(app, &loc::t("Skin_Erreur"), &loc::t("Skin_Invalide")).await;
    }
    app.skin.borrow_mut().fichier = Some(octets);
    let st = s(app);
    st.set_has_file(true);
    st.set_file_name(chemin.file_name().map(|n| n.to_string_lossy().to_string()).unwrap_or_default().into());
    st.set_info("".into());
    afficher_personnage(app);
}

async fn appliquer(app: &Rc<App>) {
    let Some(png) = app.skin.borrow().fichier.clone() else { return };
    let st = s(app);
    let fin = st.get_slim();
    st.set_busy(true);
    st.set_info("".into());
    let envoi = png.clone();
    match app.bg(async move { skins::changer_skin(envoi, fin).await }).await {
        Ok(()) => {
            {
                let mut e = app.skin.borrow_mut();
                e.skin_actuel = png.clone();
                e.fichier = None;
                if let Some(p) = e.profil.as_mut() {
                    p.fin = fin;
                }
            }
            st.set_has_file(false);
            st.set_file_name("".into());
            st.set_info(loc::t("Skin_Applique").into());
            afficher_personnage(app);
            // La pastille de l'accueil montre la nouvelle tête tout de suite
            if let Some(tete) = skins::rendre_tete(&png) {
                let home = app.ui.global::<HomeState>();
                home.set_has_avatar(true);
                home.set_avatar(Image::from_rgba8(images::tampon(&tete)));
            }
        }
        Err(e) => dialogs::erreur(app, &loc::t("Skin_Erreur"), &e.to_string()).await,
    }
    st.set_busy(false);
}

async fn changer_cape(app: &Rc<App>, id: String) {
    let st = s(app);
    st.set_busy(true);
    st.set_info("".into());
    let envoi = id.clone();
    let resultat = app
        .bg(async move { skins::activer_cape(if envoi.is_empty() { None } else { Some(envoi.as_str()) }).await })
        .await;
    match resultat {
        Ok(()) => {
            if let Some(p) = app.skin.borrow_mut().profil.as_mut() {
                for c in p.capes.iter_mut() {
                    c.active = c.id == id;
                }
            }
            afficher_capes(app);
            afficher_personnage(app);
            st.set_info(loc::t("Skin_CapeChangee").into());
        }
        Err(e) => dialogs::erreur(app, &loc::t("Skin_ErreurCape"), &e.to_string()).await,
    }
    st.set_busy(false);
}
