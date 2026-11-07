//! Session Microsoft (premium).
//!
//! Chaîne d'authentification (ce que faisait CmlLib.Core.Auth.Microsoft en C#) :
//!   1. Connexion Microsoft dans une fenêtre web → code
//!   2. code → jeton Microsoft (+ jeton de renouvellement, mis en cache)
//!   3. jeton Microsoft → jeton Xbox Live → jeton XSTS
//!   4. XSTS → jeton Minecraft → profil (pseudo + UUID)
//!
//! La fenêtre web tourne dans un sous-processus (`Misty.exe --ms-login`), car l'interface Slint
//! et la WebView ne peuvent pas partager la même boucle d'événements.

use super::{http, loc, paths, settings};
use serde::{Deserialize, Serialize};
use serde_json::json;
use std::sync::RwLock;

/// Identifiant du launcher Minecraft officiel (accepté par l'API Minecraft sans validation).
pub const CLIENT_ID: &str = "00000000402b5328";
pub const REDIRECT: &str = "https://login.live.com/oauth20_desktop.srf";
const SCOPE: &str = "service::user.auth.xboxlive.com::MBI_SSL";

pub fn url_connexion() -> String {
    format!(
        "https://login.live.com/oauth20_authorize.srf?client_id={CLIENT_ID}&response_type=code&scope={}&redirect_uri={}&prompt=select_account",
        urlencoding::encode(SCOPE),
        urlencoding::encode(REDIRECT)
    )
}

/// Session de jeu Minecraft.
#[derive(Clone, Debug)]
pub struct Session {
    pub pseudo: String,
    pub uuid: String,
    pub jeton: String,
    pub xuid: String,
}

/// Un compte Microsoft mis en cache.
#[derive(Serialize, Deserialize, Default, Clone)]
struct Cache {
    refresh_token: String,
    mc_token: String,
    mc_expire: i64,
    pseudo: String,
    uuid: String,
    xuid: String,
}

/// Contenu de DataDir\msa.json : tous les comptes + celui qui est utilisé.
/// (L'ancien format, avec un seul compte, est encore lu.)
#[derive(Serialize, Deserialize, Default)]
struct Fichier {
    #[serde(default)]
    actif: String,
    #[serde(default)]
    comptes: Vec<Cache>,
}

/// Compte affiché dans les listes.
#[derive(Clone, Debug)]
pub struct CompteInfo {
    pub pseudo: String,
    pub uuid: String,
    pub actif: bool,
}

static SESSION: RwLock<Option<Session>> = RwLock::new(None);

pub fn session() -> Option<Session> {
    SESSION.read().unwrap().clone()
}

fn fichier_cache() -> std::path::PathBuf {
    paths::data_dir().join("msa.json")
}

fn lire_fichier() -> Fichier {
    let Ok(texte) = std::fs::read_to_string(fichier_cache()) else { return Fichier::default() };
    if let Ok(f) = serde_json::from_str::<Fichier>(&texte) {
        if !f.comptes.is_empty() {
            return f;
        }
    }
    // Ancien format : un seul compte à la racine
    match serde_json::from_str::<Cache>(&texte) {
        Ok(c) if !c.uuid.is_empty() => Fichier { actif: c.uuid.clone(), comptes: vec![c] },
        _ => Fichier::default(),
    }
}

fn ecrire_fichier(f: &Fichier) {
    let _ = std::fs::write(fichier_cache(), serde_json::to_string_pretty(f).unwrap_or_default());
}

/// Ajoute ou met à jour un compte, et le rend actif.
fn enregistrer(cache: Cache) {
    let mut f = lire_fichier();
    f.actif = cache.uuid.clone();
    match f.comptes.iter_mut().find(|c| c.uuid == cache.uuid) {
        Some(existant) => *existant = cache,
        None => f.comptes.push(cache),
    }
    ecrire_fichier(&f);
}

/// Compte actif en cache (sinon le premier).
fn compte_actif() -> Option<Cache> {
    let f = lire_fichier();
    f.comptes.iter().find(|c| c.uuid == f.actif).or_else(|| f.comptes.first()).cloned()
}

/// Tous les comptes Microsoft connus.
pub fn comptes() -> Vec<CompteInfo> {
    let f = lire_fichier();
    let actif = f.comptes.iter().find(|c| c.uuid == f.actif).or_else(|| f.comptes.first()).map(|c| c.uuid.clone()).unwrap_or_default();
    f.comptes.iter().map(|c| CompteInfo { pseudo: c.pseudo.clone(), uuid: c.uuid.clone(), actif: c.uuid == actif }).collect()
}

/// Choisit le compte à utiliser, puis s'y connecte.
pub async fn activer(uuid: &str) -> anyhow::Result<Session> {
    let mut f = lire_fichier();
    if f.comptes.iter().any(|c| c.uuid == uuid) {
        f.actif = uuid.to_string();
        ecrire_fichier(&f);
    }
    connecter().await
}

/// Ajoute un autre compte : ouvre toujours la fenêtre de connexion.
pub async fn ajouter() -> anyhow::Result<Session> {
    let session = connexion_interactive().await?;
    *SESSION.write().unwrap() = Some(session.clone());
    settings::set(settings::PREMIUM, "True");
    Ok(session)
}

/// Oublie un seul compte. S'il était connecté, on se déconnecte.
pub fn oublier(uuid: &str) {
    let mut f = lire_fichier();
    f.comptes.retain(|c| c.uuid != uuid);
    if f.actif == uuid {
        f.actif = f.comptes.first().map(|c| c.uuid.clone()).unwrap_or_default();
    }
    if f.comptes.is_empty() {
        let _ = std::fs::remove_file(fichier_cache());
    } else {
        ecrire_fichier(&f);
    }
    if session().is_some_and(|s| s.uuid == uuid) {
        deconnecter();
    }
}

/// Dossier des données de la fenêtre de connexion (cookies du compte Microsoft).
pub fn dossier_webview() -> std::path::PathBuf {
    paths::data_dir().join("webview")
}

/// Connexion : session en cache si elle est encore valide, sinon renouvellement, sinon fenêtre de connexion.
pub async fn connecter() -> anyhow::Result<Session> {
    let maintenant = chrono::Utc::now().timestamp();
    let cache = compte_actif();

    let session = match cache {
        Some(c) if !c.mc_token.is_empty() && c.mc_expire > maintenant + 300 => {
            Session { pseudo: c.pseudo, uuid: c.uuid, jeton: c.mc_token, xuid: c.xuid }
        }
        Some(c) if !c.refresh_token.is_empty() => match renouveler(&c.refresh_token).await {
            Ok(s) => s,
            Err(_) => connexion_interactive().await?, // jeton expiré ou révoqué
        },
        _ => connexion_interactive().await?,
    };

    *SESSION.write().unwrap() = Some(session.clone());
    settings::set(settings::PREMIUM, "True");
    Ok(session)
}

/// Déconnexion rapide : le compte reste en cache pour la prochaine connexion.
pub fn deconnecter() {
    *SESSION.write().unwrap() = None;
    settings::set(settings::PREMIUM, "False");
}

/// Déconnexion totale : supprime aussi le compte mis en cache.
pub fn deconnexion_totale() -> anyhow::Result<()> {
    let _ = std::fs::remove_file(fichier_cache());
    if dossier_webview().exists() {
        std::fs::remove_dir_all(dossier_webview())?;
    }
    deconnecter();
    Ok(())
}

/// Ouvre la fenêtre de connexion (sous-processus) et attend le code.
async fn connexion_interactive() -> anyhow::Result<Session> {
    let sortie = tokio::process::Command::new(paths::app_exe())
        .arg("--ms-login")
        .stdout(std::process::Stdio::piped())
        .stderr(std::process::Stdio::null())
        .output()
        .await?;
    let texte = String::from_utf8_lossy(&sortie.stdout);
    let code = texte
        .lines()
        .find_map(|l| l.strip_prefix("CODE:"))
        .map(str::trim)
        .map(str::to_string);
    let Some(code) = code else {
        if let Some(erreur) = texte.lines().find_map(|l| l.strip_prefix("ERREUR:")) {
            anyhow::bail!("{}", erreur.trim());
        }
        anyhow::bail!(loc::t("Compte_Annule"));
    };
    let jetons = jeton_microsoft(&[("code", &code), ("grant_type", "authorization_code")]).await?;
    terminer(jetons).await
}

async fn renouveler(refresh_token: &str) -> anyhow::Result<Session> {
    let jetons = jeton_microsoft(&[("refresh_token", refresh_token), ("grant_type", "refresh_token")]).await?;
    terminer(jetons).await
}

#[derive(Deserialize)]
struct JetonsMs {
    access_token: String,
    #[serde(default)]
    refresh_token: String,
}

async fn jeton_microsoft(parametres: &[(&str, &str)]) -> anyhow::Result<JetonsMs> {
    let mut formulaire: Vec<(&str, &str)> = vec![("client_id", CLIENT_ID), ("redirect_uri", REDIRECT), ("scope", SCOPE)];
    formulaire.extend_from_slice(parametres);
    let reponse = http::client().post("https://login.live.com/oauth20_token.srf").form(&formulaire).send().await?;
    if !reponse.status().is_success() {
        anyhow::bail!("Microsoft : {}", reponse.text().await.unwrap_or_default());
    }
    Ok(reponse.json().await?)
}

/// Étapes Xbox Live → XSTS → Minecraft → profil, puis mise en cache.
async fn terminer(jetons: JetonsMs) -> anyhow::Result<Session> {
    let client = http::client();

    // Xbox Live. Selon le compte, le ticket doit être préfixé ("t=" / "d=") ou non.
    let mut xbl: Option<serde_json::Value> = None;
    for prefixe in ["", "t=", "d="] {
        let corps = json!({
            "Properties": { "AuthMethod": "RPS", "SiteName": "user.auth.xboxlive.com", "RpsTicket": format!("{prefixe}{}", jetons.access_token) },
            "RelyingParty": "http://auth.xboxlive.com",
            "TokenType": "JWT"
        });
        let r = client.post("https://user.auth.xboxlive.com/user/authenticate").header("Accept", "application/json").json(&corps).send().await?;
        if r.status().is_success() {
            xbl = Some(r.json().await?);
            break;
        }
    }
    let xbl = xbl.ok_or_else(|| anyhow::anyhow!("Xbox Live : authentification refusée."))?;
    let jeton_xbl = xbl["Token"].as_str().unwrap_or_default();

    // XSTS
    let corps = json!({
        "Properties": { "SandboxId": "RETAIL", "UserTokens": [jeton_xbl] },
        "RelyingParty": "rp://api.minecraftservices.com/",
        "TokenType": "JWT"
    });
    let r = client.post("https://xsts.auth.xboxlive.com/xsts/authorize").header("Accept", "application/json").json(&corps).send().await?;
    if !r.status().is_success() {
        let erreur: serde_json::Value = r.json().await.unwrap_or_default();
        let message = match erreur["XErr"].as_i64() {
            Some(2148916233) => loc::t("Compte_PasDeXbox"),
            Some(2148916235) => loc::t("Compte_PaysInterdit"),
            Some(2148916236) | Some(2148916237) => loc::t("Compte_VerifAge"),
            Some(2148916238) => loc::t("Compte_Enfant"),
            _ => format!("XSTS : {erreur}"),
        };
        anyhow::bail!(message);
    }
    let xsts: serde_json::Value = r.json().await?;
    let jeton_xsts = xsts["Token"].as_str().unwrap_or_default();
    let uhs = xsts["DisplayClaims"]["xui"][0]["uhs"].as_str().unwrap_or_default();
    let xuid = xsts["DisplayClaims"]["xui"][0]["xid"].as_str().unwrap_or_default().to_string();

    // Minecraft
    let r = client
        .post("https://api.minecraftservices.com/authentication/login_with_xbox")
        .json(&json!({ "identityToken": format!("XBL3.0 x={uhs};{jeton_xsts}") }))
        .send()
        .await?;
    if !r.status().is_success() {
        anyhow::bail!("Minecraft : {}", r.text().await.unwrap_or_default());
    }
    let mc: serde_json::Value = r.json().await?;
    let jeton_mc = mc["access_token"].as_str().unwrap_or_default().to_string();
    let expire = chrono::Utc::now().timestamp() + mc["expires_in"].as_i64().unwrap_or(86400);

    // Profil (404 = le compte ne possède pas Minecraft Java)
    let r = client.get("https://api.minecraftservices.com/minecraft/profile").bearer_auth(&jeton_mc).send().await?;
    if r.status() == reqwest::StatusCode::NOT_FOUND {
        anyhow::bail!(loc::t("Compte_PasDeJeu"));
    }
    let profil: serde_json::Value = r.error_for_status()?.json().await?;
    let session = Session {
        pseudo: profil["name"].as_str().unwrap_or_default().to_string(),
        uuid: profil["id"].as_str().unwrap_or_default().to_string(),
        jeton: jeton_mc.clone(),
        xuid: xuid.clone(),
    };

    enregistrer(Cache {
        refresh_token: jetons.refresh_token,
        mc_token: jeton_mc,
        mc_expire: expire,
        pseudo: session.pseudo.clone(),
        uuid: session.uuid.clone(),
        xuid,
    });
    Ok(session)
}

/// UUID hors-ligne, calculé comme Minecraft : UUID v3 de "OfflinePlayer:<pseudo>".
pub fn uuid_hors_ligne(pseudo: &str) -> String {
    let mut octets = md5::compute(format!("OfflinePlayer:{pseudo}")).0;
    octets[6] = (octets[6] & 0x0f) | 0x30;
    octets[8] = (octets[8] & 0x3f) | 0x80;
    hex::encode(octets)
}
