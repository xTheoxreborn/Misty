//! Explorateur de contenu Modrinth / CurseForge : recherche, tri, défilement infini, bouton d'installation.
//! Le même explorateur sert au détail d'un profil (mods, packs…) et à l'installation de modpacks.

use super::{images, lancer, maj_ligne, modele, modele_textes, App};
use crate::services::catalogue::{self, ProjetDistant, CURSEFORGE, MODRINTH};
use crate::services::instances::Instance;
use crate::services::{content, curseforge, loc, modrinth};
use crate::{BrowserState, ResultItem, TabItem};
use slint::{ComponentHandle, Model, ModelRc};
use std::rc::Rc;
use std::time::Duration;

const TAILLE_PAGE: usize = 20;

#[derive(Default, Clone)]
pub enum Contexte {
    #[default]
    Aucun,
    Instance(Instance),
    Modpacks,
}

pub struct Etat {
    contexte: Contexte,
    /// Contexte déjà configuré ("" = à reconfigurer)
    cle: String,
    /// Dernière plateforme choisie, retenue d'un explorateur à l'autre
    source: String,
    type_: String,
    types: Vec<String>,
    total: i64,
    /// Ignore les réponses des recherches périmées
    generation: u32,
    chargement: bool,
    projets: Vec<ProjetDistant>,
    installes: Vec<bool>,
    occupes: Vec<bool>,
    modele: Option<ModelRc<ResultItem>>,
    anti_rebond: slint::Timer,
}

impl Default for Etat {
    fn default() -> Self {
        Etat {
            contexte: Contexte::Aucun,
            cle: String::new(),
            source: MODRINTH.into(),
            type_: content::MOD.into(),
            types: Vec::new(),
            total: 0,
            generation: 0,
            chargement: false,
            projets: Vec::new(),
            installes: Vec::new(),
            occupes: Vec::new(),
            modele: None,
            anti_rebond: slint::Timer::default(),
        }
    }
}

fn b(app: &App) -> BrowserState<'_> {
    app.ui.global::<BrowserState>()
}

pub fn installer(app: &Rc<App>) {
    let s = b(app);
    let a = app.clone();
    s.on_type_changed(move |i| {
        let t = a.browser.borrow().types.get(i as usize).cloned();
        if let Some(t) = t {
            a.browser.borrow_mut().type_ = t;
            rechercher_plus_tard(&a, true);
        }
    });
    let a = app.clone();
    s.on_source_changed(move |i| {
        a.browser.borrow_mut().source = if i == 1 { CURSEFORGE.into() } else { MODRINTH.into() };
        remplir_tris(&a);
        rechercher_plus_tard(&a, true);
    });
    let a = app.clone();
    s.on_sort_changed(move |_| rechercher_plus_tard(&a, true));
    let a = app.clone();
    s.on_query_changed(move |_| {
        // Anti-rebond : on attend que l'utilisateur ait fini de taper
        let a2 = a.clone();
        a.browser.borrow().anti_rebond.start(slint::TimerMode::SingleShot, Duration::from_millis(350), move || {
            rechercher_plus_tard(&a2, true)
        });
    });
    let a = app.clone();
    s.on_near_end(move || {
        let (chargement, n, total) = {
            let e = a.browser.borrow();
            (e.chargement, e.projets.len() as i64, e.total)
        };
        if !chargement && n < total {
            rechercher_plus_tard(&a, false);
        }
    });
    let a = app.clone();
    s.on_install(move |i| {
        let a = a.clone();
        lancer(async move { installer_projet(&a, i as usize).await });
    });
}

fn rechercher_plus_tard(app: &Rc<App>, nouvelle: bool) {
    let a = app.clone();
    lancer(async move { rechercher(&a, nouvelle).await });
}

/// Explorateur d'un profil (mods, packs, datapacks, shaders).
pub fn configurer_instance(app: &Rc<App>, instance: Instance) {
    if app.browser.borrow().cle == instance.id {
        rafraichir_etats(app);
        return;
    }
    let mut type_ = content::MOD.to_string();
    if !instance.supporte_mods() {
        type_ = content::PACK_RESSOURCES.into();
    }
    let cle = instance.id.clone();
    configurer(app, Contexte::Instance(instance), cle, type_);
}

/// Explorateur de modpacks.
pub fn configurer_modpacks(app: &Rc<App>) {
    if app.browser.borrow().cle == "modpacks" {
        rafraichir_etats(app);
        return;
    }
    configurer(app, Contexte::Modpacks, "modpacks".into(), content::MODPACK.into());
}

/// Un autre profil a été ouvert : l'explorateur devra être reconfiguré.
pub fn oublier_instance(app: &Rc<App>) {
    let mut e = app.browser.borrow_mut();
    if matches!(e.contexte, Contexte::Instance(_)) {
        e.cle.clear();
    }
}

fn configurer(app: &Rc<App>, contexte: Contexte, cle: String, type_: String) {
    {
        let mut e = app.browser.borrow_mut();
        e.contexte = contexte;
        e.cle = cle;
        e.type_ = type_;
        e.anti_rebond.stop();
    }
    construire_types(app);
    let source = app.browser.borrow().source.clone();
    b(app).set_source(if source == CURSEFORGE { 1 } else { 0 });
    remplir_tris(app);
    b(app).set_query("".into());
    rechercher_plus_tard(app, true);
}

fn types_du_contexte(contexte: &Contexte) -> Vec<(String, String)> {
    match contexte {
        Contexte::Modpacks => vec![(content::MODPACK.into(), loc::t("Type_Modpacks"))],
        _ => content::types(),
    }
}

fn construire_types(app: &Rc<App>) {
    let (contexte, type_) = {
        let e = app.browser.borrow();
        (e.contexte.clone(), e.type_.clone())
    };
    let types = types_du_contexte(&contexte);
    // Pas de mods possibles sur un profil Vanilla / OptiFine
    let mods_possibles = match &contexte {
        Contexte::Instance(i) => i.supporte_mods(),
        _ => true,
    };
    let items: Vec<TabItem> = types
        .iter()
        .map(|(t, l)| TabItem { text: l.clone().into(), enabled: t != content::MOD || mods_possibles })
        .collect();
    b(app).set_types(modele(items));
    b(app).set_type_index(types.iter().position(|(t, _)| *t == type_).unwrap_or(0) as i32);
    app.browser.borrow_mut().types = types.into_iter().map(|(t, _)| t).collect();
}

fn remplir_tris(app: &Rc<App>) {
    let source = app.browser.borrow().source.clone();
    let tris: Vec<String> = catalogue::tris(&source).into_iter().map(|(_, l)| l).collect();
    b(app).set_sorts(modele_textes(&tris));
    b(app).set_sort_index(0);
}

pub fn langue_changee(app: &Rc<App>) {
    if matches!(app.browser.borrow().contexte, Contexte::Aucun) {
        return;
    }
    construire_types(app);
    let tri = b(app).get_sort_index();
    remplir_tris(app);
    b(app).set_sort_index(tri.max(0));
    rafraichir_etats(app);
    maj_info(app);
    let (chargement, n, total) = {
        let e = app.browser.borrow();
        (e.chargement, e.projets.len() as i64, e.total)
    };
    if !chargement && total > 0 {
        b(app).set_bottom_text(if n >= total { loc::t("Explorateur_Fin") } else { String::new() }.into());
    }
}

// ───────────────────────── Recherche ─────────────────────────

async fn rechercher(app: &Rc<App>, nouvelle: bool) {
    let generation = {
        let mut e = app.browser.borrow_mut();
        if nouvelle {
            e.generation += 1;
            e.projets.clear();
            e.installes.clear();
            e.occupes.clear();
            e.total = 0;
        } else if e.chargement {
            return;
        }
        e.generation
    };
    if nouvelle {
        let m = modele(Vec::<ResultItem>::new());
        b(app).set_results(m.clone());
        app.browser.borrow_mut().modele = Some(m);
        b(app).set_scroll_reset(b(app).get_scroll_reset() + 1);
    }

    // CurseForge sans clé : on explique comment la configurer au lieu de chercher
    let source = app.browser.borrow().source.clone();
    let sans_cle = source == CURSEFORGE && !curseforge::disponible();
    b(app).set_no_key(sans_cle);
    if sans_cle {
        return;
    }

    app.browser.borrow_mut().chargement = true;
    b(app).set_bottom_text(loc::t("Commun_ChargementMaj").into());
    maj_info(app);

    let (type_, instance, offset) = {
        let e = app.browser.borrow();
        let instance = match &e.contexte {
            Contexte::Instance(i) => Some(i.clone()),
            _ => None,
        };
        (e.type_.clone(), instance, e.projets.len())
    };
    let tris = catalogue::tris(&source);
    let tri = tris.get(b(app).get_sort_index().max(0) as usize).map(|(c, _)| c.clone()).unwrap_or_default();
    let requete = b(app).get_query().trim().to_string();

    let src = source.clone();
    let resultat = app
        .bg(async move { catalogue::rechercher(&src, &requete, &type_, instance.as_ref(), &tri, offset, TAILLE_PAGE).await })
        .await;

    if app.browser.borrow().generation != generation {
        return; // une autre recherche a été lancée entre-temps
    }

    match resultat {
        Ok((projets, total)) => {
            let debut = app.browser.borrow().projets.len();
            let items: Vec<ResultItem> = projets.iter().map(|p| item(app, p)).collect();
            let m = app.browser.borrow().modele.clone();
            if let Some(m) = &m {
                if let Some(vm) = m.as_any().downcast_ref::<slint::VecModel<ResultItem>>() {
                    for it in items {
                        vm.push(it);
                    }
                }
            }
            {
                let mut e = app.browser.borrow_mut();
                e.total = total;
                // les états « installé » sont calculés juste après par rafraichir_etats
                for _ in &projets {
                    e.installes.push(false);
                    e.occupes.push(false);
                }
                e.projets.extend(projets.iter().cloned());
            }
            rafraichir_etats(app);
            let n = app.browser.borrow().projets.len() as i64;
            b(app).set_bottom_text(
                if n == 0 {
                    loc::t("Explorateur_Aucun")
                } else if n >= total {
                    loc::t("Explorateur_Fin")
                } else {
                    String::new()
                }
                .into(),
            );

            // Icônes
            if let Some(m) = m {
                for (k, p) in projets.into_iter().enumerate() {
                    let Some(url) = p.icone_url else { continue };
                    let (m, a) = (m.clone(), app.clone());
                    lancer(async move {
                        if let Some(img) = images::depuis_url(&a, &url).await {
                            maj_ligne(&m, debut + k, |r| {
                                r.icon = img;
                                r.has_icon = true;
                            });
                        }
                    });
                }
            }
        }
        Err(e) => {
            b(app).set_bottom_text(
                format!("{} : {}", loc::tf("Explorateur_Injoignable", &[&catalogue::libelle_source(&source)]).to_uppercase(), e.to_string().to_uppercase())
                    .into(),
            );
        }
    }

    app.browser.borrow_mut().chargement = false;
    maj_info(app);
}

fn maj_info(app: &Rc<App>) {
    let e = app.browser.borrow();
    let source = catalogue::libelle_source(&e.source);
    let filtre = match &e.contexte {
        Contexte::Instance(i) => {
            let loader = if e.type_ == content::MOD || e.type_ == content::SHADER { format!(" ({})", i.loader) } else { String::new() };
            format!(" · {}{loader}", loc::tf("Explorateur_Compatibles", &[&i.version_mc]))
        }
        _ => String::new(),
    };
    let debut = if e.total > 0 { loc::tf("Explorateur_Resultats", &[&formater_entier(e.total), &source]) } else { source.to_string() };
    b(app).set_info(format!("{debut}{filtre}").into());
}

/// 12345 → "12 345"
fn formater_entier(n: i64) -> String {
    let texte = n.to_string();
    let mut resultat = String::new();
    for (i, c) in texte.chars().enumerate() {
        if i > 0 && (texte.len() - i) % 3 == 0 {
            resultat.push('\u{202F}');
        }
        resultat.push(c);
    }
    resultat
}

fn item(_app: &App, p: &ProjetDistant) -> ResultItem {
    let categories: Vec<&str> = p
        .categories
        .iter()
        .map(String::as_str)
        .filter(|c| !matches!(*c, "fabric" | "forge" | "neoforge" | "quilt" | "minecraft" | "datapack" | "iris" | "optifine"))
        .take(4)
        .collect();
    ResultItem {
        title: p.titre.clone().into(),
        author: if p.auteur.is_empty() { String::new() } else { loc::tf("Explorateur_Par", &[&p.auteur]) }.into(),
        description: p.description.clone().into(),
        downloads: modrinth::formater_nombre(p.telechargements).into(),
        categories: categories.join(" · ").into(),
        icon: images::vide(),
        has_icon: false,
        installed: false,
        busy: false,
        button_text: "".into(),
        button_icon: "".into(),
    }
}

fn est_installe(app: &App, p: &ProjetDistant) -> bool {
    match &app.browser.borrow().contexte {
        Contexte::Instance(_) => super::instance::est_installe(app, p),
        Contexte::Modpacks => super::profiles::modpack_installe(p),
        Contexte::Aucun => false,
    }
}

/// Met à jour l'état « Installé » (et le texte du bouton) des résultats affichés.
pub fn rafraichir_etats(app: &Rc<App>) {
    let projets = app.browser.borrow().projets.clone();
    let installes: Vec<bool> = projets.iter().map(|p| est_installe(app, p)).collect();
    let (occupes, m, libelle) = {
        let mut e = app.browser.borrow_mut();
        e.installes = installes.clone();
        let libelle = if matches!(e.contexte, Contexte::Modpacks) { "Profil_Creer" } else { "Explorateur_Installer" };
        (e.occupes.clone(), e.modele.clone(), libelle)
    };
    let Some(m) = m else { return };
    for i in 0..m.row_count().min(projets.len()) {
        let (installe, occupe) = (installes[i], occupes.get(i).copied().unwrap_or(false));
        let auteur = &projets[i].auteur;
        maj_ligne(&m, i, |r| {
            r.installed = installe;
            r.busy = occupe;
            r.author = if auteur.is_empty() { String::new() } else { loc::tf("Explorateur_Par", &[auteur]) }.into();
            r.button_text = loc::t(if occupe {
                "Explorateur_EnCours"
            } else if installe {
                "Explorateur_Installe"
            } else {
                libelle
            })
            .into();
            r.button_icon = if occupe { "\u{E895}" } else if installe { "\u{E73E}" } else { "\u{E896}" }.into();
        });
    }
}

// ───────────────────────── Installation ─────────────────────────

async fn installer_projet(app: &Rc<App>, i: usize) {
    let (projet, contexte, type_, installe, occupe) = {
        let e = app.browser.borrow();
        let Some(p) = e.projets.get(i).cloned() else { return };
        (p, e.contexte.clone(), e.type_.clone(), e.installes.get(i).copied().unwrap_or(false), e.occupes.get(i).copied().unwrap_or(false))
    };
    if installe || occupe {
        return;
    }
    if let Some(o) = app.browser.borrow_mut().occupes.get_mut(i) {
        *o = true;
    }
    rafraichir_etats(app);

    match contexte {
        Contexte::Instance(_) => {
            super::instance::installer_projet(app, projet, type_).await;
        }
        Contexte::Modpacks => {
            super::profiles::installer_modpack(app, projet).await;
        }
        Contexte::Aucun => {}
    }

    if let Some(o) = app.browser.borrow_mut().occupes.get_mut(i) {
        *o = false;
    }
    // les dépendances installées changent aussi d'état
    rafraichir_etats(app);
}
