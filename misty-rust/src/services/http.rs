//! Clients HTTP partagés (un seul par usage, comme les HttpClient statiques en C#).

use super::app_info;
use std::sync::OnceLock;
use std::time::Duration;

/// Client général (Modrinth, Mojang, loaders…), avec le User-Agent du launcher.
pub fn client() -> &'static reqwest::Client {
    static C: OnceLock<reqwest::Client> = OnceLock::new();
    C.get_or_init(|| {
        reqwest::Client::builder()
            .user_agent(app_info::user_agent())
            .timeout(Duration::from_secs(300))
            .connect_timeout(Duration::from_secs(20))
            .build()
            .expect("client HTTP")
    })
}

/// Lit une réponse JSON en vérifiant le code HTTP.
pub async fn get_json<T: serde::de::DeserializeOwned>(url: &str) -> anyhow::Result<T> {
    let reponse = client().get(url).send().await?.error_for_status()?;
    Ok(reponse.json().await?)
}

pub async fn get_text(url: &str) -> anyhow::Result<String> {
    let reponse = client().get(url).send().await?.error_for_status()?;
    Ok(reponse.text().await?)
}
