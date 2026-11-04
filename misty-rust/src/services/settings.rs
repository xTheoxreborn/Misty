//! Lecture / écriture de data.txt (format "cle=valeur", une clé par ligne).
//! Même format que la version C#, pour garder les réglages existants.

use super::{app_info, paths};
use std::collections::HashMap;
use std::fs;

pub const VERSION_DATA: &str = "versiondatatxt";
pub const LAST_PSEUDO: &str = "lastpseudo";
pub const LAST_VERSION: &str = "lastversion";
pub const RAM: &str = "ram";
pub const PREMIUM: &str = "premium";
pub const RACCOURCI: &str = "raccourci";
pub const SNAPSHOT: &str = "snapshot";
pub const BETA: &str = "beta";
pub const ALPHA: &str = "alpha";
pub const SUPPR: &str = "Suppr";
pub const DERNIER_PROFIL: &str = "lastprofil";
pub const RICH_PRESENCE: &str = "richpresence";
pub const LANGUE: &str = "langue";

/// Ordre des clés dans le fichier.
const CLES: &[&str] = &[
    VERSION_DATA, LAST_PSEUDO, LAST_VERSION, RAM, PREMIUM, RACCOURCI, SNAPSHOT, BETA, ALPHA, SUPPR,
    DERNIER_PROFIL, RICH_PRESENCE, LANGUE,
];

fn lire_lignes() -> Option<Vec<String>> {
    let texte = fs::read_to_string(paths::data_file()).ok()?;
    Some(texte.lines().map(str::to_string).collect())
}

fn ecrire_lignes(lignes: &[String]) {
    let mut texte = lignes.join("\r\n");
    texte.push_str("\r\n");
    let _ = fs::write(paths::data_file(), texte);
}

/// Valeur d'une clé ("" si absente).
pub fn get(cle: &str) -> String {
    let prefixe = format!("{cle}=");
    let mut valeur = String::new();
    for ligne in lire_lignes().unwrap_or_default() {
        if let Some(v) = ligne.strip_prefix(&prefixe) {
            valeur = v.to_string();
        }
    }
    valeur
}

pub fn get_bool(cle: &str, defaut: bool) -> bool {
    let valeur = get(cle);
    if valeur.trim().is_empty() {
        defaut
    } else {
        valeur.eq_ignore_ascii_case("true")
    }
}

/// Modifie une clé. Ne fait rien si data.txt n'existe pas.
pub fn set(cle: &str, valeur: &str) {
    let Some(mut lignes) = lire_lignes() else { return };
    let prefixe = format!("{cle}=");
    let mut trouvee = false;
    for ligne in lignes.iter_mut() {
        if ligne.starts_with(&prefixe) {
            *ligne = format!("{cle}={valeur}");
            trouvee = true;
        }
    }
    if !trouvee {
        lignes.push(format!("{cle}={valeur}"));
    }
    ecrire_lignes(&lignes);
}

pub fn set_bool(cle: &str, valeur: bool) {
    set(cle, if valeur { "true" } else { "false" });
}

/// Crée data.txt s'il n'existe pas, ou le réécrit au format actuel (en gardant les valeurs)
/// si sa version est différente de DATA_VERSION.
pub fn initialiser() {
    let _ = fs::create_dir_all(paths::data_dir());

    if !paths::data_file().exists() {
        let mut valeurs = HashMap::new();
        valeurs.insert(LAST_PSEUDO.to_string(), "PseudoTest".to_string());
        ecrire(valeurs);
        return;
    }

    if get(VERSION_DATA) != app_info::DATA_VERSION {
        let valeurs = CLES.iter().map(|c| (c.to_string(), get(c))).collect();
        ecrire(valeurs);
    }
}

fn ecrire(mut valeurs: HashMap<String, String>) {
    valeurs.insert(VERSION_DATA.to_string(), app_info::DATA_VERSION.to_string());
    let lignes: Vec<String> = CLES
        .iter()
        .map(|c| format!("{c}={}", valeurs.get(*c).cloned().unwrap_or_default()))
        .collect();
    ecrire_lignes(&lignes);
}

/// RAM choisie pour le jeu, en Mo.
pub fn ram_mo() -> u32 {
    get(RAM).trim().parse().unwrap_or(app_info::RAM_PAR_DEFAUT_MO)
}
