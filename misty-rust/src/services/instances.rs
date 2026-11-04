//! Profils de jeu : un dossier par profil dans %LOCALAPPDATA%\Misty\instances\.
//! Les versions, bibliothèques et assets restent partagés dans %LOCALAPPDATA%\Misty.
//!
//! instance.json garde les noms de champs de la version C# (PascalCase), pour rester compatible.

use super::{content, loc, paths};
use crate::minecraft::loaders;
use chrono::{DateTime, Local, NaiveDateTime, TimeZone};
use serde::{Deserialize, Deserializer, Serialize};
use std::fs;
use std::path::PathBuf;
use unicode_normalization::UnicodeNormalization;

const FICHIER: &str = "instance.json";

/// Un profil de jeu : une version + un loader + son propre dossier (mods, packs, mondes, options…).
#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(rename_all = "PascalCase")]
pub struct Instance {
    #[serde(default)]
    pub id: String,
    #[serde(default)]
    pub nom: String,
    #[serde(default)]
    pub version_mc: String,
    /// Nom du mode (voir les constantes de minecraft::loaders).
    #[serde(default = "vanilla")]
    pub loader: String,
    /// Version précise du loader (modpacks). None = la plus récente.
    #[serde(default)]
    pub loader_version: Option<String>,
    /// Icône distante (modpack), utilisée si aucune icon.png locale.
    #[serde(default)]
    pub icone_url: Option<String>,
    /// Plateforme du modpack d'origine (modrinth / curseforge).
    #[serde(default)]
    pub modpack_source: Option<String>,
    #[serde(default)]
    pub modpack_project_id: Option<String>,
    #[serde(default)]
    pub modpack_version: Option<String>,
    #[serde(default = "Local::now", deserialize_with = "date")]
    pub cree_le: DateTime<Local>,
    #[serde(default, deserialize_with = "date_option")]
    pub dernier_lancement: Option<DateTime<Local>>,
}

fn vanilla() -> String {
    loaders::VANILLA.to_string()
}

/// Dates écrites par .NET : avec décalage horaire ("...+02:00") ou sans ("2025-10-08T00:47:12.123").
fn parser_date(texte: &str) -> Option<DateTime<Local>> {
    if let Ok(d) = DateTime::parse_from_rfc3339(texte) {
        return Some(d.with_timezone(&Local));
    }
    let naive = NaiveDateTime::parse_from_str(texte, "%Y-%m-%dT%H:%M:%S%.f").ok()?;
    Local.from_local_datetime(&naive).single()
}

fn date<'de, D: Deserializer<'de>>(d: D) -> Result<DateTime<Local>, D::Error> {
    let texte = String::deserialize(d)?;
    Ok(parser_date(&texte).unwrap_or_else(Local::now))
}

fn date_option<'de, D: Deserializer<'de>>(d: D) -> Result<Option<DateTime<Local>>, D::Error> {
    let texte = Option::<String>::deserialize(d)?;
    Ok(texte.and_then(|t| parser_date(&t)))
}

impl Instance {
    pub fn dossier(&self) -> PathBuf {
        dossier(&self.id)
    }

    pub fn chemin_icone(&self) -> PathBuf {
        self.dossier().join("icon.png")
    }

    /// "1.20.1 · Fabric"
    pub fn resume(&self) -> String {
        format!("{} · {}", self.version_mc, self.loader)
    }

    pub fn est_modpack(&self) -> bool {
        self.modpack_project_id.is_some()
    }

    pub fn supporte_mods(&self) -> bool {
        content::supporte_mods(&self.loader)
    }

    /// "Jamais joué", "Joué aujourd'hui", "Il y a 3 jours"…
    pub fn dernier_lancement_texte(&self) -> String {
        let Some(d) = self.dernier_lancement else { return loc::t("Profil_Jamais") };
        let aujourdhui = Local::now().date_naive();
        let jours = (aujourdhui - d.date_naive()).num_days();
        match jours {
            0 => loc::t("Profil_Aujourdhui"),
            1 => loc::t("Profil_Hier"),
            j if j < 30 => loc::tf("Profil_IlYa", &[&j]),
            _ => {
                let format = if loc::code() == "fr" { "%d/%m/%Y" } else { "%m/%d/%Y" };
                loc::tf("Profil_Le", &[&d.format(format)])
            }
        }
    }
}

pub fn racine() -> PathBuf {
    paths::data_dir().join("instances")
}

pub fn dossier(id: &str) -> PathBuf {
    racine().join(id)
}

/// Tous les profils, du plus récemment joué au plus ancien.
pub fn lister() -> Vec<Instance> {
    let mut instances = Vec::new();
    let Ok(entrees) = fs::read_dir(racine()) else { return instances };
    for entree in entrees.flatten() {
        let chemin = entree.path().join(FICHIER);
        let Ok(texte) = fs::read_to_string(&chemin) else { continue };
        // instance.json illisible : on l'ignore
        let Ok(mut instance) = serde_json::from_str::<Instance>(texte.trim_start_matches('\u{feff}')) else { continue };
        instance.id = entree.file_name().to_string_lossy().to_string(); // le dossier fait foi
        instances.push(instance);
    }
    instances.sort_by_key(|i| std::cmp::Reverse(i.dernier_lancement.unwrap_or(i.cree_le)));
    instances
}

pub fn trouver(id: &str) -> Option<Instance> {
    if id.is_empty() {
        return None;
    }
    lister().into_iter().find(|i| i.id == id)
}

pub fn creer(nom: &str, version_mc: &str, loader: &str, loader_version: Option<String>) -> anyhow::Result<Instance> {
    let instance = Instance {
        id: nouvel_id(nom),
        nom: nom.trim().to_string(),
        version_mc: version_mc.to_string(),
        loader: loader.to_string(),
        loader_version,
        icone_url: None,
        modpack_source: None,
        modpack_project_id: None,
        modpack_version: None,
        cree_le: Local::now(),
        dernier_lancement: None,
    };
    for sous_dossier in ["mods", "resourcepacks", "shaderpacks", "saves"] {
        fs::create_dir_all(instance.dossier().join(sous_dossier))?;
    }
    sauver(&instance)?;
    Ok(instance)
}

pub fn sauver(instance: &Instance) -> anyhow::Result<()> {
    fs::create_dir_all(instance.dossier())?;
    fs::write(instance.dossier().join(FICHIER), serde_json::to_string_pretty(instance)?)?;
    Ok(())
}

pub fn supprimer(instance: &Instance) -> anyhow::Result<()> {
    let dossier = instance.dossier();
    if dossier.exists() {
        fs::remove_dir_all(dossier)?;
    }
    Ok(())
}

/// "Mon Profil !" -> "mon-profil-3fa2" (unique).
fn nouvel_id(nom: &str) -> String {
    let mut slug = String::new();
    for c in nom.trim().to_lowercase().nfd() {
        if c.is_ascii_alphanumeric() {
            slug.push(c);
        } else if matches!(c, ' ' | '-' | '_') && !slug.is_empty() && !slug.ends_with('-') {
            slug.push('-');
        }
        if slug.len() >= 24 {
            break;
        }
    }
    let mut base = slug.trim_matches('-').to_string();
    if base.is_empty() {
        base = "profil".into();
    }
    loop {
        let id = format!("{base}-{:04x}", rand::random::<u16>());
        if !dossier(&id).exists() {
            return id;
        }
    }
}
