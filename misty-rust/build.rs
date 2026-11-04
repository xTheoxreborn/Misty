// build.rs — compile l'interface Slint, intègre l'icône Windows et la clé CurseForge.

fn main() {
    // Interface : ui/main.slint (et tous les fichiers qu'il importe)
    slint_build::compile("ui/main.slint").expect("compilation de l'interface Slint");

    // Clé API CurseForge (privée, ignorée par git) : misty-rust/curseforge.key ou src/Misty/curseforge.key
    for chemin in ["curseforge.key", "../src/Misty/curseforge.key"] {
        println!("cargo:rerun-if-changed={chemin}");
        if let Ok(cle) = std::fs::read_to_string(chemin) {
            let cle = cle.trim();
            if !cle.is_empty() {
                println!("cargo:rustc-env=MISTY_CF_KEY={cle}");
                break;
            }
        }
    }

    // Notes de version affichées dans l'application
    println!("cargo:rerun-if-changed=../CHANGELOG.md");

    // Icône de l'exe (Windows)
    if std::env::var("CARGO_CFG_TARGET_OS").as_deref() == Ok("windows") {
        let mut res = winresource::WindowsResource::new();
        res.set_icon("assets/icone.ico");
        let _ = res.compile();
    }
}
