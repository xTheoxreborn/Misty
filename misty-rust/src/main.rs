//! Misty — launcher Minecraft, version Rust.
//!
//! - `services/`  : la logique (réglages, profils, Modrinth, CurseForge, modpacks, packs de textures…)
//! - `minecraft/` : installation et lancement du jeu, mod loaders
//! - `ui/`        : le lien entre l'interface Slint (dossier `ui/` à la racine) et les services

// Pas de console noire derrière la fenêtre (en release)
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod minecraft;
mod ms_login;
mod services;
#[cfg(test)]
mod tests;
mod ui;

use services::{discord, loc};
use slint::ComponentHandle;
use std::cell::RefCell;
use std::rc::Rc;

slint::include_modules!();

thread_local! {
    /// L'application, accessible depuis les rappels qui reviennent sur le thread de l'interface.
    pub static APP: RefCell<Option<Rc<ui::App>>> = const { RefCell::new(None) };
}

fn main() {
    // Mode « fenêtre de connexion Microsoft » (lancé par le launcher lui-même)
    if std::env::args().any(|a| a == "--ms-login") {
        ms_login::executer();
        return;
    }

    // Runtime pour le réseau et les fichiers (le thread principal reste à l'interface)
    let runtime = tokio::runtime::Builder::new_multi_thread().enable_all().build().expect("runtime tokio");

    let _ = std::fs::create_dir_all(services::paths::data_dir());
    loc::initialiser();
    discord::definir(discord::Statut::Launcher);

    let fenetre = MainWindow::new().expect("création de la fenêtre");
    let app = ui::App::new(fenetre, runtime.handle().clone());
    APP.with(|a| *a.borrow_mut() = Some(app.clone()));
    ui::installer(&app);

    app.ui.show().expect("affichage de la fenêtre");
    preparer_fenetre(&app);

    let a = app.clone();
    ui::lancer(async move { ui::demarrer(a).await });

    // Outil de développement : MISTY_CAPTURE=<dossier> enregistre une image de chaque page puis quitte
    #[cfg(debug_assertions)]
    if let Ok(dossier) = std::env::var("MISTY_CAPTURE") {
        capture(&app, std::path::PathBuf::from(dossier));
    }

    slint::run_event_loop().expect("boucle de l'interface");

    discord::arreter();
    APP.with(|a| *a.borrow_mut() = None);
    runtime.shutdown_timeout(std::time::Duration::from_secs(1));
}

/// Capture de chaque page (fenêtre placée hors de l'écran).
#[cfg(debug_assertions)]
fn capture(app: &Rc<ui::App>, dossier: std::path::PathBuf) {
    use slint::winit_030::WinitWindowAccessor;
    app.ui.window().with_winit_window(|w| w.set_outer_position(slint::winit_030::winit::dpi::PhysicalPosition::new(-6000, -6000)));
    let _ = std::fs::create_dir_all(&dossier);

    async fn attendre(ms: u64) {
        let (tx, rx) = futures::channel::oneshot::channel::<()>();
        slint::Timer::single_shot(std::time::Duration::from_millis(ms), move || {
            let _ = tx.send(());
        });
        let _ = rx.await;
    }
    let photo = |app: &ui::App, nom: &str, dossier: &std::path::Path| {
        if let Ok(b) = app.ui.window().take_snapshot() {
            if let Some(img) = image::RgbaImage::from_raw(b.width(), b.height(), b.as_bytes().to_vec()) {
                let _ = img.save(dossier.join(format!("{nom}.png")));
            }
        }
    };

    let app = app.clone();
    ui::lancer(async move {
        attendre(6000).await;
        photo(&app, "0-accueil", &dossier);
        let etat = app.ui.global::<AppState>();
        for (page, nom) in [(1, "1-profils"), (2, "2-options"), (3, "3-notes"), (4, "4-packs")] {
            match page {
                1 => ui::profiles::rafraichir(&app),
                2 => ui::settings::rafraichir(&app),
                4 => ui::packs::rafraichir(&app),
                _ => {}
            }
            etat.set_nav(page);
            etat.set_page(page);
            attendre(2500).await;
            photo(&app, nom, &dossier);
        }
        if let Some(i) = services::instances::lister().into_iter().next() {
            ui::instance::ouvrir(&app, i);
            attendre(4000).await;
            photo(&app, "5-instance", &dossier);
            app.ui.global::<InstanceState>().set_tab(1);
            app.ui.global::<InstanceState>().invoke_tab_changed(1);
            attendre(5000).await;
            photo(&app, "6-decouvrir", &dossier);
        }
        if let Some(p) = services::packs::lister().into_iter().find(|p| !p.version_mc.is_empty()) {
            ui::editor::ouvrir(&app, p);
            attendre(5000).await;
            app.ui.global::<EditorState>().invoke_select(0);
            attendre(1500).await;
            photo(&app, "7-editeur", &dossier);
        }
        let _ = slint::quit_event_loop();
    });
}

/// Centre la fenêtre et demande à Windows 11 des coins arrondis (la fenêtre n'a pas de bordure).
fn preparer_fenetre(app: &ui::App) {
    use slint::winit_030::WinitWindowAccessor;
    app.ui.window().with_winit_window(|w| {
        if let Some(ecran) = w.current_monitor() {
            let (taille, pos) = (ecran.size(), ecran.position());
            let fenetre = w.outer_size();
            let x = pos.x + (taille.width as i32 - fenetre.width as i32) / 2;
            let y = pos.y + (taille.height as i32 - fenetre.height as i32) / 2;
            w.set_outer_position(slint::winit_030::winit::dpi::PhysicalPosition::new(x, y));
        }
        #[cfg(windows)]
        coins_arrondis(w);
    });
}

#[cfg(windows)]
fn coins_arrondis(w: &slint::winit_030::winit::window::Window) {
    use slint::winit_030::winit::raw_window_handle::{HasWindowHandle, RawWindowHandle};
    use windows_sys::Win32::Graphics::Dwm::{DwmSetWindowAttribute, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND};
    let Ok(handle) = w.window_handle() else { return };
    if let RawWindowHandle::Win32(h) = handle.as_raw() {
        let preference = DWMWCP_ROUND;
        // SAFETY : HWND valide (fenêtre affichée) et pointeur vers une valeur de 4 octets, comme l'exige l'API.
        unsafe {
            DwmSetWindowAttribute(
                h.hwnd.get() as _,
                DWMWA_WINDOW_CORNER_PREFERENCE as u32,
                &preference as *const _ as *const core::ffi::c_void,
                std::mem::size_of_val(&preference) as u32,
            );
        }
    }
}
