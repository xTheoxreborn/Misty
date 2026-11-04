//! Éditeur d'un pack de textures : catégories, grille des textures du template, dessin intégré au pixel.
//! Une texture modifiée est copiée dans le pack ; « Réinitialiser » la supprime pour revenir à l'originale.

use super::{dialogs, images, lancer, maj_ligne, modele, App};
use crate::services::packs::{self, PackInfo, RACINE_TEXTURES};
use crate::services::{loc, system_info, texture_categories};
use crate::{CategoryItem, EditorState, TextureItem};
use slint::{Color, ComponentHandle, Image, ModelRc, Rgba8Pixel, SharedPixelBuffer};
use std::collections::HashMap;
use std::path::{Path, PathBuf};
use std::rc::Rc;
use std::time::{Duration, SystemTime};

const ZONE_TAILLE: u32 = 250;
const MAX_RECENTES: usize = 12;

const CRAYON: i32 = 0;
const GOMME: i32 = 1;
const POT: i32 = 2;
const PIPETTE: i32 = 3;

/// Une texture dans la grille.
#[derive(Clone)]
struct Texture {
    /// Relatif au dossier textures, ex. "block/stone.png"
    chemin: String,
    categorie: &'static str,
    chemin_pack: PathBuf,
    chemin_template: PathBuf,
    modifiee: bool,
    miniature: Option<Image>,
}

impl Texture {
    /// La version du pack si elle existe, sinon l'originale.
    fn fichier(&self) -> &Path {
        if self.modifiee {
            &self.chemin_pack
        } else {
            &self.chemin_template
        }
    }
}

#[derive(Default)]
pub struct Etat {
    pack: Option<PackInfo>,
    dossier_template: Option<PathBuf>,
    toutes: Vec<Texture>,
    categorie: String,
    tags: Vec<String>,
    /// false = « Toutes les textures », true = « Mes textures »
    perso: bool,
    /// Listes de miniatures périmées
    generation: u32,
    /// Chargements de pack périmés
    chargement_pack: u32,
    affichees: Vec<usize>,
    modele: Option<ModelRc<TextureItem>>,
    courante: Option<usize>,

    // Dessin
    largeur: u32,
    hauteur: u32,
    zoom: u32,
    /// Pixels RGBA
    px: Vec<u8>,
    avant_trait: Option<Vec<u8>>,
    annuler: Vec<Vec<u8>>,
    refaire: Vec<Vec<u8>>,
    modifie: bool,
    dessin: bool,
    outil: i32,
    couleur: [u8; 3],
    dernier: Option<(u32, u32)>,
    recentes: Vec<[u8; 3]>,
    maj_couleur: bool,

    // Éditeur externe : on recharge la texture quand son fichier change
    externe: Option<usize>,
    date_externe: Option<SystemTime>,
    surveillance: slint::Timer,
}

fn s(app: &App) -> EditorState<'_> {
    app.ui.global::<EditorState>()
}

fn couleur_slint(c: [u8; 3]) -> Color {
    Color::from_rgb_u8(c[0], c[1], c[2])
}

pub fn installer(app: &Rc<App>) {
    let st = s(app);
    st.set_checker(damier(16, 16));
    app.editor.borrow_mut().zoom = 10;
    set_couleur(app, [0, 0, 0], true);

    let a = app.clone();
    st.on_back(move || {
        enregistrer_si_besoin(&a);
        super::naviguer(&a, super::page::PACKS);
        super::packs::rafraichir(&a);
    });
    let a = app.clone();
    st.on_change_icon(move || {
        let a = a.clone();
        lancer(async move { changer_icone(&a).await });
    });
    let a = app.clone();
    st.on_search_changed(move |_| {
        if !a.editor.borrow().toutes.is_empty() {
            afficher_textures(&a);
        }
    });
    let a = app.clone();
    st.on_folder(move || {
        if let Some(p) = a.editor.borrow().pack.clone() {
            system_info::ouvrir_dossier(&p.dossier);
        }
    });
    let a = app.clone();
    st.on_export(move || {
        let Some(p) = a.editor.borrow().pack.clone() else { return };
        enregistrer_si_besoin(&a);
        let a = a.clone();
        lancer(async move {
            s(&a).set_export_enabled(false);
            super::packs::exporter(&a, &p).await;
            s(&a).set_export_enabled(true);
        });
    });
    let a = app.clone();
    st.on_mode_changed(move |m| {
        a.editor.borrow_mut().perso = m == 1;
        if !a.editor.borrow().toutes.is_empty() {
            construire_categories(&a);
        }
    });
    let a = app.clone();
    st.on_category_changed(move |i| {
        let tag = a.editor.borrow().tags.get(i as usize).cloned().unwrap_or_default();
        a.editor.borrow_mut().categorie = tag;
        s(&a).set_search("".into());
        afficher_textures(&a);
    });
    let a = app.clone();
    st.on_select(move |i| selectionner(&a, i as usize));
    let a = app.clone();
    st.on_zoom_changed(move |z| {
        a.editor.borrow_mut().zoom = z.round().clamp(1.0, 40.0) as u32;
        appliquer_zoom(&a);
    });
    let a = app.clone();
    st.on_grid_toggled(move |_| appliquer_zoom(&a));
    let a = app.clone();
    st.on_tool_changed(move |t| choisir_outil(&a, t));
    let a = app.clone();
    st.on_pointer_down(move |x, y, droit| pointeur_bas(&a, x, y, droit));
    let a = app.clone();
    st.on_pointer_move(move |x, y| {
        let (dessin, outil) = {
            let e = a.editor.borrow();
            (e.dessin, e.outil)
        };
        if dessin && outil != POT && outil != PIPETTE {
            appliquer(&a, x, y);
        }
    });
    let a = app.clone();
    st.on_pointer_up(move || finir_trait(&a));
    let a = app.clone();
    st.on_rgb_changed(move || {
        if a.editor.borrow().maj_couleur {
            return;
        }
        let st = s(&a);
        set_couleur(&a, [st.get_red() as u8, st.get_green() as u8, st.get_blue() as u8], true);
    });
    let a = app.clone();
    st.on_hex_changed(move |texte| {
        if let Some(c) = lire_hex(&texte) {
            set_couleur(&a, c, false);
        }
    });
    let a = app.clone();
    st.on_pick_color(move |c| {
        set_couleur(&a, [c.red(), c.green(), c.blue()], true);
        let outil = a.editor.borrow().outil;
        if outil == GOMME || outil == PIPETTE {
            choisir_outil(&a, CRAYON);
        }
    });
    let a = app.clone();
    st.on_undo(move || annuler(&a));
    let a = app.clone();
    st.on_redo(move || refaire(&a));
    let a = app.clone();
    st.on_save(move || enregistrer(&a));
    let a = app.clone();
    st.on_external(move || {
        let a = a.clone();
        lancer(async move { editeur_externe(&a).await });
    });
    let a = app.clone();
    st.on_reset(move || {
        let a = a.clone();
        lancer(async move { reinitialiser(&a).await });
    });
    let a = app.clone();
    st.on_key(move |texte, ctrl| touche(&a, &texte, ctrl));
}

// ───────────────────────── Chargement ─────────────────────────

pub fn ouvrir(app: &Rc<App>, p: PackInfo) {
    super::naviguer(app, super::page::EDITEUR);
    let a = app.clone();
    lancer(async move { charger(&a, p).await });
}

async fn charger(app: &Rc<App>, p: PackInfo) {
    let moi = {
        let mut e = app.editor.borrow_mut();
        e.chargement_pack += 1;
        e.pack = Some(p.clone());
        e.toutes.clear();
        e.perso = false;
        e.categorie.clear();
        e.externe = None;
        e.surveillance.stop();
        e.chargement_pack
    };
    fermer_texture(app);
    let st = s(app);
    st.set_mode(0);
    maj_mode_perso(app, 0);
    st.set_pack_name(p.nom.to_uppercase().into());
    maj_icone(app);
    st.set_categories(modele(Vec::new()));
    st.set_textures(modele(Vec::new()));
    st.set_search("".into());
    st.set_info(format!("MINECRAFT {}", p.version_mc).into());
    message(app, &loc::tf("Editeur_ChargementTemplate", &[&p.version_mc]));

    let progression = app.progression(move |ui, texte| {
        if crate::APP.with(|a| a.borrow().as_ref().map(|a| a.editor.borrow().chargement_pack)) == Some(moi) {
            ui.global::<EditorState>().set_message(texte.into());
        }
    });
    let version = p.version_mc.clone();
    let resultat = app.bg(async move { packs::template(&version, progression).await }).await;
    if app.editor.borrow().chargement_pack != moi {
        return; // un autre pack a été ouvert entre-temps
    }
    let dossier = match resultat {
        Ok((d, _)) => d,
        Err(e) => {
            message(app, "");
            dialogs::erreur(app, &loc::t("Editeur_TemplateIndisponible"), &format!("{e:#}")).await;
            super::naviguer(app, super::page::PACKS);
            super::packs::rafraichir(app);
            return;
        }
    };

    let (d, dp) = (dossier.clone(), p.dossier.clone());
    // Liste lue hors du thread de l'interface (les images Slint, elles, ne peuvent pas changer de thread)
    let brute = app
        .bloquant(move || {
            packs::lister_textures(&d)
                .into_iter()
                .map(|chemin| {
                    let relatif = format!("{RACINE_TEXTURES}{chemin}");
                    let chemin_pack = dp.join(&relatif);
                    let modifiee = chemin_pack.exists();
                    (chemin, chemin_pack, d.join(&relatif), modifiee)
                })
                .collect::<Vec<_>>()
        })
        .await;
    let liste: Vec<Texture> = brute
        .into_iter()
        .map(|(chemin, chemin_pack, chemin_template, modifiee)| Texture {
            categorie: texture_categories::categorie(&chemin),
            chemin,
            chemin_pack,
            chemin_template,
            modifiee,
            miniature: None,
        })
        .collect();
    if app.editor.borrow().chargement_pack != moi {
        return;
    }
    {
        let mut e = app.editor.borrow_mut();
        e.toutes = liste;
        e.dossier_template = Some(dossier);
    }
    maj_icone(app); // pack.png du template maintenant disponible
    construire_categories(app);
    maj_info(app);
}

/// À appeler quand on quitte la page : enregistre la texture en cours.
pub fn quitter(app: &Rc<App>) {
    enregistrer_si_besoin(app);
}

pub fn langue_changee(app: &Rc<App>) {
    if app.editor.borrow().pack.is_none() {
        return;
    }
    maj_info(app);
    construire_categories(app);
}

fn message(app: &App, texte: &str) {
    s(app).set_message(texte.into());
}

fn maj_icone(app: &App) {
    let icone = app.editor.borrow().pack.as_ref().and_then(|p| p.icone());
    let image = icone.and_then(|f| images::depuis_fichier(&f, 128));
    s(app).set_has_pack_icon(image.is_some());
    s(app).set_pack_icon(image.unwrap_or_default());
}

fn maj_info(app: &App) {
    let (version, total, modifiees) = {
        let e = app.editor.borrow();
        (e.pack.as_ref().map(|p| p.version_mc.clone()).unwrap_or_default(), e.toutes.len(), e.toutes.iter().filter(|t| t.modifiee).count())
    };
    maj_mode_perso(app, modifiees);
    s(app).set_info(loc::tf("Editeur_Info", &[&version, &total, &modifiees]).into());
}

fn maj_mode_perso(app: &App, modifiees: usize) {
    s(app).set_perso_text(format!("{} ({modifiees})", loc::t("Editeur_Perso")).into());
}

fn construire_categories(app: &Rc<App>) {
    let (perso, categorie, entrees, total_source) = {
        let e = app.editor.borrow();
        let source: Vec<&Texture> = e.toutes.iter().filter(|t| !e.perso || t.modifiee).collect();
        // (étiquette technique, nombre). "" = toutes les catégories (mode perso uniquement)
        let mut entrees: Vec<(String, usize)> = Vec::new();
        if e.perso {
            entrees.push((String::new(), source.len()));
        }
        for nom in texture_categories::ORDRE {
            let n = source.iter().filter(|t| t.categorie == *nom).count();
            if n > 0 {
                entrees.push((nom.to_string(), n));
            }
        }
        (e.perso, e.categorie.clone(), entrees, source.len())
    };

    if entrees.is_empty() || (perso && total_source == 0) {
        s(app).set_categories(modele(Vec::new()));
        s(app).set_textures(modele(Vec::new()));
        app.editor.borrow_mut().affichees.clear();
        message(app, &loc::t("Editeur_Aucune"));
        return;
    }

    // On garde la catégorie en cours si elle existe encore dans ce mode
    let cible = if entrees.iter().any(|(t, _)| *t == categorie) { categorie } else { entrees[0].0.clone() };
    let items: Vec<CategoryItem> = entrees
        .iter()
        .map(|(t, n)| CategoryItem {
            name: if t.is_empty() { loc::t("Editeur_Toutes") } else { loc::t(&format!("Cat_{t}")) }.into(),
            count: n.to_string().into(),
        })
        .collect();
    s(app).set_categories(modele(items));
    s(app).set_category_index(entrees.iter().position(|(t, _)| *t == cible).unwrap_or(0) as i32);
    {
        let mut e = app.editor.borrow_mut();
        e.tags = entrees.into_iter().map(|(t, _)| t).collect();
        e.categorie = cible;
    }
    s(app).set_search("".into());
    afficher_textures(app);
}

fn afficher_textures(app: &Rc<App>) {
    let filtre = s(app).get_search().trim().to_lowercase();
    let (affichees, items, generation) = {
        let mut e = app.editor.borrow_mut();
        let source = (0..e.toutes.len()).filter(|&i| !e.perso || e.toutes[i].modifiee);
        let affichees: Vec<usize> = if filtre.is_empty() {
            source.filter(|&i| e.categorie.is_empty() || e.toutes[i].categorie == e.categorie).collect()
        } else {
            source.filter(|&i| e.toutes[i].chemin.to_lowercase().contains(&filtre)).take(400).collect()
        };
        let items: Vec<TextureItem> = affichees
            .iter()
            .map(|&i| {
                let t = &e.toutes[i];
                TextureItem {
                    path: t.chemin.clone().into(),
                    thumb: t.miniature.clone().unwrap_or_default(),
                    has_thumb: t.miniature.is_some(),
                    modified: t.modifiee,
                }
            })
            .collect();
        e.generation += 1;
        e.affichees = affichees.clone();
        (affichees, items, e.generation)
    };

    message(
        app,
        &if !affichees.is_empty() {
            String::new()
        } else if !filtre.is_empty() {
            loc::tf("Editeur_AucunResultat", &[&filtre])
        } else {
            loc::t("Editeur_Aucune")
        },
    );
    let m = modele(items);
    s(app).set_textures(m.clone());
    s(app).set_scroll_reset(s(app).get_scroll_reset() + 1);
    app.editor.borrow_mut().modele = Some(m.clone());
    maj_selection(app);

    // Miniatures décodées par paquets, hors du thread de l'interface
    let a = app.clone();
    lancer(async move {
        let a_charger: Vec<(usize, usize, PathBuf)> = {
            let e = a.editor.borrow();
            affichees
                .iter()
                .enumerate()
                .filter(|(_, &i)| e.toutes[i].miniature.is_none())
                .map(|(ligne, &i)| (ligne, i, e.toutes[i].fichier().to_path_buf()))
                .collect()
        };
        for lot in a_charger.chunks(40) {
            let chemins: Vec<PathBuf> = lot.iter().map(|(_, _, c)| c.clone()).collect();
            let tampons = a.bloquant(move || chemins.iter().map(|c| miniature(c)).collect::<Vec<_>>()).await;
            if a.editor.borrow().generation != generation {
                return; // une autre liste est affichée entre-temps
            }
            for ((ligne, i, _), tampon) in lot.iter().zip(tampons) {
                let Some(t) = tampon else { continue };
                let img = Image::from_rgba8(t);
                a.editor.borrow_mut().toutes[*i].miniature = Some(img.clone());
                maj_ligne(&m, *ligne, |item| {
                    item.thumb = img;
                    item.has_thumb = true;
                });
            }
        }
    });
}

/// Miniature : pour une texture animée (bande verticale d'images carrées), seule la 1re image.
fn miniature(chemin: &Path) -> Option<SharedPixelBuffer<Rgba8Pixel>> {
    let img = image::open(chemin).ok()?.to_rgba8();
    let (l, h) = img.dimensions();
    let img = if h > l && l > 0 && h % l == 0 { image::imageops::crop_imm(&img, 0, 0, l, l).to_image() } else { img };
    Some(images::tampon(&img))
}

fn maj_selection(app: &App) {
    let e = app.editor.borrow();
    let selection = e.courante.and_then(|c| e.affichees.iter().position(|&i| i == c)).map(|p| p as i32).unwrap_or(-1);
    s(app).set_selected(selection);
}

// ───────────────────────── Sélection d'une texture ─────────────────────────

fn selectionner(app: &Rc<App>, ligne: usize) {
    let Some(i) = app.editor.borrow().affichees.get(ligne).copied() else { return };
    if app.editor.borrow().courante == Some(i) {
        return;
    }
    enregistrer_si_besoin(app);
    app.editor.borrow_mut().courante = Some(i);
    maj_selection(app);
    ouvrir_texture(app, false);
}

fn ouvrir_texture(app: &Rc<App>, garder_zoom: bool) {
    let Some(i) = app.editor.borrow().courante else { return };
    let fichier = app.editor.borrow().toutes[i].fichier().to_path_buf();
    let img = match image::open(&fichier) {
        Ok(img) => img.to_rgba8(),
        Err(e) => {
            fermer_texture(app);
            let a = app.clone();
            lancer(async move { dialogs::erreur(&a, &loc::t("Editeur_TextureIllisible"), &e.to_string()).await });
            return;
        }
    };
    let (l, h) = img.dimensions();
    {
        let mut e = app.editor.borrow_mut();
        e.largeur = l;
        e.hauteur = h;
        e.px = img.into_raw();
        e.annuler.clear();
        e.refaire.clear();
        e.modifie = false;
        e.dessin = false;
        if !garder_zoom {
            e.zoom = (ZONE_TAILLE / l.max(h).max(1)).clamp(1, 40);
        }
    }
    s(app).set_size_text(format!("{l}×{h}").into());
    s(app).set_editor_visible(true);
    redessiner(app);
    maj_titre(app);
    appliquer_zoom(app);
    construire_palette(app);
}

fn fermer_texture(app: &App) {
    {
        let mut e = app.editor.borrow_mut();
        e.courante = None;
        e.modifie = false;
        e.dessin = false;
        e.px.clear();
    }
    s(app).set_editor_visible(false);
    maj_selection(app);
}

/// Nom de la texture, avec un point quand elle a des modifications non enregistrées.
fn maj_titre(app: &App) {
    let e = app.editor.borrow();
    let Some(i) = e.courante else { return };
    let t = &e.toutes[i];
    s(app).set_texture_title(format!("{}{}", if e.modifie { "● " } else { "" }, t.chemin).into());
    s(app).set_reset_enabled(t.modifiee);
}

// ───────────────────────── Zoom, grille, damier ─────────────────────────

fn appliquer_zoom(app: &App) {
    let (l, h, zoom) = {
        let e = app.editor.borrow();
        (e.largeur, e.hauteur, e.zoom)
    };
    if l == 0 {
        return;
    }
    let st = s(app);
    st.set_zoom(zoom as f32);
    let (cl, ch) = (l * zoom, h * zoom);
    st.set_canvas_width(cl as f32);
    st.set_canvas_height(ch as f32);
    st.set_checker(damier(cl, ch));
    if st.get_show_grid() && zoom >= 6 {
        st.set_grid(grille(cl, ch, zoom));
    }
}

/// Damier gris clair (fond des pixels transparents), cases de 8 px.
fn damier(l: u32, h: u32) -> Image {
    let mut b = SharedPixelBuffer::<Rgba8Pixel>::new(l.max(1), h.max(1));
    let largeur = b.width() as usize;
    for (n, p) in b.make_mut_slice().iter_mut().enumerate() {
        let (x, y) = (n % largeur, n / largeur);
        let fonce = ((x / 8) + (y / 8)) % 2 == 1;
        *p = if fonce { Rgba8Pixel { r: 0xD5, g: 0xDD, b: 0xE8, a: 255 } } else { Rgba8Pixel { r: 0xF4, g: 0xF7, b: 0xFB, a: 255 } };
    }
    Image::from_rgba8(b)
}

/// Lignes de la grille (une case par pixel de la texture).
fn grille(l: u32, h: u32, zoom: u32) -> Image {
    let mut b = SharedPixelBuffer::<Rgba8Pixel>::new(l.max(1), h.max(1));
    let largeur = b.width() as usize;
    let zoom = zoom as usize;
    for (n, p) in b.make_mut_slice().iter_mut().enumerate() {
        let (x, y) = (n % largeur, n / largeur);
        *p = if x % zoom == 0 || y % zoom == 0 { Rgba8Pixel { r: 0, g: 0, b: 0, a: 70 } } else { Rgba8Pixel { r: 0, g: 0, b: 0, a: 0 } };
    }
    Image::from_rgba8(b)
}

fn redessiner(app: &App) {
    let e = app.editor.borrow();
    if e.largeur == 0 || e.px.len() != (e.largeur * e.hauteur * 4) as usize {
        return;
    }
    let b = SharedPixelBuffer::<Rgba8Pixel>::clone_from_slice(&e.px, e.largeur, e.hauteur);
    s(app).set_pixels(Image::from_rgba8(b));
}

// ───────────────────────── Dessin ─────────────────────────

fn choisir_outil(app: &App, t: i32) {
    app.editor.borrow_mut().outil = t;
    s(app).set_tool(t);
}

fn pixel(app: &App, x: f32, y: f32) -> Option<(u32, u32)> {
    let e = app.editor.borrow();
    if e.zoom == 0 || x < 0.0 || y < 0.0 {
        return None;
    }
    let (px, py) = ((x / e.zoom as f32).floor() as u32, (y / e.zoom as f32).floor() as u32);
    (px < e.largeur && py < e.hauteur).then_some((px, py))
}

fn pointeur_bas(app: &Rc<App>, x: f32, y: f32, droit: bool) {
    if app.editor.borrow().courante.is_none() {
        return;
    }
    if droit {
        // Clic droit = pipette rapide
        if let Some((px, py)) = pixel(app, x, y) {
            prendre(app, px, py, false);
        }
        return;
    }
    {
        let mut e = app.editor.borrow_mut();
        e.avant_trait = if e.outil == PIPETTE { None } else { Some(e.px.clone()) };
        e.dessin = true;
        e.dernier = None;
    }
    appliquer(app, x, y);
}

fn appliquer(app: &Rc<App>, x: f32, y: f32) {
    let Some((px, py)) = pixel(app, x, y) else { return };
    let (outil, dernier) = {
        let e = app.editor.borrow();
        (e.outil, e.dernier)
    };
    if dernier == Some((px, py)) {
        return;
    }
    match outil {
        PIPETTE => {
            prendre(app, px, py, true);
            return;
        }
        POT => remplir(app, px, py),
        _ => {
            // Souris rapide : on relie au point précédent pour ne pas laisser de trous
            let mut e = app.editor.borrow_mut();
            match dernier {
                Some((dx, dy)) => ligne(&mut e, dx as i32, dy as i32, px as i32, py as i32),
                None => peindre(&mut e, px, py),
            }
        }
    }
    app.editor.borrow_mut().dernier = Some((px, py));
    redessiner(app);
}

fn peindre(e: &mut Etat, x: u32, y: u32) {
    let i = ((y * e.largeur + x) * 4) as usize;
    let valeur = if e.outil == GOMME { [0, 0, 0, 0] } else { [e.couleur[0], e.couleur[1], e.couleur[2], 255] };
    e.px[i..i + 4].copy_from_slice(&valeur);
}

/// Segment de Bresenham entre deux pixels.
fn ligne(e: &mut Etat, mut x0: i32, mut y0: i32, x1: i32, y1: i32) {
    let (dx, dy) = ((x1 - x0).abs(), -(y1 - y0).abs());
    let (sx, sy) = (if x0 < x1 { 1 } else { -1 }, if y0 < y1 { 1 } else { -1 });
    let mut err = dx + dy;
    loop {
        peindre(e, x0 as u32, y0 as u32);
        if x0 == x1 && y0 == y1 {
            break;
        }
        let e2 = 2 * err;
        if e2 >= dy {
            err += dy;
            x0 += sx;
        }
        if e2 <= dx {
            err += dx;
            y0 += sy;
        }
    }
}

fn prendre(app: &Rc<App>, x: u32, y: u32, changer_outil: bool) {
    let c = {
        let e = app.editor.borrow();
        let i = ((y * e.largeur + x) * 4) as usize;
        if e.px[i + 3] == 0 {
            return; // pixel transparent : rien à prendre
        }
        [e.px[i], e.px[i + 1], e.px[i + 2]]
    };
    set_couleur(app, c, true);
    if changer_outil {
        choisir_outil(app, CRAYON);
    }
}

/// Pot de peinture : remplit la zone de même couleur.
fn remplir(app: &App, x: u32, y: u32) {
    let mut e = app.editor.borrow_mut();
    let (l, h) = (e.largeur as usize, e.hauteur as usize);
    let lire = |px: &[u8], n: usize| -> [u8; 4] { [px[n * 4], px[n * 4 + 1], px[n * 4 + 2], px[n * 4 + 3]] };
    let depart = y as usize * l + x as usize;
    let cible = lire(&e.px, depart);
    let nouveau = [e.couleur[0], e.couleur[1], e.couleur[2], 255];
    if cible == nouveau {
        return;
    }
    let mut pile = vec![depart];
    while let Some(n) = pile.pop() {
        if lire(&e.px, n) != cible {
            continue;
        }
        e.px[n * 4..n * 4 + 4].copy_from_slice(&nouveau);
        let (cx, cy) = (n % l, n / l);
        if cx > 0 {
            pile.push(n - 1);
        }
        if cx < l - 1 {
            pile.push(n + 1);
        }
        if cy > 0 {
            pile.push(n - l);
        }
        if cy < h - 1 {
            pile.push(n + l);
        }
    }
}

/// Fin d'un trait : une seule étape d'annulation, et seulement si quelque chose a changé.
fn finir_trait(app: &Rc<App>) {
    let recente = {
        let mut e = app.editor.borrow_mut();
        if !e.dessin {
            return;
        }
        e.dessin = false;
        let Some(avant) = e.avant_trait.take() else { return };
        if avant == e.px {
            return;
        }
        e.annuler.push(avant);
        e.refaire.clear();
        e.modifie = true;
        (e.outil == CRAYON || e.outil == POT).then_some(e.couleur)
    };
    maj_titre(app);
    if let Some(c) = recente {
        ajouter_recente(app, c);
    }
}

// ───────────────────────── Couleur ─────────────────────────

fn set_couleur(app: &App, c: [u8; 3], maj_hex: bool) {
    app.editor.borrow_mut().couleur = c;
    app.editor.borrow_mut().maj_couleur = true;
    let st = s(app);
    st.set_color(couleur_slint(c));
    st.set_red(c[0] as f32);
    st.set_green(c[1] as f32);
    st.set_blue(c[2] as f32);
    if maj_hex {
        st.set_hex_text(hex(c).into());
    }
    app.editor.borrow_mut().maj_couleur = false;
}

fn hex(c: [u8; 3]) -> String {
    format!("#{:02X}{:02X}{:02X}", c[0], c[1], c[2])
}

/// "#abc" = "#aabbcc"
fn lire_hex(texte: &str) -> Option<[u8; 3]> {
    let h = texte.trim().trim_start_matches('#');
    let h = if h.len() == 3 { h.chars().flat_map(|c| [c, c]).collect::<String>() } else { h.to_string() };
    if h.len() != 6 {
        return None;
    }
    let v = u32::from_str_radix(&h, 16).ok()?;
    Some([(v >> 16) as u8, (v >> 8) as u8, v as u8])
}

/// Les couleurs les plus utilisées dans la texture, cliquables.
fn construire_palette(app: &App) {
    let couleurs: Vec<Color> = {
        let e = app.editor.borrow();
        if (e.largeur * e.hauteur) > 65536 {
            Vec::new() // trop grande : on saute la palette
        } else {
            let mut compte: HashMap<[u8; 3], usize> = HashMap::new();
            for p in e.px.chunks_exact(4).filter(|p| p[3] > 0) {
                *compte.entry([p[0], p[1], p[2]]).or_default() += 1;
            }
            let mut liste: Vec<([u8; 3], usize)> = compte.into_iter().collect();
            liste.sort_by(|a, b| b.1.cmp(&a.1).then(a.0.cmp(&b.0)));
            liste.into_iter().take(30).map(|(c, _)| couleur_slint(c)).collect()
        }
    };
    s(app).set_palette(modele(couleurs));
}

fn ajouter_recente(app: &App, c: [u8; 3]) {
    let liste: Vec<Color> = {
        let mut e = app.editor.borrow_mut();
        e.recentes.retain(|r| *r != c);
        e.recentes.insert(0, c);
        e.recentes.truncate(MAX_RECENTES);
        e.recentes.iter().map(|&r| couleur_slint(r)).collect()
    };
    s(app).set_recents(modele(liste));
}

// ───────────────────────── Annuler / rétablir ─────────────────────────

fn annuler(app: &Rc<App>) {
    {
        let mut e = app.editor.borrow_mut();
        let Some(avant) = e.annuler.pop() else { return };
        let actuel = std::mem::replace(&mut e.px, avant);
        e.refaire.push(actuel);
        e.modifie = true;
    }
    redessiner(app);
    maj_titre(app);
}

fn refaire(app: &Rc<App>) {
    {
        let mut e = app.editor.borrow_mut();
        let Some(apres) = e.refaire.pop() else { return };
        let actuel = std::mem::replace(&mut e.px, apres);
        e.annuler.push(actuel);
        e.modifie = true;
    }
    redessiner(app);
    maj_titre(app);
}

/// Raccourcis clavier. Renvoie true si la touche est utilisée.
fn touche(app: &Rc<App>, texte: &str, ctrl: bool) -> bool {
    let t = texte.to_lowercase();
    if ctrl {
        // Avec Ctrl, Windows envoie parfois le caractère de contrôle (Ctrl+Z = \u{1a}…)
        match t.as_str() {
            "z" | "\u{1a}" => annuler(app),
            "y" | "\u{19}" => refaire(app),
            "s" | "\u{13}" => enregistrer(app),
            _ => return false,
        }
        return true;
    }
    if app.editor.borrow().courante.is_none() {
        return false;
    }
    // Raccourcis d'outils
    let outil = match t.as_str() {
        "b" | "p" => Some(CRAYON),
        "e" => Some(GOMME),
        "g" | "f" => Some(POT),
        "i" => Some(PIPETTE),
        _ => None,
    };
    if let Some(o) = outil {
        choisir_outil(app, o);
        return true;
    }
    let delta = match t.as_str() {
        "+" | "=" => 1,
        "-" => -1,
        _ => return false,
    };
    let zoom = (app.editor.borrow().zoom as i32 + delta).clamp(1, 40) as u32;
    app.editor.borrow_mut().zoom = zoom;
    appliquer_zoom(app);
    true
}

// ───────────────────────── Enregistrement ─────────────────────────

fn enregistrer_si_besoin(app: &Rc<App>) {
    if app.editor.borrow().dessin {
        finir_trait(app);
    }
    if app.editor.borrow().modifie {
        enregistrer(app);
    }
}

fn enregistrer(app: &Rc<App>) {
    let (i, chemin, l, h, px) = {
        let e = app.editor.borrow();
        let Some(i) = e.courante else { return };
        if e.pack.is_none() || e.px.is_empty() {
            return;
        }
        (i, e.toutes[i].chemin_pack.clone(), e.largeur, e.hauteur, e.px.clone())
    };

    let resultat = (|| -> anyhow::Result<()> {
        if let Some(parent) = chemin.parent() {
            std::fs::create_dir_all(parent)?;
        }
        let img = image::RgbaImage::from_raw(l, h, px).ok_or_else(|| anyhow::anyhow!("image invalide"))?;
        img.save_with_format(&chemin, image::ImageFormat::Png)?;
        Ok(())
    })();
    if let Err(e) = resultat {
        let a = app.clone();
        lancer(async move { dialogs::erreur(&a, &loc::t("Editeur_EnregistrementImpossible"), &e.to_string()).await });
        return;
    }

    let (nouvelle, perso) = {
        let mut e = app.editor.borrow_mut();
        let nouvelle = !e.toutes[i].modifiee;
        e.modifie = false;
        e.toutes[i].modifiee = true;
        e.toutes[i].miniature = miniature(&chemin).map(Image::from_rgba8);
        (nouvelle, e.perso)
    };
    maj_tuile(app, i);
    maj_titre(app);
    maj_info(app);
    construire_palette(app);
    if nouvelle && perso {
        construire_categories(app);
    }
}

/// Met à jour la tuile d'une texture dans la grille (miniature, bordure « modifiée »).
fn maj_tuile(app: &App, i: usize) {
    let (ligne, m, t) = {
        let e = app.editor.borrow();
        let Some(ligne) = e.affichees.iter().position(|&x| x == i) else { return };
        (ligne, e.modele.clone(), e.toutes[i].clone())
    };
    if let Some(m) = m {
        maj_ligne(&m, ligne, |item| {
            item.modified = t.modifiee;
            item.has_thumb = t.miniature.is_some();
            item.thumb = t.miniature.clone().unwrap_or_default();
        });
    }
}

async fn reinitialiser(app: &Rc<App>) {
    let (i, dossier_pack, chemin) = {
        let e = app.editor.borrow();
        let Some(i) = e.courante else { return };
        let Some(p) = &e.pack else { return };
        if !e.toutes[i].modifiee {
            return;
        }
        (i, p.dossier.clone(), e.toutes[i].chemin.clone())
    };
    let ok = dialogs::confirmer(
        app,
        &loc::t("Editeur_Reinitialiser_Titre"),
        &loc::t("Editeur_Reinitialiser_Texte"),
        Some(&loc::t("Editeur_Reinitialiser")),
        None,
        true,
    )
    .await;
    if !ok {
        return;
    }
    if let Err(e) = packs::restaurer(&dossier_pack, &format!("{RACINE_TEXTURES}{chemin}")) {
        dialogs::erreur(app, &loc::t("Commun_SuppressionImpossible"), &e.to_string()).await;
        return;
    }

    let perso = {
        let mut e = app.editor.borrow_mut();
        e.toutes[i].modifiee = false;
        let fichier = e.toutes[i].chemin_template.clone();
        e.toutes[i].miniature = miniature(&fichier).map(Image::from_rgba8);
        e.modifie = false;
        e.perso
    };
    maj_tuile(app, i);
    maj_info(app);
    if perso {
        // La texture n'a plus sa place dans « Mes textures »
        fermer_texture(app);
        construire_categories(app);
    } else {
        ouvrir_texture(app, true);
    }
}

/// Ouvre la texture dans Paint.NET (ou l'éditeur choisi). Elle est rechargée quand son fichier change.
async fn editeur_externe(app: &Rc<App>) {
    let (i, dossier_pack, template, chemin) = {
        let e = app.editor.borrow();
        let (Some(i), Some(p), Some(t)) = (e.courante, &e.pack, &e.dossier_template) else { return };
        (i, p.dossier.clone(), t.clone(), e.toutes[i].chemin.clone())
    };

    let mut editeur = packs::editeur().filter(|e| Path::new(e).exists());
    if editeur.is_none() {
        if let Some(f) = dialogs::choisir_fichier(&loc::t("Editeur_ChoisirEditeur"), &loc::t("Editeur_FiltreExe"), &["exe"]).await {
            let texte = f.to_string_lossy().to_string();
            packs::definir_editeur(&texte);
            editeur = Some(texte);
        }
    }

    enregistrer_si_besoin(app);
    match packs::modifier_texture(&dossier_pack, &template, &format!("{RACINE_TEXTURES}{chemin}"), editeur.as_deref()) {
        Ok(()) => {
            let chemin_pack = {
                let mut e = app.editor.borrow_mut();
                e.toutes[i].modifiee = true;
                e.externe = Some(i);
                e.toutes[i].chemin_pack.clone()
            };
            app.editor.borrow_mut().date_externe = std::fs::metadata(&chemin_pack).and_then(|m| m.modified()).ok();
            surveiller(app);
            maj_tuile(app, i);
            maj_titre(app);
            maj_info(app);
            dialogs::info(app, &loc::t("Editeur_Externe"), &loc::t("Editeur_Externe_Texte")).await;
        }
        Err(e) => dialogs::erreur(app, &loc::t("Editeur_OuvertureImpossible"), &e.to_string()).await,
    }
}

/// Vérifie chaque seconde si la texture ouverte dans l'éditeur externe a été enregistrée.
fn surveiller(app: &Rc<App>) {
    let a = app.clone();
    app.editor.borrow().surveillance.start(slint::TimerMode::Repeated, Duration::from_secs(1), move || {
        let (i, chemin, avant) = {
            let e = a.editor.borrow();
            let Some(i) = e.externe else { return };
            (i, e.toutes[i].chemin_pack.clone(), e.date_externe)
        };
        let date = std::fs::metadata(&chemin).and_then(|m| m.modified()).ok();
        if date.is_none() || date == avant {
            return;
        }
        let modifie = {
            let mut e = a.editor.borrow_mut();
            e.date_externe = date;
            e.toutes[i].miniature = miniature(&chemin).map(Image::from_rgba8);
            e.courante == Some(i) && e.modifie
        };
        maj_tuile(&a, i);
        if a.editor.borrow().courante == Some(i) && !modifie {
            ouvrir_texture(&a, true);
        }
    });
}

async fn changer_icone(app: &Rc<App>) {
    let Some(p) = app.editor.borrow().pack.clone() else { return };
    let Some(fichier) = dialogs::choisir_fichier(&loc::t("Editeur_ChangerIcone"), "Images", &["png", "jpg", "jpeg", "bmp", "gif"]).await else {
        return;
    };
    let resultat = app.bloquant(move || packs::definir_icone(&p, &fichier)).await;
    match resultat {
        Ok(()) => maj_icone(app),
        Err(e) => dialogs::erreur(app, &loc::t("Instance_ImageIllisible"), &e.to_string()).await,
    }
}
