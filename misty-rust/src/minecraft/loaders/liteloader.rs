//! LiteLoader (Minecraft 1.5.2 à 1.12.2) : liste officielle versions.json, lancement via launchwrapper.

use crate::minecraft::{version, McDirs};
use crate::services::http;
use serde_json::{json, Value};
use tokio::sync::Mutex;

const BASE: &str = "http://dl.liteloader.com/versions/";

async fn versions() -> anyhow::Result<Value> {
    static CACHE: Mutex<Option<Value>> = Mutex::const_new(None);
    let mut cache = CACHE.lock().await;
    if let Some(v) = cache.as_ref() {
        return Ok(v.clone());
    }
    let v: Value = http::get_json(&format!("{BASE}versions.json")).await?;
    *cache = Some(v.clone());
    Ok(v)
}

/// Dernière version publiée pour ce Minecraft : (infos, est un snapshot).
async fn derniere(mc: &str) -> anyhow::Result<Option<(Value, bool)>> {
    let toutes = versions().await?;
    let entree = &toutes["versions"][mc];
    for (cle, snapshot) in [("artefacts", false), ("snapshots", true)] {
        let artefact = &entree[cle]["com.mumfrey:liteloader"];
        if let Some(latest) = artefact.get("latest").filter(|l| l.is_object()) {
            return Ok(Some((latest.clone(), snapshot)));
        }
    }
    Ok(None)
}

pub async fn disponible(mc: &str) -> bool {
    derniere(mc).await.is_ok_and(|d| d.is_some())
}

pub async fn installer(dirs: &McDirs, mc: &str) -> anyhow::Result<String> {
    let (infos, snapshot) = derniere(mc).await?.ok_or_else(|| anyhow::anyhow!("LiteLoader indisponible pour {mc}"))?;
    let id = format!("{mc}-LiteLoader{mc}");
    let version_ll = infos["version"].as_str().unwrap_or(mc).to_string();
    let tweak = infos["tweakClass"].as_str().unwrap_or("com.mumfrey.liteloader.launch.LiteLoaderTweaker");

    let mut libs = vec![if snapshot {
        json!({ "name": format!("com.mumfrey:liteloader:{version_ll}"), "url": "http://repo.mumfrey.com/content/repositories/snapshots/" })
    } else {
        json!({ "name": format!("com.mumfrey:liteloader:{version_ll}"), "url": "http://dl.liteloader.com/versions/" })
    }];
    for lib in infos["libraries"].as_array().into_iter().flatten() {
        let mut l = json!({ "name": lib["name"] });
        if let Some(url) = lib["url"].as_str() {
            l["url"] = Value::String(url.to_string());
        }
        libs.push(l);
    }

    let vanilla = version::lire(dirs, mc)?;
    let base = vanilla["minecraftArguments"].as_str().unwrap_or_default();
    let v = json!({
        "id": id,
        "inheritsFrom": mc,
        "type": "release",
        "libraries": libs,
        "mainClass": "net.minecraft.launchwrapper.Launch",
        "minecraftArguments": format!("{base} --tweakClass {tweak}"),
    });
    let chemin = dirs.version_json(&id);
    std::fs::create_dir_all(chemin.parent().unwrap())?;
    std::fs::write(&chemin, serde_json::to_string_pretty(&v)?)?;
    Ok(id)
}
