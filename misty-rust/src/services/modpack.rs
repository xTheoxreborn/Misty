//! Installation d'un modpack dans un nouveau profil :
//! - Modrinth (.mrpack) : https://support.modrinth.com/en/articles/8802351-modrinth-modpack-format-mrpack
//! - CurseForge (zip + manifest.json)

use super::catalogue::{ProjetDistant, CURSEFORGE};
use super::content::{self, ContenuMeta};
use super::instances::{self, Instance};
use super::{curseforge, download, loc, modrinth, Progress};
use crate::minecraft::loaders;
use futures::StreamExt;
use std::collections::HashMap;
use std::io::Read;
use std::path::{Component, Path, PathBuf};
use std::sync::atomic::{AtomicUsize, Ordering};
use std::sync::Arc;

/// Fichier qu'on n'a pas le droit de télécharger automatiquement (choix de l'auteur sur CurseForge).
#[derive(Clone)]
pub struct FichierBloque {
    pub nom: String,
    pub page: String,
    pub dossier: String,
}

pub struct Resultat {
    pub instance: Instance,
    pub bloques: Vec<FichierBloque>,
}

pub async fn installer(projet: &ProjetDistant, progression: Progress) -> anyhow::Result<Resultat> {
    let mut instance: Option<Instance> = None;
    let mut temporaire: Option<PathBuf> = None;

    let resultat = if projet.source == CURSEFORGE {
        installer_curseforge(projet, &progression, &mut instance, &mut temporaire).await
    } else {
        installer_modrinth(projet, &progression, &mut instance, &mut temporaire).await
    };

    if let Some(t) = temporaire {
        let _ = std::fs::remove_file(t);
    }
    // Si quelque chose échoue, le profil à moitié créé est supprimé
    if resultat.is_err() {
        if let Some(i) = &instance {
            let _ = instances::supprimer(i);
        }
    }
    resultat
}

/// Télécharge l'archive dans le dossier temporaire et l'ouvre.
async fn telecharger_archive(
    url: &str,
    sha1: Option<&str>,
    progression: &Progress,
    temporaire: &mut Option<PathBuf>,
) -> anyhow::Result<zip::ZipArchive<std::fs::File>> {
    progression(loc::t("Progres_Modpack"));
    let chemin = std::env::temp_dir().join(format!("misty-{}.zip", uuid::Uuid::new_v4().simple()));
    *temporaire = Some(chemin.clone());
    download::fichier(url, &chemin, sha1).await?;
    Ok(zip::ZipArchive::new(std::fs::File::open(&chemin)?)?)
}

/// Crée le profil du modpack.
fn creer(projet: &ProjetDistant, version_mc: &str, loader: &str, version_loader: Option<String>, version_pack: String) -> anyhow::Result<Instance> {
    let mut instance = instances::creer(&projet.titre, version_mc, loader, version_loader)?;
    instance.icone_url = projet.icone_url.clone();
    instance.modpack_source = Some(projet.source.clone());
    instance.modpack_project_id = Some(projet.id.clone());
    instance.modpack_version = Some(version_pack);
    instances::sauver(&instance)?;
    Ok(instance)
}

// ───────────────────────── Modrinth ─────────────────────────

async fn installer_modrinth(
    projet: &ProjetDistant,
    progression: &Progress,
    sortie: &mut Option<Instance>,
    temporaire: &mut Option<PathBuf>,
) -> anyhow::Result<Resultat> {
    progression(loc::t("Progres_DerniereVersion"));
    let versions = modrinth::versions(&projet.id, None, None).await?;
    let version = versions
        .iter()
        .find(|v| v.version_type == "release")
        .or_else(|| versions.first())
        .ok_or_else(|| anyhow::anyhow!(loc::t("Erreur_ModpackSansVersion")))?;
    let fichier = version
        .files
        .iter()
        .find(|f| f.filename.to_lowercase().ends_with(".mrpack"))
        .ok_or_else(|| anyhow::anyhow!(loc::t("Erreur_SansMrpack")))?;

    let mut zip = telecharger_archive(&fichier.url, fichier.hashes.get("sha1").map(String::as_str), progression, temporaire).await?;

    let index: modrinth::IndexModpack = lire_json(&mut zip, "modrinth.index.json")?;
    let version_mc = index
        .dependencies
        .get("minecraft")
        .cloned()
        .ok_or_else(|| anyhow::anyhow!(loc::t("Erreur_ModpackSansMc")))?;
    let (loader, version_loader) = loader_modrinth(&index.dependencies);

    let instance = creer(projet, &version_mc, loader, version_loader, version.version_number.clone())?;
    *sortie = Some(instance.clone());

    // Fichiers à télécharger (sauf ceux réservés au serveur)
    let mut taches = Vec::new();
    for f in index.files.iter().filter(|f| f.env.as_ref().and_then(|e| e.get("client")).map(String::as_str) != Some("unsupported")) {
        let destination = chemin_sur(&instance.dossier(), &f.path)?;
        taches.push((f.downloads.clone(), destination, f.hashes.get("sha1").cloned(), f.path.clone()));
    }
    en_parallele(taches, progression, |(urls, destination, sha1, chemin)| async move {
        let mut derniere = None;
        for url in &urls {
            match download::fichier(url, &destination, sha1.as_deref()).await {
                Ok(()) => return Ok(()),
                Err(e) => derniere = Some(e),
            }
        }
        let detail = derniere.map(|e| format!("\n{e}")).unwrap_or_default();
        anyhow::bail!("{}{detail}", loc::tf("Erreur_Telechargement", &[&chemin]))
    })
    .await?;

    progression(loc::t("Progres_Config"));
    extraire(&mut zip, "overrides/", &instance.dossier())?;
    extraire(&mut zip, "client-overrides/", &instance.dossier())?;

    Ok(Resultat { instance, bloques: Vec::new() })
}

fn loader_modrinth(dependances: &HashMap<String, String>) -> (&'static str, Option<String>) {
    if let Some(v) = dependances.get("fabric-loader") {
        return (loaders::FABRIC, Some(v.clone()));
    }
    if let Some(v) = dependances.get("neoforge") {
        return (loaders::NEOFORGE, Some(v.clone()));
    }
    if let Some(v) = dependances.get("forge") {
        return (loaders::FORGE, Some(v.clone()));
    }
    (loaders::VANILLA, None)
}

// ───────────────────────── CurseForge ─────────────────────────

async fn installer_curseforge(
    projet: &ProjetDistant,
    progression: &Progress,
    sortie: &mut Option<Instance>,
    temporaire: &mut Option<PathBuf>,
) -> anyhow::Result<Resultat> {
    progression(loc::t("Progres_DerniereVersion"));
    let fichiers_pack = curseforge::fichiers(projet.id.parse()?, None).await?;
    let fichier_pack = fichiers_pack
        .iter()
        .find(|f| f.release_type == 1 && f.is_available)
        .or_else(|| fichiers_pack.first())
        .ok_or_else(|| anyhow::anyhow!(loc::t("Erreur_ModpackSansVersion")))?;
    let Some(url) = fichier_pack.download_url.clone().filter(|u| !u.is_empty()) else {
        anyhow::bail!(loc::t("Erreur_ModpackCFInterdit"));
    };

    let mut zip = telecharger_archive(&url, fichier_pack.sha1(), progression, temporaire).await?;
    let manifeste: curseforge::ManifestCF = lire_json(&mut zip, "manifest.json")?;
    let (loader, version_loader) = loader_curseforge(&manifeste.minecraft.mod_loaders)?;

    let version_pack = if manifeste.version.is_empty() { fichier_pack.display_name.clone() } else { manifeste.version.clone() };
    let instance = creer(projet, &manifeste.minecraft.version, loader, version_loader, version_pack)?;
    *sortie = Some(instance.clone());

    // Infos des fichiers (URL, nom) et des projets (type, nom, icône)
    progression(loc::t("Progres_ListeMods"));
    let requis: Vec<&curseforge::FichierManifestCF> = manifeste.files.iter().filter(|f| f.required).collect();
    let fichiers: HashMap<i64, curseforge::FichierCF> = curseforge::fichiers_par_ids(&requis.iter().map(|f| f.file_id).collect::<Vec<_>>())
        .await?
        .into_iter()
        .map(|f| (f.id, f))
        .collect();
    let mods: HashMap<i64, curseforge::ModCF> = curseforge::mods(&requis.iter().map(|f| f.project_id).collect::<Vec<_>>())
        .await?
        .into_iter()
        .map(|m| (m.id, m))
        .collect();

    let mut bloques = Vec::new();
    let mut metas = Vec::new();
    let mut a_telecharger = Vec::new();

    for entree in requis {
        let Some(fichier) = fichiers.get(&entree.file_id) else { continue };
        let m = mods.get(&entree.project_id);

        let type_ = curseforge::type_depuis_classe(m.and_then(|m| m.class_id).unwrap_or(curseforge::CLASSE_MODS));
        let dossier = content::dossier(&instance, type_, None)?;
        let nom_dossier = dossier.file_name().map(|n| n.to_string_lossy().to_string()).unwrap_or_default();
        let destination = chemin_sur(&instance.dossier(), &format!("{nom_dossier}/{}", download::nom_fichier_sur(&fichier.file_name)))?;
        let titre = m.map(|m| m.name.clone()).unwrap_or_else(|| fichier.display_name.clone());

        let Some(url) = fichier.download_url.clone().filter(|u| !u.is_empty()) else {
            bloques.push(FichierBloque {
                nom: titre,
                page: curseforge::page_fichier(m, entree.project_id, entree.file_id),
                dossier: nom_dossier,
            });
            continue;
        };

        metas.push(ContenuMeta {
            chemin: content::relatif(&instance, &destination),
            source: Some(CURSEFORGE.into()),
            project_id: Some(entree.project_id.to_string()),
            version_id: Some(entree.file_id.to_string()),
            titre,
            version_nom: Some(fichier.display_name.clone()),
            icone_url: m.and_then(|m| m.logo.as_ref()).and_then(|l| l.thumbnail_url.clone()),
            type_: type_.to_string(),
        });
        a_telecharger.push((url, destination, fichier.sha1().map(str::to_string)));
    }

    en_parallele(a_telecharger, progression, |(url, destination, sha1)| async move {
        download::fichier(&url, &destination, sha1.as_deref()).await
    })
    .await?;
    content::enregistrer_metas(&instance, metas)?;

    progression(loc::t("Progres_Config"));
    let overrides = manifeste.overrides.as_deref().map(|o| o.trim_matches('/')).filter(|o| !o.is_empty()).unwrap_or("overrides");
    extraire(&mut zip, &format!("{overrides}/"), &instance.dossier())?;

    Ok(Resultat { instance, bloques })
}

/// "forge-47.2.0" → (Forge, "47.2.0"). Le loader principal est privilégié.
fn loader_curseforge(liste: &[curseforge::LoaderManifestCF]) -> anyhow::Result<(&'static str, Option<String>)> {
    let Some(principal) = liste.iter().find(|l| l.primary).or_else(|| liste.first()) else {
        return Ok((loaders::VANILLA, None));
    };
    let (nom, version) = match principal.id.split_once('-') {
        Some((n, v)) => (n, Some(v.to_string())),
        None => (principal.id.as_str(), None),
    };
    Ok(match nom.to_lowercase().as_str() {
        "forge" => (loaders::FORGE, version),
        "neoforge" => (loaders::NEOFORGE, version),
        "fabric" => (loaders::FABRIC, version),
        _ => anyhow::bail!(loc::tf("Erreur_LoaderNonPris", &[&principal.id])),
    })
}

// ───────────────────────── Commun ─────────────────────────

fn lire_json<T: serde::de::DeserializeOwned>(zip: &mut zip::ZipArchive<std::fs::File>, nom: &str) -> anyhow::Result<T> {
    let mut entree = zip.by_name(nom).map_err(|_| anyhow::anyhow!(loc::tf("Erreur_Manquant", &[&nom])))?;
    let mut texte = String::new();
    entree.read_to_string(&mut texte)?;
    serde_json::from_str(texte.trim_start_matches('\u{feff}')).map_err(|e| anyhow::anyhow!("{nom} illisible : {e}"))
}

/// Exécute les téléchargements 6 par 6 en affichant l'avancement.
async fn en_parallele<T, F, Fut>(elements: Vec<T>, progression: &Progress, action: F) -> anyhow::Result<()>
where
    F: Fn(T) -> Fut,
    Fut: std::future::Future<Output = anyhow::Result<()>>,
{
    let total = elements.len();
    let faits = Arc::new(AtomicUsize::new(0));
    let mut flux = futures::stream::iter(elements.into_iter().map(|e| {
        let faits = faits.clone();
        let progression = progression.clone();
        let tache = action(e);
        async move {
            tache.await?;
            let n = faits.fetch_add(1, Ordering::SeqCst) + 1;
            progression(loc::tf("Progres_Fichiers", &[&n, &total]));
            Ok::<(), anyhow::Error>(())
        }
    }))
    .buffer_unordered(6);
    while let Some(r) = flux.next().await {
        r?;
    }
    Ok(())
}

/// Extrait les fichiers de l'archive qui commencent par `prefixe` dans `racine`.
fn extraire(zip: &mut zip::ZipArchive<std::fs::File>, prefixe: &str, racine: &Path) -> anyhow::Result<()> {
    let prefixe_min = prefixe.to_lowercase();
    for i in 0..zip.len() {
        let mut entree = zip.by_index(i)?;
        let nom = entree.name().replace('\\', "/");
        if !nom.to_lowercase().starts_with(&prefixe_min) || nom.ends_with('/') {
            continue; // hors du préfixe, ou dossier
        }
        let destination = chemin_sur(racine, &nom[prefixe.len()..])?;
        if let Some(parent) = destination.parent() {
            std::fs::create_dir_all(parent)?;
        }
        let mut sortie = std::fs::File::create(&destination)?;
        std::io::copy(&mut entree, &mut sortie)?;
    }
    Ok(())
}

/// Refuse les chemins qui sortiraient du dossier du profil ("../", chemins absolus…).
pub fn chemin_sur(racine: &Path, relatif: &str) -> anyhow::Result<PathBuf> {
    let rel = Path::new(relatif);
    if rel.components().any(|c| !matches!(c, Component::Normal(_) | Component::CurDir)) {
        anyhow::bail!("Chemin invalide dans le modpack : {relatif}");
    }
    Ok(racine.join(rel))
}
