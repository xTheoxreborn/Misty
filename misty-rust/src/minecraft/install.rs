//! Installation d'une version : JSON, jar du jeu, bibliothèques, natives, assets, Java.

use super::version::{self, FichierLib};
use super::{java, FileProgress, McDirs};
use crate::services::{download, http, settings};
use futures::StreamExt;
use serde::Deserialize;
use serde_json::Value;
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicUsize, Ordering};
use std::sync::Arc;
use tokio::sync::Mutex;

const MANIFEST_URL: &str = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
const RESSOURCES_URL: &str = "https://resources.download.minecraft.net/";

#[derive(Deserialize, Clone)]
pub struct VersionManifest {
    pub id: String,
    #[serde(rename = "type")]
    pub type_: String,
    pub url: String,
    #[serde(default)]
    pub sha1: String,
}

#[derive(Deserialize)]
struct Manifest {
    versions: Vec<VersionManifest>,
}

/// Manifeste des versions de Mojang (gardé en mémoire après le premier chargement).
pub async fn manifeste() -> anyhow::Result<Vec<VersionManifest>> {
    static CACHE: Mutex<Option<Vec<VersionManifest>>> = Mutex::const_new(None);
    let mut cache = CACHE.lock().await;
    if let Some(v) = cache.as_ref() {
        return Ok(v.clone());
    }
    let m: Manifest = http::get_json(MANIFEST_URL).await?;
    *cache = Some(m.versions.clone());
    Ok(m.versions)
}

/// Versions à proposer : releases + snapshots/bêtas/alphas selon les options.
pub async fn versions_proposees() -> anyhow::Result<Vec<String>> {
    let snapshot = settings::get_bool(settings::SNAPSHOT, false);
    let beta = settings::get_bool(settings::BETA, false);
    let alpha = settings::get_bool(settings::ALPHA, false);
    Ok(manifeste()
        .await?
        .into_iter()
        .filter(|v| match v.type_.as_str() {
            "release" => true,
            "snapshot" => snapshot,
            "old_beta" => beta,
            "old_alpha" => alpha,
            _ => false,
        })
        .map(|v| v.id)
        .collect())
}

/// Une version prête à être lancée.
pub struct VersionInstallee {
    pub id: String,
    pub json: Value,
    pub jar: PathBuf,
    pub java: PathBuf,
    pub assets_index: String,
    /// Dossier des assets tel que l'attend le jeu (virtuel pour les très vieilles versions).
    pub game_assets: PathBuf,
    pub map_to_resources: bool,
    pub logging: Option<(String, PathBuf)>,
}

/// Télécharge le JSON d'une version vanilla s'il n'est pas déjà là.
pub async fn assurer_json(dirs: &McDirs, id: &str) -> anyhow::Result<()> {
    let chemin = dirs.version_json(id);
    if chemin.exists() {
        return Ok(());
    }
    let Some(v) = manifeste().await?.into_iter().find(|v| v.id == id) else {
        anyhow::bail!("Version {id} introuvable chez Mojang");
    };
    download::fichier(&v.url, &chemin, Some(&v.sha1)).await
}

/// Installe tout ce qu'il faut pour lancer `id` (déjà présent : vérification rapide).
pub async fn installer(dirs: &McDirs, id: &str, progression: FileProgress) -> anyhow::Result<VersionInstallee> {
    // JSON de la version et de ses parents
    let mut courant = id.to_string();
    loop {
        assurer_json(dirs, &courant).await?;
        let json = version::lire(dirs, &courant)?;
        match json.get("inheritsFrom").and_then(Value::as_str) {
            Some(parent) => courant = parent.to_string(),
            None => break,
        }
    }
    let json = version::fusionner(dirs, id)?;
    let vanilla = version::id_vanilla(&json);

    let mut fichiers: Vec<Tache> = Vec::new();

    // Jar du jeu : celui de la version vanilla
    let jar_vanilla = dirs.version_jar(&vanilla);
    if let Some(client) = json.get("downloads").and_then(|d| d.get("client")) {
        if let Some(url) = client.get("url").and_then(Value::as_str) {
            fichiers.push(Tache {
                chemin: jar_vanilla.clone(),
                url: url.to_string(),
                sha1: client.get("sha1").and_then(Value::as_str).map(str::to_string),
                taille: client.get("size").and_then(Value::as_u64),
            });
        }
    }

    // Bibliothèques
    let libs = version::bibliotheques(dirs, &json);
    for lib in &libs {
        match &lib.url {
            Some(url) => fichiers.push(Tache { chemin: lib.chemin.clone(), url: url.clone(), sha1: lib.sha1.clone(), taille: lib.taille }),
            None if !lib.chemin.exists() => {
                anyhow::bail!("Bibliothèque manquante : {}", lib.chemin.display());
            }
            None => {}
        }
    }

    // Configuration des logs (corrige aussi la faille Log4Shell)
    let mut logging = None;
    if let Some(client) = json.get("logging").and_then(|l| l.get("client")) {
        let fichier = &client["file"];
        if let (Some(idf), Some(url), Some(arg)) = (fichier["id"].as_str(), fichier["url"].as_str(), client["argument"].as_str()) {
            let chemin = dirs.assets().join("log_configs").join(idf);
            fichiers.push(Tache { chemin: chemin.clone(), url: url.to_string(), sha1: fichier["sha1"].as_str().map(str::to_string), taille: None });
            logging = Some((arg.to_string(), chemin));
        }
    }

    telecharger_tout(fichiers, &progression).await?;

    // Assets
    let (assets_index, game_assets, map_to_resources) = installer_assets(dirs, &json, &progression).await?;

    // Java
    let (composant, majeure) = version::java_requis(&json);
    let java = java::assurer(dirs, &composant, majeure, &progression).await?;

    // Jar à mettre dans le classpath : versions/<id>/<id>.jar (copie du jar vanilla pour les loaders,
    // Forge 1.17+ s'attend à ce nom), ou celui indiqué par "jar" (anciens Forge)
    let jar = match json.get("jar").and_then(Value::as_str) {
        Some(j) => dirs.version_jar(j),
        None => {
            let propre = dirs.version_jar(id);
            if propre != jar_vanilla && !propre.exists() && jar_vanilla.exists() {
                if let Some(parent) = propre.parent() {
                    std::fs::create_dir_all(parent)?;
                }
                std::fs::copy(&jar_vanilla, &propre)?;
            }
            if propre.exists() { propre } else { jar_vanilla }
        }
    };

    Ok(VersionInstallee { id: id.to_string(), json, jar, java, assets_index, game_assets, map_to_resources, logging })
}

/// Extrait les DLL des bibliothèques natives dans `dossier`.
pub fn extraire_natives(libs: &[FichierLib], dossier: &Path) -> anyhow::Result<()> {
    std::fs::create_dir_all(dossier)?;
    for lib in libs.iter().filter(|l| l.natif) {
        let Ok(f) = std::fs::File::open(&lib.chemin) else { continue };
        let Ok(mut zip) = zip::ZipArchive::new(f) else { continue };
        for i in 0..zip.len() {
            let mut entree = zip.by_index(i)?;
            let nom = entree.name().to_string();
            if nom.ends_with('/') || nom.starts_with("META-INF") || lib.exclure.iter().any(|e| nom.starts_with(e.as_str())) {
                continue;
            }
            let destination = if lib.natif_seul {
                // Ancien format : les fichiers sont à la racine du jar
                dossier.join(&nom)
            } else {
                // Nouveau format : seulement les DLL, rangées par architecture ("windows/x64/...") → on aplatit
                let n = nom.to_lowercase();
                let autre_arch = cfg!(target_arch = "x86_64") && (n.contains("/arm64/") || n.contains("/x86/"));
                if !n.ends_with(".dll") || autre_arch {
                    continue;
                }
                dossier.join(nom.rsplit('/').next().unwrap_or(&nom))
            };
            if let Some(parent) = destination.parent() {
                std::fs::create_dir_all(parent)?;
            }
            // DLL déjà utilisée (jeu ouvert) : on garde l'existante
            if let Ok(mut sortie) = std::fs::File::create(&destination) {
                std::io::copy(&mut entree, &mut sortie)?;
            }
        }
    }
    Ok(())
}

// ───────────────────────── Assets ─────────────────────────

async fn installer_assets(dirs: &McDirs, json: &Value, progression: &FileProgress) -> anyhow::Result<(String, PathBuf, bool)> {
    let Some(index) = json.get("assetIndex") else {
        return Ok(("legacy".into(), dirs.assets(), false));
    };
    let id = index["id"].as_str().unwrap_or("legacy").to_string();
    let chemin_index = dirs.assets().join("indexes").join(format!("{id}.json"));
    if !chemin_index.exists() {
        if let Some(url) = index["url"].as_str() {
            download::fichier(url, &chemin_index, index["sha1"].as_str()).await?;
        }
    }
    let contenu: Value = serde_json::from_str(&std::fs::read_to_string(&chemin_index)?)?;
    let virtuel = contenu.get("virtual").and_then(Value::as_bool).unwrap_or(false);
    let map_to_resources = contenu.get("map_to_resources").and_then(Value::as_bool).unwrap_or(false);

    let objets = contenu.get("objects").and_then(Value::as_object).cloned().unwrap_or_default();
    let mut taches = Vec::new();
    for objet in objets.values() {
        let Some(hash) = objet["hash"].as_str() else { continue };
        let prefixe = &hash[..2];
        taches.push(Tache {
            chemin: dirs.assets().join("objects").join(prefixe).join(hash),
            url: format!("{RESSOURCES_URL}{prefixe}/{hash}"),
            sha1: Some(hash.to_string()),
            taille: objet["size"].as_u64(),
        });
    }
    telecharger_tout(taches, progression).await?;

    // Très vieilles versions : les assets sont attendus sous leur vrai nom
    let mut game_assets = dirs.assets();
    if virtuel || map_to_resources {
        let dossier = dirs.assets().join("virtual").join(&id);
        for (nom, objet) in &objets {
            let Some(hash) = objet["hash"].as_str() else { continue };
            let destination = dossier.join(nom);
            if !destination.exists() {
                if let Some(parent) = destination.parent() {
                    std::fs::create_dir_all(parent)?;
                }
                let _ = std::fs::copy(dirs.assets().join("objects").join(&hash[..2]).join(hash), &destination);
            }
        }
        game_assets = dossier;
    }
    Ok((id, game_assets, map_to_resources))
}

/// Copie les assets dans <jeu>/resources (versions d'avant 1.6).
pub fn copier_resources(assets_virtuels: &Path, dossier_jeu: &Path) {
    fn copier(source: &Path, destination: &Path) {
        let Ok(entrees) = std::fs::read_dir(source) else { return };
        for e in entrees.flatten() {
            let cible = destination.join(e.file_name());
            if e.path().is_dir() {
                copier(&e.path(), &cible);
            } else if !cible.exists() {
                let _ = std::fs::create_dir_all(destination);
                let _ = std::fs::copy(e.path(), &cible);
            }
        }
    }
    copier(assets_virtuels, &dossier_jeu.join("resources"));
}

// ───────────────────────── Téléchargements en parallèle ─────────────────────────

pub struct Tache {
    pub chemin: PathBuf,
    pub url: String,
    pub sha1: Option<String>,
    pub taille: Option<u64>,
}

/// Fichier déjà présent et de la bonne taille ?
fn deja_la(t: &Tache) -> bool {
    match std::fs::metadata(&t.chemin) {
        Ok(m) => t.taille.is_none_or(|taille| m.len() == taille),
        Err(_) => false,
    }
}

/// Télécharge les fichiers manquants, 16 à la fois, en signalant l'avancement.
pub async fn telecharger_tout(taches: Vec<Tache>, progression: &FileProgress) -> anyhow::Result<()> {
    let manquants: Vec<Tache> = taches.into_iter().filter(|t| !deja_la(t)).collect();
    let total = manquants.len();
    if total == 0 {
        return Ok(());
    }
    let faits = Arc::new(AtomicUsize::new(0));
    let mut flux = futures::stream::iter(manquants.into_iter().map(|t| {
        let faits = faits.clone();
        let progression = progression.clone();
        async move {
            // Deux essais : les gros téléchargements ont parfois des coupures
            let mut resultat = download::fichier(&t.url, &t.chemin, t.sha1.as_deref()).await;
            if resultat.is_err() {
                resultat = download::fichier(&t.url, &t.chemin, t.sha1.as_deref()).await;
            }
            resultat.map_err(|e| anyhow::anyhow!("{} : {e}", t.url))?;
            let n = faits.fetch_add(1, Ordering::SeqCst) + 1;
            let nom = t.chemin.file_name().map(|n| n.to_string_lossy().to_string()).unwrap_or_default();
            progression(n, total, &nom);
            Ok::<(), anyhow::Error>(())
        }
    }))
    .buffer_unordered(16);
    while let Some(r) = flux.next().await {
        r?;
    }
    Ok(())
}
