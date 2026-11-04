//! OptiFine : la liste vient de la page de téléchargement d'optifine.net.
//! L'installation fait comme l'installateur d'OptiFine :
//!   1. le jar d'OptiFine est « patché » avec le jar du jeu (classe optifine.Patcher) → bibliothèque OptiFine ;
//!   2. on extrait launchwrapper-of (le chargeur fourni avec OptiFine) ;
//!   3. on écrit une version qui lance le jeu via launchwrapper + OptiFineTweaker.

use crate::minecraft::{java, sans_console, version, FileProgress, McDirs};
use crate::services::{download, http};
use serde_json::{json, Value};
use std::io::Read;
use tokio::sync::Mutex;

/// (version de Minecraft, nom du fichier "OptiFine_1.20.1_HD_U_I6.jar"), la plus récente d'abord.
async fn liste() -> anyhow::Result<Vec<(String, String)>> {
    static CACHE: Mutex<Option<Vec<(String, String)>>> = Mutex::const_new(None);
    let mut cache = CACHE.lock().await;
    if let Some(v) = cache.as_ref() {
        return Ok(v.clone());
    }
    let html = http::get_text("https://optifine.net/downloads").await?;
    // Les versions > 1.21.11 (26.x) n'existent que en préversion ("preview_OptiFine_26.2_HD_U_K2_pre5.jar") : on les garde.
    let re = regex::Regex::new(r"adloadx\?f=((?:preview_)?OptiFine_([0-9][0-9.]*)_HD_[^\x22'&<>\s]+\.jar)").unwrap();
    let mut liste: Vec<(String, String)> = re.captures_iter(&html).map(|c| (c[2].to_string(), c[1].to_string())).collect();
    // Les versions stables passent avant les préversions (tri stable : l'ordre « plus récente d'abord » est conservé)
    liste.sort_by_key(|(_, f)| f.starts_with("preview_"));
    *cache = Some(liste.clone());
    Ok(liste)
}

pub async fn disponible(version: &str) -> bool {
    liste().await.is_ok_and(|l| l.iter().any(|(v, _)| v == version))
}

pub async fn installer(dirs: &McDirs, mc: &str, progression: &FileProgress) -> anyhow::Result<String> {
    let fichier = liste()
        .await?
        .into_iter()
        .find(|(v, _)| v == mc)
        .map(|(_, f)| f)
        .ok_or_else(|| anyhow::anyhow!("OptiFine indisponible pour {mc}"))?;

    // "OptiFine_1.20.1_HD_U_I6.jar" → édition "HD_U_I6"
    let edition = fichier.trim_end_matches(".jar").trim_start_matches("preview_").trim_start_matches(&format!("OptiFine_{mc}_")).to_string();
    let id = format!("{mc}-OptiFine_{edition}");
    let lib_version = format!("{mc}_{edition}");
    let jar_lib = dirs.libraries().join(format!("optifine/OptiFine/{lib_version}/OptiFine-{lib_version}.jar"));

    if dirs.version_json(&id).exists() && jar_lib.exists() {
        return Ok(id);
    }
    progression(0, 1, &fichier);

    // Le vrai lien de téléchargement est sur la page intermédiaire "adloadx"
    let page = http::get_text(&format!("https://optifine.net/adloadx?f={fichier}")).await?;
    let lien = regex::Regex::new(r"downloadx\?f=[^\x22'<>\s]+")
        .unwrap()
        .find(&page)
        .map(|m| format!("https://optifine.net/{}", m.as_str()))
        .ok_or_else(|| anyhow::anyhow!("Lien de téléchargement OptiFine introuvable"))?;

    let travail = std::env::temp_dir().join(format!("misty-optifine-{}", uuid::Uuid::new_v4().simple()));
    std::fs::create_dir_all(&travail)?;
    let installateur = travail.join(&fichier);
    let resultat = async {
        download::fichier(&lien, &installateur, None).await?;

        // 1. Patch du jar du jeu
        let vanilla = version::lire(dirs, mc)?;
        let (composant, majeure) = version::java_requis(&vanilla);
        let java = java::java_console(&java::assurer(dirs, &composant, majeure, progression).await?);
        std::fs::create_dir_all(jar_lib.parent().unwrap())?;
        let mut commande = std::process::Command::new(&java);
        commande.arg("-cp").arg(&installateur).arg("optifine.Patcher").arg(dirs.version_jar(mc)).arg(&installateur).arg(&jar_lib);
        sans_console(&mut commande);
        let sortie = commande.output()?;
        if !sortie.status.success() || !jar_lib.exists() {
            anyhow::bail!("Échec du patch OptiFine : {}", String::from_utf8_lossy(&sortie.stderr).lines().last().unwrap_or_default());
        }

        // 2. launchwrapper-of, fourni dans le jar d'OptiFine (sinon le launchwrapper de Mojang)
        let mut zip = zip::ZipArchive::new(std::fs::File::open(&installateur)?)?;
        let mut version_lw = String::new();
        if let Ok(mut e) = zip.by_name("launchwrapper-of.txt") {
            e.read_to_string(&mut version_lw)?;
        }
        let version_lw = version_lw.trim().to_string();
        let lib_lw = if version_lw.is_empty() {
            json!({ "name": "net.minecraft:launchwrapper:1.12" })
        } else {
            let destination = dirs.libraries().join(format!("optifine/launchwrapper-of/{version_lw}/launchwrapper-of-{version_lw}.jar"));
            if let Ok(mut e) = zip.by_name(&format!("launchwrapper-of-{version_lw}.jar")) {
                std::fs::create_dir_all(destination.parent().unwrap())?;
                std::io::copy(&mut e, &mut std::fs::File::create(&destination)?)?;
            }
            json!({ "name": format!("optifine:launchwrapper-of:{version_lw}") })
        };

        // 3. Version
        let mut v = json!({
            "id": id,
            "inheritsFrom": mc,
            "type": "release",
            "time": chrono::Utc::now().to_rfc3339(),
            "releaseTime": chrono::Utc::now().to_rfc3339(),
            "libraries": [ { "name": format!("optifine:OptiFine:{lib_version}") }, lib_lw ],
            "mainClass": "net.minecraft.launchwrapper.Launch",
        });
        if vanilla.get("arguments").is_some() {
            v["arguments"] = json!({ "game": ["--tweakClass", "optifine.OptiFineTweaker"] });
        } else {
            let base = vanilla["minecraftArguments"].as_str().unwrap_or_default();
            v["minecraftArguments"] = Value::String(format!("{base} --tweakClass optifine.OptiFineTweaker"));
        }
        let chemin = dirs.version_json(&id);
        std::fs::create_dir_all(chemin.parent().unwrap())?;
        std::fs::write(&chemin, serde_json::to_string_pretty(&v)?)?;
        Ok(())
    }
    .await;
    let _ = std::fs::remove_dir_all(&travail);
    resultat?;
    Ok(id)
}
