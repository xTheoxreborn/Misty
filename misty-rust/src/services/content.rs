//! Contenu d'un profil : mods, packs de ressources, datapacks, shaders.
//! Installation depuis Modrinth ou CurseForge (avec dépendances), liste, activation/désactivation, suppression.

use super::catalogue::{CURSEFORGE, MODRINTH};
use super::instances::Instance;
use super::{curseforge, download, loc, modrinth, Progress};
use crate::minecraft::loaders;
use serde::{Deserialize, Serialize};
use std::collections::{HashMap, HashSet};
use std::fs;
use std::future::Future;
use std::path::{Path, PathBuf};
use std::pin::Pin;

pub const MOD: &str = "mod";
pub const PACK_RESSOURCES: &str = "resourcepack";
pub const DATAPACK: &str = "datapack";
pub const SHADER: &str = "shader";
pub const MODPACK: &str = "modpack";

/// Un fichier désactivé est renommé "xxx.jar.disabled" (même convention que Modrinth App / Prism).
pub const SUFFIXE_DESACTIVE: &str = ".disabled";

const FICHIER_META: &str = "misty-contenu.json";

// Projets Modrinth utiles pour les shaders
pub const ID_IRIS: &str = "YL57xq9U";
pub const ID_OCULUS: &str = "GchcoXML";

/// Métadonnées d'un fichier installé depuis Modrinth / CurseForge (ou identifié par son hash).
/// Stockées dans instances\{Id}\misty-contenu.json (noms de champs de la version C#).
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(rename_all = "PascalCase")]
pub struct ContenuMeta {
    /// Chemin relatif au dossier du profil, sans le suffixe ".disabled".
    #[serde(default)]
    pub chemin: String,
    /// modrinth ou curseforge (None = modrinth, fichiers créés avant CurseForge).
    pub source: Option<String>,
    pub project_id: Option<String>,
    pub version_id: Option<String>,
    #[serde(default)]
    pub titre: String,
    pub version_nom: Option<String>,
    pub icone_url: Option<String>,
    /// Le champ JSON s'appelle "Type" : `type` est un mot réservé en Rust.
    #[serde(default, rename = "Type")]
    pub type_: String,
}

impl ContenuMeta {
    pub fn correspond(&self, source: &str, id: &str) -> bool {
        self.source.as_deref().unwrap_or(MODRINTH) == source && self.project_id.as_deref() == Some(id)
    }
}

/// Un fichier présent dans le profil (mod, pack de ressources, datapack, shader).
#[derive(Clone, Debug)]
pub struct ContenuInstalle {
    pub chemin_fichier: PathBuf,
    pub type_: String,
    pub monde: Option<String>,
    pub meta: Option<ContenuMeta>,
}

impl ContenuInstalle {
    pub fn nom_fichier(&self) -> String {
        self.chemin_fichier.file_name().map(|n| n.to_string_lossy().to_string()).unwrap_or_default()
    }

    pub fn titre(&self) -> String {
        match &self.meta {
            Some(m) if !m.titre.is_empty() => m.titre.clone(),
            _ => nom_sans_suffixe(&self.nom_fichier()),
        }
    }

    pub fn details(&self) -> String {
        let mut morceaux = vec![libelle_type(&self.type_)];
        if let Some(v) = self.meta.as_ref().and_then(|m| m.version_nom.clone()).filter(|v| !v.is_empty()) {
            morceaux.push(v);
        }
        if self.meta.as_ref().and_then(|m| m.source.as_deref()) == Some(CURSEFORGE) {
            morceaux.push("CurseForge".into());
        }
        if let Some(monde) = &self.monde {
            morceaux.push(loc::tf("Contenu_Monde", &[monde]));
        }
        if self.meta.as_ref().and_then(|m| m.project_id.as_ref()).is_none() {
            morceaux.push(self.nom_fichier());
        }
        morceaux.join(" · ")
    }

    pub fn actif(&self) -> bool {
        !self.nom_fichier().to_lowercase().ends_with(SUFFIXE_DESACTIVE)
    }

    pub fn icone_url(&self) -> Option<String> {
        self.meta.as_ref().and_then(|m| m.icone_url.clone())
    }
}

/// Types installables dans un profil, avec leur libellé dans la langue actuelle.
pub fn types() -> Vec<(String, String)> {
    vec![
        (MOD.into(), loc::t("Type_Mods")),
        (PACK_RESSOURCES.into(), loc::t("Type_Packs")),
        (DATAPACK.into(), loc::t("Type_Datapacks")),
        (SHADER.into(), loc::t("Type_Shaders")),
    ]
}

pub fn libelle_type(type_: &str) -> String {
    match type_ {
        MOD => loc::t("Type_Mod"),
        PACK_RESSOURCES => loc::t("Type_Pack"),
        DATAPACK => loc::t("Type_Datapack"),
        SHADER => loc::t("Type_Shader"),
        MODPACK => loc::t("Type_Modpack"),
        autre => autre.to_string(),
    }
}

/// "mod" / "mods" selon le nombre, en minuscules (ex. "3 mods").
pub fn libelle_nombre(type_: &str, n: usize) -> String {
    let pluriel = n > 1;
    match type_ {
        MOD => loc::t(if pluriel { "Nb_Mods" } else { "Nb_Mod" }),
        PACK_RESSOURCES => loc::t(if pluriel { "Nb_Packs" } else { "Nb_Pack" }),
        DATAPACK => loc::t(if pluriel { "Nb_Datapacks" } else { "Nb_Datapack" }),
        SHADER => loc::t(if pluriel { "Nb_Shaders" } else { "Nb_Shader" }),
        autre => autre.to_string(),
    }
}

pub fn supporte_mods(loader: &str) -> bool {
    matches!(loader, loaders::FABRIC | loaders::QUILT | loaders::FORGE | loaders::NEOFORGE | loaders::LITELOADER)
}

/// Loaders Modrinth compatibles avec le profil pour ce type de contenu (None = pas de filtre).
pub fn loaders_modrinth(instance: &Instance, type_: &str) -> Option<Vec<&'static str>> {
    match type_ {
        MOD => Some(match instance.loader.as_str() {
            loaders::FABRIC => vec!["fabric"],
            loaders::QUILT => vec!["quilt", "fabric"], // Quilt charge aussi les mods Fabric
            loaders::FORGE => vec!["forge"],
            loaders::NEOFORGE => vec!["neoforge"],
            loaders::LITELOADER => vec!["liteloader"],
            _ => vec!["__aucun__"],
        }),
        PACK_RESSOURCES => Some(vec!["minecraft"]),
        DATAPACK => Some(vec!["datapack"]),
        SHADER => Some(if instance.loader == loaders::OPTIFINE { vec!["optifine"] } else { vec!["iris", "optifine"] }),
        _ => None,
    }
}

/// Catégories à passer à la recherche (le loader pour les mods et shaders).
pub fn categories_recherche(instance: &Instance, type_: &str) -> Option<Vec<&'static str>> {
    if type_ == MOD || type_ == SHADER {
        loaders_modrinth(instance, type_)
    } else {
        None
    }
}

pub fn dossier(instance: &Instance, type_: &str, monde: Option<&str>) -> anyhow::Result<PathBuf> {
    let base = instance.dossier();
    Ok(match type_ {
        MOD => base.join("mods"),
        PACK_RESSOURCES => base.join("resourcepacks"),
        SHADER => base.join("shaderpacks"),
        DATAPACK => base
            .join("saves")
            .join(monde.ok_or_else(|| anyhow::anyhow!("Monde requis pour un datapack"))?)
            .join("datapacks"),
        autre => anyhow::bail!("Type inconnu : {autre}"),
    })
}

/// Mondes du profil, du plus récent au plus ancien.
pub fn mondes(instance: &Instance) -> Vec<String> {
    let Ok(entrees) = fs::read_dir(instance.dossier().join("saves")) else { return Vec::new() };
    let mut liste: Vec<(std::time::SystemTime, String)> = entrees
        .flatten()
        .filter(|e| e.path().is_dir())
        .map(|e| {
            let date = e.metadata().and_then(|m| m.modified()).unwrap_or(std::time::UNIX_EPOCH);
            (date, e.file_name().to_string_lossy().to_string())
        })
        .collect();
    liste.sort_by(|a, b| b.0.cmp(&a.0));
    liste.into_iter().map(|(_, n)| n).collect()
}

pub fn nom_sans_suffixe(nom_fichier: &str) -> String {
    let mut nom = nom_fichier;
    if nom.to_lowercase().ends_with(SUFFIXE_DESACTIVE) {
        nom = &nom[..nom.len() - SUFFIXE_DESACTIVE.len()];
    }
    match nom.rfind('.') {
        Some(i) if i > 0 => nom[..i].to_string(),
        _ => nom.to_string(),
    }
}

// ───────────────────────── Liste ─────────────────────────

pub fn lister(instance: &Instance) -> Vec<ContenuInstalle> {
    let mut metas: HashMap<String, ContenuMeta> = HashMap::new();
    for m in lire_meta(instance) {
        metas.insert(m.chemin.to_lowercase(), m);
    }
    let mut resultat = Vec::new();

    let mut scanner = |dossier: PathBuf, type_: &str, extensions: &[&str], monde: Option<String>| {
        let Ok(entrees) = fs::read_dir(&dossier) else { return };
        for entree in entrees.flatten() {
            let chemin = entree.path();
            if !chemin.is_file() {
                continue;
            }
            let nom = entree.file_name().to_string_lossy().to_lowercase();
            let nom = nom.strip_suffix(SUFFIXE_DESACTIVE).unwrap_or(&nom);
            if !extensions.iter().any(|e| nom.ends_with(e)) {
                continue;
            }
            let meta = metas.get(&relatif(instance, &chemin).to_lowercase()).cloned();
            resultat.push(ContenuInstalle { chemin_fichier: chemin, type_: type_.to_string(), monde: monde.clone(), meta });
        }
    };

    let base = instance.dossier();
    scanner(base.join("mods"), MOD, &[".jar", ".litemod"], None);
    scanner(base.join("resourcepacks"), PACK_RESSOURCES, &[".zip"], None);
    scanner(base.join("shaderpacks"), SHADER, &[".zip"], None);
    for monde in mondes(instance) {
        if let Ok(d) = dossier(instance, DATAPACK, Some(&monde)) {
            scanner(d, DATAPACK, &[".zip"], Some(monde));
        }
    }

    resultat.sort_by(|a, b| a.type_.cmp(&b.type_).then_with(|| a.titre().to_lowercase().cmp(&b.titre().to_lowercase())));
    resultat
}

/// Indique si un projet (d'une source donnée) est présent dans la liste.
pub fn est_installe(contenus: &[ContenuInstalle], source: &str, id: &str) -> bool {
    contenus.iter().any(|c| c.meta.as_ref().is_some_and(|m| m.correspond(source, id)))
}

/// Identifie les fichiers ajoutés à la main, pour afficher leur nom et leur icône :
/// d'abord via Modrinth (SHA-1), puis via CurseForge (empreinte) pour ceux qui restent inconnus.
/// Les fichiers introuvables sont mémorisés pour ne pas être re-hachés à chaque fois.
/// Renvoie la liste mise à jour.
pub async fn identifier(instance: &Instance, mut contenus: Vec<ContenuInstalle>) -> anyhow::Result<Vec<ContenuInstalle>> {
    let inconnus: Vec<usize> = (0..contenus.len()).filter(|&i| contenus[i].meta.is_none()).collect();
    if inconnus.is_empty() {
        return Ok(contenus);
    }

    // Chaque fichier inconnu reçoit une métadonnée (vide si on ne le trouve nulle part)
    for &i in &inconnus {
        let c = &mut contenus[i];
        c.meta = Some(ContenuMeta { chemin: relatif(instance, &c.chemin_fichier), type_: c.type_.clone(), ..Default::default() });
    }

    // 1. Modrinth
    let mut par_hash: HashMap<String, usize> = HashMap::new();
    for &i in &inconnus {
        let chemin = contenus[i].chemin_fichier.clone();
        if let Ok(Ok(h)) = tokio::task::spawn_blocking(move || download::sha1_fichier(&chemin)).await {
            par_hash.insert(h, i);
        }
    }
    let hashes: Vec<String> = par_hash.keys().cloned().collect();
    let versions = modrinth::versions_par_hash(&hashes).await?;
    let ids: Vec<String> = versions.values().map(|v| v.project_id.clone()).collect();
    let projets: HashMap<String, modrinth::ProjetDetail> =
        modrinth::projets(&ids).await?.into_iter().map(|p| (p.id.clone(), p)).collect();

    for (hash, &i) in &par_hash {
        let Some(version) = versions.get(hash) else { continue };
        let Some(projet) = projets.get(&version.project_id) else { continue };
        let meta = contenus[i].meta.as_mut().unwrap();
        meta.source = Some(MODRINTH.into());
        meta.project_id = Some(projet.id.clone());
        meta.version_id = Some(version.id.clone());
        meta.titre = projet.title.clone();
        meta.version_nom = Some(version.version_number.clone());
        meta.icone_url = projet.icon_url.clone();
    }

    // 2. CurseForge, pour ce qui n'est pas sur Modrinth (injoignable : on garde les noms de fichiers)
    let restants: Vec<usize> = inconnus.iter().copied().filter(|&i| contenus[i].meta.as_ref().unwrap().project_id.is_none()).collect();
    if !restants.is_empty() && curseforge::disponible() {
        let mut par_empreinte: HashMap<u32, usize> = HashMap::new();
        for &i in &restants {
            let chemin = contenus[i].chemin_fichier.clone();
            if let Ok(Ok(e)) = tokio::task::spawn_blocking(move || curseforge::empreinte(&chemin)).await {
                par_empreinte.insert(e, i);
            }
        }
        let empreintes: Vec<u32> = par_empreinte.keys().copied().collect();
        if let Ok(correspondances) = curseforge::empreintes(&empreintes).await {
            let ids: Vec<i64> = correspondances.iter().map(|c| c.id).collect();
            if let Ok(mods) = curseforge::mods(&ids).await {
                let mods: HashMap<i64, curseforge::ModCF> = mods.into_iter().map(|m| (m.id, m)).collect();
                for c in &correspondances {
                    let Some(&i) = par_empreinte.get(&(c.file.file_fingerprint as u32)) else { continue };
                    let Some(m) = mods.get(&c.id) else { continue };
                    let meta = contenus[i].meta.as_mut().unwrap();
                    meta.source = Some(CURSEFORGE.into());
                    meta.project_id = Some(m.id.to_string());
                    meta.version_id = Some(c.file.id.to_string());
                    meta.titre = m.name.clone();
                    meta.version_nom = Some(c.file.display_name.clone());
                    meta.icone_url = m.logo.as_ref().and_then(|l| l.thumbnail_url.clone());
                }
            }
        }
    }

    let nouvelles: Vec<ContenuMeta> = inconnus.iter().map(|&i| contenus[i].meta.clone().unwrap()).collect();
    enregistrer_metas(instance, nouvelles)?;
    Ok(contenus)
}

/// Ajoute ou remplace des métadonnées (même chemin) dans misty-contenu.json.
pub fn enregistrer_metas(instance: &Instance, nouvelles: Vec<ContenuMeta>) -> anyhow::Result<()> {
    let mut metas = lire_meta(instance);
    for meta in nouvelles {
        metas.retain(|m| !m.chemin.eq_ignore_ascii_case(&meta.chemin));
        metas.push(meta);
    }
    ecrire_meta(instance, &metas)
}

/// Active / désactive un fichier. Renvoie le nouveau chemin.
pub fn basculer(contenu: &ContenuInstalle) -> anyhow::Result<PathBuf> {
    let chemin = contenu.chemin_fichier.to_string_lossy().to_string();
    let nouveau = if contenu.actif() {
        format!("{chemin}{SUFFIXE_DESACTIVE}")
    } else {
        chemin[..chemin.len() - SUFFIXE_DESACTIVE.len()].to_string()
    };
    fs::rename(&contenu.chemin_fichier, &nouveau)?;
    Ok(PathBuf::from(nouveau))
}

pub fn supprimer(instance: &Instance, contenu: &ContenuInstalle) -> anyhow::Result<()> {
    if contenu.chemin_fichier.exists() {
        fs::remove_file(&contenu.chemin_fichier)?;
    }
    let rel = relatif(instance, &contenu.chemin_fichier);
    let mut metas = lire_meta(instance);
    metas.retain(|m| !m.chemin.eq_ignore_ascii_case(&rel));
    ecrire_meta(instance, &metas)
}

// ───────────────────────── Installation ─────────────────────────

/// État partagé d'une installation (métadonnées, projets installés, projets déjà visités).
struct Installation<'a> {
    instance: &'a Instance,
    metas: Vec<ContenuMeta>,
    installes: Vec<String>,
    visites_mr: HashSet<String>,
    visites_cf: HashSet<i64>,
    progression: Option<Progress>,
}

/// Installe la meilleure version compatible d'un projet (et ses dépendances requises pour les mods).
/// Remplace la version déjà installée du même projet. Renvoie les noms des projets installés.
pub async fn installer(
    instance: &Instance,
    source: &str,
    project_id: &str,
    type_: &str,
    monde: Option<&str>,
    progression: Option<Progress>,
) -> anyhow::Result<Vec<String>> {
    let mut etat = Installation {
        instance,
        metas: lire_meta(instance),
        installes: Vec::new(),
        visites_mr: HashSet::new(),
        visites_cf: HashSet::new(),
        progression,
    };
    let resultat = if source == CURSEFORGE {
        installer_cf(&mut etat, project_id.parse()?, type_.to_string(), monde.map(str::to_string)).await
    } else {
        installer_mr(&mut etat, project_id.to_string(), type_.to_string(), monde.map(str::to_string), None).await
    };
    // On garde ce qui a déjà été installé même en cas d'erreur
    ecrire_meta(instance, &etat.metas)?;
    resultat?;
    Ok(etat.installes)
}

type Fut<'a> = Pin<Box<dyn Future<Output = anyhow::Result<()>> + Send + 'a>>;

fn installer_mr<'a>(
    etat: &'a mut Installation<'_>,
    project_id: String,
    type_: String,
    monde: Option<String>,
    version_imposee: Option<modrinth::VersionModrinth>,
) -> Fut<'a> {
    Box::pin(async move {
        if !etat.visites_mr.insert(project_id.clone()) {
            return Ok(());
        }
        let instance = etat.instance;

        let version = match version_imposee {
            Some(v) => Some(v),
            None => {
                let loaders = loaders_modrinth(instance, &type_);
                meilleure_version(modrinth::versions(&project_id, loaders.as_deref(), Some(&instance.version_mc)).await?)
            }
        };
        let projet = modrinth::projets(std::slice::from_ref(&project_id)).await?.into_iter().next();
        let Some(version) = version else {
            let titre = projet.map(|p| p.title).unwrap_or(project_id);
            anyhow::bail!(loc::tf("Erreur_PasDeVersion", &[&titre, &instance.version_mc, &instance.loader]));
        };
        let fichier = version.fichier_principal().cloned().ok_or_else(|| anyhow::anyhow!(loc::t("Erreur_AucunFichier")))?;
        // On garde l'ID réel, même si on a reçu un slug
        let project_id = projet.as_ref().map(|p| p.id.clone()).unwrap_or(project_id);
        etat.visites_mr.insert(project_id.clone());
        let titre = projet.as_ref().map(|p| p.title.clone()).unwrap_or_else(|| fichier.filename.clone());
        let dossier = dossier(instance, &type_, monde.as_deref())?;

        retirer_ancienne_version(instance, &mut etat.metas, MODRINTH, &project_id, &type_, &dossier);

        if let Some(p) = &etat.progression {
            p(loc::tf("Progres_Telechargement", &[&titre]));
        }
        let destination = dossier.join(download::nom_fichier_sur(&fichier.filename));
        download::fichier(&fichier.url, &destination, fichier.hashes.get("sha1").map(String::as_str)).await?;

        let rel = relatif(instance, &destination);
        etat.metas.retain(|m| !m.chemin.eq_ignore_ascii_case(&rel));
        etat.metas.push(ContenuMeta {
            chemin: rel,
            source: Some(MODRINTH.into()),
            project_id: Some(project_id.clone()),
            version_id: Some(version.id.clone()),
            titre: titre.clone(),
            version_nom: Some(version.version_number.clone()),
            icone_url: projet.as_ref().and_then(|p| p.icon_url.clone()),
            type_: type_.clone(),
        });
        etat.installes.push(titre);

        if type_ != MOD {
            return Ok(());
        }

        // Dépendances requises
        for dep in version.dependencies.iter().filter(|d| d.dependency_type == "required") {
            let version_dep = match &dep.version_id {
                Some(id) => modrinth::version(id).await.ok(),
                None => None,
            };
            let Some(id_dep) = dep.project_id.clone().or_else(|| version_dep.as_ref().map(|v| v.project_id.clone())) else { continue };
            if etat.metas.iter().any(|m| m.correspond(MODRINTH, &id_dep)) {
                continue; // déjà là
            }
            installer_mr(etat, id_dep, MOD.into(), None, version_dep).await?;
        }
        Ok(())
    })
}

/// Privilégie la dernière version stable, sinon la plus récente.
fn meilleure_version(versions: Vec<modrinth::VersionModrinth>) -> Option<modrinth::VersionModrinth> {
    let stable = versions.iter().position(|v| v.version_type == "release");
    let mut versions = versions;
    match stable {
        Some(i) => Some(versions.swap_remove(i)),
        None => versions.into_iter().next(),
    }
}

fn installer_cf<'a>(etat: &'a mut Installation<'_>, mod_id: i64, type_: String, monde: Option<String>) -> Fut<'a> {
    Box::pin(async move {
        if !etat.visites_cf.insert(mod_id) {
            return Ok(());
        }
        let instance = etat.instance;

        let m = curseforge::mods(&[mod_id])
            .await?
            .into_iter()
            .next()
            .ok_or_else(|| anyhow::anyhow!(loc::tf("Erreur_ProjetCF", &[&mod_id])))?;

        // Fichiers compatibles : bonne version de Minecraft (+ bon loader pour les mods)
        let mut fichiers: Vec<curseforge::FichierCF> = curseforge::fichiers(mod_id, Some(&instance.version_mc))
            .await?
            .into_iter()
            .filter(|f| f.is_available && f.game_versions.contains(&instance.version_mc))
            .collect();
        if type_ == MOD {
            let noms = curseforge::noms_loader(&instance.loader);
            fichiers.retain(|f| f.game_versions.iter().any(|v| noms.iter().any(|n| n.eq_ignore_ascii_case(v))));
        }
        let fichier = fichiers
            .iter()
            .find(|f| f.release_type == 1)
            .or_else(|| fichiers.first())
            .cloned()
            .ok_or_else(|| anyhow::anyhow!(loc::tf("Erreur_PasDeVersionCF", &[&m.name, &instance.version_mc, &instance.loader])))?;

        let Some(url) = fichier.download_url.clone().filter(|u| !u.is_empty()) else {
            anyhow::bail!(loc::tf("Erreur_CFInterdit", &[&m.name, &curseforge::page_fichier(Some(&m), mod_id, fichier.id)]));
        };

        let dossier = dossier(instance, &type_, monde.as_deref())?;
        retirer_ancienne_version(instance, &mut etat.metas, CURSEFORGE, &mod_id.to_string(), &type_, &dossier);

        if let Some(p) = &etat.progression {
            p(loc::tf("Progres_Telechargement", &[&m.name]));
        }
        let destination = dossier.join(download::nom_fichier_sur(&fichier.file_name));
        download::fichier(&url, &destination, fichier.sha1()).await?;

        let rel = relatif(instance, &destination);
        etat.metas.retain(|x| !x.chemin.eq_ignore_ascii_case(&rel));
        etat.metas.push(ContenuMeta {
            chemin: rel,
            source: Some(CURSEFORGE.into()),
            project_id: Some(mod_id.to_string()),
            version_id: Some(fichier.id.to_string()),
            titre: m.name.clone(),
            version_nom: Some(fichier.display_name.clone()),
            icone_url: m.logo.as_ref().and_then(|l| l.thumbnail_url.clone()),
            type_: type_.clone(),
        });
        etat.installes.push(m.name.clone());

        if type_ != MOD {
            return Ok(());
        }

        // Dépendances requises (relationType 3)
        for dep in fichier.dependencies.iter().filter(|d| d.relation_type == 3) {
            if etat.metas.iter().any(|x| x.correspond(CURSEFORGE, &dep.mod_id.to_string())) {
                continue;
            }
            installer_cf(etat, dep.mod_id, MOD.into(), None).await?;
        }
        Ok(())
    })
}

/// Mise à jour : retire l'ancienne version du même projet (au même endroit).
fn retirer_ancienne_version(instance: &Instance, metas: &mut Vec<ContenuMeta>, source: &str, project_id: &str, type_: &str, dossier: &Path) {
    let base = instance.dossier();
    metas.retain(|ancien| {
        if !(ancien.correspond(source, project_id) && ancien.type_ == type_) {
            return true;
        }
        let chemin = base.join(&ancien.chemin);
        let meme_dossier = chemin
            .parent()
            .map(|p| p.to_string_lossy().to_lowercase().replace('/', "\\") == dossier.to_string_lossy().to_lowercase().replace('/', "\\"))
            .unwrap_or(false);
        if !meme_dossier {
            return true;
        }
        let _ = fs::remove_file(&chemin);
        let _ = fs::remove_file(format!("{}{SUFFIXE_DESACTIVE}", chemin.display()));
        false
    });
}

/// Prérequis manquant pour les shaders : renvoie l'ID Modrinth du mod à installer (Iris / Oculus),
/// Some("") si les shaders sont impossibles avec ce loader, None si tout est prêt.
/// Iris / Oculus installés depuis CurseForge ou à la main sont reconnus à leur nom.
pub fn prerequis_shaders(instance: &Instance, contenus: &[ContenuInstalle]) -> Option<&'static str> {
    let present = |id: &str, nom: &str| {
        contenus.iter().any(|c| {
            c.type_ == MOD
                && (c.meta.as_ref().is_some_and(|m| m.correspond(MODRINTH, id)) || c.titre().to_lowercase().starts_with(&nom.to_lowercase()))
        })
    };
    match instance.loader.as_str() {
        loaders::OPTIFINE => None,
        loaders::FABRIC | loaders::QUILT | loaders::NEOFORGE => (!present(ID_IRIS, "Iris")).then_some(ID_IRIS),
        loaders::FORGE => (!present(ID_OCULUS, "Oculus")).then_some(ID_OCULUS),
        _ => Some(""),
    }
}

// ───────────────────────── Utilitaires ─────────────────────────

/// Chemin relatif au profil, avec des "/" et sans ".disabled".
pub fn relatif(instance: &Instance, chemin: &Path) -> String {
    let mut texte = chemin.to_string_lossy().to_string();
    if texte.to_lowercase().ends_with(SUFFIXE_DESACTIVE) {
        texte.truncate(texte.len() - SUFFIXE_DESACTIVE.len());
    }
    let base = instance.dossier();
    let rel = Path::new(&texte).strip_prefix(&base).map(|p| p.to_path_buf()).unwrap_or_else(|_| PathBuf::from(&texte));
    rel.to_string_lossy().replace('\\', "/")
}

fn lire_meta(instance: &Instance) -> Vec<ContenuMeta> {
    let chemin = instance.dossier().join(FICHIER_META);
    fs::read_to_string(chemin)
        .ok()
        .and_then(|t| serde_json::from_str(t.trim_start_matches('\u{feff}')).ok())
        .unwrap_or_default()
}

fn ecrire_meta(instance: &Instance, metas: &[ContenuMeta]) -> anyhow::Result<()> {
    fs::create_dir_all(instance.dossier())?;
    fs::write(instance.dossier().join(FICHIER_META), serde_json::to_string_pretty(metas)?)?;
    Ok(())
}
