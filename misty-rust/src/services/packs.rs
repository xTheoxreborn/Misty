//! Packs de textures : template vanilla, création, export, liste.

use super::{download, http, loc, paths, Progress};
use std::fs;
use std::io::{Read, Write};
use std::path::{Path, PathBuf};

pub const RACINE_TEXTURES: &str = "assets/minecraft/textures/";

pub fn dossier_templates() -> PathBuf {
    paths::data_dir().join("templates")
}

pub fn dossier_packs() -> PathBuf {
    paths::data_dir().join("packs")
}

#[derive(Clone, Debug)]
pub struct PackInfo {
    pub nom: String,
    pub description: String,
    pub dossier: PathBuf,
    pub nb_textures: usize,
    pub version_mc: String,
}

impl PackInfo {
    /// pack.png du pack, sinon celle de Minecraft (template), sinon None.
    pub fn icone(&self) -> Option<PathBuf> {
        let propre = self.dossier.join("pack.png");
        if propre.exists() {
            return Some(propre);
        }
        let template = dossier_templates().join(&self.version_mc).join("pack.png");
        template.exists().then_some(template)
    }

    pub fn a_pack_png(&self) -> bool {
        self.dossier.join("pack.png").exists()
    }
}

/// Télécharge (une seule fois) le template vanilla d'une version. Renvoie (dossier, pack_format).
pub async fn template(version_mc: &str, progression: Progress) -> anyhow::Result<(PathBuf, i64)> {
    let dossier = dossier_templates().join(version_mc);
    let marqueur = dossier.join(".ok");
    if marqueur.exists() {
        if let Some(format) = fs::read_to_string(dossier.join(".format")).ok().and_then(|f| f.trim().parse().ok()) {
            return Ok((dossier, format));
        }
    }

    progression(loc::t("Pack_RechercheVersion"));
    let manifeste: serde_json::Value = http::get_json("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json").await?;
    let url_version = manifeste["versions"]
        .as_array()
        .and_then(|v| v.iter().find(|v| v["id"] == version_mc))
        .and_then(|v| v["url"].as_str())
        .ok_or_else(|| anyhow::anyhow!(loc::tf("Pack_VersionInconnue", &[&version_mc])))?
        .to_string();
    let version: serde_json::Value = http::get_json(&url_version).await?;
    let client = &version["downloads"]["client"];
    let url = client["url"].as_str().ok_or_else(|| anyhow::anyhow!(loc::tf("Pack_VersionInconnue", &[&version_mc])))?;

    progression(loc::t("Pack_TelechargementTemplate"));
    let jar = std::env::temp_dir().join(format!("misty-{}.jar", uuid::Uuid::new_v4().simple()));
    let resultat = async {
        download::fichier(url, &jar, client["sha1"].as_str()).await?;
        progression(loc::t("Pack_Extraction"));
        let jar = jar.clone();
        let dossier = dossier.clone();
        let version_mc = version_mc.to_string();
        tokio::task::spawn_blocking(move || extraire_template(&jar, &dossier, &version_mc)).await?
    }
    .await;
    let _ = fs::remove_file(&jar);
    let format = resultat?;
    Ok((dossier, format))
}

fn extraire_template(jar: &Path, dossier: &Path, version_mc: &str) -> anyhow::Result<i64> {
    if dossier.exists() {
        fs::remove_dir_all(dossier)?; // extraction précédente interrompue
    }
    fs::create_dir_all(dossier)?;
    let mut zip = zip::ZipArchive::new(fs::File::open(jar)?)?;
    let mut format = None;
    for i in 0..zip.len() {
        let mut entree = zip.by_index(i)?;
        let nom = entree.name().to_string();
        if nom == "version.json" {
            let mut texte = String::new();
            entree.read_to_string(&mut texte)?;
            format = lire_format(&texte);
            continue;
        }
        let utile = nom.starts_with(RACINE_TEXTURES) || nom == "pack.png";
        if !utile || nom.ends_with('/') {
            continue;
        }
        let destination = dossier.join(&nom);
        if let Some(parent) = destination.parent() {
            fs::create_dir_all(parent)?;
        }
        std::io::copy(&mut entree, &mut fs::File::create(&destination)?)?;
    }

    if lister_textures(dossier).is_empty() {
        anyhow::bail!(loc::tf("Pack_VersionTropAncienne", &[&version_mc]));
    }

    let format = format.unwrap_or_else(|| format_depuis_nom(version_mc));
    fs::write(dossier.join(".format"), format.to_string())?;
    fs::write(dossier.join(".ok"), "")?;
    Ok(format)
}

/// Le jar contient version.json (1.14+) avec le pack_format des packs de ressources.
fn lire_format(texte: &str) -> Option<i64> {
    let doc: serde_json::Value = serde_json::from_str(texte).ok()?;
    let pv = doc.get("pack_version")?;
    if let Some(n) = pv.as_i64() {
        return Some(n); // anciennes versions
    }
    pv.get("resource").and_then(|r| r.as_i64()) // 1.20.2+
        .or_else(|| pv.get("resource_major").and_then(|m| m.as_i64())) // 1.21.9+
}

/// Avant la 1.14, le jar n'indique pas le format : on le déduit du numéro de version.
fn format_depuis_nom(version_mc: &str) -> i64 {
    let morceaux: Vec<&str> = version_mc.split(['.', '-', ' ']).collect();
    if morceaux.len() < 2 || morceaux[0] != "1" {
        return 1;
    }
    match morceaux[1].parse::<u32>() {
        Ok(m) if m <= 8 => 1,
        Ok(m) if m <= 10 => 2,
        Ok(m) if m <= 12 => 3,
        Ok(_) => 4,
        Err(_) => 1,
    }
}

/// Crée le dossier du pack avec son pack.mcmeta. Renvoie le dossier.
pub fn creer_pack(nom: &str, description: &str, format: i64, version_mc: &str) -> anyhow::Result<PathBuf> {
    let nom = download::nom_fichier_sur(nom);
    let nom = nom.trim().trim_end_matches('.');
    if nom.is_empty() {
        anyhow::bail!(loc::t("Pack_NomInvalide"));
    }
    let dossier = dossier_packs().join(nom);
    if dossier.exists() {
        anyhow::bail!(loc::t("Pack_ExisteDeja"));
    }
    fs::create_dir_all(&dossier)?;

    let mut pack = serde_json::json!({ "pack_format": format, "description": description });
    // Depuis le format 65 (1.21.9), Minecraft exige min_format / max_format
    if format >= 65 {
        pack["min_format"] = format.into();
        pack["max_format"] = format.into();
    }
    fs::write(dossier.join("pack.mcmeta"), serde_json::to_string_pretty(&serde_json::json!({ "pack": pack }))?)?;

    // Infos propres à Misty (ignorées par Minecraft)
    fs::write(dossier.join("misty.json"), serde_json::json!({ "versionMc": version_mc }).to_string())?;
    Ok(dossier)
}

/// Remplace l'icône du pack (pack.png), redimensionnée en 128 px.
pub fn definir_icone(pack: &PackInfo, image: &Path) -> anyhow::Result<()> {
    enregistrer_icone(image, &pack.dossier.join("pack.png"))
}

/// Réencode une image en PNG de 128 px de large.
pub fn enregistrer_icone(source: &Path, destination: &Path) -> anyhow::Result<()> {
    let img = image::open(source)?;
    let img = if img.width() > 128 { img.resize(128, 128 * img.height() / img.width().max(1), image::imageops::FilterType::Lanczos3) } else { img };
    img.save_with_format(destination, image::ImageFormat::Png)?;
    Ok(())
}

/// Copie une texture du template vers le pack (si pas déjà fait) et l'ouvre dans l'éditeur externe.
/// `chemin_relatif` ex. : "assets/minecraft/textures/block/stone.png"
pub fn modifier_texture(dossier_pack: &Path, dossier_template: &Path, chemin_relatif: &str, editeur: Option<&str>) -> anyhow::Result<()> {
    let source = dossier_template.join(chemin_relatif);
    let copie = dossier_pack.join(chemin_relatif);
    if !copie.exists() {
        if let Some(parent) = copie.parent() {
            fs::create_dir_all(parent)?;
        }
        fs::copy(&source, &copie)?;
    }
    match editeur.filter(|e| !e.trim().is_empty()) {
        Some(e) => {
            std::process::Command::new(e).arg(&copie).spawn()?;
        }
        None => open::that_detached(&copie)?, // appli par défaut
    }
    Ok(())
}

/// Remet une texture à l'original : on supprime la copie du pack (et les dossiers devenus vides).
pub fn restaurer(dossier_pack: &Path, chemin_relatif: &str) -> anyhow::Result<()> {
    let copie = dossier_pack.join(chemin_relatif);
    if copie.exists() {
        fs::remove_file(&copie)?;
    }
    let mut dossier = copie.parent().map(Path::to_path_buf);
    while let Some(d) = dossier {
        if d.as_os_str().len() <= dossier_pack.as_os_str().len() || !d.exists() {
            break;
        }
        if fs::read_dir(&d).map(|mut e| e.next().is_some()).unwrap_or(true) {
            break;
        }
        fs::remove_dir(&d)?;
        dossier = d.parent().map(Path::to_path_buf);
    }
    Ok(())
}

/// Zippe le pack (pack.mcmeta à la racine du zip, comme Minecraft l'attend). misty.json est exclu.
pub fn exporter(pack: &PackInfo, destination: &Path) -> anyhow::Result<()> {
    if destination.exists() {
        fs::remove_file(destination)?; // l'utilisateur a déjà confirmé l'écrasement
    }
    let mut zip = zip::ZipWriter::new(fs::File::create(destination)?);
    let options = zip::write::SimpleFileOptions::default().compression_method(zip::CompressionMethod::Deflated);

    let mut fichiers = Vec::new();
    lister_fichiers(&pack.dossier, &mut fichiers);
    for fichier in fichiers {
        let relatif = fichier.strip_prefix(&pack.dossier)?.to_string_lossy().replace('\\', "/");
        if relatif.eq_ignore_ascii_case("misty.json") {
            continue;
        }
        zip.start_file(relatif, options)?;
        zip.write_all(&fs::read(&fichier)?)?;
    }

    // Icône du pack : celle de Minecraft, si le pack n'en a pas
    if !pack.a_pack_png() {
        let icone = dossier_templates().join(&pack.version_mc).join("pack.png");
        if icone.exists() {
            zip.start_file("pack.png", options)?;
            zip.write_all(&fs::read(icone)?)?;
        }
    }
    zip.finish()?;
    Ok(())
}

fn lister_fichiers(dossier: &Path, sortie: &mut Vec<PathBuf>) {
    let Ok(entrees) = fs::read_dir(dossier) else { return };
    for e in entrees.flatten() {
        let p = e.path();
        if p.is_dir() {
            lister_fichiers(&p, sortie);
        } else {
            sortie.push(p);
        }
    }
}

pub fn lister() -> Vec<PackInfo> {
    let mut liste = Vec::new();
    let Ok(entrees) = fs::read_dir(dossier_packs()) else { return liste };
    for entree in entrees.flatten() {
        let dossier = entree.path();
        if !dossier.is_dir() {
            continue;
        }
        // mcmeta / misty.json illisibles : on affiche quand même le pack
        let description = fs::read_to_string(dossier.join("pack.mcmeta"))
            .ok()
            .and_then(|t| serde_json::from_str::<serde_json::Value>(&t).ok())
            .and_then(|d| d["pack"]["description"].as_str().map(str::to_string))
            .unwrap_or_default();
        let version_mc = fs::read_to_string(dossier.join("misty.json"))
            .ok()
            .and_then(|t| serde_json::from_str::<serde_json::Value>(&t).ok())
            .and_then(|d| d["versionMc"].as_str().map(str::to_string))
            .unwrap_or_default();

        let mut pngs = Vec::new();
        lister_fichiers(&dossier.join("assets").join("minecraft").join("textures"), &mut pngs);
        let nb = pngs.iter().filter(|p| p.extension().is_some_and(|e| e.eq_ignore_ascii_case("png"))).count();

        liste.push(PackInfo {
            nom: entree.file_name().to_string_lossy().to_string(),
            description,
            dossier,
            nb_textures: nb,
            version_mc,
        });
    }
    liste.sort_by_key(|p| p.nom.to_lowercase());
    liste
}

fn fichier_editeur() -> PathBuf {
    paths::data_dir().join("editeur.txt")
}

/// Chemin de l'éditeur d'images choisi (ex. paintdotnet.exe), ou None.
pub fn editeur() -> Option<String> {
    fs::read_to_string(fichier_editeur()).ok().map(|t| t.trim().to_string()).filter(|t| !t.is_empty())
}

pub fn definir_editeur(chemin: &str) {
    let _ = fs::write(fichier_editeur(), chemin);
}

/// Toutes les textures PNG du template, ex. "block/stone.png".
pub fn lister_textures(dossier_template: &Path) -> Vec<String> {
    let racine = dossier_template.join("assets").join("minecraft").join("textures");
    let mut fichiers = Vec::new();
    lister_fichiers(&racine, &mut fichiers);
    let mut liste: Vec<String> = fichiers
        .into_iter()
        .filter(|f| f.extension().is_some_and(|e| e.eq_ignore_ascii_case("png")))
        .filter_map(|f| f.strip_prefix(&racine).ok().map(|r| r.to_string_lossy().replace('\\', "/")))
        .collect();
    liste.sort();
    liste
}
