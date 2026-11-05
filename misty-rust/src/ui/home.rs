//! Page d'accueil : choix du compte, du profil (ou jeu rapide : version + mod loader) et lancement du jeu.

use super::{dialogs, images, lancer, modele_textes, App};
use crate::minecraft::{install, launch, loaders, FileProgress, McDirs};
use crate::services::instances::{self, Instance};
use crate::services::{account, content, discord, loc, pseudos, settings};
use crate::{AppState, HomeState};
use slint::ComponentHandle;
use std::rc::Rc;
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::Arc;

#[derive(Default)]
pub struct Etat {
    version_choisie: String,
    modes_charges: bool,
    installation_en_cours: bool,
    /// Minecraft a été lancé depuis Misty et tourne encore.
    pub jeu_en_cours: bool,
    profil: Option<Instance>,
    /// Profils de la liste déroulante (None = jeu rapide).
    profils: Vec<Option<Instance>>,
    versions: Vec<String>,
    modes: Vec<String>,
    pseudos: Vec<String>,
}

fn h(app: &App) -> HomeState<'_> {
    app.ui.global::<HomeState>()
}

pub fn installer(app: &Rc<App>) {
    let s = h(app);
    s.set_play_text(loc::t("Home_Bouton_Jouer").into());
    s.set_status(loc::t("Home_Chargement").into());

    let a = app.clone();
    s.on_account_selected(move |i| {
        let pseudo = a.home.borrow().pseudos.get(i as usize).cloned();
        if let Some(p) = pseudo {
            settings::set(settings::LAST_PSEUDO, &p);
        }
        maj_avatar(&a);
    });
    let a = app.clone();
    s.on_ms_clicked(move || {
        let a = a.clone();
        lancer(async move {
            if account::session().is_none() {
                se_connecter(&a).await;
            } else {
                account::deconnecter();
                session_changee(&a);
            }
        });
    });
    let a = app.clone();
    s.on_profile_selected(move |i| {
        let profil = a.home.borrow().profils.get(i as usize).cloned().flatten();
        settings::set(settings::DERNIER_PROFIL, profil.as_ref().map(|p| p.id.as_str()).unwrap_or(""));
        a.home.borrow_mut().profil = profil;
        maj_mode_profil(&a);
    });
    let a = app.clone();
    s.on_version_selected(move |i| {
        let a = a.clone();
        lancer(async move { version_choisie(&a, i).await });
    });
    let a = app.clone();
    s.on_mode_selected(move |i| {
        if a.home.borrow().profil.is_none() {
            let mode = a.home.borrow().modes.get(i as usize).cloned().unwrap_or_else(|| loaders::VANILLA.into());
            h(&a).set_card_loader(mode.to_uppercase().into());
        }
    });
    let a = app.clone();
    s.on_reload(move || {
        let a = a.clone();
        lancer(async move { recharger_versions(&a).await });
    });
    let a = app.clone();
    s.on_manage(move || {
        let profil = a.home.borrow().profil.clone();
        if let Some(p) = profil {
            a.ui.global::<AppState>().set_nav(super::page::PROFILS);
            super::profiles::rafraichir(&a);
            super::instance::ouvrir(&a, p);
        }
    });
    let a = app.clone();
    s.on_play(move || {
        let a = a.clone();
        lancer(async move { jouer(&a).await });
    });
}

fn statut(app: &App, texte: &str) {
    h(app).set_status(texte.into());
}

// ───────────────────────── Initialisation ─────────────────────────

/// Appelé une fois au démarrage, après la préparation des fichiers de config.
pub async fn initialiser(app: &Rc<App>) {
    charger_pseudos(app);
    maj_compte(app);
    charger_profils(app);

    // Connexion automatique en parallèle du chargement des versions
    // (pas pendant les captures d'écran de développement : la fenêtre de connexion s'ouvrirait)
    let capture = cfg!(debug_assertions) && std::env::var_os("MISTY_CAPTURE").is_some();
    if !capture && settings::get_bool(settings::PREMIUM, false) && account::session().is_none() {
        let a = app.clone();
        lancer(async move { se_connecter(&a).await });
    }

    statut(app, &loc::t("Home_ChargementVersions"));
    charger_versions(app).await;
    charger_derniere_version(app);
    if app.home.borrow().profil.is_none() && h(app).get_version_index() < 0 {
        statut(app, &loc::t("Home_ChoisisVersion"));
    }
}

/// Appelé quand on revient sur la page (les options ont pu changer).
pub fn rafraichir(app: &Rc<App>, recharger_versions_aussi: bool) {
    charger_pseudos(app);
    charger_profils(app);
    if recharger_versions_aussi {
        let a = app.clone();
        lancer(async move { recharger_versions(&a).await });
    }
}

pub fn langue_changee(app: &Rc<App>) {
    maj_compte(app);
    charger_profils(app); // "Jeu rapide" + résumé du profil
    if !app.home.borrow().installation_en_cours {
        h(app).set_play_text(loc::t("Home_Bouton_Jouer").into());
    }
}

// ───────────────────────── Comptes ─────────────────────────

fn charger_pseudos(app: &Rc<App>) {
    let liste = pseudos::lire();
    let dernier = settings::get(settings::LAST_PSEUDO);
    let index = liste.iter().position(|p| *p == dernier).map(|i| i as i32).unwrap_or(if liste.is_empty() { -1 } else { 0 });
    h(app).set_accounts(modele_textes(&liste));
    h(app).set_account_index(index);
    app.home.borrow_mut().pseudos = liste;
    maj_avatar(app);
}

async fn se_connecter(app: &Rc<App>) {
    h(app).set_ms_busy(true);
    super::settings::occupe_microsoft(app, true);
    let ancien = h(app).get_status().to_string();
    statut(app, &loc::t("Home_ConnexionMicrosoft"));
    if let Err(e) = app.bg(account::connecter()).await {
        dialogs::erreur(app, &loc::t("Compte_ConnexionImpossible"), &e.to_string()).await;
    }
    h(app).set_ms_busy(false);
    super::settings::occupe_microsoft(app, false);
    statut(app, &ancien);
    session_changee(app);
}

/// Connexion / déconnexion : met à jour l'accueil et les options.
pub fn session_changee(app: &Rc<App>) {
    maj_compte(app);
    super::settings::maj_microsoft(app);
    super::skin::session_changee(app);
}

fn maj_compte(app: &Rc<App>) {
    let session = account::session();
    h(app).set_premium(session.is_some());
    h(app).set_premium_name(session.map(|s| s.pseudo).unwrap_or_default().into());
    maj_avatar(app);
}

pub fn pseudo_actuel(app: &App) -> String {
    if let Some(s) = account::session() {
        return s.pseudo;
    }
    let i = h(app).get_account_index();
    app.home
        .borrow()
        .pseudos
        .get(i.max(0) as usize)
        .filter(|p| !p.is_empty())
        .cloned()
        .unwrap_or_else(|| "Misty".into())
}

/// Tête du skin (mc-heads.net). Si le téléchargement échoue, le bloc d'herbe reste visible.
fn maj_avatar(app: &Rc<App>) {
    let pseudo = pseudo_actuel(app);
    let a = app.clone();
    lancer(async move {
        let url = format!("https://mc-heads.net/avatar/{}/64", urlencoding::encode(&pseudo));
        let image = images::depuis_url(&a, &url).await;
        if pseudo_actuel(&a) != pseudo {
            return; // le compte a changé entre-temps
        }
        h(&a).set_has_avatar(image.is_some());
        h(&a).set_avatar(image.unwrap_or_default());
    });
}

// ───────────────────────── Profils ─────────────────────────

fn charger_profils(app: &Rc<App>) {
    let id_voulu = app.home.borrow().profil.as_ref().map(|p| p.id.clone()).unwrap_or_else(|| settings::get(settings::DERNIER_PROFIL));

    let mut profils: Vec<Option<Instance>> = vec![None];
    profils.extend(instances::lister().into_iter().map(Some));
    let noms: Vec<String> = profils
        .iter()
        .map(|p| p.as_ref().map(|i| i.nom.clone()).unwrap_or_else(|| loc::t("Home_JeuRapide")))
        .collect();
    let index = profils.iter().position(|p| p.as_ref().is_some_and(|i| i.id == id_voulu)).unwrap_or(0);

    h(app).set_profiles(modele_textes(&noms));
    h(app).set_profile_index(index as i32);
    {
        let mut e = app.home.borrow_mut();
        e.profil = profils[index].clone();
        e.profils = profils;
    }
    maj_mode_profil(app);
}

/// Bascule l'affichage entre le jeu rapide et le profil sélectionné.
fn maj_mode_profil(app: &Rc<App>) {
    let (profil, version, modes_charges, installation, jeu) = {
        let e = app.home.borrow();
        (e.profil.clone(), e.version_choisie.clone(), e.modes_charges, e.installation_en_cours, e.jeu_en_cours)
    };
    let s = h(app);
    s.set_quick(profil.is_none());

    match &profil {
        Some(p) => {
            s.set_card_label(format!("{} · {}", loc::t("Home_Profil"), p.nom.to_uppercase()).into());
            s.set_card_version(p.version_mc.clone().into());
            s.set_card_loader(p.loader.to_uppercase().into());
            s.set_summary(resume_contenu(p).into());
            // L'icône du profil remplace le bloc d'herbe
            s.set_has_card_icon(false);
            let (fichier, url) = (p.chemin_icone(), p.icone_url.clone());
            let id = p.id.clone();
            let a = app.clone();
            lancer(async move {
                let image = match images::depuis_fichier(&fichier, 256) {
                    Some(i) => Some(i),
                    None => match url {
                        Some(u) => images::depuis_url(&a, &u).await,
                        None => None,
                    },
                };
                if a.home.borrow().profil.as_ref().map(|p| p.id.as_str()) == Some(id.as_str()) {
                    if let Some(i) = image {
                        h(&a).set_card_icon(i);
                        h(&a).set_has_card_icon(true);
                    }
                }
            });
        }
        None => {
            s.set_card_label(loc::t("Home_Version_Selected").into());
            s.set_card_version(if version.is_empty() { "—".into() } else { version.into() });
            let mode = app.home.borrow().modes.get(s.get_mode_index().max(0) as usize).cloned().unwrap_or_else(|| loaders::VANILLA.into());
            s.set_card_loader(mode.to_uppercase().into());
            s.set_has_card_icon(false);
        }
    }

    if !installation {
        let pret = profil.is_some() || modes_charges;
        s.set_play_enabled(pret);
        if pret {
            statut(app, &loc::t(if jeu { "Home_EnCours" } else { "Home_Pret" }));
        }
    }
}

/// "12 mods · 2 shaders"
fn resume_contenu(instance: &Instance) -> String {
    let mut compte: Vec<(String, usize)> = Vec::new();
    for c in content::lister(instance).into_iter().filter(|c| c.actif()) {
        match compte.iter_mut().find(|(t, _)| *t == c.type_) {
            Some((_, n)) => *n += 1,
            None => compte.push((c.type_.clone(), 1)),
        }
    }
    if compte.is_empty() {
        return loc::t("Home_AucunContenu");
    }
    compte.iter().map(|(t, n)| format!("{n} {}", content::libelle_nombre(t, *n))).collect::<Vec<_>>().join(" · ")
}

/// Sélectionne un profil et lance la partie (depuis la page Profils).
pub fn selectionner_et_jouer(app: &Rc<App>, instance: Instance) {
    settings::set(settings::DERNIER_PROFIL, &instance.id);
    app.home.borrow_mut().profil = Some(instance);
    charger_profils(app);
    if !app.home.borrow().installation_en_cours {
        let a = app.clone();
        lancer(async move { jouer(&a).await });
    }
}

// ───────────────────────── Versions (jeu rapide) ─────────────────────────

async fn charger_versions(app: &Rc<App>) {
    match app.bg(install::versions_proposees()).await {
        Ok(v) => {
            h(app).set_versions(modele_textes(&v));
            app.home.borrow_mut().versions = v;
        }
        Err(e) => {
            statut(app, &loc::t("Home_VersionsImpossible"));
            dialogs::erreur(app, &loc::t("Home_VersionsIndisponibles"), &loc::tf("Home_VersionsErreur", &[&e])).await;
        }
    }
}

fn charger_derniere_version(app: &Rc<App>) {
    let derniere = settings::get(settings::LAST_VERSION);
    let index = {
        let e = app.home.borrow();
        e.versions.iter().position(|v| *v == derniere).or(if e.versions.is_empty() { None } else { Some(0) }) // la plus récente
    };
    if let Some(i) = index {
        h(app).set_version_index(i as i32);
        let a = app.clone();
        lancer(async move { version_choisie(&a, i as i32).await });
    }
}

async fn recharger_versions(app: &Rc<App>) {
    let actuelle = app.home.borrow().version_choisie.clone();
    h(app).set_reload_enabled(false);
    if app.home.borrow().profil.is_none() {
        statut(app, &loc::t("Home_ChargementVersions"));
    }
    charger_versions(app).await;

    let index = app.home.borrow().versions.iter().position(|v| *v == actuelle);
    match index {
        Some(i) => h(app).set_version_index(i as i32),
        None => {
            h(app).set_version_index(-1);
            if app.home.borrow().profil.is_none() {
                statut(app, &loc::t("Home_ChoisisVersion"));
            }
        }
    }
    h(app).set_reload_enabled(true);
}

async fn version_choisie(app: &Rc<App>, i: i32) {
    let Some(version) = app.home.borrow().versions.get(i.max(0) as usize).cloned() else { return };
    {
        let mut e = app.home.borrow_mut();
        e.modes_charges = false;
        e.modes.clear();
        e.version_choisie = version.clone();
    }
    h(app).set_modes_enabled(false);
    h(app).set_modes(modele_textes(&[]));
    h(app).set_mode_index(-1);
    settings::set(settings::LAST_VERSION, &version);

    let rapide = app.home.borrow().profil.is_none();
    if rapide {
        h(app).set_play_enabled(false);
        h(app).set_card_version(version.clone().into());
        statut(app, &loc::tf("Commun_RechercheLoaders", &[&version]));
    }

    let v = version.clone();
    let modes = app.bg(async move { loaders::modes_disponibles(&v).await }).await;

    // L'utilisateur a changé de version pendant le chargement : on laisse la nouvelle sélection gérer
    if app.home.borrow().version_choisie != version {
        return;
    }
    h(app).set_modes(modele_textes(&modes));
    h(app).set_mode_index(0);
    let installation = app.home.borrow().installation_en_cours;
    h(app).set_modes_enabled(!installation);
    {
        let mut e = app.home.borrow_mut();
        e.modes = modes;
        e.modes_charges = true;
    }
    if app.home.borrow().profil.is_none() {
        maj_mode_profil(app);
    }
}

// ───────────────────────── Lancement ─────────────────────────

async fn jouer(app: &Rc<App>) {
    let (instance, version, mode) = {
        let e = app.home.borrow();
        let mode_rapide = e.modes.get(h(app).get_mode_index().max(0) as usize).cloned().unwrap_or_else(|| loaders::VANILLA.into());
        let instance = e.profil.clone();
        let version = instance.as_ref().map(|i| i.version_mc.clone()).unwrap_or_else(|| e.version_choisie.clone());
        let mode = instance.as_ref().map(|i| i.loader.clone()).unwrap_or(mode_rapide);
        (instance, version, mode)
    };
    if version.is_empty() || app.home.borrow().installation_en_cours {
        return;
    }

    app.home.borrow_mut().installation_en_cours = true;
    h(app).set_play_enabled(false);
    h(app).set_selectors_enabled(false);
    let nom = instance.as_ref().map(|i| i.nom.clone()).unwrap_or_else(|| format!("{version} {mode}"));
    afficher_progression(app, 0.0, &loc::tf("Home_Preparation", &[&nom]));

    // Téléchargement de la version et du loader
    let progression = progression_fichiers(app);
    let (v, m, lv) = (version.clone(), mode.clone(), instance.as_ref().and_then(|i| i.loader_version.clone()));
    let resultat = app
        .bg(async move {
            let dirs = McDirs::misty();
            let nom_version = loaders::installer(&dirs, &v, &m, lv.as_deref(), progression.clone()).await?;
            install::installer(&dirs, &nom_version, progression).await
        })
        .await;

    match resultat {
        Ok(installee) => {
            afficher_progression(app, 100.0, &loc::t("Home_Lancement"));
            lancer_jeu(app, &version, &mode, installee, instance).await;
        }
        Err(e) => {
            statut(app, &loc::t("Home_EchecTelechargement"));
            dialogs::erreur(app, &loc::t("Home_TelechargementImpossible"), &format!("{e:#}")).await;
        }
    }

    app.home.borrow_mut().installation_en_cours = false;
    let s = h(app);
    s.set_play_text(loc::t("Home_Bouton_Jouer").into());
    s.set_play_enabled(true);
    s.set_selectors_enabled(true);
    s.set_modes_enabled(app.home.borrow().modes_charges);
    s.set_percent_text("".into());
}

async fn lancer_jeu(app: &Rc<App>, version: &str, mode: &str, installee: install::VersionInstallee, instance: Option<Instance>) {
    let joueur = match account::session() {
        Some(s) => launch::Joueur { pseudo: s.pseudo, uuid: s.uuid, jeton: s.jeton, xuid: s.xuid, type_compte: "msa".into() },
        None => {
            let pseudo = pseudo_actuel(app);
            launch::Joueur { uuid: account::uuid_hors_ligne(&pseudo), pseudo, jeton: "0".into(), xuid: String::new(), type_compte: "legacy".into() }
        }
    };
    // Un profil a son propre dossier de jeu (mods, options, mondes) ; le jeu rapide utilise le dossier principal
    let options = launch::Options {
        joueur,
        ram_mo: settings::ram_mo(),
        dossier_jeu: instance.as_ref().map(|i| i.dossier()).unwrap_or_else(crate::services::paths::data_dir),
    };

    let resultat = app.bloquant(move || launch::lancer(&McDirs::misty(), &installee, &options)).await;
    let mut processus = match resultat {
        Ok(p) => p,
        Err(e) => {
            statut(app, &loc::t("Home_EchecLancement"));
            dialogs::erreur(app, &loc::t("Home_LancementImpossible"), &format!("{e:#}")).await;
            return;
        }
    };

    let detail = match &instance {
        Some(i) => format!("{mode} · {}", i.nom),
        None => mode.to_string(),
    };
    discord::definir(discord::Statut::EnJeu { version: version.to_string(), mode: detail });
    app.home.borrow_mut().jeu_en_cours = true;
    statut(app, &loc::tf("Home_Lance", &[&instance.as_ref().map(|i| i.nom.clone()).unwrap_or_else(|| format!("Minecraft {version}"))]));

    if let Some(mut i) = instance {
        i.dernier_lancement = Some(chrono::Local::now());
        let _ = instances::sauver(&i);
    }

    // Fin du jeu : on revient à l'état « prêt »
    let faible = app.ui.as_weak();
    std::thread::spawn(move || {
        let _ = processus.wait();
        let _ = faible.upgrade_in_event_loop(|_| {
            if let Some(a) = crate::APP.with(|a| a.borrow().clone()) {
                a.home.borrow_mut().jeu_en_cours = false;
                if a.ui.global::<AppState>().get_page() != super::page::PACKS {
                    discord::definir(discord::Statut::Launcher);
                }
                statut(&a, &loc::t("Home_Pret"));
                let profil = a.home.borrow().profil.clone();
                if let Some(p) = profil {
                    h(&a).set_summary(resume_contenu(&p).into());
                }
            }
        });
    });
}

// ───────────────────────── Affichage ─────────────────────────

/// Avancement des téléchargements (limité à ~20 mises à jour par seconde).
fn progression_fichiers(app: &Rc<App>) -> FileProgress {
    let faible = app.ui.as_weak();
    let derniere = Arc::new(AtomicU64::new(0));
    Arc::new(move |fait: usize, total: usize, nom: &str| {
        if total == 0 {
            return;
        }
        let maintenant = std::time::SystemTime::now().duration_since(std::time::UNIX_EPOCH).map(|d| d.as_millis() as u64).unwrap_or(0);
        if fait < total && maintenant.saturating_sub(derniere.load(Ordering::Relaxed)) < 50 {
            return;
        }
        derniere.store(maintenant, Ordering::Relaxed);
        let pourcentage = fait as f32 * 100.0 / total as f32;
        let texte = loc::tf("Home_Telechargement", &[&nom]);
        let _ = faible.upgrade_in_event_loop(move |ui| {
            if let Some(a) = crate::APP.with(|a| a.borrow().clone()) {
                afficher_progression(&a, pourcentage, &texte);
            } else {
                let _ = ui;
            }
        });
    })
}

fn afficher_progression(app: &App, pourcentage: f32, texte: &str) {
    let s = h(app);
    s.set_status(texte.into());
    s.set_percent_text(format!("{pourcentage:.0}%").into());
    s.set_progress(pourcentage / 100.0);
    if app.home.borrow().installation_en_cours {
        s.set_play_text(format!("{pourcentage:.0}%").into());
    }
}
