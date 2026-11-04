//! Téléchargement de fichiers avec vérification d'intégrité (SHA-1).

use super::{http, loc};
use futures::StreamExt;
use sha1::{Digest, Sha1};
use std::path::Path;
use tokio::io::AsyncWriteExt;

/// Télécharge un fichier (via un .part) et vérifie son SHA-1 s'il est connu.
pub async fn fichier(url: &str, destination: &Path, sha1: Option<&str>) -> anyhow::Result<()> {
    if let Some(parent) = destination.parent() {
        tokio::fs::create_dir_all(parent).await?;
    }
    let mut temporaire = destination.as_os_str().to_owned();
    temporaire.push(".part");
    let temporaire = std::path::PathBuf::from(temporaire);

    let reponse = http::client().get(url).send().await?.error_for_status()?;
    let mut flux = reponse.bytes_stream();
    let mut hasher = Sha1::new();
    {
        let mut sortie = tokio::fs::File::create(&temporaire).await?;
        while let Some(morceau) = flux.next().await {
            let morceau = morceau?;
            hasher.update(&morceau);
            sortie.write_all(&morceau).await?;
        }
        sortie.flush().await?;
    }

    if let Some(attendu) = sha1.filter(|s| !s.is_empty()) {
        let obtenu = hex::encode(hasher.finalize());
        if !obtenu.eq_ignore_ascii_case(attendu) {
            let _ = tokio::fs::remove_file(&temporaire).await;
            let nom = destination.file_name().map(|n| n.to_string_lossy().to_string()).unwrap_or_default();
            anyhow::bail!(loc::tf("Erreur_Corrompu", &[&nom]));
        }
    }

    // Remplace l'éventuel fichier existant
    let _ = tokio::fs::remove_file(destination).await;
    tokio::fs::rename(&temporaire, destination).await?;
    Ok(())
}

/// SHA-1 d'un fichier, en hexadécimal minuscule.
pub fn sha1_fichier(chemin: &Path) -> std::io::Result<String> {
    use std::io::Read;
    let mut fichier = std::fs::File::open(chemin)?;
    let mut hasher = Sha1::new();
    let mut tampon = vec![0u8; 64 * 1024];
    loop {
        let n = fichier.read(&mut tampon)?;
        if n == 0 {
            break;
        }
        hasher.update(&tampon[..n]);
    }
    Ok(hex::encode(hasher.finalize()))
}

/// Remplace les caractères interdits dans un nom de fichier Windows.
pub fn nom_fichier_sur(nom: &str) -> String {
    let nom = Path::new(nom).file_name().map(|n| n.to_string_lossy().to_string()).unwrap_or_default();
    nom.chars()
        .map(|c| if matches!(c, '<' | '>' | ':' | '"' | '/' | '\\' | '|' | '?' | '*') || c.is_control() { '_' } else { c })
        .collect()
}
