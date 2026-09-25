using Microsoft.Win32;
using Misty.Models;
using Misty.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Misty.Views
{
    /// <summary>
    /// Détail d'un profil : contenu installé (mods, packs, datapacks, shaders) et explorateur Modrinth / CurseForge.
    /// </summary>
    public partial class InstanceView : UserControl
    {
        private Instance instance = new();
        private List<ContenuInstalle> contenus = new();
        private string filtreType = "";

        public event Action? RetourDemande;
        public event Action<Instance>? JouerDemande;

        public InstanceView()
        {
            InitializeComponent();

            Explorateur.Installer = InstallerAsync;
            Explorateur.EstInstalle = p => ContentService.EstInstalle(contenus, p.Source, p.Id);

            // Filtres par type
            var filtres = new List<(string Type, string Libelle)> { ("", "Tout") };
            filtres.AddRange(ContentService.Types);
            foreach (var (type, libelle) in filtres)
            {
                var onglet = new RadioButton
                {
                    Style = (Style)FindResource("TabPill"),
                    Content = libelle,
                    GroupName = "FiltreContenu",
                    Tag = type,
                    IsChecked = type == "",
                    Padding = new Thickness(12, 6, 12, 6),
                };
                onglet.Checked += (s, _) =>
                {
                    filtreType = (string)((RadioButton)s!).Tag;
                    AppliquerFiltre();
                };
                PanneauFiltres.Children.Add(onglet);
            }
        }

        /// <summary>Affiche un profil (appelé à chaque ouverture de la page).</summary>
        public void Charger(Instance instance)
        {
            this.instance = instance;
            PanneauRenommer.Visibility = Visibility.Collapsed;
            TxtNom.Visibility = Visibility.Visible;
            TxtStatut.Text = "";
            MajEnTete();

            OngletContenu.IsChecked = true;
            Explorateur.Tag = null; // l'explorateur sera (re)configuré à la première ouverture de l'onglet
            ChargerContenu();
        }

        private void MajEnTete()
        {
            TxtNom.Text = instance.Nom.ToUpperInvariant();
            ImgIcone.Source = instance.Icone;

            PanneauBadges.Children.Clear();
            AjouterBadge(instance.VersionMc, "NavyBrush");
            AjouterBadge(instance.Loader + (instance.LoaderVersion != null ? " " + instance.LoaderVersion : ""), "GreenBrush");
            if (instance.EstModpack)
                AjouterBadge("MODPACK " + instance.ModpackVersion, "BlueBrush");
        }

        private void AjouterBadge(string texte, string couleur)
        {
            PanneauBadges.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = (System.Windows.Media.Brush)FindResource(couleur),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 6, 0),
                Child = new TextBlock
                {
                    Text = texte.ToUpperInvariant(),
                    Style = (Style)FindResource("PixelLabel"),
                    Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 11,
                },
            });
        }

        // ───────────────────────── Contenu installé ─────────────────────────

        private async void ChargerContenu()
        {
            try
            {
                contenus = ContentService.Lister(instance);
            }
            catch (Exception ex)
            {
                contenus = new();
                TxtStatut.Text = "Lecture du dossier impossible : " + ex.Message;
            }
            AppliquerFiltre();

            // Identification des fichiers ajoutés à la main (nom + icône depuis Modrinth / CurseForge)
            if (contenus.Any(c => c.Meta == null))
            {
                try
                {
                    TxtStatut.Text = "Identification des fichiers…";
                    await ContentService.IdentifierAsync(instance, contenus);
                    TxtStatut.Text = "";
                    AppliquerFiltre();
                }
                catch
                {
                    TxtStatut.Text = ""; // hors-ligne : on garde les noms de fichiers
                }
            }
        }

        private void AppliquerFiltre()
        {
            string texte = TxtFiltre.Text.Trim();
            var visibles = contenus
                .Where(c => filtreType == "" || c.Type == filtreType)
                .Where(c => texte.Length == 0 || c.Titre.Contains(texte, StringComparison.CurrentCultureIgnoreCase)
                                              || c.NomFichier.Contains(texte, StringComparison.CurrentCultureIgnoreCase))
                .ToList();

            ListeContenu.ItemsSource = visibles;
            PanneauVide.Visibility = visibles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            TxtOngletContenu.Text = contenus.Count > 0 ? $"Contenu ({contenus.Count})" : "Contenu";
        }

        private void TxtFiltre_TextChanged(object sender, TextChangedEventArgs e) => AppliquerFiltre();

        private async void ChkActif_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not ContenuInstalle contenu) return;
            try
            {
                ContentService.Basculer(contenu);
            }
            catch (Exception ex)
            {
                contenu.Rafraichir();
                await Dialogs.ErreurAsync("Impossible de modifier le fichier", ex.Message + "\n\nLe jeu est peut-être encore ouvert.");
            }
        }

        private async void BtnSupprimerContenu_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not ContenuInstalle contenu) return;
            if (!await Dialogs.ConfirmerAsync("Supprimer", $"Supprimer « {contenu.Titre} » de ce profil ?", "Supprimer", danger: true))
                return;

            try
            {
                ContentService.Supprimer(instance, contenu);
                contenus.Remove(contenu);
                AppliquerFiltre();
            }
            catch (Exception ex)
            {
                await Dialogs.ErreurAsync("Suppression impossible", ex.Message + "\n\nLe jeu est peut-être encore ouvert.");
            }
        }

        // ───────────────────────── Découvrir ─────────────────────────

        private void Onglet_Checked(object sender, RoutedEventArgs e)
        {
            if (Explorateur == null) return; // pendant InitializeComponent

            bool decouvrir = OngletDecouvrir.IsChecked == true;
            PanneauContenu.Visibility = decouvrir ? Visibility.Collapsed : Visibility.Visible;
            Explorateur.Visibility = decouvrir ? Visibility.Visible : Visibility.Collapsed;

            if (decouvrir && Explorateur.Tag as string != instance.Id)
            {
                Explorateur.Tag = instance.Id;
                Explorateur.Configurer(instance, ContentService.Types);
            }
            if (!decouvrir)
                ChargerContenu();
        }

        /// <summary>Installation depuis l'explorateur (avec les cas particuliers datapacks / shaders).</summary>
        private async Task<bool> InstallerAsync(ProjetDistant projet, string type)
        {
            string? monde = null;
            var progression = new Progress<string>(m => TxtStatut.Text = m);

            try
            {
                if (type == ContentService.Datapack)
                {
                    var mondes = ContentService.Mondes(instance);
                    if (mondes.Count == 0)
                    {
                        await Dialogs.InfoAsync("Aucun monde",
                            "Les datapacks s'installent dans un monde. Lance ce profil et crée un monde, puis reviens ici.");
                        return false;
                    }
                    monde = mondes.Count == 1 ? mondes[0]
                        : await Dialogs.ChoisirAsync("Choisir un monde", $"Dans quel monde installer « {projet.Titre} » ?", mondes);
                    if (monde == null) return false;
                }

                if (type == ContentService.Shader && !await PreparerShadersAsync(progression))
                    return false;

                var installes = await ContentService.InstallerAsync(instance, projet.Source, projet.Id, type, monde, progression);
                TxtStatut.Text = installes.Count > 1
                    ? $"{projet.Titre} installé (+ {installes.Count - 1} dépendance{(installes.Count > 2 ? "s" : "")})"
                    : $"{projet.Titre} installé";

                contenus = ContentService.Lister(instance);
                AppliquerFiltre();
                return true;
            }
            catch (Exception ex)
            {
                TxtStatut.Text = "";
                await Dialogs.ErreurAsync("Installation impossible", ex.Message);
                return false;
            }
        }

        /// <summary>Les shaders ont besoin d'Iris (Fabric/Quilt/NeoForge), Oculus (Forge) ou OptiFine.</summary>
        private async Task<bool> PreparerShadersAsync(IProgress<string> progression)
        {
            string? prerequis = ContentService.PrerequisShaders(instance, ContentService.Lister(instance));
            if (prerequis == null) return true;

            if (prerequis == "")
                return await Dialogs.ConfirmerAsync("Shaders non pris en charge",
                    $"Avec {instance.Loader}, Minecraft ne peut pas charger de shaders. Il faut un profil OptiFine, " +
                    "ou Fabric / NeoForge avec Iris.\n\nInstaller quand même ?", "Installer quand même");

            string nom = prerequis == ContentService.IdIris ? "Iris" : "Oculus";
            if (!await Dialogs.ConfirmerAsync("Mod requis",
                    $"Les shaders ont besoin du mod {nom} pour fonctionner. L'installer aussi ?", $"Installer {nom}", "Ignorer"))
                return true;

            await ContentService.InstallerAsync(instance, Sources.Modrinth, prerequis, ContentService.Mod, null, progression);
            return true;
        }

        // ───────────────────────── Actions du profil ─────────────────────────

        private void BtnRetour_Click(object sender, RoutedEventArgs e) => RetourDemande?.Invoke();

        private void BtnJouer_Click(object sender, RoutedEventArgs e) => JouerDemande?.Invoke(instance);

        private void BtnDossier_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(instance.Dossier);
            Process.Start("explorer.exe", instance.Dossier);
        }

        private async void BtnIcone_Click(object sender, RoutedEventArgs e)
        {
            var dialogue = new OpenFileDialog
            {
                Title = "Icône du profil",
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
            };
            if (dialogue.ShowDialog() != true) return;

            try
            {
                // Réencodé en PNG 128px pour garder un fichier léger
                var source = new BitmapImage();
                source.BeginInit();
                source.UriSource = new Uri(dialogue.FileName);
                source.DecodePixelWidth = 128;
                source.CacheOption = BitmapCacheOption.OnLoad;
                source.EndInit();

                var encodeur = new PngBitmapEncoder();
                encodeur.Frames.Add(BitmapFrame.Create(source));
                using (var fichier = File.Create(instance.CheminIcone))
                    encodeur.Save(fichier);

                InstanceStore.Sauver(instance);
                MajEnTete();
            }
            catch (Exception ex)
            {
                await Dialogs.ErreurAsync("Image illisible", ex.Message);
            }
        }

        private void BtnRenommer_Click(object sender, RoutedEventArgs e)
        {
            TxtNouveauNom.Text = instance.Nom;
            TxtNom.Visibility = Visibility.Collapsed;
            PanneauRenommer.Visibility = Visibility.Visible;
            TxtNouveauNom.Focus();
            TxtNouveauNom.SelectAll();
        }

        private void TxtNouveauNom_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ValiderNom();
            if (e.Key == Key.Escape)
            {
                PanneauRenommer.Visibility = Visibility.Collapsed;
                TxtNom.Visibility = Visibility.Visible;
                e.Handled = true;
            }
        }

        private void BtnValiderNom_Click(object sender, RoutedEventArgs e) => ValiderNom();

        private void ValiderNom()
        {
            string nom = TxtNouveauNom.Text.Trim();
            if (nom.Length > 0)
            {
                instance.Nom = nom;
                InstanceStore.Sauver(instance);
                MajEnTete();
            }
            PanneauRenommer.Visibility = Visibility.Collapsed;
            TxtNom.Visibility = Visibility.Visible;
        }

        private async void BtnSupprimer_Click(object sender, RoutedEventArgs e)
        {
            if (!await Dialogs.ConfirmerAsync("Supprimer le profil",
                    $"Supprimer « {instance.Nom} » ? Ses mods, packs, mondes et options seront effacés définitivement.",
                    "Supprimer", danger: true))
                return;

            try
            {
                InstanceStore.Supprimer(instance);
                RetourDemande?.Invoke();
            }
            catch (Exception ex)
            {
                await Dialogs.ErreurAsync("Suppression impossible", ex.Message + "\n\nLe jeu est peut-être encore ouvert.");
            }
        }
    }
}
