//! Notes de version, lues depuis CHANGELOG.md (intégré à l'exe).

use super::{modele, modele_textes, App};
use crate::{ChangelogSection, ChangelogState};
use slint::ComponentHandle;
use std::rc::Rc;

const CHANGELOG: &str = include_str!("../../../CHANGELOG.md");

pub fn installer(app: &Rc<App>) {
    let sections: Vec<ChangelogSection> = lire()
        .into_iter()
        .enumerate()
        .map(|(i, (titre, lignes))| ChangelogSection { title: titre.into(), lines: modele_textes(&lignes), recent: i == 0 })
        .collect();
    app.ui.global::<ChangelogState>().set_sections(modele(sections));
}

/// Découpe le markdown : "## titre" = une section, "- texte" = une ligne.
fn lire() -> Vec<(String, Vec<String>)> {
    let mut sections: Vec<(String, Vec<String>)> = Vec::new();
    for ligne in CHANGELOG.lines() {
        if let Some(titre) = ligne.strip_prefix("## ") {
            sections.push((titre.trim().to_uppercase(), Vec::new()));
        } else if let (Some(texte), Some(courante)) = (ligne.strip_prefix("- "), sections.last_mut()) {
            courante.1.push(texte.replace('`', ""));
        }
    }
    sections
}
