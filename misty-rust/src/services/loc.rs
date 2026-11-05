//! Traductions. Les fichiers lang/*.json sont intégrés à l'exe.
//!
//! `t("Cle")` renvoie le texte dans la langue actuelle (ou en français, ou la clé elle-même).
//! `tf("Cle", &[&a, &b])` remplace {0}, {1}… par les arguments.

use super::settings;
use std::collections::HashMap;
use std::sync::RwLock;

const DEFAUT: &str = "fr";

/// (code, contenu JSON) de chaque langue intégrée.
const LANGUES: &[(&str, &str)] = &[
    ("fr", include_str!("../../lang/fr.json")),
    ("en", include_str!("../../lang/en.json")),
    ("es", include_str!("../../lang/es.json")),
    ("ru", include_str!("../../lang/ru.json")),
    ("it", include_str!("../../lang/it.json")),
];

#[derive(Clone)]
pub struct LangueInfo {
    pub code: String,
    pub nom: String,
    /// Code du drapeau (assets/flags/xx.png).
    pub pays: String,
}

struct Etat {
    code: String,
    textes: HashMap<String, String>,
    secours: HashMap<String, String>,
}

static ETAT: RwLock<Option<Etat>> = RwLock::new(None);

fn lire(code: &str) -> HashMap<String, String> {
    LANGUES
        .iter()
        .find(|(c, _)| *c == code)
        .and_then(|(_, json)| serde_json::from_str(json).ok())
        .unwrap_or_default()
}

fn existe(code: &str) -> bool {
    LANGUES.iter().any(|(c, _)| *c == code)
}

/// À appeler une fois au démarrage : langue enregistrée, sinon celle de Windows.
pub fn initialiser() {
    let mut code = settings::get(settings::LANGUE);
    if code.trim().is_empty() {
        code = sys_locale::get_locale()
            .map(|l| l.chars().take(2).collect::<String>().to_lowercase())
            .unwrap_or_else(|| DEFAUT.to_string());
    }
    charger(&code);
}

/// Change la langue. Langue non traduite : anglais (plus universel), sinon la langue par défaut.
pub fn charger(code: &str) {
    let code = if existe(code) {
        code
    } else if existe("en") {
        "en"
    } else {
        DEFAUT
    };
    let etat = Etat { code: code.to_string(), textes: lire(code), secours: lire(DEFAUT) };
    *ETAT.write().unwrap() = Some(etat);
}

/// Code de la langue actuelle ("fr", "en"…).
pub fn code() -> String {
    ETAT.read().unwrap().as_ref().map(|e| e.code.clone()).unwrap_or_else(|| DEFAUT.to_string())
}

/// Texte traduit.
pub fn t(cle: &str) -> String {
    let etat = ETAT.read().unwrap();
    let Some(etat) = etat.as_ref() else { return cle.to_string() };
    etat.textes
        .get(cle)
        .or_else(|| etat.secours.get(cle))
        .cloned()
        .unwrap_or_else(|| cle.to_string())
}

/// Texte traduit avec des arguments {0}, {1}…
pub fn tf(cle: &str, args: &[&dyn std::fmt::Display]) -> String {
    let mut texte = t(cle);
    for (i, arg) in args.iter().enumerate() {
        texte = texte.replace(&format!("{{{i}}}"), &arg.to_string());
    }
    texte
}

/// Langues disponibles, triées par nom.
pub fn disponibles() -> Vec<LangueInfo> {
    let mut liste: Vec<LangueInfo> = LANGUES
        .iter()
        .map(|(code, _)| {
            let d = lire(code);
            LangueInfo {
                code: code.to_string(),
                nom: d.get("_nom").cloned().unwrap_or_else(|| code.to_string()),
                pays: d.get("_drapeau").cloned().unwrap_or_else(|| code.to_string()),
            }
        })
        .collect();
    liste.sort_by(|a, b| a.nom.cmp(&b.nom));
    liste
}
