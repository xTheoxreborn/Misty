//! Tous les chemins utilisés par le launcher.

use std::path::PathBuf;
use std::sync::OnceLock;

/// Dossier de données : %LOCALAPPDATA%\Misty (contient aussi le .minecraft).
pub fn data_dir() -> PathBuf {
    static DIR: OnceLock<PathBuf> = OnceLock::new();
    DIR.get_or_init(|| {
        dirs::data_local_dir()
            .unwrap_or_else(|| PathBuf::from("."))
            .join("Misty")
    })
    .clone()
}

pub fn data_file() -> PathBuf {
    data_dir().join("data.txt")
}

pub fn profiles_file() -> PathBuf {
    data_dir().join("listpseudo.txt")
}

/// Ancien dossier de données (avant 0.2.3.9.5), migré au démarrage.
pub const LEGACY_DATA_DIR: &str = r"C:\TEXT\";

/// Chemin de l'exécutable en cours.
pub fn app_exe() -> PathBuf {
    std::env::current_exe().unwrap_or_else(|_| PathBuf::from("Misty.exe"))
}

/// Dossier de l'exécutable.
pub fn app_dir() -> PathBuf {
    app_exe()
        .parent()
        .map(|p| p.to_path_buf())
        .unwrap_or_else(|| PathBuf::from("."))
}

/// Icône personnalisée optionnelle posée à côté de l'exe.
pub fn custom_icon() -> PathBuf {
    app_dir().join("Icon.ico")
}
