//! Interface : relie les pages Slint (ui/*.slint) aux services.
//!
//! Principe :
//! - toute la logique d'interface tourne sur le thread de l'interface, dans des tâches `slint::spawn_local`
//!   (l'équivalent des `async void` du C#) ;
//! - le travail lourd (réseau, fichiers) part sur le runtime tokio avec `bg(...)`, et on attend le résultat ;
//! - chaque page a son module, avec son état dans un `RefCell`.

pub mod browser;
pub mod changelog;
pub mod dialogs;
pub mod editor;
pub mod home;
pub mod images;
pub mod instance;
pub mod packs;
pub mod profiles;
pub mod settings;

use crate::services::{discord, loc, paths, system_info, Progress};
use crate::*;
use slint::{ComponentHandle, Model, ModelRc, SharedString, VecModel};
use std::cell::{Cell, RefCell};
use std::future::Future;
use std::rc::Rc;
use std::sync::Arc;

/// Pages (doivent correspondre à AppState.page dans state.slint).
pub mod page {
    pub const ACCUEIL: i32 = 0;
    pub const PROFILS: i32 = 1;
    pub const OPTIONS: i32 = 2;
    #[allow(dead_code)]
    pub const NOTES: i32 = 3;
    pub const PACKS: i32 = 4;
    pub const INSTANCE: i32 = 5;
    pub const EDITEUR: i32 = 6;
}

/// L'application : la fenêtre, le runtime et l'état de chaque page.
pub struct App {
    pub ui: MainWindow,
    pub rt: tokio::runtime::Handle,
    pub dialog: RefCell<Option<futures::channel::oneshot::Sender<Option<i32>>>>,
    pub images: images::Cache,
    pub home: RefCell<home::Etat>,
    pub profiles: RefCell<profiles::Etat>,
    pub instance: RefCell<instance::Etat>,
    pub browser: RefCell<browser::Etat>,
    pub settings: RefCell<settings::Etat>,
    pub packs: RefCell<packs::Etat>,
    pub editor: RefCell<editor::Etat>,
    /// Avertissement « expérimental » des packs déjà montré
    pub averti_packs: Cell<bool>,
}

impl App {
    pub fn new(ui: MainWindow, rt: tokio::runtime::Handle) -> Rc<App> {
        Rc::new(App {
            ui,
            rt,
            dialog: RefCell::new(None),
            images: images::Cache::default(),
            home: RefCell::default(),
            profiles: RefCell::default(),
            instance: RefCell::default(),
            browser: RefCell::default(),
            settings: RefCell::default(),
            packs: RefCell::default(),
            editor: RefCell::default(),
            averti_packs: Cell::new(false),
        })
    }

    /// Lance un travail sur le runtime tokio et attend son résultat.
    pub fn bg<T, F>(&self, f: F) -> impl Future<Output = T>
    where
        F: Future<Output = T> + Send + 'static,
        T: Send + 'static,
    {
        let tache = self.rt.spawn(f);
        async move { tache.await.expect("tâche de fond interrompue") }
    }

    /// Lance un calcul bloquant (fichiers, images…) hors du thread de l'interface.
    pub fn bloquant<T, F>(&self, f: F) -> impl Future<Output = T>
    where
        F: FnOnce() -> T + Send + 'static,
        T: Send + 'static,
    {
        let tache = self.rt.spawn_blocking(f);
        async move { tache.await.expect("tâche de fond interrompue") }
    }

    /// Fonction de progression qui affiche le texte avec `afficher` (sur le thread de l'interface).
    pub fn progression(&self, afficher: impl Fn(&MainWindow, String) + Send + Sync + Clone + 'static) -> Progress {
        let faible = self.ui.as_weak();
        Arc::new(move |texte: String| {
            let afficher = afficher.clone();
            let _ = faible.upgrade_in_event_loop(move |ui| afficher(&ui, texte));
        })
    }
}

/// Lance une tâche asynchrone sur le thread de l'interface.
pub fn lancer(f: impl Future<Output = ()> + 'static) {
    let _ = slint::spawn_local(f);
}

/// Vec<String> → modèle Slint de textes.
pub fn modele_textes(textes: &[String]) -> ModelRc<SharedString> {
    ModelRc::new(VecModel::from(textes.iter().map(SharedString::from).collect::<Vec<_>>()))
}

/// Vec<T> → modèle Slint.
pub fn modele<T: Clone + 'static>(v: Vec<T>) -> ModelRc<T> {
    ModelRc::new(VecModel::from(v))
}

/// Modifie une ligne d'un modèle (s'il a encore cette ligne).
pub fn maj_ligne<T: Clone + 'static>(m: &ModelRc<T>, i: usize, f: impl FnOnce(&mut T)) {
    if let Some(mut ligne) = m.row_data(i) {
        f(&mut ligne);
        m.set_row_data(i, ligne);
    }
}

// ───────────────────────── Démarrage ─────────────────────────

pub fn installer(app: &Rc<App>) {
    let ui = &app.ui;

    // Traductions
    ui.global::<Tr>().on_lookup(|cle| loc::t(&cle).into());
    ui.global::<Assets>().set_grass(images::bloc_herbe());
    ui.global::<AppState>().set_version_text(format!("v{}", crate::services::app_info::VERSION).into());
    ui.global::<AppState>().set_particles(modele(particules()));

    // Fenêtre
    let a = app.clone();
    ui.global::<AppState>().on_drag(move || fenetre(&a, |w| {
        let _ = w.drag_window();
    }));
    let a = app.clone();
    ui.global::<AppState>().on_minimize(move || fenetre(&a, |w| w.set_minimized(true)));
    ui.global::<AppState>().on_close(|| {
        let _ = slint::quit_event_loop();
    });
    ui.global::<AppState>().on_open_data_folder(|| system_info::ouvrir_dossier(&paths::data_dir()));
    let a = app.clone();
    ui.global::<AppState>().on_nav_clicked(move |i| nav_clicked(&a, i));

    dialogs::installer(app);
    home::installer(app);
    profiles::installer(app);
    instance::installer(app);
    browser::installer(app);
    settings::installer(app);
    changelog::installer(app);
    packs::installer(app);
    editor::installer(app);
}

/// Accès à la fenêtre winit (déplacement, réduction).
fn fenetre(app: &App, f: impl FnOnce(&slint::winit_030::winit::window::Window)) {
    use slint::winit_030::WinitWindowAccessor;
    app.ui.window().with_winit_window(f);
}

/// Petits carrés blancs translucides qui flottent lentement en fond.
fn particules() -> Vec<Particle> {
    let mut graine: u32 = 7;
    let mut hasard = move || {
        graine = graine.wrapping_mul(1_103_515_245).wrapping_add(12_345);
        ((graine >> 16) & 0x7fff) as f32 / 32768.0
    };
    (0..14)
        .map(|_| {
            let taille = 8.0 + hasard() * 22.0;
            Particle {
                x: 120.0 + hasard() * 1000.0,
                y: 20.0 + hasard() * 640.0,
                size: taille,
                opacity: 0.06 + hasard() * 0.14,
                amp: 10.0 + hasard() * 10.0,
                period: (8000.0 + hasard() * 8000.0) as i64,
            }
        })
        .collect()
}

/// Démarrage (après l'affichage de la fenêtre) : migration, mises à jour, accueil.
pub async fn demarrer(app: Rc<App>) {
    // Ancien dossier C:\TEXT
    match app.bloquant(crate::services::update::migrer_ancien_dossier).await {
        Ok(true) => dialogs::info(&app, &loc::t("Demarrage_Migre"), &loc::t("Demarrage_Migre_Texte")).await,
        Ok(false) => {}
        Err(e) => {
            dialogs::erreur(&app, &loc::t("Demarrage_MigrationImpossible"), &loc::tf("Demarrage_MigrationImpossible_Texte", &[&e])).await
        }
    }

    crate::services::settings::initialiser();
    crate::services::pseudos::initialiser();

    verifier_mise_a_jour(&app).await;
    match app.bg(crate::services::update::finaliser()).await {
        Ok(true) => {
            dialogs::info(&app, &loc::t("Maj_Terminee"), &loc::tf("Maj_Terminee_Texte", &[&crate::services::app_info::VERSION])).await
        }
        Ok(false) => {}
        Err(e) => dialogs::erreur(&app, &loc::t("Maj_Nettoyage"), &loc::tf("Maj_Nettoyage_Texte", &[&e])).await,
    }

    home::initialiser(&app).await;
}

async fn verifier_mise_a_jour(app: &Rc<App>) {
    let Ok(Some(release)) = app.bg(crate::services::update::verifier()).await else { return };
    let installer = dialogs::confirmer(
        app,
        &loc::t("Maj_Disponible"),
        &loc::tf("Maj_Disponible_Texte", &[&release.version, &crate::services::app_info::VERSION]),
        Some(&loc::t("Maj_Installer")),
        Some(&loc::t("Commun_PlusTard")),
        false,
    )
    .await;
    if !installer {
        return;
    }
    match app.bg(async move { crate::services::update::installer(&release).await }).await {
        Ok(()) => {
            let _ = slint::quit_event_loop();
        }
        Err(e) => dialogs::erreur(app, &loc::t("Maj_Impossible"), &e.to_string()).await,
    }
}

// ───────────────────────── Navigation ─────────────────────────

fn nav_clicked(app: &Rc<App>, i: i32) {
    let etat = app.ui.global::<AppState>();
    let page = etat.get_page();

    // Re-cliquer sur l'onglet actif depuis une sous-page (profil, éditeur) ramène à sa page principale
    if i == etat.get_nav() {
        if i == page::PROFILS && page == page::INSTANCE {
            profiles::rafraichir(app);
            naviguer(app, page::PROFILS);
        } else if i == page::PACKS && page == page::EDITEUR {
            naviguer(app, page::PACKS);
            packs::rafraichir(app);
        }
        return;
    }

    etat.set_nav(i);
    match i {
        page::ACCUEIL => {
            let recharger = settings::consommer_changement_types(app);
            home::rafraichir(app, recharger);
        }
        page::OPTIONS => settings::rafraichir(app),
        page::PROFILS => profiles::rafraichir(app),
        _ => {}
    }
    naviguer(app, i);

    if i == page::PACKS {
        packs::rafraichir(app); // après naviguer : l'éditeur a pu enregistrer une texture
        discord::definir(discord::Statut::PackEditeur);
        // Avertissement montré une seule fois par session
        if !app.averti_packs.replace(true) {
            let a = app.clone();
            lancer(async move { dialogs::info(&a, &loc::t("Pack_Experimental"), &loc::t("Pack_Experimental_Texte")).await });
        }
    } else if !app.home.borrow().jeu_en_cours {
        discord::definir(discord::Statut::Launcher);
    }
}

/// Affiche une page (et la barre latérale correspondante).
pub fn naviguer(app: &Rc<App>, p: i32) {
    let etat = app.ui.global::<AppState>();
    if etat.get_page() == p {
        return;
    }
    // On quitte l'éditeur : la texture en cours ne doit pas être perdue
    if etat.get_page() == page::EDITEUR {
        editor::quitter(app);
    }
    etat.set_nav(match p {
        page::INSTANCE => page::PROFILS,
        page::EDITEUR => page::PACKS,
        autre => autre,
    });
    etat.set_page(p);
}

/// Langue changée : recalcule les textes posés depuis Rust.
pub fn langue_changee(app: &Rc<App>) {
    let tr = app.ui.global::<Tr>();
    tr.set_rev(tr.get_rev() + 1);
    discord::reappliquer();
    home::langue_changee(app);
    profiles::rafraichir_liste(app);
    instance::langue_changee(app);
    browser::langue_changee(app);
    settings::maj_textes(app);
    editor::langue_changee(app);
}
