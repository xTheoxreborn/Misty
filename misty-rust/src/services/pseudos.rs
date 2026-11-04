//! Liste des pseudos hors-ligne, stockée dans listpseudo.txt ("listpseudo=a,b,c").

use super::paths;
use std::fs;

const CLE: &str = "listpseudo=";

pub fn initialiser() {
    let _ = fs::create_dir_all(paths::data_dir());
    if !paths::profiles_file().exists() {
        let _ = fs::write(paths::profiles_file(), format!("{CLE}PseudoTest\r\n"));
    }
}

pub fn lire() -> Vec<String> {
    let Ok(texte) = fs::read_to_string(paths::profiles_file()) else { return Vec::new() };
    let mut resultat = Vec::new();
    for ligne in texte.lines() {
        if let Some(valeur) = ligne.strip_prefix(CLE) {
            if !valeur.is_empty() {
                resultat.extend(valeur.split(',').map(str::to_string));
            }
        }
    }
    resultat
}

pub fn ajouter(pseudo: &str) {
    if pseudo.trim().is_empty() {
        return;
    }
    let mut pseudos = lire();
    if pseudos.iter().any(|p| p == pseudo) {
        return;
    }
    pseudos.push(pseudo.to_string());
    ecrire(&pseudos);
}

pub fn supprimer(pseudo: &str) {
    let mut pseudos = lire();
    let avant = pseudos.len();
    pseudos.retain(|p| p != pseudo);
    if pseudos.len() != avant {
        ecrire(&pseudos);
    }
}

fn ecrire(pseudos: &[String]) {
    let Ok(texte) = fs::read_to_string(paths::profiles_file()) else { return };
    let nouvelle = format!("{CLE}{}", pseudos.join(","));
    let mut lignes: Vec<String> = texte.lines().map(str::to_string).collect();
    match lignes.iter().position(|l| l.starts_with(CLE)) {
        Some(i) => lignes[i] = nouvelle,
        None => lignes.push(nouvelle),
    }
    let _ = fs::write(paths::profiles_file(), lignes.join("\r\n") + "\r\n");
}
