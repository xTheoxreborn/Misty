//! Boîtes de dialogue intégrées à la fenêtre (remplacent les MessageBox Windows).
//! Chaque fonction affiche la boîte et attend la réponse de l'utilisateur.

use super::{modele_textes, App};
use crate::services::loc;
use crate::DialogState;
use futures::channel::oneshot;
use slint::ComponentHandle;
use std::rc::Rc;

const ICONE_INFO: &str = "\u{E946}";
const ICONE_QUESTION: &str = "\u{E9CE}";
const ICONE_DANGER: &str = "\u{E7BA}";

/// Réponse : Some(-1) = OK, Some(i) = choix i, None = Annuler / Échap.
fn repondre(app: &App, reponse: Option<i32>) {
    app.ui.global::<DialogState>().set_visible(false);
    if let Some(tx) = app.dialog.borrow_mut().take() {
        let _ = tx.send(reponse);
    }
}

pub fn installer(app: &Rc<App>) {
    let d = app.ui.global::<DialogState>();
    let a = app.clone();
    d.on_ok(move || repondre(&a, Some(-1)));
    let a = app.clone();
    d.on_cancel(move || repondre(&a, None));
    let a = app.clone();
    d.on_choose(move |i| repondre(&a, Some(i)));
}

async fn ouvrir(app: &App, titre: &str, message: &str, ok: &str, annuler: Option<&str>, danger: bool, choix: &[String]) -> Option<i32> {
    // Une boîte déjà ouverte est annulée
    if let Some(tx) = app.dialog.borrow_mut().take() {
        let _ = tx.send(None);
    }
    let (tx, rx) = oneshot::channel();
    *app.dialog.borrow_mut() = Some(tx);

    let d = app.ui.global::<DialogState>();
    d.set_title(titre.into());
    d.set_message(message.into());
    d.set_ok_text(ok.into());
    d.set_cancel_text(annuler.unwrap_or("").into());
    d.set_danger(danger);
    d.set_icon(if danger { ICONE_DANGER } else if annuler.is_some() { ICONE_QUESTION } else { ICONE_INFO }.into());
    d.set_choices(modele_textes(choix));
    d.set_visible(true);

    rx.await.unwrap_or(None)
}

pub async fn info(app: &App, titre: &str, message: &str) {
    ouvrir(app, titre, message, "OK", None, false, &[]).await;
}

pub async fn erreur(app: &App, titre: &str, message: &str) {
    ouvrir(app, titre, message, "OK", None, true, &[]).await;
}

/// `oui` : None = "Oui" traduit ; `non` : None = "Annuler" traduit.
pub async fn confirmer(app: &App, titre: &str, message: &str, oui: Option<&str>, non: Option<&str>, danger: bool) -> bool {
    let oui = oui.map(str::to_string).unwrap_or_else(|| loc::t("Commun_Oui"));
    let non = non.map(str::to_string).unwrap_or_else(|| loc::t("Commun_Annuler"));
    ouvrir(app, titre, message, &oui, Some(&non), danger, &[]).await.is_some()
}

/// Choix dans une liste. Renvoie l'index choisi, ou None si annulé.
pub async fn choisir(app: &App, titre: &str, message: &str, options: &[String]) -> Option<usize> {
    let r = ouvrir(app, titre, message, "", Some(&loc::t("Commun_Annuler")), false, options).await;
    r.filter(|&i| i >= 0).map(|i| i as usize)
}

/// Demande un fichier à l'utilisateur (boîte de dialogue Windows).
pub async fn choisir_fichier(titre: &str, nom_filtre: &str, extensions: &[&str]) -> Option<std::path::PathBuf> {
    rfd::AsyncFileDialog::new()
        .set_title(titre)
        .add_filter(nom_filtre, extensions)
        .pick_file()
        .await
        .map(|f| f.path().to_path_buf())
}

pub async fn choisir_dossier(titre: &str) -> Option<std::path::PathBuf> {
    rfd::AsyncFileDialog::new().set_title(titre).pick_folder().await.map(|f| f.path().to_path_buf())
}

pub async fn enregistrer_fichier(titre: &str, nom: &str, nom_filtre: &str, extension: &str) -> Option<std::path::PathBuf> {
    rfd::AsyncFileDialog::new()
        .set_title(titre)
        .set_file_name(nom)
        .add_filter(nom_filtre, &[extension])
        .save_file()
        .await
        .map(|f| f.path().to_path_buf())
}
