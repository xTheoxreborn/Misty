//! Recherche de contenu, quelle que soit la plateforme (Modrinth ou CurseForge).

use super::content::{self, MOD};
use super::instances::Instance;
use super::{curseforge, loc, modrinth};
use crate::minecraft::loaders;

pub const MODRINTH: &str = "modrinth";
pub const CURSEFORGE: &str = "curseforge";

pub fn libelle_source(source: &str) -> &'static str {
    if source == CURSEFORGE {
        "CurseForge"
    } else {
        "Modrinth"
    }
}

/// Un projet (mod, pack, modpack…) trouvé sur Modrinth ou CurseForge, tel qu'affiché dans l'explorateur.
#[derive(Clone, Debug, Default)]
pub struct ProjetDistant {
    pub source: String,
    /// ID du projet sur sa plateforme (numérique pour CurseForge).
    pub id: String,
    pub titre: String,
    pub description: String,
    pub auteur: String,
    pub telechargements: i64,
    pub icone_url: Option<String>,
    pub categories: Vec<String>,
}

/// Tris proposés par la plateforme : (clé API, libellé traduit).
pub fn tris(source: &str) -> Vec<(String, String)> {
    let liste = if source == CURSEFORGE { curseforge::TRIS } else { modrinth::TRIS };
    liste.iter().map(|(cle, libelle)| (cle.to_string(), loc::t(libelle))).collect()
}

/// `instance` : profil cible (filtre version + loader), ou None pour les modpacks.
pub async fn rechercher(
    source: &str,
    requete: &str,
    type_: &str,
    instance: Option<&Instance>,
    tri: &str,
    offset: usize,
    limite: usize,
) -> anyhow::Result<(Vec<ProjetDistant>, i64)> {
    if source == CURSEFORGE {
        // Sur CurseForge, les mods Quilt sont presque tous publiés comme mods Fabric
        let loader = match instance {
            Some(i) if type_ == MOD => {
                let l = if i.loader == loaders::QUILT { loaders::FABRIC } else { i.loader.as_str() };
                curseforge::type_loader(l)
            }
            _ => None,
        };
        let (mods, total) = curseforge::rechercher(
            requete,
            curseforge::classe(type_),
            instance.map(|i| i.version_mc.as_str()),
            loader,
            tri,
            offset,
            limite,
        )
        .await?;
        return Ok((mods.into_iter().map(curseforge::vers_projet).collect(), total));
    }

    let categories = instance.and_then(|i| content::categories_recherche(i, type_));
    let reponse = modrinth::rechercher(
        requete,
        type_,
        instance.map(|i| i.version_mc.as_str()),
        categories.as_deref(),
        tri,
        offset,
        limite,
    )
    .await?;
    Ok((reponse.hits.into_iter().map(modrinth::vers_projet).collect(), reponse.total_hits))
}
