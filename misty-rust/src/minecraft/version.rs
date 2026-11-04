//! Fichiers de version (versions/<id>/<id>.json) : lecture, fusion avec le parent, règles.

use super::{maven_path, McDirs};
use serde_json::Value;
use std::path::PathBuf;

/// Lit le JSON d'une version installée.
pub fn lire(dirs: &McDirs, id: &str) -> anyhow::Result<Value> {
    let texte = std::fs::read_to_string(dirs.version_json(id))
        .map_err(|e| anyhow::anyhow!("Version {id} introuvable ({e})"))?;
    Ok(serde_json::from_str(texte.trim_start_matches('\u{feff}'))?)
}

/// Fusionne une version avec ses parents ("inheritsFrom"), comme le launcher officiel :
/// les champs de l'enfant remplacent ceux du parent, les listes (bibliothèques, arguments) s'ajoutent.
pub fn fusionner(dirs: &McDirs, id: &str) -> anyhow::Result<Value> {
    let enfant = lire(dirs, id)?;
    let Some(parent_id) = enfant.get("inheritsFrom").and_then(Value::as_str).map(str::to_string) else {
        return Ok(enfant);
    };
    let mut parent = fusionner(dirs, &parent_id)?;
    let (Some(p), Value::Object(e)) = (parent.as_object_mut(), enfant) else { return Ok(parent) };

    for (cle, valeur) in e {
        match cle.as_str() {
            "libraries" => {
                // Bibliothèques de l'enfant d'abord : elles sont prioritaires
                let mut libs = valeur.as_array().cloned().unwrap_or_default();
                libs.extend(p.get("libraries").and_then(Value::as_array).cloned().unwrap_or_default());
                p.insert(cle, Value::Array(libs));
            }
            "arguments" => {
                let mut args = p.get("arguments").and_then(Value::as_object).cloned().unwrap_or_default();
                for (sorte, liste) in valeur.as_object().cloned().unwrap_or_default() {
                    let mut tout = args.get(&sorte).and_then(Value::as_array).cloned().unwrap_or_default();
                    tout.extend(liste.as_array().cloned().unwrap_or_default());
                    args.insert(sorte, Value::Array(tout));
                }
                p.insert(cle, Value::Object(args));
            }
            "inheritsFrom" => {}
            _ => {
                p.insert(cle, valeur);
            }
        }
    }
    // Garde la trace de la version vanilla (pour le jar du jeu)
    if !p.contains_key("_vanilla") {
        p.insert("_vanilla".into(), Value::String(parent_id));
    }
    Ok(parent)
}

/// Les règles d'une bibliothèque ou d'un argument s'appliquent-elles ici (Windows 64 bits) ?
pub fn regles_ok(regles: Option<&Value>) -> bool {
    let Some(liste) = regles.and_then(Value::as_array) else { return true };
    let mut autorise = false;
    for regle in liste {
        let action = regle.get("action").and_then(Value::as_str) == Some("allow");
        if regle_correspond(regle) {
            autorise = action;
        }
    }
    autorise
}

fn regle_correspond(regle: &Value) -> bool {
    if let Some(os) = regle.get("os") {
        if let Some(nom) = os.get("name").and_then(Value::as_str) {
            if nom != "windows" {
                return false;
            }
        }
        if let Some(arch) = os.get("arch").and_then(Value::as_str) {
            let actuelle = if cfg!(target_arch = "x86") { "x86" } else if cfg!(target_arch = "aarch64") { "arm64" } else { "x86_64" };
            if arch != actuelle {
                return false;
            }
        }
    }
    // Fonctionnalités (démo, résolution personnalisée, quick play…) : aucune n'est activée
    if let Some(features) = regle.get("features").and_then(Value::as_object) {
        if features.values().any(|v| v.as_bool() == Some(true)) {
            return false;
        }
    }
    true
}

/// Un fichier de bibliothèque à télécharger / mettre dans le classpath.
#[derive(Clone, Debug)]
pub struct FichierLib {
    pub chemin: PathBuf,
    pub url: Option<String>,
    pub sha1: Option<String>,
    pub taille: Option<u64>,
    /// Contient des DLL à extraire dans le dossier natives.
    pub natif: bool,
    /// Uniquement des natives (pas dans le classpath) : ancien format "natives" + "classifiers".
    pub natif_seul: bool,
    pub exclure: Vec<String>,
    /// group:artifact[:classifier], pour éviter les doublons dans le classpath.
    pub cle: String,
}

fn cle_lib(nom: &str) -> String {
    let p: Vec<&str> = nom.split('@').next().unwrap_or(nom).split(':').collect();
    match p.len() {
        0..=2 => nom.to_string(),
        3 => format!("{}:{}", p[0], p[1]),
        _ => format!("{}:{}:{}", p[0], p[1], p[3]),
    }
}

fn fichier_depuis(dirs: &McDirs, nom: &str, artefact: Option<&Value>, base: Option<&str>) -> Option<FichierLib> {
    let (chemin, url, sha1, taille) = match artefact {
        Some(a) => {
            let chemin = a.get("path").and_then(Value::as_str).map(str::to_string).or_else(|| maven_path(nom))?;
            let url = a.get("url").and_then(Value::as_str).filter(|u| !u.is_empty()).map(str::to_string);
            (chemin, url, a.get("sha1").and_then(Value::as_str).map(str::to_string), a.get("size").and_then(Value::as_u64))
        }
        None => {
            let chemin = maven_path(nom)?;
            let base = base.unwrap_or("https://libraries.minecraft.net/");
            let base = if base.ends_with('/') { base.to_string() } else { format!("{base}/") };
            let url = format!("{base}{chemin}");
            (chemin, Some(url), None, None)
        }
    };
    Some(FichierLib {
        chemin: dirs.libraries().join(chemin.replace('/', "\\")),
        url: url.map(|u| u.replace("http://files.minecraftforge.net/maven/", "https://maven.minecraftforge.net/")),
        sha1,
        taille,
        natif: false,
        natif_seul: false,
        exclure: Vec::new(),
        cle: cle_lib(nom),
    })
}

/// Toutes les bibliothèques applicables d'une version fusionnée.
pub fn bibliotheques(dirs: &McDirs, version: &Value) -> Vec<FichierLib> {
    let mut resultat = Vec::new();
    let arch = if cfg!(target_pointer_width = "64") { "64" } else { "32" };

    for lib in version.get("libraries").and_then(Value::as_array).into_iter().flatten() {
        if !regles_ok(lib.get("rules")) {
            continue;
        }
        let Some(nom) = lib.get("name").and_then(Value::as_str) else { continue };
        let downloads = lib.get("downloads");
        let base = lib.get("url").and_then(Value::as_str);
        let exclure: Vec<String> = lib["extract"]["exclude"]
            .as_array()
            .map(|a| a.iter().filter_map(|v| v.as_str().map(str::to_string)).collect())
            .unwrap_or_default();

        // Ancien format : natives par système dans "classifiers"
        if let Some(classifieur) = lib.get("natives").and_then(|n| n.get("windows")).and_then(Value::as_str) {
            let classifieur = classifieur.replace("${arch}", arch);
            let artefact = downloads.and_then(|d| d.get("classifiers")).and_then(|c| c.get(&classifieur));
            let nom_natif = format!("{nom}:{classifieur}");
            if let Some(mut f) = fichier_depuis(dirs, &nom_natif, artefact, base) {
                f.natif = true;
                f.natif_seul = true;
                f.exclure = exclure.clone();
                resultat.push(f);
            }
            // Parfois accompagné d'un jar normal
            if let Some(a) = downloads.and_then(|d| d.get("artifact")) {
                if let Some(f) = fichier_depuis(dirs, nom, Some(a), base) {
                    resultat.push(f);
                }
            }
            continue;
        }

        let artefact = downloads.and_then(|d| d.get("artifact"));
        if downloads.is_some() && artefact.is_none() {
            continue; // seulement des classifiers sans natives pour Windows
        }
        if let Some(mut f) = fichier_depuis(dirs, nom, artefact, base) {
            // Nouveau format (1.19+) : "...:natives-windows" est un jar normal qui contient des DLL
            if nom.split(':').nth(3).is_some_and(|c| c.starts_with("natives-windows")) {
                f.natif = true;
                f.exclure = exclure;
            }
            resultat.push(f);
        }
    }
    resultat
}

/// ID de la version vanilla d'une version fusionnée (pour les modloaders : leur parent).
pub fn id_vanilla(version: &Value) -> String {
    version
        .get("_vanilla")
        .and_then(Value::as_str)
        .or_else(|| version.get("id").and_then(Value::as_str))
        .unwrap_or_default()
        .to_string()
}

/// Composant Java demandé ("java-runtime-delta"…) et version majeure.
pub fn java_requis(version: &Value) -> (String, u32) {
    let jv = version.get("javaVersion");
    let composant = jv.and_then(|j| j.get("component")).and_then(Value::as_str).unwrap_or("jre-legacy").to_string();
    let majeure = jv.and_then(|j| j.get("majorVersion")).and_then(Value::as_u64).unwrap_or(8) as u32;
    (composant, majeure)
}

