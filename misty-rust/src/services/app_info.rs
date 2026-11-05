//! Constantes globales de l'application.

/// Version du launcher. Doit correspondre au tag de la release GitHub (sans le "v")
/// et au nom du dossier d'installation "Misty-{VERSION}".
pub const VERSION: &str = "0.3.2";
/// Version du format de data.txt. À incrémenter quand on ajoute/retire une clé.
pub const DATA_VERSION: &str = "1.2.4";
pub const GITHUB_REPO: &str = "xTheoxreborn/Misty";
pub const DISCORD_APP_ID: &str = "1536551827640680528";
pub const RAM_PAR_DEFAUT_MO: u32 = 4096;

/// User-Agent demandé par Modrinth : "auteur/projet/version (contact)".
pub fn user_agent() -> String {
    format!("{GITHUB_REPO}/{VERSION} (github.com/{GITHUB_REPO})")
}
