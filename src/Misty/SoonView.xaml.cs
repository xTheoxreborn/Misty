using Microsoft.Win32;
using Misty.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Misty
{
    /// <summary>
    /// Packs de textures : liste, création, export, suppression. L'édition se fait dans <see cref="Views.PackEditorView"/>.
    /// </summary>
    public partial class SoonView : UserControl
    {
        internal event Action<PackService.PackInfo>? EditeurDemande;

        public SoonView()
        {
            InitializeComponent();
            Loaded += (_, _) => Rafraichir();
        }

        /// <summary>Recharge la liste des packs (nombre de textures modifiées, icône…).</summary>
        public void Rafraichir()
        {
            var packs = PackService.Lister();
            ListePacks.ItemsSource = packs;
            PanneauVide.Visibility = packs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ───────────────────────── Création ─────────────────────────

        private async void BtnCreerPack_Click(object sender, RoutedEventArgs e)
        {
            TxtNomPack.Text = "";
            TxtDescriptionPack.Text = "";
            Loc.Lier(TxtInfoPack, TextBlock.TextProperty, "Pack_InfoTemplate");
            PanneauPack.Visibility = Visibility.Visible;
            TxtNomPack.Focus();

            if (CmbVersion.Items.Count == 0)
            {
                try
                {
                    var versions = await MinecraftService.VersionsAsync();
                    CmbVersion.ItemsSource = versions;
                    CmbVersion.SelectedIndex = versions.Count > 0 ? 0 : -1;
                }
                catch (Exception ex)
                {
                    await Dialogs.ErreurAsync(Loc.T("Home_VersionsIndisponibles"), ex.Message);
                }
            }
        }

        private void ChampPack_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && BtnValiderPack.IsEnabled) BtnValiderPack_Click(sender, e);
            if (e.Key == Key.Escape) PanneauPack.Visibility = Visibility.Collapsed;
        }

        private async void BtnValiderPack_Click(object sender, RoutedEventArgs e)
        {
            string nom = TxtNomPack.Text.Trim();

            if (nom.Length == 0 || CmbVersion.SelectedItem is not string version)
            {
                TxtInfoPack.Text = Loc.T("Pack_Manquant");
                return;
            }

            BtnValiderPack.IsEnabled = false;
            CmbVersion.IsEnabled = false;
            try
            {
                var progression = new Progress<string>(m => TxtInfoPack.Text = m);
                var (_, format) = await PackService.TemplateAsync(version, progression);
                string dossier = PackService.CreerPack(nom, TxtDescriptionPack.Text.Trim(), format, version);

                PanneauPack.Visibility = Visibility.Collapsed;
                Rafraichir();

                // On ouvre directement le nouveau pack dans l'éditeur
                var pack = PackService.Lister().FirstOrDefault(p => p.Dossier == dossier);
                if (pack != null) EditeurDemande?.Invoke(pack);
            }
            catch (Exception ex)
            {
                TxtInfoPack.Text = "";
                await Dialogs.ErreurAsync(Loc.T("Pack_CreationImpossible"), ex.Message);
            }
            finally
            {
                BtnValiderPack.IsEnabled = true;
                CmbVersion.IsEnabled = true;
            }
        }

        private void BtnAnnulerPack_Click(object sender, RoutedEventArgs e) => PanneauPack.Visibility = Visibility.Collapsed;

        private void FondPack_Click(object sender, MouseButtonEventArgs e)
        {
            if (BtnValiderPack.IsEnabled) PanneauPack.Visibility = Visibility.Collapsed; // pas pendant un téléchargement
        }

        // ───────────────────────── Actions sur un pack ─────────────────────────

        private void Carte_Click(object sender, MouseButtonEventArgs e) => Editer(sender);

        private void BtnTextures_Click(object sender, RoutedEventArgs e) => Editer(sender);

        private async void Editer(object sender)
        {
            if ((sender as FrameworkElement)?.DataContext is not PackService.PackInfo pack) return;

            if (string.IsNullOrEmpty(pack.VersionMc))
            {
                await Dialogs.ErreurAsync(Loc.T("Pack_VersionManquante"), Loc.T("Pack_VersionManquante_Texte"));
                return;
            }

            EditeurDemande?.Invoke(pack);
        }

        private void BtnDossierPack_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is PackService.PackInfo pack)
                Process.Start("explorer.exe", pack.Dossier);
        }

        private async void BtnSupprimerPack_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not PackService.PackInfo pack) return;

            if (!await Dialogs.ConfirmerAsync(Loc.T("Pack_Supprimer"), Loc.T("Pack_Supprimer_Texte", pack.Nom),
                    Loc.T("Commun_Supprimer"), danger: true))
                return;

            try
            {
                Directory.Delete(pack.Dossier, recursive: true);
            }
            catch (Exception ex)
            {
                await Dialogs.ErreurAsync(Loc.T("Commun_SuppressionImpossible"), ex.Message);
            }
            Rafraichir();
        }

        private async void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is PackService.PackInfo pack)
                await ExporterAsync(pack, (Button)sender);
        }

        /// <summary>Exporte le pack en .zip (aussi utilisé par l'éditeur).</summary>
        internal static async Task ExporterAsync(PackService.PackInfo pack, Button? bouton = null)
        {
            var dialogue = new SaveFileDialog
            {
                Title = Loc.T("Pack_ExportDialogue"),
                Filter = Loc.T("Pack_ExportFiltre") + " (*.zip)|*.zip",
                FileName = pack.Nom + ".zip",
                DefaultExt = ".zip",
                AddExtension = true,
                OverwritePrompt = true,
            };
            if (dialogue.ShowDialog() != true) return;

            if (bouton != null) bouton.IsEnabled = false;
            try
            {
                await Task.Run(() => PackService.Exporter(pack, dialogue.FileName));

                bool ouvrir = await Dialogs.ConfirmerAsync(Loc.T("Pack_ExportTitre"),
                    Loc.T("Pack_Exporte", pack.Nom, dialogue.FileName),
                    Loc.T("Commun_OuvrirDossier"), Loc.T("Commun_Fermer"));

                if (ouvrir)
                    Process.Start("explorer.exe", $"/select,\"{dialogue.FileName}\"");
            }
            catch (Exception ex)
            {
                await Dialogs.ErreurAsync(Loc.T("Pack_ExportImpossible"), ex.Message);
            }
            finally
            {
                if (bouton != null) bouton.IsEnabled = true;
            }
        }
    }
}
