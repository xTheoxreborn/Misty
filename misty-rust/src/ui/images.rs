//! Images : bloc d'herbe en pixel art, icônes distantes (avec cache), images locales.

use super::App;
use slint::{Image, Rgba8Pixel, SharedPixelBuffer};
use std::cell::RefCell;
use std::collections::HashMap;
use std::path::Path;

/// Cache des icônes distantes (thread de l'interface seulement).
#[derive(Default)]
pub struct Cache {
    images: RefCell<HashMap<String, Option<Image>>>,
}

/// Face avant d'un bloc d'herbe 16×16 (motif toujours identique).
pub fn bloc_herbe() -> Image {
    const HERBE: [[u8; 3]; 4] = [[0x5D, 0xBB, 0x4A], [0x4C, 0xA4, 0x3B], [0x6F, 0xCF, 0x5A], [0x56, 0xB0, 0x44]];
    const TERRE: [[u8; 3]; 4] = [[0x8B, 0x5A, 0x3C], [0x7A, 0x4E, 0x33], [0x9C, 0x6B, 0x47], [0x6B, 0x43, 0x29]];
    let mut graine: u32 = 42;
    let mut hasard = move |n: u32| {
        graine = graine.wrapping_mul(1_664_525).wrapping_add(1_013_904_223);
        (graine >> 24) % n
    };
    let mut buffer = SharedPixelBuffer::<Rgba8Pixel>::new(16, 16);
    let largeur = 16;
    let pixels = buffer.make_mut_slice();
    for x in 0..16 {
        // L'herbe « coule » sur 3 à 6 pixels selon la colonne
        let hauteur_herbe = 3 + hasard(4) as usize;
        for y in 0..16 {
            let c = if y < hauteur_herbe { HERBE[hasard(4) as usize] } else { TERRE[hasard(4) as usize] };
            pixels[y * largeur + x] = Rgba8Pixel { r: c[0], g: c[1], b: c[2], a: 255 };
        }
    }
    Image::from_rgba8(buffer)
}

/// Décode une image (fichier ou octets) en tampon de pixels, réduite à `max` pixels de large.
pub fn decoder(octets: &[u8], max: u32) -> Option<SharedPixelBuffer<Rgba8Pixel>> {
    let img = image::load_from_memory(octets).ok()?;
    let img = if max > 0 && img.width() > max {
        img.resize(max, max * img.height() / img.width().max(1), image::imageops::FilterType::Triangle)
    } else {
        img
    };
    Some(tampon(&img.to_rgba8()))
}

pub fn tampon(rgba: &image::RgbaImage) -> SharedPixelBuffer<Rgba8Pixel> {
    SharedPixelBuffer::<Rgba8Pixel>::clone_from_slice(rgba.as_raw(), rgba.width(), rgba.height())
}

/// Image locale chargée en mémoire (le fichier n'est pas verrouillé). None si illisible.
pub fn depuis_fichier(chemin: &Path, max: u32) -> Option<Image> {
    let octets = std::fs::read(chemin).ok()?;
    decoder(&octets, max).map(Image::from_rgba8)
}


/// Image distante (icône de projet…), téléchargée en arrière-plan et gardée en cache.
pub async fn depuis_url(app: &App, url: &str) -> Option<Image> {
    if url.is_empty() {
        return None;
    }
    if let Some(img) = app.images.images.borrow().get(url) {
        return img.clone();
    }
    let u = url.to_string();
    let tampon = app
        .bg(async move {
            let reponse = crate::services::http::client().get(&u).send().await.ok()?.error_for_status().ok()?;
            let octets = reponse.bytes().await.ok()?;
            tokio::task::spawn_blocking(move || decoder(&octets, 96)).await.ok()?
        })
        .await;
    let image = tampon.map(Image::from_rgba8);
    app.images.images.borrow_mut().insert(url.to_string(), image.clone());
    image
}

/// Image vide (pour les champs `image` des modèles quand il n'y a rien à afficher).
pub fn vide() -> Image {
    Image::default()
}
