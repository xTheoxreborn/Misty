//! Tests (lancer avec `cargo test -- --ignored --nocapture` : ils utilisent Internet).
//! Ils installent / vérifient des versions dans %LOCALAPPDATA%\Misty mais ne lancent jamais le jeu.

use crate::minecraft::{install, launch, loaders, FileProgress, McDirs};
use crate::services::{account, loc};
use std::sync::atomic::{AtomicUsize, Ordering};
use std::sync::Arc;

fn compteur() -> (FileProgress, Arc<AtomicUsize>) {
    let n = Arc::new(AtomicUsize::new(0));
    let n2 = n.clone();
    (Arc::new(move |_, _, _| { n2.fetch_add(1, Ordering::Relaxed); }), n)
}

/// Vérifie qu'une version installée produit une commande de lancement cohérente.
async fn verifier(id: &str) {
    let dirs = McDirs::misty();
    let (prog, n) = compteur();
    let v = install::installer(&dirs, id, prog).await.unwrap_or_else(|e| panic!("{id} : {e:#}"));
    let options = launch::Options {
        joueur: launch::Joueur {
            pseudo: "Test".into(),
            uuid: account::uuid_hors_ligne("Test"),
            jeton: "0".into(),
            xuid: String::new(),
            type_compte: "legacy".into(),
        },
        ram_mo: 2048,
        dossier_jeu: std::env::temp_dir().join("misty-test-jeu"),
    };
    let cmd = launch::preparer(&dirs, &v, &options).unwrap();
    let args: Vec<String> = cmd.get_args().map(|a| a.to_string_lossy().to_string()).collect();
    let cp_index = args.iter().position(|a| a == "-cp" || a == "-classpath").expect("classpath");
    let cp = &args[cp_index + 1];
    let manquants: Vec<&str> = cp.split(';').filter(|p| !std::path::Path::new(p).exists()).collect();
    let classe = v.json["mainClass"].as_str().unwrap();
    assert!(args.iter().any(|a| a == classe), "classe principale absente");
    assert!(!args.iter().any(|a| a.contains("${")), "variable non remplacée : {:?}", args.iter().find(|a| a.contains("${")));
    println!(
        "{id:<40} java={} fichiers téléchargés={} classpath={} manquants={:?} main={classe}",
        v.java.file_name().unwrap().to_string_lossy(),
        n.load(Ordering::Relaxed),
        cp.split(';').count(),
        manquants
    );
    assert!(manquants.is_empty(), "{id} : fichiers manquants dans le classpath");
}

#[tokio::test]
#[ignore]
async fn versions_installees() {
    loc::initialiser();
    for id in ["1.21.1", "1.8.9", "1.7.10", "1.18.1-forge-39.1.0", "1.8.9-OptiFine_HD_U_M6_pre2", "1.8.9-forge1.8.9-11.15.1.2318-1.8.9"] {
        verifier(id).await;
    }
}

#[tokio::test]
#[ignore]
async fn modes_disponibles() {
    for v in ["1.21.1", "1.20.1", "1.12.2", "1.8.9", "1.7.10"] {
        println!("{v:<8} {:?}", loaders::modes_disponibles(v).await);
    }
}

#[tokio::test]
#[ignore]
async fn fabric_et_forge() {
    loc::initialiser();
    let dirs = McDirs::misty();
    for (version, mode) in [("1.21.1", loaders::FABRIC), ("1.21.1", loaders::NEOFORGE), ("1.21.1", loaders::FORGE)] {
        let (prog, _) = compteur();
        let nom = loaders::installer(&dirs, version, mode, None, prog).await.unwrap_or_else(|e| panic!("{mode} : {e:#}"));
        verifier(&nom).await;
    }
}

/// Dev tool: MISTY_SKIN=<skin.png> MISTY_OUT=<out.png> cargo test apercu_3d -- --ignored
#[test]
#[ignore]
fn apercu_3d() {
    let png = std::fs::read(std::env::var("MISTY_SKIN").unwrap()).unwrap();
    let mut cape = image::RgbaImage::from_pixel(64, 32, image::Rgba([60, 200, 240, 255]));
    for x in 0..64 {
        cape.put_pixel(x, 5, image::Rgba([255, 255, 255, 255]));
    }
    let mut octets = std::io::Cursor::new(Vec::new());
    cape.write_to(&mut octets, image::ImageFormat::Png).unwrap();
    let vues = crate::services::skins::rendre_tour(&png, false, Some(&octets.into_inner())).unwrap();
    let choix = [0usize, 5, 9, 14, 18, 27];
    let mut feuille = image::RgbaImage::from_pixel(160 * choix.len() as u32, 320, image::Rgba([90, 120, 160, 255]));
    for (i, k) in choix.iter().enumerate() {
        image::imageops::overlay(&mut feuille, &vues[*k], 160 * i as i64, 0);
    }
    feuille.save(std::env::var("MISTY_OUT").unwrap()).unwrap();
}
