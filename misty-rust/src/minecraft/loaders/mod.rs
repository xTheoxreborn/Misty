//! Détection et installation des mod loaders (Forge, NeoForge, Fabric, Quilt, OptiFine, LiteLoader).

mod fabric;
mod forge;
mod liteloader;
mod optifine;

use super::{install, FileProgress, McDirs};
use crate::services::version_compat;

pub const VANILLA: &str = "Vanilla";
pub const FORGE: &str = "Forge";
pub const NEOFORGE: &str = "NeoForge";
pub const FABRIC: &str = "Fabric";
pub const OPTIFINE: &str = "OptiFine";
pub const LITELOADER: &str = "LiteLoader";
pub const QUILT: &str = "Quilt";

/// Liste des modes disponibles pour une version de Minecraft (Vanilla toujours en premier).
/// Un loader injoignable ou sans version pour ce Minecraft n'est simplement pas proposé.
pub async fn modes_disponibles(version: &str) -> Vec<String> {
    let (forge, optifine, neoforge, fabric, lite) = tokio::join!(
        async { version_compat::forge(version) && forge::disponible(forge::Sorte::Forge, version).await },
        async { version_compat::optifine(version) && optifine::disponible(version).await },
        async { version_compat::neoforge(version) && forge::disponible(forge::Sorte::NeoForge, version).await },
        async { version_compat::fabric(version) && fabric::disponible(fabric::Sorte::Fabric, version).await },
        async { version_compat::liteloader(version) && liteloader::disponible(version).await },
    );
    let mut modes = vec![VANILLA.to_string()];
    for (ok, nom) in [(forge, FORGE), (optifine, OPTIFINE), (neoforge, NEOFORGE), (fabric, FABRIC), (lite, LITELOADER)] {
        if ok {
            modes.push(nom.to_string());
        }
    }
    modes
}

/// Installe la version + le loader choisi (version du loader précise si fournie, pour les modpacks).
/// Renvoie le nom de version à lancer.
pub async fn installer(
    dirs: &McDirs,
    version: &str,
    mode: &str,
    version_loader: Option<&str>,
    progression: FileProgress,
) -> anyhow::Result<String> {
    // Toujours la version vanilla d'abord (le jar sert aux installateurs)
    install::installer(dirs, version, progression.clone()).await?;

    let nom = match mode {
        FORGE => forge::installer(dirs, forge::Sorte::Forge, version, version_loader, &progression).await?,
        NEOFORGE => forge::installer(dirs, forge::Sorte::NeoForge, version, version_loader, &progression).await?,
        FABRIC => fabric::installer(dirs, fabric::Sorte::Fabric, version, version_loader).await?,
        QUILT => fabric::installer(dirs, fabric::Sorte::Quilt, version, version_loader).await?,
        OPTIFINE => optifine::installer(dirs, version, &progression).await?,
        LITELOADER => liteloader::installer(dirs, version).await?,
        _ => version.to_string(),
    };
    Ok(nom)
}
