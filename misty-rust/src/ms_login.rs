//! Fenêtre de connexion Microsoft (mode `Misty.exe --ms-login`).
//!
//! Affiche la page de connexion de Microsoft dans une WebView (WebView2 sous Windows).
//! Quand Microsoft redirige vers l'URL de fin, on lit le code dans l'adresse,
//! on l'écrit sur la sortie standard ("CODE:...") et on ferme la fenêtre.
//! Le processus principal lit cette sortie (voir services::account).

use crate::services::{account, loc};
use std::io::Write;
use std::sync::{Arc, Mutex};
use tao::event::{Event, WindowEvent};
use tao::event_loop::{ControlFlow, EventLoopBuilder};
use tao::window::WindowBuilder;
use wry::{WebContext, WebViewBuilder};

enum Fin {
    Code(String),
    Erreur(String),
}

pub fn executer() {
    loc::initialiser();

    let event_loop = EventLoopBuilder::<Fin>::with_user_event().build();
    let proxy = event_loop.create_proxy();

    let fenetre = WindowBuilder::new()
        .with_title(format!("Misty — {}", loc::t("Compte_SeConnecter")))
        .with_inner_size(tao::dpi::LogicalSize::new(520.0, 680.0))
        .build(&event_loop)
        .expect("fenêtre de connexion");

    // Cookies stockés dans le dossier de Misty (pas à côté de l'exe)
    let _ = std::fs::create_dir_all(account::dossier_webview());
    let mut contexte = WebContext::new(Some(account::dossier_webview()));

    let deja_fini = Arc::new(Mutex::new(false));
    let navigation = {
        let deja_fini = deja_fini.clone();
        move |url: String| -> bool {
            if !url.starts_with(account::REDIRECT) {
                return true; // on laisse la page de Microsoft naviguer
            }
            let mut fini = deja_fini.lock().unwrap();
            if !*fini {
                *fini = true;
                let fin = match parametre(&url, "code") {
                    Some(code) => Fin::Code(code),
                    None => Fin::Erreur(parametre(&url, "error_description").unwrap_or_else(|| loc::t("Compte_Annule"))),
                };
                let _ = proxy.send_event(fin);
            }
            false
        }
    };

    let _webview = WebViewBuilder::new_with_web_context(&mut contexte)
        .with_url(account::url_connexion())
        .with_navigation_handler(navigation)
        .build(&fenetre)
        .expect("WebView (WebView2 est-il installé ?)");

    event_loop.run(move |event, _, control_flow| {
        *control_flow = ControlFlow::Wait;
        match event {
            Event::UserEvent(fin) => {
                let ligne = match fin {
                    Fin::Code(c) => format!("CODE:{c}"),
                    Fin::Erreur(e) => format!("ERREUR:{e}"),
                };
                let mut sortie = std::io::stdout();
                let _ = writeln!(sortie, "{ligne}");
                let _ = sortie.flush();
                *control_flow = ControlFlow::Exit;
            }
            Event::WindowEvent { event: WindowEvent::CloseRequested, .. } => {
                *control_flow = ControlFlow::Exit;
            }
            _ => {}
        }
    });
}

/// Lit un paramètre de l'URL (après "?" ou "#").
fn parametre(url: &str, nom: &str) -> Option<String> {
    let requete = url.split_once(['?', '#'])?.1;
    requete.split(['&', '#']).find_map(|p| {
        let (k, v) = p.split_once('=')?;
        (k == nom).then(|| urlencoding::decode(&v.replace('+', " ")).map(|s| s.into_owned()).unwrap_or_default())
    })
}
