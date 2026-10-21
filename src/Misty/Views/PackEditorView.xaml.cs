using Misty.Services;
using System.ComponentModel;
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
    public partial class PackEditorView : UserControl
    {
        private enum Outil { Crayon, Gomme, Pot, Pipette }

        private const int ZoneTaille = 300;

        public event Action? RetourDemande;

        private PackService.PackInfo? pack;
        private string? dossierTemplate;
        private List<TextureVM> toutes = new();
        private string categorie = "";
        private int generation;
        private TextureVM? courante;

        // Éditeur
        private int largeur, hauteur, zoom = 10;
        private byte[] px = Array.Empty<byte>(); // BGRA
        private WriteableBitmap? bmp;
        private readonly Stack<byte[]> annuler = new();
        private readonly Stack<byte[]> refaire = new();
        private bool modifie, dessin, majCouleur;
        private Outil outil = Outil.Crayon;
        private Color couleur = Colors.Black;
        private (int X, int Y) dernier = (-1, -1);
        private bool perso; // false = « Toutes les textures », true = « Mes textures »

        public PackEditorView()
        {
            InitializeComponent();
            ConstruireDamier();
            SetCouleur(Colors.Black);
        }

        // ───────────────────────── Chargement ─────────────────────────

        internal async Task ChargerAsync(PackService.PackInfo p)
        {
            pack = p;
            toutes = new();   // évite que Mode_Checked ne touche à l'ancienne liste
            perso = false;
            ModeTout.IsChecked = true;
            TxtAucune.Visibility = Visibility.Collapsed;
            TxtPack.Text = p.Nom.ToUpperInvariant();
            FermerTexture();
            PanneauCategories.Children.Clear();
            ListeTextures.ItemsSource = null;
            TxtRecherche.Text = "";
            TxtInfo.Text = $"Chargement du template {p.VersionMc}…";

            try
            {
                var (dossier, _) = await PackService.TemplateAsync(p.VersionMc, new Progress<string>(m => TxtInfo.Text = m));
                dossierTemplate = dossier;

                toutes = PackService.ListerTextures(dossier)
                    .Select(c => new TextureVM(c, p.Dossier, dossier))
                    .ToList();

                ConstruireCategories();
                MajInfo();
            }
            catch (Exception ex)
            {
                TxtInfo.Text = "";
                await Dialogs.ErreurAsync("Template indisponible", ex.Message);
                RetourDemande?.Invoke();
            }
        }

        private void MajInfo()
        {
            int modifiees = toutes.Count(t => t.Modifiee);
            ModePerso.Content = $"Mes textures ({modifiees})";
            TxtInfo.Text = $"MINECRAFT {pack?.VersionMc} · {toutes.Count} textures · {modifiees} modifiée(s)";
        }
        private void Mode_Checked(object sender, RoutedEventArgs e)
        {
            perso = (string)((RadioButton)sender).Tag == "perso";
            if (toutes.Count == 0) return; // pendant InitializeComponent ou avant le chargement

            TxtAucune.Visibility = Visibility.Collapsed;
            ConstruireCategories();
        }

        private void ConstruireCategories()
        {
            PanneauCategories.Children.Clear();

            var source = perso ? toutes.Where(t => t.Modifiee).ToList() : toutes;

            // (étiquette technique, texte affiché). "" = toutes les catégories (mode perso uniquement)
            var entrees = new List<(string Tag, string Libelle)>();
            if (perso) entrees.Add(("", $"Toutes ({source.Count})"));
            foreach (var nom in TextureCategories.Ordre)
            {
                int n = source.Count(t => t.Categorie == nom);
                if (n > 0) entrees.Add((nom, $"{nom} ({n})"));
            }
            if (entrees.Count == 0) return;

            // On garde la catégorie en cours si elle existe encore dans ce mode
            string cible = entrees.Any(x => x.Tag == categorie) ? categorie : entrees[0].Tag;

            foreach (var (tag, libelle) in entrees)
            {
                var onglet = new RadioButton
                {
                    Style = (Style)FindResource("TabPill"),
                    Content = libelle,
                    GroupName = "categories",
                    Margin = new Thickness(0, 0, 0, 6),
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

            TxtAucune.Visibility = perso && liste.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

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
                var images = await Task.Run(() => lot.Select(t => ChargerImage(t.Fichier)).ToArray());
                if (gen != generation) return; // une autre liste est affichée entre-temps
                for (int i = 0; i < lot.Length; i++)
                    lot[i].Miniature = images[i];
            }
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
            if ((sender as FrameworkElement)?.DataContext is not TextureVM t) return;

            EnregistrerSiBesoin();
            if (courante != null) courante.Selectionnee = false;
            courante = t;
            t.Selectionnee = true;
            OuvrirTexture(t);
        }

        private void OuvrirTexture(TextureVM t)
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

            bmp = new WriteableBitmap(largeur, hauteur, 96, 96, PixelFormats.Bgra32, null);
            Redessiner();
            ImgPixels.Source = bmp;

            annuler.Clear();
            refaire.Clear();
            modifie = false;

            TxtTexture.Text = t.Chemin.ToUpperInvariant();
            TxtVide.Visibility = Visibility.Collapsed;
            PanneauEditeur.Visibility = Visibility.Visible;

            zoom = Math.Clamp(ZoneTaille / Math.Max(largeur, hauteur), 1, 40);
            SldZoom.Value = zoom;
            AppliquerZoom();
            ConstruirePalette();
        }

        private void FermerTexture()
        {
            courante = null;
            bmp = null;
            modifie = false;
            PanneauEditeur.Visibility = Visibility.Collapsed;
            TxtVide.Visibility = Visibility.Visible;
        }

        // ───────────────────────── Zoom, grille, damier ─────────────────────────

        private void Zoom_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (bmp == null) return;
            zoom = (int)SldZoom.Value;
            AppliquerZoom();
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
            var dessin = new DrawingGroup();
            dessin.Children.Add(new GeometryDrawing(clair, null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
            dessin.Children.Add(new GeometryDrawing(fonce, null, new RectangleGeometry(new Rect(0, 0, 8, 8))));
            dessin.Children.Add(new GeometryDrawing(fonce, null, new RectangleGeometry(new Rect(8, 8, 8, 8))));
            Damier.Fill = new DrawingBrush(dessin)
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
            if (outil != Outil.Pipette) PousserAnnulation();
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

        private void Zone_Up(object sender, MouseButtonEventArgs e)
        {
            dessin = false;
            Zone.ReleaseMouseCapture();
        }

        /// <summary>Clic droit = pipette rapide.</summary>
        private void Zone_Droit(object sender, MouseButtonEventArgs e)
        {
            if (bmp == null) return;
            var (x, y) = Pixel(e.GetPosition(Zone));
            if (x < 0) return;
            Prendre((y * largeur + x) * 4);
        }

        private (int X, int Y) Pixel(Point p)
        {
            int x = (int)(p.X / zoom), y = (int)(p.Y / zoom);
            return x < 0 || y < 0 || x >= largeur || y >= hauteur ? (-1, -1) : (x, y);
        }

        private void Appliquer(Point p)
        {
            var (x, y) = Pixel(p);
            if (x < 0 || (x, y) == dernier) return;
            dernier = (x, y);
            int i = (y * largeur + x) * 4;

            switch (outil)
            {
                case Outil.Crayon: Ecrire(i, couleur.B, couleur.G, couleur.R, 255); break;
                case Outil.Gomme: Ecrire(i, 0, 0, 0, 0); break;
                case Outil.Pipette: Prendre(i); return;
                case Outil.Pot: Remplir(x, y); break;
            }
            modifie = true;
            Redessiner();
        }

        private void Ecrire(int i, byte b, byte g, byte r, byte a)
        {
            px[i] = b; px[i + 1] = g; px[i + 2] = r; px[i + 3] = a;
        }

        private void Prendre(int i)
        {
            if (px[i + 3] == 0) return; // pixel transparent : rien à prendre
            SetCouleur(Color.FromRgb(px[i + 2], px[i + 1], px[i]));
            OutilCrayon.IsChecked = true;
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
            if (majHex) TxtHex.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            Echantillon.Background = new SolidColorBrush(c);
            majCouleur = false;
        }

        private void Couleur_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (majCouleur || SldB == null) return;
            SetCouleur(Color.FromRgb((byte)SldR.Value, (byte)SldG.Value, (byte)SldB.Value));
        }

        private void Hex_Changed(object sender, TextChangedEventArgs e)
        {
            if (majCouleur || TxtHex == null) return;
            string h = TxtHex.Text.Trim().TrimStart('#');
            if (h.Length != 6 || !int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v)) return;
            SetCouleur(Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v), majHex: false);
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
                .Take(28)
                .Select(g => Color.FromRgb(g.Key.R, g.Key.G, g.Key.B));

            foreach (var c in couleurs)
            {
                var tuile = new Border
                {
                    Width = 24,
                    Height = 24,
                    CornerRadius = new CornerRadius(6),
                    Margin = new Thickness(0, 0, 6, 6),
                    Background = new SolidColorBrush(c),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    ToolTip = $"#{c.R:X2}{c.G:X2}{c.B:X2}",
                };
                tuile.MouseLeftButtonDown += (_, _) => { SetCouleur(c); OutilCrayon.IsChecked = true; };
                Palette.Children.Add(tuile);
            }
        }

        // ───────────────────────── Annuler / rétablir ─────────────────────────

        private void PousserAnnulation()
        {
            annuler.Push((byte[])px.Clone());
            refaire.Clear();
        }

        private void BtnAnnuler_Click(object sender, RoutedEventArgs e)
        {
            if (bmp == null || annuler.Count == 0) return;
            refaire.Push((byte[])px.Clone());
            px = annuler.Pop();
            modifie = true;
            Redessiner();
        }

        private void BtnRetablir_Click(object sender, RoutedEventArgs e)
        {
            if (bmp == null || refaire.Count == 0) return;
            annuler.Push((byte[])px.Clone());
            px = refaire.Pop();
            modifie = true;
            Redessiner();
        }

        private void Vue_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.FocusedElement is TextBox || Keyboard.Modifiers != ModifierKeys.Control) return;

            switch (e.Key)
            {
                case Key.Z: BtnAnnuler_Click(this, e); e.Handled = true; break;
                case Key.Y: BtnRetablir_Click(this, e); e.Handled = true; break;
                case Key.S: Enregistrer(); e.Handled = true; break;
            }
        }

        // ───────────────────────── Enregistrement ─────────────────────────

        private void BtnEnregistrer_Click(object sender, RoutedEventArgs e) => Enregistrer();

        private void EnregistrerSiBesoin()
        {
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

                modifie = false;
                courante.Modifiee = true;
                courante.Miniature = source;
                MajInfo();
            }
            catch (Exception ex)
            {
                _ = Dialogs.ErreurAsync("Enregistrement impossible", ex.Message);
            }
        }

        private async void BtnReinitialiser_Click(object sender, RoutedEventArgs e)
        {
            if (courante == null || pack == null || !courante.Modifiee) return;

            if (!await Dialogs.ConfirmerAsync("Réinitialiser la texture",
                    "Remettre la texture d'origine ? Ta version modifiée sera supprimée du pack.",
                    "Réinitialiser", danger: true))
                return;

            PackService.Restaurer(pack.Dossier, PackService.RacineTextures + courante.Chemin);
            courante.Modifiee = false;
            courante.Miniature = ChargerImage(courante.Fichier);
            OuvrirTexture(courante);
            MajInfo();
            if (perso) ConstruireCategories();
        }

        /// <summary>Ouvre la texture dans Paint.NET (ou l'éditeur choisi). On la rouvre ensuite en cliquant dessus.</summary>
        private async void BtnPaint_Click(object sender, RoutedEventArgs e)
        {
            if (courante == null || pack == null || dossierTemplate == null) return;

            string? editeur = PackService.Editeur;
            if (string.IsNullOrWhiteSpace(editeur) || !File.Exists(editeur))
            {
                var dialogue = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Choisis ton éditeur (ex. paintdotnet.exe). Annuler = application par défaut",
                    Filter = "Programme (*.exe)|*.exe",
                };
                if (dialogue.ShowDialog() == true) { PackService.Editeur = dialogue.FileName; editeur = dialogue.FileName; }
                else editeur = null;
            }

            try
            {
                EnregistrerSiBesoin();
                PackService.ModifierTexture(pack.Dossier, dossierTemplate, PackService.RacineTextures + courante.Chemin, editeur);
                courante.Modifiee = true;
                await Dialogs.InfoAsync("Éditeur externe",
                    "Afin d'enregistrer les modifications, enrigistre dans ton éditeur uniquement, pas besoin de cliquer sur enregistrer dans Misty.");
            }
            catch (Exception ex)
            {
                await Dialogs.ErreurAsync("Ouverture impossible", ex.Message);
            }
        }

        private void BtnRetour_Click(object sender, RoutedEventArgs e)
        {
            EnregistrerSiBesoin();
            RetourDemande?.Invoke();
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