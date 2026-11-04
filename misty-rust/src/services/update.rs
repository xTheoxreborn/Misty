//! Mise à jour automatique via les releases GitHub.
//!
//! Fonctionnement : le launcher est installé dans un dossier "Misty-{version}". La mise à jour
//! télécharge le zip de la dernière release à côté, l'extrait (=> "Misty-{nouvelle}"), note l'ancien
//! dossier dans data.txt (clé Suppr) puis lance le nouvel exe. Au démarrage suivant, le nouvel exe
//! supprime l'ancien dossier et met à jour le raccourci.

use super::{app_info, http, paths, settings, shortcut};
use std::fs;
use std::path::{Path, PathBuf};

pub struct ReleaseInfo {
    pub version: String,
    pub url_telechargement: String,
}

/// Renvoie la dernière release si elle est plus récente que la version actuelle, sinon None.
pub async fn verifier() -> anyhow::Result<Option<ReleaseInfo>> {
    // GitHub refuse les requêtes sans User-Agent (le client partagé en a un)
    let doc: serde_json::Value =
        http::get_json(&format!("https://api.github.com/repos/{}/releases/latest", app_info::GITHUB_REPO)).await?;

    let version = doc["tag_name"].as_str().unwrap_or("").trim_start_matches('v').to_string();
    // Le zip "Misty-x.y.zip" (la release peut aussi contenir l'installateur .exe)
    let url = doc["assets"]
        .as_array()
        .into_iter()
        .flatten()
        .filter_map(|a| a["browser_download_url"].as_str())
        .find(|u| u.to_lowercase().ends_with(".zip"))
        .map(str::to_string);

    match url {
        Some(url) if !version.is_empty() && est_plus_recente(&version, app_info::VERSION) => {
            Ok(Some(ReleaseInfo { version, url_telechargement: url }))
        }
        _ => Ok(None),
    }
}

/// Compare deux versions segment par segment ("0.2.3.9.5.1" > "0.2.3.9.5").
pub fn est_plus_recente(distante: &str, locale: &str) -> bool {
    // "3v2" -> 3 : on ne garde que les chiffres de tête de chaque segment
    let segments = |v: &str| -> Vec<u64> {
        v.split('.')
            .map(|s| s.chars().take_while(char::is_ascii_digit).collect::<String>().parse().unwrap_or(0))
            .collect()
    };
    let (a, b) = (segments(distante), segments(locale));
    for i in 0..a.len().max(b.len()) {
        let (x, y) = (a.get(i).copied().unwrap_or(0), b.get(i).copied().unwrap_or(0));
        if x != y {
            return x > y;
        }
    }
    false
}

/// Dossier qui contient les dossiers "Misty-{version}".
fn racine() -> PathBuf {
    let dossier_actuel = format!("Misty-{}", app_info::VERSION);
    let app = paths::app_dir().to_string_lossy().to_string();
    match app.find(&dossier_actuel) {
        Some(i) => PathBuf::from(&app[..i]),
        None => PathBuf::from(app),
    }
}

/// Télécharge, extrait et lance la nouvelle version. L'appelant doit ensuite quitter l'application.
pub async fn installer(release: &ReleaseInfo) -> anyhow::Result<()> {
    let racine = racine();
    let zip = racine.join(format!("Misty-{}.zip", release.version));
    let nouveau_dossier = racine.join(format!("Misty-{}", release.version));

    super::download::fichier(&release.url_telechargement, &zip, None).await?;
    {
        let zip = zip.clone();
        let racine = racine.clone();
        tokio::task::spawn_blocking(move || -> anyhow::Result<()> {
            let mut archive = zip::ZipArchive::new(fs::File::open(&zip)?)?;
            archive.extract(&racine)?;
            Ok(())
        })
        .await??;
    }
    let _ = fs::remove_file(&zip);

    settings::set(settings::SUPPR, &racine.join(format!("Misty-{}", app_info::VERSION)).to_string_lossy());

    if paths::custom_icon().exists() {
        let _ = fs::copy(paths::custom_icon(), nouveau_dossier.join("Icon.ico"));
    }

    std::process::Command::new(nouveau_dossier.join("Misty.exe")).current_dir(&nouveau_dossier).spawn()?;
    Ok(())
}

/// Après une mise à jour : supprime l'ancien dossier et remet le raccourci sur le nouvel exe.
/// Renvoie true si une mise à jour vient d'être finalisée.
pub async fn finaliser() -> anyhow::Result<bool> {
    let ancien = settings::get(settings::SUPPR);
    if ancien.is_empty() {
        return Ok(false);
    }

    tokio::time::sleep(std::time::Duration::from_secs(1)).await; // laisse le temps à l'ancien exe de se fermer

    let raccourci = settings::get(settings::RACCOURCI);
    if !raccourci.is_empty() {
        let _ = shortcut::creer(Path::new(&raccourci));
    }

    // Installé avec le setup : ses raccourcis (menu Démarrer, bureau) et l'entrée
    // "Applications installées" pointent encore vers l'ancien dossier
    shortcut::rediriger(&ancien);
    maj_entree_desinstallation();

    if Path::new(&ancien).exists() {
        fs::remove_dir_all(&ancien)?;
    }
    settings::set(settings::SUPPR, "");
    Ok(true)
}

/// ID de l'installateur Inno Setup (installer\Misty.iss).
#[cfg(windows)]
const CLE_DESINSTALLATION: &str = r"Software\Microsoft\Windows\CurrentVersion\Uninstall\{8F4C2A61-3B7E-4D19-9A55-6C1E0F2B7D43}_is1";

/// Met à jour la version et l'icône affichées dans "Applications installées".
#[cfg(windows)]
fn maj_entree_desinstallation() {
    use winreg::enums::{HKEY_CURRENT_USER, KEY_SET_VALUE};
    let Ok(cle) = winreg::RegKey::predef(HKEY_CURRENT_USER).open_subkey_with_flags(CLE_DESINSTALLATION, KEY_SET_VALUE) else { return };
    let _ = cle.set_value("DisplayVersion", &app_info::VERSION);
    let _ = cle.set_value("DisplayIcon", &paths::app_exe().to_string_lossy().to_string());
}

#[cfg(not(windows))]
fn maj_entree_desinstallation() {}

/// Migre l'ancien dossier de données C:\TEXT vers %LOCALAPPDATA%\Misty.
/// Renvoie true si une migration a eu lieu.
pub fn migrer_ancien_dossier() -> anyhow::Result<bool> {
    let ancien = Path::new(paths::LEGACY_DATA_DIR);
    if !ancien.exists() {
        return Ok(false);
    }
    if !paths::data_dir().exists() {
        copier_dossier(ancien, &paths::data_dir())?;
    }
    fs::remove_dir_all(ancien)?;
    Ok(true)
}

fn copier_dossier(source: &Path, destination: &Path) -> anyhow::Result<()> {
    fs::create_dir_all(destination)?;
    for entree in fs::read_dir(source)?.flatten() {
        let cible = destination.join(entree.file_name());
        if entree.path().is_dir() {
            copier_dossier(&entree.path(), &cible)?;
        } else {
            fs::copy(entree.path(), cible)?;
        }
    }
    Ok(())
}
