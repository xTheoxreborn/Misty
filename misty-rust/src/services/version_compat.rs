//! Plages de versions Minecraft supportées par chaque mod loader
//! (filtre rapide avant d'interroger les API des loaders).

type V = (u32, u32, u32);

const MAX_CONNUE: V = (26, 5, 0);

pub fn forge(version: &str) -> bool {
    entre(version, (1, 7, 10), MAX_CONNUE)
}
pub fn optifine(version: &str) -> bool {
    entre(version, (1, 7, 2), MAX_CONNUE)
}
pub fn neoforge(version: &str) -> bool {
    entre(version, (1, 20, 1), MAX_CONNUE)
}
pub fn fabric(version: &str) -> bool {
    entre(version, (1, 14, 0), MAX_CONNUE)
}
/// Fork de Fabric : mêmes versions. (Quilt est désactivé dans la liste des modes, comme dans la version C#.)
#[allow(dead_code)]
pub fn quilt(version: &str) -> bool {
    entre(version, (1, 14, 0), MAX_CONNUE)
}
pub fn liteloader(version: &str) -> bool {
    entre(version, (1, 5, 2), (1, 12, 2))
}

/// "1.20" -> (1, 20, 0). None si ce n'est pas un numéro de release (snapshot, bêta…).
pub fn parser(version: &str) -> Option<V> {
    let morceaux: Vec<&str> = version.split('.').collect();
    if !(2..=3).contains(&morceaux.len()) {
        return None;
    }
    let n = |i: usize| -> Option<u32> { morceaux.get(i).map_or(Some(0), |s| s.parse().ok()) };
    Some((n(0)?, n(1)?, n(2)?))
}

fn entre(version: &str, min: V, max: V) -> bool {
    parser(version).is_some_and(|v| v >= min && v <= max)
}
