//! Services : toute la logique du launcher, sans interface.
//! Chaque fichier correspond à un service de l'ancienne version C#.

pub mod account;
pub mod app_info;
pub mod catalogue;
pub mod content;
pub mod curseforge;
pub mod discord;
pub mod download;
pub mod http;
pub mod instances;
pub mod loc;
pub mod modpack;
pub mod modrinth;
pub mod packs;
pub mod paths;
pub mod pseudos;
pub mod settings;
pub mod shortcut;
pub mod system_info;
pub mod texture_categories;
pub mod update;
pub mod version_compat;

/// Fonction de progression (texte affiché à l'utilisateur), utilisable depuis n'importe quel thread.
pub type Progress = std::sync::Arc<dyn Fn(String) + Send + Sync>;

