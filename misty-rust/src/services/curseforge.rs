//! Client minimal de l'API CurseForge v1 (https://docs.curseforge.com/rest-api/).
//!
//! L'API exige une clé personnelle (console.curseforge.com), qui doit rester privée :
//! elle est lue depuis la variable d'environnement MISTY_CURSEFORGE_KEY, sinon depuis
//! curseforge.key (ignoré par git, intégré à l'exe à la compilation par build.rs).

use super::catalogue::{ProjetDistant, CURSEFORGE};
use super::content::{DATAPACK, MOD, MODPACK, PACK_RESSOURCES, SHADER};
use super::{app_info, loc};
use crate::minecraft::loaders;
use serde::de::DeserializeOwned;
use serde::Deserialize;
use std::sync::OnceLock;
use std::time::Duration;

const BASE: &str = "https://api.curseforge.com/v1/";
const ID_MINECRAFT: i64 = 432;

// Classes CurseForge (types de projets) pour Minecraft
pub const CLASSE_MODS: i64 = 6;
pub const CLASSE_PACKS_RESSOURCES: i64 = 12;
pub const CLASSE_SHADERS: i64 = 6552;
pub const CLASSE_DATAPACKS: i64 = 6945;
pub const CLASSE_MODPACKS: i64 = 4471;

pub const TRIS: &[(&str, &str)] = &[
    ("2", "Tri_Popularite"),
    ("6", "Tri_Telechargements"),
    ("3", "Tri_MisAJour"),
    ("1", "Tri_Vedette"),
    ("4", "Tri_Nom"),
];

/// Clé API, ou None si elle n'est pas configurée.
pub fn cle() -> Option<&'static str> {
    static CLE: OnceLock<Option<String>> = OnceLock::new();
    CLE.get_or_init(|| {
        let cle = std::env::var("MISTY_CURSEFORGE_KEY")
            .ok()
            .filter(|c| !c.trim().is_empty())
            .or_else(|| option_env!("MISTY_CF_KEY").map(str::to_string));
        cle.map(|c| c.trim().to_string()).filter(|c| !c.is_empty())
    })
    .as_deref()
}

pub fn disponible() -> bool {
    cle().is_some()
}

fn client() -> anyhow::Result<&'static reqwest::Client> {
    static C: OnceLock<reqwest::Client> = OnceLock::new();
    let Some(cle) = cle() else { anyhow::bail!(loc::t("Erreur_CleCF")) };
    Ok(C.get_or_init(|| {
        let mut entetes = reqwest::header::HeaderMap::new();
        if let Ok(v) = reqwest::header::HeaderValue::from_str(cle) {
            entetes.insert("x-api-key", v);
        }
        reqwest::Client::builder()
            .user_agent(app_info::user_agent())
            .default_headers(entetes)
            .timeout(Duration::from_secs(300))
            .build()
            .expect("client CurseForge")
    }))
}

// ───────────────────────── DTO ─────────────────────────

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ReponseCF<T> {
    pub data: T,
    pub pagination: Option<PaginationCF>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PaginationCF {
    #[serde(default)]
    pub total_count: i64,
}

#[derive(Deserialize, Clone, Default)]
#[serde(rename_all = "camelCase")]
pub struct ModCF {
    pub id: i64,
    #[serde(default)]
    pub name: String,
    #[serde(default)]
    pub summary: String,
    #[serde(default)]
    pub download_count: f64,
    #[serde(default)]
    pub class_id: Option<i64>,
    pub logo: Option<LogoCF>,
    #[serde(default)]
    pub authors: Vec<NomCF>,
    #[serde(default)]
    pub categories: Vec<NomCF>,
    pub links: Option<LiensCF>,
}

#[derive(Deserialize, Clone, Default)]
#[serde(rename_all = "camelCase")]
pub struct LogoCF {
    pub thumbnail_url: Option<String>,
    pub url: Option<String>,
}

#[derive(Deserialize, Clone, Default)]
pub struct NomCF {
    #[serde(default)]
    pub name: String,
}

#[derive(Deserialize, Clone, Default)]
#[serde(rename_all = "camelCase")]
pub struct LiensCF {
    pub website_url: Option<String>,
}

#[derive(Deserialize, Clone)]
#[serde(rename_all = "camelCase")]
pub struct FichierCF {
    pub id: i64,
    #[serde(default)]
    pub display_name: String,
    #[serde(default)]
    pub file_name: String,
    /// 1 = release, 2 = bêta, 3 = alpha.
    #[serde(default)]
    pub release_type: i64,
    /// None si l'auteur interdit le téléchargement par des applications tierces.
    pub download_url: Option<String>,
    #[serde(default)]
    pub file_date: String,
    #[serde(default = "vrai")]
    pub is_available: bool,
    /// Empreinte MurmurHash2 du fichier (voir `empreinte`).
    #[serde(default)]
    pub file_fingerprint: i64,
    #[serde(default)]
    pub hashes: Vec<HashCF>,
    #[serde(default)]
    pub dependencies: Vec<DependanceCF>,
    /// Versions de Minecraft ET noms de loaders ("1.21.1", "Fabric"…).
    #[serde(default)]
    pub game_versions: Vec<String>,
}

fn vrai() -> bool {
    true
}

impl FichierCF {
    pub fn sha1(&self) -> Option<&str> {
        self.hashes.iter().find(|h| h.algo == 1).map(|h| h.value.as_str())
    }
}

#[derive(Deserialize, Clone)]
pub struct HashCF {
    #[serde(default)]
    pub value: String,
    /// 1 = SHA-1, 2 = MD5.
    #[serde(default)]
    pub algo: i64,
}

#[derive(Deserialize, Clone)]
#[serde(rename_all = "camelCase")]
pub struct DependanceCF {
    pub mod_id: i64,
    /// 3 = dépendance requise.
    #[serde(default)]
    pub relation_type: i64,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct EmpreintesCF {
    #[serde(default)]
    exact_matches: Vec<CorrespondanceCF>,
}

#[derive(Deserialize)]
pub struct CorrespondanceCF {
    pub id: i64,
    pub file: FichierCF,
}

// ── Format des modpacks CurseForge (manifest.json) ──

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ManifestCF {
    pub minecraft: MinecraftCF,
    #[serde(default)]
    pub version: String,
    #[serde(default)]
    pub files: Vec<FichierManifestCF>,
    pub overrides: Option<String>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct MinecraftCF {
    #[serde(default)]
    pub version: String,
    #[serde(default)]
    pub mod_loaders: Vec<LoaderManifestCF>,
}

#[derive(Deserialize)]
pub struct LoaderManifestCF {
    /// "forge-47.2.0", "fabric-0.15.7", "neoforge-21.1.77"…
    #[serde(default)]
    pub id: String,
    #[serde(default)]
    pub primary: bool,
}

#[derive(Deserialize)]
pub struct FichierManifestCF {
    #[serde(rename = "projectID")]
    pub project_id: i64,
    #[serde(rename = "fileID")]
    pub file_id: i64,
    #[serde(default = "vrai")]
    pub required: bool,
}

// ───────────────────────── Correspondances ─────────────────────────

pub fn classe(type_: &str) -> i64 {
    match type_ {
        PACK_RESSOURCES => CLASSE_PACKS_RESSOURCES,
        SHADER => CLASSE_SHADERS,
        DATAPACK => CLASSE_DATAPACKS,
        MODPACK => CLASSE_MODPACKS,
        _ => CLASSE_MODS,
    }
}

/// Type de contenu Misty correspondant à une classe CurseForge (pour ranger les fichiers d'un modpack).
pub fn type_depuis_classe(classe: i64) -> &'static str {
    match classe {
        CLASSE_PACKS_RESSOURCES => PACK_RESSOURCES,
        CLASSE_SHADERS => SHADER,
        _ => MOD,
    }
}

/// Identifiant CurseForge du loader (ModLoaderType), ou None.
pub fn type_loader(loader: &str) -> Option<i64> {
    match loader {
        loaders::FORGE => Some(1),
        loaders::LITELOADER => Some(3),
        loaders::FABRIC => Some(4),
        loaders::QUILT => Some(5),
        loaders::NEOFORGE => Some(6),
        _ => None,
    }
}

/// Noms de loaders acceptés dans GameVersions d'un fichier de mod.
pub fn noms_loader(loader: &str) -> Vec<&str> {
    match loader {
        loaders::QUILT => vec!["Quilt", "Fabric"], // Quilt charge aussi les mods Fabric
        autre => vec![autre],
    }
}

// ───────────────────────── Requêtes ─────────────────────────

async fn get<T: DeserializeOwned>(url: &str) -> anyhow::Result<T> {
    Ok(client()?.get(url).send().await?.error_for_status()?.json().await?)
}

async fn post<T: DeserializeOwned>(url: &str, corps: serde_json::Value) -> anyhow::Result<T> {
    Ok(client()?.post(url).json(&corps).send().await?.error_for_status()?.json().await?)
}

pub async fn rechercher(
    requete: &str,
    classe: i64,
    version_mc: Option<&str>,
    type_loader: Option<i64>,
    tri: &str,
    index: usize,
    taille: usize,
) -> anyhow::Result<(Vec<ModCF>, i64)> {
    // CurseForge refuse index + pageSize > 10 000
    let taille = taille.min(10_000usize.saturating_sub(index));
    if taille == 0 {
        return Ok((Vec::new(), index as i64));
    }
    let mut url = format!(
        "{BASE}mods/search?gameId={ID_MINECRAFT}&classId={classe}&searchFilter={}&sortField={tri}&sortOrder=desc&index={index}&pageSize={taille}",
        urlencoding::encode(requete)
    );
    if let Some(v) = version_mc.filter(|v| !v.is_empty()) {
        url += &format!("&gameVersion={}", urlencoding::encode(v));
    }
    if let Some(l) = type_loader {
        url += &format!("&modLoaderType={l}");
    }
    let reponse: ReponseCF<Vec<ModCF>> = get(&url).await?;
    let total = reponse.pagination.map(|p| p.total_count).unwrap_or(0).min(10_000);
    Ok((reponse.data, total))
}

/// Fichiers d'un projet (les plus récents d'abord), filtrés sur une version de Minecraft.
pub async fn fichiers(mod_id: i64, version_mc: Option<&str>) -> anyhow::Result<Vec<FichierCF>> {
    let mut fichiers = Vec::new();
    // On se limite aux 500 fichiers les plus récents
    for index in (0..500).step_by(50) {
        let mut url = format!("{BASE}mods/{mod_id}/files?index={index}&pageSize=50");
        if let Some(v) = version_mc.filter(|v| !v.is_empty()) {
            url += &format!("&gameVersion={}", urlencoding::encode(v));
        }
        let reponse: ReponseCF<Vec<FichierCF>> = get(&url).await?;
        let n = reponse.data.len();
        fichiers.extend(reponse.data);
        let total = reponse.pagination.map(|p| p.total_count).unwrap_or(0);
        if n < 50 || fichiers.len() as i64 >= total {
            break;
        }
    }
    // Les dates ISO 8601 se trient comme du texte
    fichiers.sort_by(|a, b| b.file_date.cmp(&a.file_date));
    Ok(fichiers)
}

fn distincts(ids: &[i64]) -> Vec<i64> {
    let mut v = ids.to_vec();
    v.sort_unstable();
    v.dedup();
    v
}

pub async fn fichiers_par_ids(ids: &[i64]) -> anyhow::Result<Vec<FichierCF>> {
    let mut resultat = Vec::new();
    for paquet in distincts(ids).chunks(500) {
        let r: ReponseCF<Vec<FichierCF>> = post(&format!("{BASE}mods/files"), serde_json::json!({ "fileIds": paquet })).await?;
        resultat.extend(r.data);
    }
    Ok(resultat)
}

pub async fn mods(ids: &[i64]) -> anyhow::Result<Vec<ModCF>> {
    let mut resultat = Vec::new();
    for paquet in distincts(ids).chunks(500) {
        let r: ReponseCF<Vec<ModCF>> = post(&format!("{BASE}mods"), serde_json::json!({ "modIds": paquet })).await?;
        resultat.extend(r.data);
    }
    Ok(resultat)
}

/// Identifie des fichiers par leur empreinte CurseForge (voir `empreinte`).
pub async fn empreintes(empreintes: &[u32]) -> anyhow::Result<Vec<CorrespondanceCF>> {
    let mut liste = empreintes.to_vec();
    liste.sort_unstable();
    liste.dedup();
    if liste.is_empty() {
        return Ok(Vec::new());
    }
    let r: ReponseCF<EmpreintesCF> =
        post(&format!("{BASE}fingerprints/{ID_MINECRAFT}"), serde_json::json!({ "fingerprints": liste })).await?;
    Ok(r.data.exact_matches)
}

/// Page du fichier sur le site CurseForge (pour un téléchargement manuel).
pub fn page_fichier(m: Option<&ModCF>, mod_id: i64, file_id: i64) -> String {
    match m.and_then(|m| m.links.as_ref()).and_then(|l| l.website_url.as_deref()).filter(|s| !s.is_empty()) {
        Some(site) => format!("{}/files/{file_id}", site.trim_end_matches('/')),
        None => format!("https://www.curseforge.com/projects/{mod_id}"),
    }
}

/// Empreinte CurseForge d'un fichier : MurmurHash2 (graine 1) calculé en ignorant
/// les octets d'espacement (tabulation, retour chariot, saut de ligne, espace).
pub fn empreinte(chemin: &std::path::Path) -> std::io::Result<u32> {
    let octets: Vec<u8> = std::fs::read(chemin)?.into_iter().filter(|b| !matches!(b, 9 | 10 | 13 | 32)).collect();

    const M: u32 = 0x5bd1_e995;
    const R: u32 = 24;
    let mut longueur = octets.len() as u32;
    let mut h: u32 = 1 ^ longueur;
    let mut i = 0usize;

    while longueur >= 4 {
        let mut k = u32::from_le_bytes([octets[i], octets[i + 1], octets[i + 2], octets[i + 3]]);
        k = k.wrapping_mul(M);
        k ^= k >> R;
        k = k.wrapping_mul(M);
        h = h.wrapping_mul(M);
        h ^= k;
        i += 4;
        longueur -= 4;
    }

    if longueur == 3 {
        h ^= (octets[i + 2] as u32) << 16;
    }
    if longueur >= 2 {
        h ^= (octets[i + 1] as u32) << 8;
    }
    if longueur >= 1 {
        h ^= octets[i] as u32;
        h = h.wrapping_mul(M);
    }

    h ^= h >> 13;
    h = h.wrapping_mul(M);
    h ^= h >> 15;
    Ok(h)
}

/// Conversion vers le modèle commun de l'explorateur.
pub fn vers_projet(m: ModCF) -> ProjetDistant {
    ProjetDistant {
        source: CURSEFORGE.into(),
        id: m.id.to_string(),
        titre: m.name,
        description: m.summary,
        auteur: m.authors.first().map(|a| a.name.clone()).unwrap_or_default(),
        telechargements: m.download_count as i64,
        icone_url: m.logo.as_ref().and_then(|l| l.thumbnail_url.clone().or_else(|| l.url.clone())),
        categories: m.categories.into_iter().map(|c| c.name).collect(),
    }
}
