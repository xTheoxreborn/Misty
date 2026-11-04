//! Installation et lancement de Minecraft (ce que faisait CmlLib.Core en C#).
//!
//! Organisation des fichiers (identique au .minecraft officiel) :
//!   %LOCALAPPDATA%\Misty\versions\<id>\<id>.json / .jar   versions installées
//!   %LOCALAPPDATA%\Misty\libraries\...                     bibliothèques Java (format Maven)
//!   %LOCALAPPDATA%\Misty\assets\indexes|objects            sons, langues, textures du jeu
//!   %LOCALAPPDATA%\Misty\runtime\<composant>\              Java fourni par Mojang
//! Le dossier de jeu (mods, options, mondes) est celui du profil, ou Misty lui-même en jeu rapide.

pub mod install;
pub mod java;
pub mod launch;
pub mod loaders;
pub mod version;

use std::path::PathBuf;
use std::sync::Arc;

/// Avancement des téléchargements : (fichiers faits, total, nom du fichier en cours).
pub type FileProgress = Arc<dyn Fn(usize, usize, &str) + Send + Sync>;

/// Dossiers partagés de Minecraft.
#[derive(Clone, Debug)]
pub struct McDirs {
    pub root: PathBuf,
}

impl McDirs {
    pub fn misty() -> Self {
        McDirs { root: crate::services::paths::data_dir() }
    }
    pub fn versions(&self) -> PathBuf {
        self.root.join("versions")
    }
    pub fn version_json(&self, id: &str) -> PathBuf {
        self.versions().join(id).join(format!("{id}.json"))
    }
    pub fn version_jar(&self, id: &str) -> PathBuf {
        self.versions().join(id).join(format!("{id}.jar"))
    }
    pub fn libraries(&self) -> PathBuf {
        self.root.join("libraries")
    }
    pub fn assets(&self) -> PathBuf {
        self.root.join("assets")
    }
    pub fn runtime(&self) -> PathBuf {
        self.root.join("runtime")
    }
    pub fn natives(&self, id: &str) -> PathBuf {
        self.versions().join(id).join("natives")
    }
}

/// "group:artifact:version[:classifier][@ext]" → "group/path/artifact/version/artifact-version[-classifier].ext"
pub fn maven_path(nom: &str) -> Option<String> {
    let (coords, ext) = match nom.split_once('@') {
        Some((c, e)) => (c, e),
        None => (nom, "jar"),
    };
    let parties: Vec<&str> = coords.split(':').collect();
    if parties.len() < 3 {
        return None;
    }
    let (groupe, artefact, version) = (parties[0].replace('.', "/"), parties[1], parties[2]);
    let classifieur = parties.get(3).map(|c| format!("-{c}")).unwrap_or_default();
    Some(format!("{groupe}/{artefact}/{version}/{artefact}-{version}{classifieur}.{ext}"))
}

/// Exécution d'une commande sans fenêtre de console.
pub fn sans_console(cmd: &mut std::process::Command) -> &mut std::process::Command {
    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        cmd.creation_flags(0x0800_0000); // CREATE_NO_WINDOW
    }
    cmd
}
