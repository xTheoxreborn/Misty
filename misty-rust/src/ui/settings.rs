//! Options : comptes hors-ligne, compte Microsoft, RAM, raccourci, types de versions, stockage, langue.

use super::{dialogs, lancer, modele, App};
use crate::services::{account, app_info, discord, loc, paths, pseudos, settings, shortcut, system_info};
use crate::{LangItem, PseudoRow, SettingsState};
use slint::ComponentHandle;
use std::path::Path;
use std::rc::Rc;

#[derive(Default)]
pub struct Etat {
    types_modifies: bool,
    langues: Vec<String>,
}

fn s(app: &App) -> SettingsState<'_> {
    app.ui.global::<SettingsState>()
}

pub fn installer(app: &Rc<App>) {
    let st = s(app);
    st.set_data_dir(paths::data_dir().to_string_lossy().to_string().into());

    let a = app.clone();
    st.on_add_pseudo(move || {
        let pseudo = s(&a).get_new_pseudo().trim().replace(',', "");
        if pseudo.is_empty() {
            return;
        }
        pseudos::ajouter(&pseudo);
        s(&a).set_new_pseudo("".into());
        charger_pseudos(&a);
    });
    let a = app.clone();
    st.on_remove_pseudo(move |pseudo| {
        let a = a.clone();
        lancer(async move {
            let ok = dialogs::confirmer(
                &a,
                &loc::t("Options_SupprimerCompte"),
                &loc::tf("Options_SupprimerCompte_Texte", &[&pseudo]),
                Some(&loc::t("Commun_Supprimer")),
                None,
                true,
            )
            .await;
            if ok {
                pseudos::supprimer(&pseudo);
                charger_pseudos(&a);
            }
        });
    });

    // Microsoft
    let a = app.clone();
    st.on_connect(move || {
        let a = a.clone();
        lancer(async move {
            occupe_microsoft(&a, true);
            if let Err(e) = a.bg(account::connecter()).await {
                dialogs::erreur(&a, &loc::t("Compte_ConnexionImpossible"), &e.to_string()).await;
            }
            occupe_microsoft(&a, false);
            super::home::session_changee(&a);
        });
    });
    let a = app.clone();
    st.on_disconnect(move || {
        account::deconnecter();
        super::home::session_changee(&a);
    });
    let a = app.clone();
    st.on_disconnect_all(move || {
        let a = a.clone();
        lancer(async move {
            match account::deconnexion_totale() {
                Ok(()) => dialogs::info(&a, &loc::t("Compte_Oublie"), &loc::t("Compte_Oublie_Texte")).await,
                Err(e) => dialogs::erreur(&a, &loc::t("Compte_ErreurDeconnexion"), &e.to_string()).await,
            }
            super::home::session_changee(&a);
        });
    });

    // Raccourci
    let a = app.clone();
    st.on_shortcut_toggled(move |actif| {
        let a = a.clone();
        lancer(async move { raccourci_change(&a, actif).await });
    });
    let a = app.clone();
    st.on_choose_shortcut(move || {
        let a = a.clone();
        lancer(async move { choisir_dossier_raccourci(&a).await });
    });
    let a = app.clone();
    st.on_recreate_shortcut(move || {
        let a = a.clone();
        lancer(async move {
            let dossier = settings::get(settings::RACCOURCI);
            if dossier.is_empty() {
                choisir_dossier_raccourci(&a).await;
            } else {
                creer_raccourci(&a, &dossier).await;
            }
        });
    });

    // Langue
    let a = app.clone();
    st.on_lang_selected(move |i| {
        let code = a.settings.borrow().langues.get(i as usize).cloned();
        if let Some(code) = code {
            if code != loc::code() {
                settings::set(settings::LANGUE, &code);
                loc::charger(&code);
                super::langue_changee(&a);
            }
        }
    });

    // RAM
    let a = app.clone();
    st.on_ram_changed(move |v| {
        settings::set(settings::RAM, &(v as u32).to_string());
        afficher_ram(&a);
    });

    // Types de versions
    let a = app.clone();
    st.on_types_changed(move || {
        let st = s(&a);
        settings::set_bool(settings::SNAPSHOT, st.get_snapshot());
        settings::set_bool(settings::BETA, st.get_beta());
        settings::set_bool(settings::ALPHA, st.get_alpha());
        a.settings.borrow_mut().types_modifies = true;
    });

    // Stockage
    st.on_open_folder(|| system_info::ouvrir_dossier(&paths::data_dir()));
    let a = app.clone();
    st.on_delete_all(move || {
        let a = a.clone();
        lancer(async move { tout_supprimer(&a).await });
    });

    // Discord
    st.on_rich_toggled(|actif| {
        settings::set_bool(settings::RICH_PRESENCE, actif);
        discord::reappliquer();
    });

    st.on_github(|| system_info::ouvrir(&format!("https://github.com/{}", app_info::GITHUB_REPO)));
}

/// Recharge l'affichage depuis les fichiers de config (appelé à chaque ouverture de la page).
pub fn rafraichir(app: &Rc<App>) {
    let st = s(app);

    // Langues
    let langues = loc::disponibles();
    let items: Vec<LangItem> = langues
        .iter()
        .map(|l| LangItem { name: l.nom.clone().into(), flag: drapeau(&l.pays) })
        .collect();
    st.set_langs(modele(items));
    st.set_lang_index(langues.iter().position(|l| l.code == loc::code()).map(|i| i as i32).unwrap_or(-1));
    app.settings.borrow_mut().langues = langues.into_iter().map(|l| l.code).collect();

    charger_pseudos(app);

    let ram_max = system_info::ram_max_proposee_mo();
    st.set_ram_max(ram_max as f32);
    st.set_ram_value(settings::ram_mo().clamp(1024, ram_max) as f32);

    st.set_snapshot(settings::get_bool(settings::SNAPSHOT, false));
    st.set_beta(settings::get_bool(settings::BETA, false));
    st.set_alpha(settings::get_bool(settings::ALPHA, false));
    st.set_shortcut_enabled(!settings::get(settings::RACCOURCI).is_empty());
    st.set_rich(discord::active());

    maj_textes(app);
}

/// Drapeau intégré (assets/flags/xx.png).
fn drapeau(pays: &str) -> slint::Image {
    let octets: &[u8] = match pays {
        "fr" => include_bytes!("../../assets/flags/fr.png"),
        "us" => include_bytes!("../../assets/flags/us.png"),
        "es" => include_bytes!("../../assets/flags/es.png"),
        "ru" => include_bytes!("../../assets/flags/ru.png"),
        "it" => include_bytes!("../../assets/flags/it.png"),
        _ => &[],
    };
    super::images::decoder(octets, 0).map(slint::Image::from_rgba8).unwrap_or_default()
}

/// Textes calculés en code (rappelé au changement de langue).
pub fn maj_textes(app: &Rc<App>) {
    let st = s(app);
    st.set_about_text(loc::tf("Options_APropos_Texte", &[&app_info::VERSION]).into());
    st.set_ram_total_text(loc::tf("Options_Ram_Texte", &[&format!("{:.1}", system_info::ram_totale_mo() as f64 / 1024.0).replace(".0", "")]).into());
    let raccourci = settings::get(settings::RACCOURCI);
    st.set_shortcut_path(if raccourci.is_empty() { loc::t("Options_AucunDossier") } else { raccourci }.into());
    maj_microsoft(app);
    afficher_ram(app);
    calculer_taille(app);
}

/// Indique (une seule fois) si les types de versions ont changé depuis le dernier appel.
pub fn consommer_changement_types(app: &App) -> bool {
    std::mem::take(&mut app.settings.borrow_mut().types_modifies)
}

fn charger_pseudos(app: &Rc<App>) {
    // Trois pseudos par ligne
    let lignes: Vec<PseudoRow> = pseudos::lire()
        .chunks(3)
        .map(|c| PseudoRow { items: super::modele_textes(c) })
        .collect();
    s(app).set_pseudo_rows(modele(lignes));
}

// ───────────────────────── Microsoft ─────────────────────────

pub fn maj_microsoft(app: &App) {
    let session = account::session();
    let st = s(app);
    st.set_ms_state(
        match &session {
            Some(s) => loc::tf("Compte_Connecte", &[&s.pseudo]),
            None => loc::t("Compte_NonConnecte"),
        }
        .into(),
    );
    st.set_ms_connected(session.is_some());
}

pub fn occupe_microsoft(app: &App, occupe: bool) {
    s(app).set_ms_busy(occupe);
}

// ───────────────────────── RAM ─────────────────────────

fn afficher_ram(app: &App) {
    let st = s(app);
    st.set_ram_text(format!("{:.0} {}", st.get_ram_value() / 1024.0, loc::t("Unite_Go")).into());
}

// ───────────────────────── Raccourci ─────────────────────────

async fn raccourci_change(app: &Rc<App>, actif: bool) {
    if actif {
        // Pas encore de dossier : on le demande directement
        if settings::get(settings::RACCOURCI).is_empty() {
            choisir_dossier_raccourci(app).await;
        }
        // Choix annulé : on remet l'interrupteur sur off
        if settings::get(settings::RACCOURCI).is_empty() {
            s(app).set_shortcut_enabled(false);
        }
    } else {
        shortcut::supprimer(Path::new(&settings::get(settings::RACCOURCI)));
        settings::set(settings::RACCOURCI, "");
        s(app).set_shortcut_path(loc::t("Options_AucunDossier").into());
    }
}

async fn choisir_dossier_raccourci(app: &Rc<App>) {
    let Some(dossier) = dialogs::choisir_dossier(&loc::t("Options_DossierRaccourci")).await else { return };
    shortcut::supprimer(Path::new(&settings::get(settings::RACCOURCI))); // ancien emplacement
    let texte = dossier.to_string_lossy().to_string();
    settings::set(settings::RACCOURCI, &texte);
    s(app).set_shortcut_path(texte.clone().into());
    creer_raccourci(app, &texte).await;
}

async fn creer_raccourci(app: &App, dossier: &str) {
    if let Err(e) = shortcut::creer(Path::new(dossier)) {
        dialogs::erreur(app, &loc::t("Options_Raccourci"), &loc::tf("Options_RaccourciErreur", &[&e])).await;
    }
}

// ───────────────────────── Stockage ─────────────────────────

fn calculer_taille(app: &Rc<App>) {
    s(app).set_size_text(loc::t("Options_Calcul").into());
    let a = app.clone();
    lancer(async move {
        let octets = a.bloquant(|| system_info::taille_dossier(&paths::data_dir())).await;
        let mo = octets as f64 / (1024.0 * 1024.0);
        let texte = if mo >= 1024.0 { format!("{:.2} {}", mo / 1024.0, loc::t("Unite_Go")) } else { format!("{mo:.0} {}", loc::t("Unite_Mo")) };
        s(&a).set_size_text(texte.into());
    });
}

async fn tout_supprimer(app: &Rc<App>) {
    let ok = dialogs::confirmer(
        app,
        &loc::t("Options_ToutSupprimer"),
        &loc::t("Options_ToutSupprimer_Texte"),
        Some(&loc::t("Options_ToutSupprimer")),
        None,
        true,
    )
    .await;
    if !ok {
        return;
    }
    let resultat = app.bloquant(|| {
        let d = paths::data_dir();
        if d.exists() {
            std::fs::remove_dir_all(d)
        } else {
            Ok(())
        }
    });
    if let Err(e) = resultat.await {
        dialogs::erreur(app, &loc::t("Options_SuppressionIncomplete"), &e.to_string()).await;
    }
    calculer_taille(app);

    if dialogs::confirmer(
        app,
        &loc::t("Options_FermerMisty"),
        &loc::t("Options_FermerMisty_Texte"),
        Some(&loc::t("Commun_Fermer")),
        Some(&loc::t("Options_Rester")),
        false,
    )
    .await
    {
        let _ = slint::quit_event_loop();
    }
}
