//! Forge et NeoForge : on télécharge l'installateur officiel et on fait ce qu'il ferait,
//! sans son interface :
//!   - ancien format (≤ 1.12.2) : le JSON de version est dans install_profile.json ("versionInfo"),
//!     et le jar "universal" est copié dans les bibliothèques ;
//!   - format actuel : version.json + bibliothèques, puis les « processors » (petits programmes Java
//!     qui génèrent le jar patché du jeu) sont exécutés un par un.

use crate::minecraft::install::{telecharger_tout, Tache};
use crate::minecraft::{java, maven_path, sans_console, version, FileProgress, McDirs};
use crate::services::http;
use serde_json::Value;
use std::collections::HashMap;
use std::io::Read;
use std::path::{Path, PathBuf};
use tokio::sync::Mutex;

#[derive(Clone, Copy, PartialEq)]
pub enum Sorte {
    Forge,
    NeoForge,
}

const FORGE_MAVEN: &str = "https://maven.minecraftforge.net/net/minecraftforge/forge/";
const NEO_MAVEN: &str = "https://maven.neoforged.net/releases/net/neoforged/";

// ───────────────────────── Listes de versions ─────────────────────────

/// Toutes les versions Forge ("1.20.1-47.2.0"…), gardées en mémoire.
async fn versions_forge() -> anyhow::Result<Vec<String>> {
    static CACHE: Mutex<Option<Vec<String>>> = Mutex::const_new(None);
    let mut cache = CACHE.lock().await;
    if let Some(v) = cache.as_ref() {
        return Ok(v.clone());
    }
    let xml = http::get_text(&format!("{FORGE_MAVEN}maven-metadata.xml")).await?;
    let re = regex::Regex::new(r"<version>([^<]+)</version>").unwrap();
    let liste: Vec<String> = re.captures_iter(&xml).map(|c| c[1].to_string()).collect();
    *cache = Some(liste.clone());
    Ok(liste)
}

/// Versions NeoForge d'un artefact ("neoforge" ou "forge" pour la 1.20.1).
async fn versions_neo(artefact: &str) -> anyhow::Result<Vec<String>> {
    static CACHE: Mutex<Option<HashMap<String, Vec<String>>>> = Mutex::const_new(None);
    let mut cache = CACHE.lock().await;
    if let Some(v) = cache.as_ref().and_then(|c| c.get(artefact)) {
        return Ok(v.clone());
    }
    let url = format!("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/{artefact}");
    let v: Value = http::get_json(&url).await?;
    let liste: Vec<String> = v["versions"].as_array().into_iter().flatten().filter_map(|x| x.as_str().map(str::to_string)).collect();
    cache.get_or_insert_with(HashMap::new).insert(artefact.to_string(), liste.clone());
    Ok(liste)
}

/// Compare deux numéros de version segment par segment ("47.10.0" > "47.9.2").
fn comparer(a: &str, b: &str) -> std::cmp::Ordering {
    let seg = |s: &str| -> Vec<u64> {
        s.split(|c: char| !c.is_ascii_digit()).filter(|x| !x.is_empty()).map(|x| x.parse().unwrap_or(0)).collect()
    };
    seg(a).cmp(&seg(b))
}

fn est_stable(v: &str) -> bool {
    let v = v.to_lowercase();
    !v.contains("beta") && !v.contains("alpha") && !v.contains("rc")
}

/// Préfixe des versions NeoForge pour un Minecraft : "1.21.1" → "21.1.", "26.1" → "26.1.0."
fn prefixes_neo(mc: &str) -> Vec<String> {
    let parties: Vec<&str> = mc.split('.').collect();
    if parties.first() == Some(&"1") {
        let mineure = parties.get(1).copied().unwrap_or("0");
        let patch = parties.get(2).copied().unwrap_or("0");
        vec![format!("{mineure}.{patch}.")]
    } else {
        // Numérotation par année (26.1, 26.1.1…)
        let patch = parties.get(2).copied().unwrap_or("0");
        vec![format!("{}.{}.{patch}.", parties[0], parties.get(1).copied().unwrap_or("0")), format!("{mc}.")]
    }
}

/// Résout la version complète à installer, et l'URL de l'installateur.
async fn choisir(sorte: Sorte, mc: &str, loader: Option<&str>) -> anyhow::Result<Option<(String, String)>> {
    match sorte {
        Sorte::Forge => {
            let toutes = versions_forge().await?;
            let mut candidates: Vec<&String> = toutes.iter().filter(|v| v.starts_with(&format!("{mc}-"))).collect();
            if candidates.is_empty() {
                return Ok(None);
            }
            let choisie = match loader {
                Some(l) => candidates
                    .iter()
                    .find(|v| v.as_str() == format!("{mc}-{l}") || v.starts_with(&format!("{mc}-{l}-")) || v.as_str() == l)
                    .map(|v| v.to_string()),
                None => {
                    // Version recommandée, sinon la dernière
                    let promos: Value = http::get_json("https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json")
                        .await
                        .unwrap_or_default();
                    let promo = promos["promos"][format!("{mc}-recommended")].as_str().or_else(|| promos["promos"][format!("{mc}-latest")].as_str());
                    promo
                        .and_then(|p| candidates.iter().find(|v| v.as_str() == format!("{mc}-{p}") || v.starts_with(&format!("{mc}-{p}-"))))
                        .map(|v| v.to_string())
                        .or_else(|| {
                            candidates.sort_by(|a, b| comparer(a, b));
                            candidates.last().map(|v| v.to_string())
                        })
                }
            };
            Ok(choisie.map(|v| {
                let url = format!("{FORGE_MAVEN}{v}/forge-{v}-installer.jar");
                (v, url)
            }))
        }
        Sorte::NeoForge => {
            // La 1.20.1 utilisait encore l'artefact "forge" ("1.20.1-47.1.106")
            let (artefact, filtre): (&str, Vec<String>) =
                if mc == "1.20.1" { ("forge", vec![format!("{mc}-")]) } else { ("neoforge", prefixes_neo(mc)) };
            let toutes = versions_neo(artefact).await?;
            let mut candidates: Vec<&String> = Vec::new();
            for prefixe in &filtre {
                candidates = toutes.iter().filter(|v| v.starts_with(prefixe.as_str())).collect();
                if !candidates.is_empty() {
                    break;
                }
            }
            if candidates.is_empty() {
                return Ok(None);
            }
            let choisie = match loader {
                Some(l) => {
                    let complet = if artefact == "forge" && !l.starts_with(&format!("{mc}-")) { format!("{mc}-{l}") } else { l.to_string() };
                    candidates.iter().find(|v| ***v == complet).map(|v| v.to_string()).or(Some(complet))
                }
                None => {
                    candidates.sort_by(|a, b| comparer(a, b));
                    candidates.iter().rev().find(|v| est_stable(v)).or(candidates.last()).map(|v| v.to_string())
                }
            };
            Ok(choisie.map(|v| {
                let url = format!("{NEO_MAVEN}{artefact}/{v}/{artefact}-{v}-installer.jar");
                (v, url)
            }))
        }
    }
}

pub async fn disponible(sorte: Sorte, mc: &str) -> bool {
    match sorte {
        Sorte::Forge => versions_forge().await.is_ok_and(|v| v.iter().any(|x| x.starts_with(&format!("{mc}-")))),
        Sorte::NeoForge => choisir(sorte, mc, None).await.is_ok_and(|c| c.is_some()),
    }
}

// ───────────────────────── Installation ─────────────────────────

pub async fn installer(dirs: &McDirs, sorte: Sorte, mc: &str, loader: Option<&str>, progression: &FileProgress) -> anyhow::Result<String> {
    let nom = if sorte == Sorte::Forge { "Forge" } else { "NeoForge" };
    let (version, url) = choisir(sorte, mc, loader).await?.ok_or_else(|| anyhow::anyhow!("{nom} indisponible pour {mc}"))?;

    // Déjà installé (et processors déjà exécutés) : rien à refaire
    let marqueur = dirs.versions().join(".misty").join(format!("{nom}-{version}.ok"));
    if let Ok(id) = std::fs::read_to_string(&marqueur) {
        if dirs.version_json(id.trim()).exists() {
            return Ok(id.trim().to_string());
        }
    }

    let travail = std::env::temp_dir().join(format!("misty-forge-{}", uuid::Uuid::new_v4().simple()));
    std::fs::create_dir_all(&travail)?;
    let installateur = travail.join("installer.jar");
    progression(0, 1, &format!("{nom} {version}"));

    let resultat = async {
        crate::services::download::fichier(&url, &installateur, None).await?;
        let mut zip = zip::ZipArchive::new(std::fs::File::open(&installateur)?)?;
        let profil: Value = serde_json::from_str(&lire_texte(&mut zip, "install_profile.json")?)?;
        if profil.get("versionInfo").is_some() {
            installer_ancien(dirs, mc, &profil, &mut zip)
        } else {
            installer_moderne(dirs, mc, &profil, &mut zip, &installateur, &travail, progression).await
        }
    }
    .await;
    let _ = std::fs::remove_dir_all(&travail);
    let id = resultat?;

    std::fs::create_dir_all(marqueur.parent().unwrap())?;
    std::fs::write(&marqueur, &id)?;
    Ok(id)
}

fn lire_texte(zip: &mut zip::ZipArchive<std::fs::File>, nom: &str) -> anyhow::Result<String> {
    let mut entree = zip.by_name(nom.trim_start_matches('/'))?;
    let mut texte = String::new();
    entree.read_to_string(&mut texte)?;
    Ok(texte)
}

fn extraire(zip: &mut zip::ZipArchive<std::fs::File>, nom: &str, destination: &Path) -> anyhow::Result<bool> {
    let Ok(mut entree) = zip.by_name(nom.trim_start_matches('/')) else { return Ok(false) };
    if let Some(parent) = destination.parent() {
        std::fs::create_dir_all(parent)?;
    }
    std::io::copy(&mut entree, &mut std::fs::File::create(destination)?)?;
    Ok(true)
}

fn ecrire_version(dirs: &McDirs, json: &Value) -> anyhow::Result<String> {
    let id = json["id"].as_str().ok_or_else(|| anyhow::anyhow!("version.json sans id"))?.to_string();
    let chemin = dirs.version_json(&id);
    std::fs::create_dir_all(chemin.parent().unwrap())?;
    std::fs::write(&chemin, serde_json::to_string_pretty(json)?)?;
    Ok(id)
}

/// Forge ≤ 1.12.2.
fn installer_ancien(dirs: &McDirs, mc: &str, profil: &Value, zip: &mut zip::ZipArchive<std::fs::File>) -> anyhow::Result<String> {
    let mut json = profil["versionInfo"].clone();
    let obj = json.as_object_mut().ok_or_else(|| anyhow::anyhow!("versionInfo invalide"))?;
    obj.entry("inheritsFrom").or_insert_with(|| Value::String(mc.to_string()));
    obj.entry("jar").or_insert_with(|| Value::String(mc.to_string()));
    // Bibliothèques réservées au serveur : inutiles
    if let Some(libs) = obj.get_mut("libraries").and_then(Value::as_array_mut) {
        libs.retain(|l| l.get("clientreq").and_then(Value::as_bool) != Some(false));
    }

    // Le jar "universal" de Forge, intégré à l'installateur
    let install = &profil["install"];
    if let (Some(chemin_zip), Some(coords)) = (install["filePath"].as_str(), install["path"].as_str()) {
        if let Some(rel) = maven_path(coords) {
            extraire(zip, chemin_zip, &dirs.libraries().join(rel))?;
        }
    }
    ecrire_version(dirs, &json)
}

/// Forge 1.13+ et NeoForge.
async fn installer_moderne(
    dirs: &McDirs,
    mc: &str,
    profil: &Value,
    zip: &mut zip::ZipArchive<std::fs::File>,
    installateur: &Path,
    travail: &Path,
    progression: &FileProgress,
) -> anyhow::Result<String> {
    let chemin_json = profil["json"].as_str().unwrap_or("/version.json");
    let json: Value = serde_json::from_str(&lire_texte(zip, chemin_json)?)?;
    let id = ecrire_version(dirs, &json)?;

    // Bibliothèques de l'installateur et de la version : téléchargées, ou extraites de l'installateur
    let mut taches = Vec::new();
    for lib in profil["libraries"].as_array().into_iter().flatten().chain(json["libraries"].as_array().into_iter().flatten()) {
        let Some(nom) = lib["name"].as_str() else { continue };
        let artefact = &lib["downloads"]["artifact"];
        let Some(rel) = artefact["path"].as_str().map(str::to_string).or_else(|| maven_path(nom)) else { continue };
        let chemin = dirs.libraries().join(&rel);
        match artefact["url"].as_str().filter(|u| !u.is_empty()) {
            Some(url) => taches.push(Tache {
                chemin,
                url: url.to_string(),
                sha1: artefact["sha1"].as_str().map(str::to_string),
                taille: artefact["size"].as_u64(),
            }),
            None => {
                if !chemin.exists() {
                    extraire(zip, &format!("maven/{rel}"), &chemin)?;
                }
            }
        }
    }
    telecharger_tout(taches, progression).await?;

    // Variables des processors
    let mut donnees: HashMap<String, String> = HashMap::new();
    for (cle, valeur) in profil["data"].as_object().into_iter().flatten() {
        let Some(v) = valeur["client"].as_str() else { continue };
        let resolue = if let Some(coords) = v.strip_prefix('[').and_then(|s| s.strip_suffix(']')) {
            maven_path(coords).map(|r| dirs.libraries().join(r).to_string_lossy().to_string()).unwrap_or_default()
        } else if let Some(litteral) = v.strip_prefix('\'').and_then(|s| s.strip_suffix('\'')) {
            litteral.to_string()
        } else if v.starts_with('/') {
            let destination = travail.join(v.trim_start_matches('/'));
            extraire(zip, v, &destination)?;
            destination.to_string_lossy().to_string()
        } else {
            v.to_string()
        };
        donnees.insert(cle.clone(), resolue);
    }
    donnees.insert("SIDE".into(), "client".into());
    donnees.insert("MINECRAFT_JAR".into(), dirs.version_jar(mc).to_string_lossy().to_string());
    donnees.insert("MINECRAFT_VERSION".into(), mc.to_string());
    donnees.insert("ROOT".into(), dirs.root.to_string_lossy().to_string());
    donnees.insert("INSTALLER".into(), installateur.to_string_lossy().to_string());
    donnees.insert("LIBRARY_DIR".into(), dirs.libraries().to_string_lossy().to_string());

    // Java de la version vanilla
    let vanilla = version::lire(dirs, mc)?;
    let (composant, majeure) = version::java_requis(&vanilla);
    let java = java::java_console(&java::assurer(dirs, &composant, majeure, progression).await?);

    let processors: Vec<&Value> = profil["processors"]
        .as_array()
        .into_iter()
        .flatten()
        .filter(|p| p["sides"].as_array().is_none_or(|s| s.iter().any(|x| x == "client")))
        .collect();
    let total = processors.len();
    let re = regex::Regex::new(r"\{([A-Z_]+)\}").unwrap();
    for (i, p) in processors.into_iter().enumerate() {
        progression(i, total, &format!("Forge ({}/{total})", i + 1));
        executer_processor(dirs, p, &donnees, &java, &re)?;
    }
    progression(total, total, "Forge");
    Ok(id)
}

fn chemin_lib(dirs: &McDirs, coords: &str) -> anyhow::Result<PathBuf> {
    Ok(dirs.libraries().join(maven_path(coords).ok_or_else(|| anyhow::anyhow!("Coordonnées invalides : {coords}"))?))
}

fn executer_processor(dirs: &McDirs, p: &Value, donnees: &HashMap<String, String>, java: &Path, re: &regex::Regex) -> anyhow::Result<()> {
    let substituer = |texte: &str| -> anyhow::Result<String> {
        if let Some(coords) = texte.strip_prefix('[').and_then(|s| s.strip_suffix(']')) {
            return Ok(chemin_lib(dirs, coords)?.to_string_lossy().to_string());
        }
        Ok(re.replace_all(texte, |c: &regex::Captures| donnees.get(&c[1]).cloned().unwrap_or_else(|| c[0].to_string())).to_string())
    };

    // Sorties déjà présentes et correctes : étape déjà faite
    if let Some(sorties) = p["outputs"].as_object() {
        let deja = !sorties.is_empty()
            && sorties.iter().all(|(fichier, sha1)| {
                let (Ok(f), Ok(h)) = (substituer(fichier), substituer(sha1.as_str().unwrap_or_default())) else { return false };
                let h = h.trim_matches('\'');
                crate::services::download::sha1_fichier(Path::new(&f)).is_ok_and(|x| x.eq_ignore_ascii_case(h))
            });
        if deja {
            return Ok(());
        }
    }

    let jar = chemin_lib(dirs, p["jar"].as_str().unwrap_or_default())?;
    let classe = classe_principale(&jar)?;
    let mut classpath = vec![jar.to_string_lossy().to_string()];
    for c in p["classpath"].as_array().into_iter().flatten().filter_map(Value::as_str) {
        classpath.push(chemin_lib(dirs, c)?.to_string_lossy().to_string());
    }
    let mut args = Vec::new();
    for a in p["args"].as_array().into_iter().flatten().filter_map(Value::as_str) {
        args.push(substituer(a)?);
    }

    let mut commande = std::process::Command::new(java);
    commande.arg("-cp").arg(classpath.join(";")).arg(&classe).args(&args).current_dir(&dirs.root);
    sans_console(&mut commande);
    let sortie = commande.output()?;
    if !sortie.status.success() {
        let erreur = String::from_utf8_lossy(&sortie.stderr);
        let fin: Vec<&str> = erreur.lines().rev().take(8).collect();
        anyhow::bail!("L'installation de Forge a échoué ({classe}) :\n{}", fin.into_iter().rev().collect::<Vec<_>>().join("\n"));
    }
    Ok(())
}

/// Classe principale d'un jar (ligne "Main-Class" du manifeste).
fn classe_principale(jar: &Path) -> anyhow::Result<String> {
    let mut zip = zip::ZipArchive::new(std::fs::File::open(jar)?)?;
    let manifeste = lire_texte(&mut zip, "META-INF/MANIFEST.MF")?;
    manifeste
        .lines()
        .find_map(|l| l.strip_prefix("Main-Class:"))
        .map(|c| c.trim().to_string())
        .ok_or_else(|| anyhow::anyhow!("Main-Class introuvable dans {}", jar.display()))
}
