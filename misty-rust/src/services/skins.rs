//! Skin et capes d'un compte Microsoft (API api.minecraftservices.com).

use super::{account, http};
use image::{imageops, Rgba, RgbaImage};
use serde::Deserialize;

const API: &str = "https://api.minecraftservices.com/minecraft/profile";

#[derive(Clone, Debug)]
pub struct Cape {
    pub id: String,
    pub nom: String,
    pub url: String,
    pub active: bool,
}

#[derive(Clone, Debug, Default)]
pub struct Profil {
    pub skin_url: String,
    pub fin: bool,
    pub capes: Vec<Cape>,
}

#[derive(Deserialize)]
struct ProfilJson {
    #[serde(default)]
    skins: Vec<SkinJson>,
    #[serde(default)]
    capes: Vec<CapeJson>,
}

#[derive(Deserialize)]
struct SkinJson {
    #[serde(default)]
    state: String,
    #[serde(default)]
    url: String,
    #[serde(default)]
    variant: String,
}

#[derive(Deserialize)]
struct CapeJson {
    id: String,
    #[serde(default)]
    state: String,
    #[serde(default)]
    url: String,
    #[serde(default)]
    alias: String,
}

/// Jeton valide (renouvelé si besoin).
async fn jeton() -> anyhow::Result<String> {
    Ok(account::connecter().await?.jeton)
}

/// Skin et capes actuels du compte.
pub async fn profil() -> anyhow::Result<Profil> {
    let reponse = http::client().get(API).bearer_auth(jeton().await?).send().await?.error_for_status()?;
    let p: ProfilJson = reponse.json().await?;
    let skin = p.skins.iter().find(|s| s.state == "ACTIVE").or(p.skins.first());
    Ok(Profil {
        skin_url: skin.map(|s| s.url.clone()).unwrap_or_default(),
        fin: skin.map(|s| s.variant.eq_ignore_ascii_case("slim")).unwrap_or(false),
        capes: p
            .capes
            .into_iter()
            .map(|c| Cape { active: c.state == "ACTIVE", nom: majuscule(&c.alias), id: c.id, url: c.url })
            .collect(),
    })
}

fn majuscule(alias: &str) -> String {
    let mut c = alias.chars();
    match c.next() {
        Some(p) => p.to_uppercase().collect::<String>() + c.as_str(),
        None => "Cape".into(),
    }
}

pub async fn telecharger(url: &str) -> anyhow::Result<Vec<u8>> {
    Ok(http::client().get(url).send().await?.error_for_status()?.bytes().await?.to_vec())
}

/// Envoie un nouveau skin (PNG 64×64 ou 64×32).
pub async fn changer_skin(png: Vec<u8>, fin: bool) -> anyhow::Result<()> {
    let limite = "----MistyLimite7MA4YWxkTrZu0gW";
    let mut corps: Vec<u8> = Vec::new();
    corps.extend_from_slice(
        format!(
            "--{limite}\r\nContent-Disposition: form-data; name=\"variant\"\r\n\r\n{}\r\n--{limite}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"skin.png\"\r\nContent-Type: image/png\r\n\r\n",
            if fin { "slim" } else { "classic" }
        )
        .as_bytes(),
    );
    corps.extend_from_slice(&png);
    corps.extend_from_slice(format!("\r\n--{limite}--\r\n").as_bytes());
    let r = http::client()
        .post(format!("{API}/skins"))
        .bearer_auth(jeton().await?)
        .header("Content-Type", format!("multipart/form-data; boundary={limite}"))
        .body(corps)
        .send()
        .await?;
    verifier(r).await
}

/// Active une cape (`None` = ne plus en porter).
pub async fn activer_cape(id: Option<&str>) -> anyhow::Result<()> {
    let jeton = jeton().await?;
    let url = format!("{API}/capes/active");
    let r = match id {
        Some(id) => http::client().put(url).bearer_auth(jeton).json(&serde_json::json!({ "capeId": id })).send().await?,
        None => http::client().delete(url).bearer_auth(jeton).send().await?,
    };
    verifier(r).await
}

async fn verifier(r: reqwest::Response) -> anyhow::Result<()> {
    if r.status().is_success() {
        return Ok(());
    }
    let statut = r.status();
    let texte = r.text().await.unwrap_or_default();
    let detail = serde_json::from_str::<serde_json::Value>(&texte)
        .ok()
        .and_then(|v| v["errorMessage"].as_str().or(v["error"].as_str()).map(str::to_string))
        .unwrap_or(texte);
    anyhow::bail!("{statut} {detail}")
}

/// Un skin est valide s'il fait 64×64 ou 64×32 pixels.
pub fn skin_valide(png: &[u8]) -> bool {
    image::load_from_memory(png).map(|i| i.width() == 64 && (i.height() == 64 || i.height() == 32)).unwrap_or(false)
}

fn coller(dest: &mut RgbaImage, skin: &RgbaImage, src: (u32, u32, u32, u32), vers: (u32, u32), miroir: bool) {
    let (sx, sy, w, h) = src;
    for y in 0..h {
        for x in 0..w {
            let px = skin.get_pixel(if miroir { sx + w - 1 - x } else { sx + x }, sy + y);
            if px[3] == 0 {
                continue;
            }
            let (dx, dy) = (vers.0 + x, vers.1 + y);
            if px[3] == 255 {
                dest.put_pixel(dx, dy, *px);
            } else {
                let mut d = *dest.get_pixel(dx, dy);
                let a = px[3] as f32 / 255.0;
                for i in 0..3 {
                    d[i] = (px[i] as f32 * a + d[i] as f32 * (1.0 - a)) as u8;
                }
                d[3] = d[3].max(px[3]);
                dest.put_pixel(dx, dy, d);
            }
        }
    }
}

// ───────────────────────── 3D preview ─────────────────────────
// Tiny software ray caster: the player model is a handful of textured boxes,
// seen from the side with an orthographic camera that turns around the character.

/// Number of pre-rendered views for one full turn (keep in sync with ui/skin.slint).
pub const IMAGES_TOUR: usize = 36;
const ECHELLE: f32 = 9.0;
const LARGEUR: u32 = 160;
const HAUTEUR: u32 = 320;

/// Region of the texture used by one face of a box (x, y, width, height).
#[derive(Clone, Copy)]
struct Region(u32, u32, u32, u32);

struct Boite {
    min: [f32; 3],
    max: [f32; 3],
    /// +z, -z, -x, +x (front, back, character right, character left)
    faces: [Region; 4],
    /// the picture of each face is flipped horizontally
    miroir: bool,
}

/// Box of the player model, with the layout of the Minecraft skin (origin u,v and size w,h,d).
fn boite_skin(origine: (u32, u32), taille: (u32, u32, u32), min: [f32; 3], gonfle: f32, miroir: bool) -> Boite {
    let (u, v) = origine;
    let (w, h, d) = taille;
    let droite = Region(u, v + d, d, h);
    let devant = Region(u + d, v + d, w, h);
    let gauche = Region(u + d + w, v + d, d, h);
    let dos = Region(u + 2 * d + w, v + d, w, h);
    Boite {
        min: [min[0] - gonfle, min[1] - gonfle, min[2] - gonfle],
        max: [min[0] + w as f32 + gonfle, min[1] + h as f32 + gonfle, min[2] + d as f32 + gonfle],
        faces: if miroir { [devant, dos, gauche, droite] } else { [devant, dos, droite, gauche] },
        miroir,
    }
}

/// Cape: a thin box behind the body, its outside faces backwards.
fn boite_cape(f: u32) -> Boite {
    Boite {
        min: [-5.0, 8.0, -3.25],
        max: [5.0, 24.0, -2.25],
        faces: [Region(12 * f, f, 10 * f, 16 * f), Region(f, f, 10 * f, 16 * f), Region(0, f, f, 16 * f), Region(11 * f, f, f, 16 * f)],
        miroir: false,
    }
}

/// Entry point of a ray into a box: distance, axis and whether the face is on the positive side.
fn entrer(o: [f32; 3], d: [f32; 3], b: &Boite) -> Option<(f32, usize, bool)> {
    let (mut tmin, mut tmax) = (f32::NEG_INFINITY, f32::INFINITY);
    let (mut axe, mut positif) = (1, false);
    for i in 0..3 {
        if d[i].abs() < 1e-6 {
            if o[i] < b.min[i] || o[i] > b.max[i] {
                return None;
            }
            continue;
        }
        let t1 = (b.min[i] - o[i]) / d[i];
        let t2 = (b.max[i] - o[i]) / d[i];
        let (proche, loin) = if t1 < t2 { (t1, t2) } else { (t2, t1) };
        if proche > tmin {
            tmin = proche;
            axe = i;
            positif = d[i] < 0.0;
        }
        tmax = tmax.min(loin);
    }
    if tmin > tmax || tmax < 0.0 || axe == 1 {
        return None;
    }
    Some((tmin, axe, positif))
}

fn echantillon(b: &Boite, texture: &RgbaImage, p: [f32; 3], axe: usize, positif: bool) -> Rgba<u8> {
    let largeur = b.max[axe_largeur(axe)] - b.min[axe_largeur(axe)];
    let (face, mut fu) = match (axe, positif) {
        (2, true) => (0, (p[0] - b.min[0]) / largeur),
        (2, false) => (1, (b.max[0] - p[0]) / largeur),
        (0, false) => (2, (p[2] - b.min[2]) / largeur),
        _ => (3, (b.max[2] - p[2]) / largeur),
    };
    if b.miroir {
        fu = 1.0 - fu;
    }
    let fv = (b.max[1] - p[1]) / (b.max[1] - b.min[1]);
    let Region(x, y, w, h) = b.faces[face];
    let tx = x + ((fu.clamp(0.0, 0.9999)) * w as f32) as u32;
    let ty = y + ((fv.clamp(0.0, 0.9999)) * h as f32) as u32;
    *texture.get_pixel(tx.min(texture.width() - 1), ty.min(texture.height() - 1))
}

fn axe_largeur(axe: usize) -> usize {
    // faces facing z are as wide as the box is along x, faces facing x as the box is deep along z
    if axe == 2 {
        0
    } else {
        2
    }
}

/// The character turning on itself: `IMAGES_TOUR` views, the first one seen from the front.
/// The cape (texture bytes of the whole cape) is only visible from behind.
pub fn rendre_tour(png: &[u8], fin: bool, cape: Option<&[u8]>) -> Option<Vec<RgbaImage>> {
    let skin = image::load_from_memory(png).ok()?.to_rgba8();
    if skin.width() != 64 || skin.height() < 32 {
        return None;
    }
    let cape_img = cape.and_then(|c| image::load_from_memory(c).ok()).map(|i| i.to_rgba8());
    let moderne = skin.height() >= 64;
    let bras: u32 = if fin { 3 } else { 4 };
    let b = bras as f32;

    let mut boites: Vec<Boite> = vec![
        boite_skin((0, 0), (8, 8, 8), [-4.0, 24.0, -4.0], 0.0, false),
        boite_skin((16, 16), (8, 12, 4), [-4.0, 12.0, -2.0], 0.0, false),
        boite_skin((40, 16), (bras, 12, 4), [-4.0 - b, 12.0, -2.0], 0.0, false),
        boite_skin((0, 16), (4, 12, 4), [-4.0, 0.0, -2.0], 0.0, false),
    ];
    if moderne {
        boites.push(boite_skin((32, 48), (bras, 12, 4), [4.0, 12.0, -2.0], 0.0, false));
        boites.push(boite_skin((16, 48), (4, 12, 4), [0.0, 0.0, -2.0], 0.0, false));
    } else {
        boites.push(boite_skin((40, 16), (bras, 12, 4), [4.0, 12.0, -2.0], 0.0, true));
        boites.push(boite_skin((0, 16), (4, 12, 4), [0.0, 0.0, -2.0], 0.0, true));
    }
    // Clothes layer, a little bigger than the base layer
    boites.push(boite_skin((32, 0), (8, 8, 8), [-4.0, 24.0, -4.0], 0.5, false));
    if moderne {
        boites.push(boite_skin((16, 32), (8, 12, 4), [-4.0, 12.0, -2.0], 0.25, false));
        boites.push(boite_skin((40, 32), (bras, 12, 4), [-4.0 - b, 12.0, -2.0], 0.25, false));
        boites.push(boite_skin((48, 48), (bras, 12, 4), [4.0, 12.0, -2.0], 0.25, false));
        boites.push(boite_skin((0, 32), (4, 12, 4), [-4.0, 0.0, -2.0], 0.25, false));
        boites.push(boite_skin((0, 48), (4, 12, 4), [0.0, 0.0, -2.0], 0.25, false));
    }
    let nb_skin = boites.len();
    let facteur = cape_img.as_ref().map(|c| (c.width() / 64).max(1)).unwrap_or(1);
    if cape_img.is_some() {
        boites.push(boite_cape(facteur));
    }

    let mut vues = Vec::with_capacity(IMAGES_TOUR);
    for k in 0..IMAGES_TOUR {
        let a = k as f32 * std::f32::consts::TAU / IMAGES_TOUR as f32;
        let (s, c) = a.sin_cos();
        // the viewer is at (sin, 0, cos) and looks at the character; screen right is (cos, 0, -sin)
        let regard = [-s, 0.0, -c];
        let droite = [c, 0.0, -s];
        let mut vue = RgbaImage::from_pixel(LARGEUR, HAUTEUR, Rgba([0, 0, 0, 0]));
        for py in 0..HAUTEUR {
            let sy = (HAUTEUR as f32 / 2.0 - (py as f32 + 0.5)) / ECHELLE + 16.0;
            for px in 0..LARGEUR {
                let sx = (px as f32 + 0.5 - LARGEUR as f32 / 2.0) / ECHELLE;
                let o = [droite[0] * sx - regard[0] * 100.0, sy, droite[2] * sx - regard[2] * 100.0];
                let mut meilleur: Option<(f32, Rgba<u8>)> = None;
                for (i, boite) in boites.iter().enumerate() {
                    let Some((t, axe, positif)) = entrer(o, regard, boite) else { continue };
                    if meilleur.map(|(m, _)| t >= m).unwrap_or(false) {
                        continue;
                    }
                    let p = [o[0] + regard[0] * t, o[1], o[2] + regard[2] * t];
                    let texture = if i >= nb_skin { cape_img.as_ref().unwrap() } else { &skin };
                    let couleur = echantillon(boite, texture, p, axe, positif);
                    if couleur[3] >= 128 {
                        meilleur = Some((t, Rgba([couleur[0], couleur[1], couleur[2], 255])));
                    }
                }
                if let Some((_, couleur)) = meilleur {
                    vue.put_pixel(px, py, couleur);
                }
            }
        }
        vues.push(vue);
    }
    Some(vues)
}

/// Tête du skin avec le chapeau, agrandie à 64×64 sans lissage.
pub fn rendre_tete(png: &[u8]) -> Option<RgbaImage> {
    let skin = image::load_from_memory(png).ok()?.to_rgba8();
    if skin.width() != 64 || skin.height() < 32 {
        return None;
    }
    let mut r = RgbaImage::from_pixel(8, 8, Rgba([0, 0, 0, 0]));
    coller(&mut r, &skin, (8, 8, 8, 8), (0, 0), false);
    coller(&mut r, &skin, (40, 8, 8, 8), (0, 0), false);
    Some(imageops::resize(&r, 64, 64, imageops::FilterType::Nearest))
}

/// Face extérieure d'une cape (10×16 sur une texture 64×32, ou un multiple).
pub fn rendre_cape(png: &[u8]) -> Option<RgbaImage> {
    let cape = image::load_from_memory(png).ok()?.to_rgba8();
    let f = (cape.width() / 64).max(1);
    if cape.width() < 22 * f || cape.height() < 17 * f {
        return None;
    }
    Some(imageops::crop_imm(&cape, f, f, 10 * f, 16 * f).to_image())
}
