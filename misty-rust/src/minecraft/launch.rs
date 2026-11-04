//! Construction de la ligne de commande Java qui lance Minecraft.

use super::install::{self, VersionInstallee};
use super::version::{self, regles_ok};
use super::McDirs;
use crate::services::app_info;
use serde_json::Value;
use std::collections::{HashMap, HashSet};
use std::path::PathBuf;
use std::process::{Child, Command};

/// Compte utilisé pour la partie.
pub struct Joueur {
    pub pseudo: String,
    pub uuid: String,
    pub jeton: String,
    pub xuid: String,
    /// "msa" (compte Microsoft) ou "legacy" (hors-ligne).
    pub type_compte: String,
}

pub struct Options {
    pub joueur: Joueur,
    pub ram_mo: u32,
    /// Dossier de jeu (mods, options, mondes…).
    pub dossier_jeu: PathBuf,
}

/// Prépare les natives et lance le jeu. Renvoie le processus Java.
pub fn lancer(dirs: &McDirs, v: &VersionInstallee, options: &Options) -> anyhow::Result<Child> {
    Ok(preparer(dirs, v, options)?.spawn()?)
}

/// Prépare les natives et construit la commande, sans la lancer.
pub fn preparer(dirs: &McDirs, v: &VersionInstallee, options: &Options) -> anyhow::Result<Command> {
    std::fs::create_dir_all(&options.dossier_jeu)?;

    let libs = version::bibliotheques(dirs, &v.json);
    let natives = dirs.natives(&v.id);
    install::extraire_natives(&libs, &natives)?;
    if v.map_to_resources {
        install::copier_resources(&v.game_assets, &options.dossier_jeu);
    }

    // Classpath : bibliothèques (sans doublons, la première l'emporte) puis le jar du jeu
    let mut vus = HashSet::new();
    let mut classpath: Vec<String> = Vec::new();
    for lib in libs.iter().filter(|l| !l.natif_seul) {
        if vus.insert(lib.cle.clone()) {
            classpath.push(lib.chemin.to_string_lossy().to_string());
        }
    }
    classpath.push(v.jar.to_string_lossy().to_string());

    let j = &options.joueur;
    let valeurs: HashMap<&str, String> = HashMap::from([
        ("auth_player_name", j.pseudo.clone()),
        ("version_name", v.id.clone()),
        ("game_directory", options.dossier_jeu.to_string_lossy().to_string()),
        ("assets_root", dirs.assets().to_string_lossy().to_string()),
        ("game_assets", v.game_assets.to_string_lossy().to_string()),
        ("assets_index_name", v.assets_index.clone()),
        ("auth_uuid", j.uuid.clone()),
        ("auth_access_token", j.jeton.clone()),
        ("auth_session", j.jeton.clone()),
        ("auth_xuid", j.xuid.clone()),
        ("clientid", String::new()),
        ("user_type", j.type_compte.clone()),
        ("user_properties", "{}".into()),
        ("version_type", v.json.get("type").and_then(Value::as_str).unwrap_or("release").to_string()),
        ("natives_directory", natives.to_string_lossy().to_string()),
        ("launcher_name", "Misty".into()),
        ("launcher_version", app_info::VERSION.into()),
        ("classpath", classpath.join(";")),
        ("classpath_separator", ";".into()),
        ("library_directory", dirs.libraries().to_string_lossy().to_string()),
    ]);
    let remplacer = |texte: &str| -> String {
        let mut resultat = texte.to_string();
        // Plusieurs ${...} possibles dans le même argument ; on avance après chaque remplacement
        let mut pos = 0;
        while let Some(i) = resultat[pos..].find("${") {
            let debut = pos + i;
            let Some(fin) = resultat[debut..].find('}') else { break };
            let cle = resultat[debut + 2..debut + fin].to_string();
            let valeur = valeurs.get(cle.as_str()).cloned().unwrap_or_default();
            resultat.replace_range(debut..debut + fin + 1, &valeur);
            pos = debut + valeur.len();
        }
        resultat
    };

    let mut args: Vec<String> = vec![
        format!("-Xmx{}M", options.ram_mo),
        "-XX:+UnlockExperimentalVMOptions".into(),
        "-XX:+UseG1GC".into(),
        "-XX:G1NewSizePercent=20".into(),
        "-XX:G1ReservePercent=20".into(),
        "-XX:MaxGCPauseMillis=50".into(),
        "-XX:G1HeapRegionSize=16M".into(),
        "-Dlog4j2.formatMsgNoLookups=true".into(),
        "-Dminecraft.launcher.brand=misty".into(),
        format!("-Dminecraft.launcher.version={}", app_info::VERSION),
    ];

    let arguments = v.json.get("arguments");
    match arguments.and_then(|a| a.get("jvm")).and_then(Value::as_array) {
        Some(jvm) => args.extend(lire_arguments(jvm).iter().map(|a| remplacer(a))),
        None => {
            // Anciennes versions : arguments Java par défaut
            args.push("-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump".into());
            args.push(remplacer("-Djava.library.path=${natives_directory}"));
            args.push("-cp".into());
            args.push(remplacer("${classpath}"));
        }
    }
    if let Some((argument, chemin)) = &v.logging {
        args.push(argument.replace("${path}", &chemin.to_string_lossy()));
    }

    let classe = v.json.get("mainClass").and_then(Value::as_str).ok_or_else(|| anyhow::anyhow!("mainClass manquante"))?;
    args.push(classe.to_string());

    match arguments.and_then(|a| a.get("game")).and_then(Value::as_array) {
        Some(game) => args.extend(lire_arguments(game).iter().map(|a| remplacer(a))),
        None => {
            let ligne = v.json.get("minecraftArguments").and_then(Value::as_str).unwrap_or_default();
            args.extend(ligne.split_whitespace().map(remplacer));
        }
    }

    let mut commande = Command::new(&v.java);
    commande.args(&args).current_dir(&options.dossier_jeu);
    super::sans_console(&mut commande);
    Ok(commande)
}

/// Arguments du JSON (texte simple, ou objet avec règles et valeur(s)).
fn lire_arguments(liste: &[Value]) -> Vec<String> {
    let mut resultat = Vec::new();
    for arg in liste {
        match arg {
            Value::String(s) => resultat.push(s.clone()),
            Value::Object(o) => {
                if !regles_ok(o.get("rules")) {
                    continue;
                }
                match o.get("value") {
                    Some(Value::String(s)) => resultat.push(s.clone()),
                    Some(Value::Array(a)) => resultat.extend(a.iter().filter_map(|v| v.as_str().map(str::to_string))),
                    _ => {}
                }
            }
            _ => {}
        }
    }
    resultat
}
