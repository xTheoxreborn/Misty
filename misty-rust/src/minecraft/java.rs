//! Java fourni par Mojang (le même que le launcher officiel), téléchargé à la demande.
//! Chaque version de Minecraft indique le composant voulu ("jre-legacy" = Java 8, "java-runtime-delta" = Java 21…).

use super::install::{telecharger_tout, Tache};
use super::{FileProgress, McDirs};
use crate::services::http;
use serde_json::Value;
use std::path::PathBuf;

const RUNTIMES_URL: &str =
    "https://piston-meta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";

fn plateforme() -> &'static str {
    if cfg!(target_arch = "aarch64") {
        "windows-arm64"
    } else if cfg!(target_arch = "x86") {
        "windows-x86"
    } else {
        "windows-x64"
    }
}

/// Chemin de javaw.exe pour ce composant (téléchargé si besoin).
pub async fn assurer(dirs: &McDirs, composant: &str, majeure: u32, progression: &FileProgress) -> anyhow::Result<PathBuf> {
    // Même organisation que la version C# : runtime\windows-x64\<composant>
    let dossier = dirs.runtime().join(plateforme()).join(composant);
    let javaw = dossier.join("bin").join("javaw.exe");
    let marqueur = dossier.join(".misty-version");

    let tous: Value = match http::get_json(RUNTIMES_URL).await {
        Ok(v) => v,
        // Hors-ligne : on garde le Java déjà installé
        Err(e) if javaw.exists() => {
            let _ = e;
            return Ok(javaw);
        }
        Err(e) => return Err(e),
    };

    // Composant indisponible pour cette plateforme : on prend un Java de version proche
    let mut entree = tous[plateforme()][composant].as_array().and_then(|a| a.first()).cloned();
    if entree.is_none() {
        let secours = match majeure {
            0..=8 => "jre-legacy",
            9..=16 => "java-runtime-alpha",
            17 => "java-runtime-gamma",
            _ => "java-runtime-delta",
        };
        entree = tous[plateforme()][secours].as_array().and_then(|a| a.first()).cloned();
    }
    let Some(entree) = entree else { anyhow::bail!("Java {majeure} indisponible pour {}", plateforme()) };
    let url_manifeste = entree["manifest"]["url"].as_str().unwrap_or_default().to_string();
    let sha1_manifeste = entree["manifest"]["sha1"].as_str().unwrap_or_default().to_string();

    // Déjà installé et à jour
    if javaw.exists() && std::fs::read_to_string(&marqueur).ok().as_deref() == Some(sha1_manifeste.as_str()) {
        return Ok(javaw);
    }

    let manifeste: Value = http::get_json(&url_manifeste).await?;
    let mut taches = Vec::new();
    for (chemin, fichier) in manifeste["files"].as_object().into_iter().flatten() {
        let destination = dossier.join(chemin.replace('/', "\\"));
        match fichier["type"].as_str() {
            Some("directory") => {
                std::fs::create_dir_all(&destination)?;
            }
            Some("file") => {
                let raw = &fichier["downloads"]["raw"];
                if let Some(url) = raw["url"].as_str() {
                    taches.push(Tache {
                        chemin: destination,
                        url: url.to_string(),
                        sha1: raw["sha1"].as_str().map(str::to_string),
                        taille: raw["size"].as_u64(),
                    });
                }
            }
            _ => {} // liens symboliques : inutiles sous Windows
        }
    }
    telecharger_tout(taches, progression).await?;
    std::fs::write(&marqueur, &sha1_manifeste)?;

    if !javaw.exists() {
        anyhow::bail!("Installation de Java incomplète ({composant})");
    }
    Ok(javaw)
}

/// java.exe à côté de javaw.exe (pour les outils en ligne de commande comme les installateurs).
pub fn java_console(javaw: &std::path::Path) -> PathBuf {
    let java = javaw.with_file_name("java.exe");
    if java.exists() {
        java
    } else {
        javaw.to_path_buf()
    }
}
