//! Informations système : RAM, taille de dossier, ouverture de dossiers et de liens.

use std::path::Path;

/// RAM physique totale, en Mo.
#[cfg(windows)]
pub fn ram_totale_mo() -> u32 {
    use windows_sys::Win32::System::SystemInformation::{GlobalMemoryStatusEx, MEMORYSTATUSEX};
    // SAFETY : structure initialisée à zéro avec sa taille, comme l'exige l'API Windows.
    unsafe {
        let mut status: MEMORYSTATUSEX = std::mem::zeroed();
        status.dwLength = std::mem::size_of::<MEMORYSTATUSEX>() as u32;
        if GlobalMemoryStatusEx(&mut status) == 0 {
            return 8192;
        }
        (status.ullTotalPhys / 1024 / 1024) as u32
    }
}

#[cfg(not(windows))]
pub fn ram_totale_mo() -> u32 {
    8192
}

/// RAM maximale proposée : la RAM totale arrondie au Go supérieur, moins 1 Go pour le système.
pub fn ram_max_proposee_mo() -> u32 {
    let total_go = (ram_totale_mo() as f64 / 1024.0).ceil() as u32;
    total_go.saturating_sub(1).max(1) * 1024
}

/// Taille totale d'un dossier en octets (0 s'il n'existe pas).
pub fn taille_dossier(dossier: &Path) -> u64 {
    let Ok(entrees) = std::fs::read_dir(dossier) else { return 0 };
    let mut total = 0;
    for entree in entrees.flatten() {
        let Ok(meta) = entree.metadata() else { continue };
        if meta.is_dir() {
            total += taille_dossier(&entree.path());
        } else {
            total += meta.len();
        }
    }
    total
}

/// Ouvre un dossier dans l'explorateur (le crée s'il n'existe pas).
pub fn ouvrir_dossier(dossier: &Path) {
    let _ = std::fs::create_dir_all(dossier);
    let _ = std::process::Command::new("explorer.exe").arg(dossier).spawn();
}

/// Ouvre l'explorateur avec un fichier sélectionné.
pub fn montrer_fichier(fichier: &Path) {
    let _ = std::process::Command::new("explorer.exe")
        .arg(format!("/select,{}", fichier.display()))
        .spawn();
}

/// Ouvre une URL (ou un fichier) avec l'application par défaut.
pub fn ouvrir(cible: &str) {
    let _ = open::that_detached(cible);
}
