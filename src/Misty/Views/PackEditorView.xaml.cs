using Microsoft.Win32;
using Misty.Services;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Misty.Views
{
    /// <summary>
    /// Éditeur d'un pack de textures : catégories, grille des textures du template, dessin intégré au pixel.
    /// Une texture modifiée est copiée dans le pack ; « Réinitialiser » la supprime pour revenir à l'originale.
    /// </summary>
    public partial class PackEditorView : UserControl
    {
        private enum Outil { Crayon, Gomme, Pot, Pipette }

        private const int ZoneTaille = 250;
        private const int MaxRecentes = 12;

        public event Action? RetourDemande;

        private PackService.PackInfo? pack;
        private string? dossierTemplate;
        private List<TextureVM> toutes = new();
        private string categorie = "";
        private int generation;     // listes de miniatures périmées
        private int chargementPack; // ChargerAsync périmés
        private TextureVM? courante;
        private bool perso; // false = « Toutes les textures », true = « Mes textures »

        // Éditeur
        private int largeur, hauteur, zoom = 10;
        private byte[] px = Array.Empty<byte>(); // BGRA
        private byte[]? avantTrait;
        private WriteableBitmap? bmp;
        private readonly Stack<byte[]> annuler = new();
        private readonly Stack<byte[]> refaire = new();
        private bool modifie, dessin, majCouleur;
        private Outil outil = Outil.Crayon;
        private Color couleur = Colors.Black;
        private (int X, int Y) dernier = (-1, -1);
        private readonly List<Color> recentes = new();

        // Éditeur externe : on recharge la texture quand on revient dans Misty
        private TextureVM? externe;
        private DateTime dateExterne;

        public PackEditorView()
        {
            InitializeComponent();
            ConstruireDamier();
            SetCouleur(Colors.Black);
            MajModePerso(0);

            Loc.Changee += () =>
            {
                if (pack == null) return;
                MajInfo();
                ConstruireCategories();
            };

            Loaded += (_, _) => { if (Application.Current != null) Application.Current.Activated += App_Activated; };
            Unloaded += (_, _) => { if (Application.Current != null) Application.Current.Activated -= App_Activated; };
        }

        // ───────────────────────── Chargement ─────────────────────────

        internal async Task ChargerAsync(PackService.PackInfo p)
        {
            int moi = ++chargementPack;
            pack = p;
            toutes = new();   // évite que Mode_Checked ne touche à l'ancienne liste
            perso = false;
            categorie = "";
            externe = null;
            ModeTout.IsChecked = true;
            MajModePerso(0);
            TxtPack.Text = p.Nom.ToUpperInvariant();
            ImgIcone.Source = p.Icone;
            FermerTexture();
            PanneauCategories.Children.Clear();
            ListeTextures.ItemsSource = null;
            TxtRecherche.Text = "";
            TxtInfo.Text = "MINECRAFT " + p.VersionMc;
            Message(Loc.T("Editeur_ChargementTemplate", p.VersionMc));

            try
            {
                var (dossier, _) = await PackService.TemplateAsync(p.VersionMc, new Progress<string>(m =>
                {
                    if (moi == chargementPack) Message(m);
                }));
                if (moi != chargementPack) return; // un autre pack a été ouvert entre-temps
                dossierTemplate = dossier;

                var liste = await Task.Run(() => PackService.ListerTextures(dossier)
                    .Select(c => new TextureVM(c, p.Dossier, dossier))
                    .ToList());
                if (moi != chargementPack) return;

                toutes = liste;
                ImgIcone.Source = p.Icone; // pack.png du template maintenant disponible
                ConstruireCategories();
                MajInfo();
            }
            catch (Exception ex)
            {
                if (moi != chargementPack) return;
                Message("");
                await Dialogs.ErreurAsync(Loc.T("Editeur_TemplateIndisponible"), ex.Message);
                RetourDemande?.Invoke();
            }
        }

        /// <summary>À appeler quand on quitte la page : enregistre la texture en cours.</summary>
        public void Quitter() => EnregistrerSiBesoin();

        private void Message(string texte)
        {
            TxtMessage.Text = texte;
            PanneauMessage.Visibility = texte.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void MajInfo()
        {
            int modifiees = toutes.Count(t => t.Modifiee);
            MajModePerso(modifiees);
            TxtInfo.Text = Loc.T("Editeur_Info", pack?.VersionMc ?? "", toutes.Count, modifiees);
        }

        private void MajModePerso(int modifiees) =>
            ModePerso.Content = $"{Loc.T("Editeur_Perso")} ({modifiees})";

        private void Mode_Checked(object sender, RoutedEventArgs e)
        {
            perso = (string)((RadioButton)sender).Tag == "perso";
            if (toutes.Count == 0) return; // pendant InitializeComponent ou avant le chargement
            ConstruireCategories();
        }

        private void ConstruireCategories()
        {
            PanneauCategories.Children.Clear();

            var source = perso ? toutes.Where(t => t.Modifiee).ToList() : toutes;

            // (étiquette technique, nombre). "" = toutes les catégories (mode perso uniquement)
            var entrees = new List<(string Tag, int Nombre)>();
            if (perso) entrees.Add(("", source.Count));
            foreach (var nom in TextureCategories.Ordre)
            {
                int n = source.Count(t => t.Categorie == nom);
                if (n > 0) entrees.Add((nom, n));
            }

            if (entrees.Count == 0 || (perso && source.Count == 0))
            {
                ListeTextures.ItemsSource = null;
                Message(Loc.T("Editeur_Aucune"));
                return;
            }

            // On garde la catégorie en cours si elle existe encore dans ce mode
            string cible = entrees.Any(x => x.Tag == categorie) ? categorie : entrees[0].Tag;

            foreach (var (tag, nombre) in entrees)
            {
                var onglet = new RadioButton
                {
                    Style = (Style)FindResource("CatPill"),
                    Content = tag == "" ? Loc.T("Editeur_Toutes") : Loc.T("Cat_" + tag),
                    Tag = nombre.ToString(CultureInfo.CurrentCulture),
                    GroupName = "categories",
                };
                onglet.Checked += (_, _) =>
                {
                    categorie = tag;
                    if (TxtRecherche.Text.Length > 0) TxtRecherche.Text = ""; // relance l'affichage via TextChanged
                    else AfficherTextures();
                };
                PanneauCategories.Children.Add(onglet);

                if (tag == cible) onglet.IsChecked = true;
            }
        }

        private void TxtRecherche_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (toutes.Count > 0) AfficherTextures();
        }

        private void AfficherTextures()
        {
            string filtre = TxtRecherche.Text.Trim();
            var source = perso ? toutes.Where(t => t.Modifiee) : toutes;

            var liste = (string.IsNullOrEmpty(filtre)
                    ? source.Where(t => categorie == "" || t.Categorie == categorie)
                    : source.Where(t => t.Chemin.Contains(filtre, StringComparison.OrdinalIgnoreCase)).Take(400))
                .ToList();

            Message(liste.Count > 0 ? ""
                : filtre.Length > 0 ? Loc.T("Editeur_AucunResultat", filtre)
                : Loc.T("Editeur_Aucune"));

            ListeTextures.ItemsSource = liste;
            DefilementTextures.ScrollToTop();
            _ = ChargerMiniaturesAsync(liste, ++generation);
        }

        /// <summary>Décode les miniatures par paquets, hors du thread d'interface.</summary>
        private async Task ChargerMiniaturesAsync(List<TextureVM> liste, int gen)
        {
            var aCharger = liste.Where(t => t.Miniature == null).ToList();
            foreach (var lot in aCharger.Chunk(40))
            {
                var images = await Task.Run(() => lot.Select(t => ChargerMiniature(t.Fichier)).ToArray());
                if (gen != generation) return; // une autre liste est affichée entre-temps
                for (int i = 0; i < lot.Length; i++)
                    lot[i].Miniature = images[i];
            }
        }

        /// <summary>Miniature : pour une texture animée (bande verticale d'images carrées), seule la 1re image.</summary>
        private static BitmapSource? ChargerMiniature(string chemin)
        {
            var img = ChargerImage(chemin);
            if (img == null || img.PixelHeight <= img.PixelWidth || img.PixelHeight % img.PixelWidth != 0) return img;
            try
            {
                var premiere = new CroppedBitmap(img, new Int32Rect(0, 0, img.PixelWidth, img.PixelWidth));
                premiere.Freeze();
                return premiere;
            }
            catch { return img; }
        }

        private static BitmapSource? ChargerImage(string chemin)
        {
            try
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                img.UriSource = new Uri(chemin);
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch { return null; }
        }

        // ───────────────────────── Sélection d'une texture ─────────────────────────

        private void Tuile_Click(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not TextureVM t || t == courante) return;

            EnregistrerSiBesoin();
            if (courante != null) courante.Selectionnee = false;
            courante = t;
            t.Selectionnee = true;
            OuvrirTexture(t);
        }

        private void OuvrirTexture(TextureVM t, bool garderZoom = false)
        {
            try
            {
                var src = new BitmapImage();
                src.BeginInit();
                src.CacheOption = BitmapCacheOption.OnLoad;
                src.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                src.UriSource = new Uri(t.Fichier);
                src.EndInit();

                var conv = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
                largeur = conv.PixelWidth;
                hauteur = conv.PixelHeight;
                px = new byte[largeur * hauteur * 4];
                conv.CopyPixels(px, largeur * 4, 0);
            }
            catch (Exception ex)
            {
                FermerTexture();
                _ = Dialogs.ErreurAsync(Loc.T("Editeur_TextureIllisible"), ex.Message);
                return;
            }

            bmp = new WriteableBitmap(largeur, hauteur, 96, 96, PixelFormats.Bgra32, null);
            Redessiner();
            ImgPixels.Source = bmp;

            annuler.Clear();
            refaire.Clear();
            modifie = false;
            dessin = false;

            TxtTaille.Text = $"{largeur}×{hauteur}";
            MajTitreTexture();
            TxtVide.Visibility = Visibility.Collapsed;
            PanneauEditeur.Visibility = Visibility.Visible;

            if (!garderZoom)
            {
                zoom = Math.Clamp(ZoneTaille / Math.Max(largeur, hauteur), 1, 40);
                SldZoom.Value = zoom;
            }
            AppliquerZoom();
            ConstruirePalette();
        }

        private void FermerTexture()
        {
            if (courante != null) courante.Selectionnee = false;
            courante = null;
            bmp = null;
            modifie = false;
            dessin = false;
            PanneauEditeur.Visibility = Visibility.Collapsed;
            TxtVide.Visibility = Visibility.Visible;
        }

        /// <summary>Nom de la texture, avec un point quand elle a des modifications non enregistrées.</summary>
        private void MajTitreTexture()
        {
            if (courante == null) return;
            TxtTexture.Text = (modifie ? "● " : "") + courante.Chemin;
            TxtTexture.ToolTip = courante.Chemin;
            BtnReinitialiser.IsEnabled = courante.Modifiee;
        }

        // ───────────────────────── Zoom, grille, damier ─────────────────────────

        private void Zoom_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (bmp == null) return;
            zoom = (int)SldZoom.Value;
            AppliquerZoom();
        }

        /// <summary>Ctrl + molette : zoom. Molette seule : défilement normal.</summary>
        private void Plateau_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (bmp == null || Keyboard.Modifiers != ModifierKeys.Control) return;
            SldZoom.Value = Math.Clamp(SldZoom.Value + (e.Delta > 0 ? 1 : -1) * Math.Max(1, zoom / 6), SldZoom.Minimum, SldZoom.Maximum);
            e.Handled = true;
        }

        private void ChkGrille_Click(object sender, RoutedEventArgs e) => AppliquerZoom();

        private void AppliquerZoom()
        {
            if (bmp == null) return;
            Plateau.Width = largeur * zoom;
            Plateau.Height = hauteur * zoom;

            var trait = new Pen(new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), 1);
            var lignes = new GeometryGroup();
            lignes.Children.Add(new LineGeometry(new Point(0, 0), new Point(zoom, 0)));
            lignes.Children.Add(new LineGeometry(new Point(0, 0), new Point(0, zoom)));
            Grille.Fill = new DrawingBrush(new GeometryDrawing(null, trait, lignes))
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, zoom, zoom),
                ViewportUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, zoom, zoom),
                ViewboxUnits = BrushMappingMode.Absolute,
            };
            Grille.Visibility = ChkGrille.IsChecked == true && zoom >= 6 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ConstruireDamier()
        {
            var clair = new SolidColorBrush(Color.FromRgb(0xF4, 0xF7, 0xFB));
            var fonce = new SolidColorBrush(Color.FromRgb(0xD5, 0xDD, 0xE8));
            var dessinDamier = new DrawingGroup();
            dessinDamier.Children.Add(new GeometryDrawing(clair, null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
            dessinDamier.Children.Add(new GeometryDrawing(fonce, null, new RectangleGeometry(new Rect(0, 0, 8, 8))));
            dessinDamier.Children.Add(new GeometryDrawing(fonce, null, new RectangleGeometry(new Rect(8, 8, 8, 8))));
            Damier.Fill = new DrawingBrush(dessinDamier)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 16, 16),
                ViewportUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, 16, 16),
                ViewboxUnits = BrushMappingMode.Absolute,
            };
        }

        // ───────────────────────── Dessin ─────────────────────────

        private void Outil_Checked(object sender, RoutedEventArgs e) =>
            outil = Enum.Parse<Outil>((string)((RadioButton)sender).Tag);

        private void Zone_Down(object sender, MouseButtonEventArgs e)
        {
            if (bmp == null) return;
            Focus(); // pour que Ctrl+Z / Ctrl+S arrivent jusqu'ici
            avantTrait = outil == Outil.Pipette ? null : (byte[])px.Clone();
            dessin = true;
            dernier = (-1, -1);
            Zone.CaptureMouse();
            Appliquer(e.GetPosition(Zone));
        }

        private void Zone_Move(object sender, MouseEventArgs e)
        {
            if (!dessin || outil is Outil.Pot or Outil.Pipette) return;
            Appliquer(e.GetPosition(Zone));
        }

        private void Zone_Up(object sender, MouseButtonEventArgs e) => Zone.ReleaseMouseCapture();

        private void Zone_LostCapture(object sender, MouseEventArgs e) => FinirTrait();

        /// <summary>Fin d'un trait : une seule étape d'annulation, et seulement si quelque chose a changé.</summary>
        private void FinirTrait()
        {
            if (!dessin) return;
            dessin = false;

            if (avantTrait == null || avantTrait.AsSpan().SequenceEqual(px)) return;
            annuler.Push(avantTrait);
            refaire.Clear();
            avantTrait = null;
            modifie = true;
            MajTitreTexture();
            if (outil is Outil.Crayon or Outil.Pot) AjouterRecente(couleur);
        }

        /// <summary>Clic droit = pipette rapide.</summary>
        private void Zone_Droit(object sender, MouseButtonEventArgs e)
        {
            if (bmp == null) return;
            var (x, y) = Pixel(e.GetPosition(Zone));
            if (x < 0) return;
            Prendre((y * largeur + x) * 4, changerOutil: false);
        }

        private (int X, int Y) Pixel(Point p)
        {
            int x = (int)Math.Floor(p.X / zoom), y = (int)Math.Floor(p.Y / zoom);
            return x < 0 || y < 0 || x >= largeur || y >= hauteur ? (-1, -1) : (x, y);
        }

        private void Appliquer(Point p)
        {
            var (x, y) = Pixel(p);
            if (x < 0 || (x, y) == dernier) return;

            switch (outil)
            {
                case Outil.Pipette:
                    Prendre((y * largeur + x) * 4, changerOutil: true);
                    return;
                case Outil.Pot:
                    Remplir(x, y);
                    break;
                default:
                    // Souris rapide : on relie au point précédent pour ne pas laisser de trous
                    if (dernier.X >= 0) Ligne(dernier.X, dernier.Y, x, y);
                    else Peindre(x, y);
                    break;
            }
            dernier = (x, y);
            Redessiner();
        }

        private void Peindre(int x, int y)
        {
            int i = (y * largeur + x) * 4;
            if (outil == Outil.Gomme) Ecrire(i, 0, 0, 0, 0);
            else Ecrire(i, couleur.B, couleur.G, couleur.R, 255);
        }

        /// <summary>Segment de Bresenham entre deux pixels.</summary>
        private void Ligne(int x0, int y0, int x1, int y1)
        {
            int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                Peindre(x0, y0);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        private void Ecrire(int i, byte b, byte g, byte r, byte a)
        {
            px[i] = b; px[i + 1] = g; px[i + 2] = r; px[i + 3] = a;
        }

        private void Prendre(int i, bool changerOutil)
        {
            if (px[i + 3] == 0) return; // pixel transparent : rien à prendre
            SetCouleur(Color.FromRgb(px[i + 2], px[i + 1], px[i]));
            if (changerOutil) OutilCrayon.IsChecked = true;
        }

        private void Remplir(int x, int y)
        {
            int cible = BitConverter.ToInt32(px, (y * largeur + x) * 4);
            int nouveau = couleur.B | (couleur.G << 8) | (couleur.R << 16) | (255 << 24);
            if (cible == nouveau) return;

            var pile = new Stack<int>();
            pile.Push(y * largeur + x);
            while (pile.Count > 0)
            {
                int n = pile.Pop();
                int j = n * 4;
                if (BitConverter.ToInt32(px, j) != cible) continue;

                Ecrire(j, couleur.B, couleur.G, couleur.R, 255);
                int cx = n % largeur, cy = n / largeur;
                if (cx > 0) pile.Push(n - 1);
                if (cx < largeur - 1) pile.Push(n + 1);
                if (cy > 0) pile.Push(n - largeur);
                if (cy < hauteur - 1) pile.Push(n + largeur);
            }
        }

        private void Redessiner() =>
            bmp?.WritePixels(new Int32Rect(0, 0, largeur, hauteur), px, largeur * 4, 0);

        // ───────────────────────── Couleur ─────────────────────────

        private void SetCouleur(Color c, bool majHex = true)
        {
            majCouleur = true;
            couleur = c;
            SldR.Value = c.R; SldG.Value = c.G; SldB.Value = c.B;
            if (majHex) TxtHex.Text = Hex(c);
            Echantillon.Background = new SolidColorBrush(c);
            majCouleur = false;
        }

        private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        private void Couleur_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (majCouleur || SldB == null || TxtHex == null) return;
            SetCouleur(Color.FromRgb((byte)SldR.Value, (byte)SldG.Value, (byte)SldB.Value));
        }

        private void Hex_Changed(object sender, TextChangedEventArgs e)
        {
            if (majCouleur || TxtHex == null || SldB == null) return;
            string h = TxtHex.Text.Trim().TrimStart('#');
            if (h.Length == 3) h = string.Concat(h.Select(c => $"{c}{c}")); // #abc = #aabbcc
            if (h.Length != 6 || !int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v)) return;
            SetCouleur(Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v), majHex: false);
        }

        private Border Pastille(Color c)
        {
            var tuile = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(7),
                Margin = new Thickness(0, 0, 6, 6),
                Background = new SolidColorBrush(c),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1.5),
                Cursor = Cursors.Hand,
                ToolTip = Hex(c),
            };
            tuile.MouseLeftButtonDown += (_, _) =>
            {
                SetCouleur(c);
                if (outil is Outil.Gomme or Outil.Pipette) OutilCrayon.IsChecked = true;
            };
            return tuile;
        }

        /// <summary>Les couleurs les plus utilisées dans la texture, cliquables.</summary>
        private void ConstruirePalette()
        {
            Palette.Children.Clear();
            if (largeur * hauteur > 65536) return; // trop grande : on saute la palette

            var couleurs = Enumerable.Range(0, largeur * hauteur)
                .Where(i => px[i * 4 + 3] > 0)
                .GroupBy(i => (R: px[i * 4 + 2], G: px[i * 4 + 1], B: px[i * 4]))
                .OrderByDescending(g => g.Count())
                .Take(30)
                .Select(g => Color.FromRgb(g.Key.R, g.Key.G, g.Key.B));

            foreach (var c in couleurs)
                Palette.Children.Add(Pastille(c));
        }

        private void AjouterRecente(Color c)
        {
            recentes.Remove(c);
            recentes.Insert(0, c);
            if (recentes.Count > MaxRecentes) recentes.RemoveAt(recentes.Count - 1);

            Recentes.Children.Clear();
            foreach (var r in recentes)
                Recentes.Children.Add(Pastille(r));
            TxtRecentes.Visibility = Visibility.Visible;
        }

        // ───────────────────────── Annuler / rétablir ─────────────────────────

        private void BtnAnnuler_Click(object sender, RoutedEventArgs e)
        {
            if (bmp == null || annuler.Count == 0) return;
            refaire.Push((byte[])px.Clone());
            px = annuler.Pop();
            modifie = true;
            Redessiner();
            MajTitreTexture();
        }

        private void BtnRetablir_Click(object sender, RoutedEventArgs e)
        {
            if (bmp == null || refaire.Count == 0) return;
            annuler.Push((byte[])px.Clone());
            px = refaire.Pop();
            modifie = true;
            Redessiner();
            MajTitreTexture();
        }

        private void Vue_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.FocusedElement is TextBox) return;

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.Z: BtnAnnuler_Click(this, e); e.Handled = true; break;
                    case Key.Y: BtnRetablir_Click(this, e); e.Handled = true; break;
                    case Key.S: Enregistrer(); e.Handled = true; break;
                }
                return;
            }
            if (Keyboard.Modifiers != ModifierKeys.None || bmp == null) return;

            // Raccourcis d'outils
            RadioButton? choix = e.Key switch
            {
                Key.B or Key.P => OutilCrayon,
                Key.E => OutilGomme,
                Key.G or Key.F => OutilPot,
                Key.I => OutilPipette,
                _ => null,
            };
            if (choix != null) { choix.IsChecked = true; e.Handled = true; return; }

            if (e.Key is Key.Add or Key.OemPlus) { SldZoom.Value = Math.Min(SldZoom.Maximum, SldZoom.Value + 1); e.Handled = true; }
            if (e.Key is Key.Subtract or Key.OemMinus) { SldZoom.Value = Math.Max(SldZoom.Minimum, SldZoom.Value - 1); e.Handled = true; }
        }

        // ───────────────────────── Enregistrement ─────────────────────────

        private void BtnEnregistrer_Click(object sender, RoutedEventArgs e) => Enregistrer();

        private void EnregistrerSiBesoin()
        {
            if (dessin) FinirTrait();
            if (modifie) Enregistrer();
        }

        private void Enregistrer()
        {
            if (courante == null || pack == null || bmp == null) return;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(courante.CheminPack)!);

                var source = BitmapSource.Create(largeur, hauteur, 96, 96, PixelFormats.Bgra32, null, px, largeur * 4);
                source.Freeze();
                var encodeur = new PngBitmapEncoder();
                encodeur.Frames.Add(BitmapFrame.Create(source));
                using (var flux = File.Create(courante.CheminPack))
                    encodeur.Save(flux);

                bool nouvelle = !courante.Modifiee;
                modifie = false;
                courante.Modifiee = true;
                courante.Miniature = ChargerMiniature(courante.CheminPack);
                MajTitreTexture();
                MajInfo();
                ConstruirePalette();
                if (nouvelle && perso) ConstruireCategories();
            }
            catch (Exception ex)
            {
                _ = Dialogs.ErreurAsync(Loc.T("Editeur_EnregistrementImpossible"), ex.Message);
            }
        }

        private async void BtnReinitialiser_Click(object sender, RoutedEventArgs e)
        {
            if (courante == null || pack == null || !courante.Modifiee) return;

            if (!await Dialogs.ConfirmerAsync(Loc.T("Editeur_Reinitialiser_Titre"), Loc.T("Editeur_Reinitialiser_Texte"),
                    Loc.T("Editeur_Reinitialiser"), danger: true))
                return;

            try
            {
                PackService.Restaurer(pack.Dossier, PackService.RacineTextures + courante.Chemin);
            }
            catch (Exception ex)
            {
                await Dialogs.ErreurAsync(Loc.T("Commun_SuppressionImpossible"), ex.Message);
                return;
            }

            var t = courante;
            t.Modifiee = false;
            t.Miniature = ChargerMiniature(t.Fichier);
            MajInfo();

            if (perso)
            {
                // La texture n'a plus sa place dans « Mes textures »
                FermerTexture();
                ConstruireCategories();
            }
            else
                OuvrirTexture(t, garderZoom: true);
        }

        /// <summary>Ouvre la texture dans Paint.NET (ou l'éditeur choisi). Elle est rechargée au retour dans Misty.</summary>
        private async void BtnPaint_Click(object sender, RoutedEventArgs e)
        {
            if (courante == null || pack == null || dossierTemplate == null) return;

            string? editeur = PackService.Editeur;
            if (string.IsNullOrWhiteSpace(editeur) || !File.Exists(editeur))
            {
                var dialogue = new OpenFileDialog
                {
                    Title = Loc.T("Editeur_ChoisirEditeur"),
                    Filter = Loc.T("Editeur_FiltreExe") + " (*.exe)|*.exe",
                };
                if (dialogue.ShowDialog() == true) { PackService.Editeur = dialogue.FileName; editeur = dialogue.FileName; }
                else editeur = null;
            }

            try
            {
                EnregistrerSiBesoin();
                PackService.ModifierTexture(pack.Dossier, dossierTemplate, PackService.RacineTextures + courante.Chemin, editeur);
                courante.Modifiee = true;
                externe = courante;
                dateExterne = File.GetLastWriteTimeUtc(courante.CheminPack);
                MajTitreTexture();
                MajInfo();
                await Dialogs.InfoAsync(Loc.T("Editeur_Externe"), Loc.T("Editeur_Externe_Texte"));
            }
            catch (Exception ex)
            {
                await Dialogs.ErreurAsync(Loc.T("Editeur_OuvertureImpossible"), ex.Message);
            }
        }

        /// <summary>Retour dans Misty : si la texture a été enregistrée dans l'éditeur externe, on la recharge.</summary>
        private void App_Activated(object? sender, EventArgs e)
        {
            if (externe == null || !File.Exists(externe.CheminPack)) return;

            var date = File.GetLastWriteTimeUtc(externe.CheminPack);
            if (date == dateExterne) return;
            dateExterne = date;

            externe.Miniature = ChargerMiniature(externe.CheminPack);
            if (externe == courante && !modifie)
                OuvrirTexture(externe, garderZoom: true);
        }

        // ───────────────────────── En-tête ─────────────────────────

        private void BtnRetour_Click(object sender, RoutedEventArgs e)
        {
            EnregistrerSiBesoin();
            RetourDemande?.Invoke();
        }

        private void BtnDossier_Click(object sender, RoutedEventArgs e)
        {
            if (pack != null) Process.Start("explorer.exe", pack.Dossier);
        }

        private async void BtnExporter_Click(object sender, RoutedEventArgs e)
        {
            if (pack == null) return;
            EnregistrerSiBesoin();
            await SoonView.ExporterAsync(pack, BtnExporter);
        }

        private async void Icone_Click(object sender, MouseButtonEventArgs e)
        {
            if (pack == null) return;

            var dialogue = new OpenFileDialog
            {
                Title = Loc.T("Editeur_ChangerIcone"),
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
            };
            if (dialogue.ShowDialog() != true) return;

            try
            {
                PackService.DefinirIcone(pack, dialogue.FileName);
                ImgIcone.Source = pack.Icone;
            }
            catch (Exception ex)
            {
                await Dialogs.ErreurAsync(Loc.T("Instance_ImageIllisible"), ex.Message);
            }
        }
    }

    /// <summary>Une texture dans la grille.</summary>
    internal sealed class TextureVM : INotifyPropertyChanged
    {
        private readonly string cheminTemplate;
        private bool modifiee, selectionnee;
        private ImageSource? miniature;

        public TextureVM(string chemin, string dossierPack, string dossierTemplate)
        {
            Chemin = chemin;
            Categorie = TextureCategories.Categorie(chemin);

            string relatif = Path.Combine("assets", "minecraft", "textures", chemin.Replace('/', Path.DirectorySeparatorChar));
            CheminPack = Path.Combine(dossierPack, relatif);
            cheminTemplate = Path.Combine(dossierTemplate, relatif);
            modifiee = File.Exists(CheminPack);
        }

        public string Chemin { get; }
        public string Categorie { get; }
        public string CheminPack { get; }

        /// <summary>La version du pack si elle existe, sinon l'originale.</summary>
        public string Fichier => Modifiee ? CheminPack : cheminTemplate;

        public bool Modifiee { get => modifiee; set { modifiee = value; Notifier(); } }
        public bool Selectionnee { get => selectionnee; set { selectionnee = value; Notifier(); } }
        public ImageSource? Miniature { get => miniature; set { miniature = value; Notifier(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notifier([CallerMemberName] string? nom = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nom));
    }
}
