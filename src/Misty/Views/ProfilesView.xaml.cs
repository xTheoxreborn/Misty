using Misty.Models;
using Misty.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Misty.Views
{
    /// <summary>
    /// Liste des profils, création d'un profil, installation d'un modpack Modrinth / CurseForge.
    /// </summary>
    public partial class ProfilesView : UserControl
    {
        public event Action<Instance>? OuvrirDemande;
        public event Action<Instance>? JouerDemande;

        public ProfilesView()
        {
            InitializeComponent();

            ExplorateurModpacks.LibelleInstaller = "Profil_Creer";
            ExplorateurModpacks.Installer = InstallerModpackAsync;
            ExplorateurModpacks.EstInstalle = p => InstanceStore.Lister()
                .Any(i => i.ModpackProjectId == p.Id && (i.ModpackSource ?? Sources.Modrinth) == p.Source);

            // "Joué hier"… est calculé à l'affichage
            Loc.Changee += () =>
            {
                if (ListeProfils.ItemsSource != null) ListeProfils.ItemsSource = InstanceStore.Lister();
            };
        }

        /// <summary>Recharge la liste (appelé à chaque ouverture de la page).</summary>
        public void Rafraichir()
        {
            var instances = InstanceStore.Lister();
            ListeProfils.ItemsSource = instances;
            PanneauVide.Visibility = instances.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            AfficherListe();
        }

        private void AfficherListe()
        {
            PanneauListe.Visibility = Visibility.Visible;
            PanneauModpacks.Visibility = Visibility.Collapsed;
        }

        private void Carte_Click(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is Instance instance)
                OuvrirDemande?.Invoke(instance);
        }

        private void BtnJouer_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is Instance instance)
                JouerDemande?.Invoke(instance);
        }

        // ───────────────────────── Création ─────────────────────────
        private async void BtnSupprimer_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not Instance instance) return;

            if (!await Dialogs.ConfirmerAsync(Loc.T("Profil_Supprimer"), Loc.T("Profil_Supprimer_Texte", instance.Nom),
                    Loc.T("Commun_Supprimer"), danger: true))
                return;

            try
            {
                InstanceStore.Supprimer(instance);
                Rafraichir();
            }
            catch (Exception ex)
            {
                await Dialogs.ErreurAsync(Loc.T("Commun_SuppressionImpossible"), ex.Message + "\n\n" + Loc.T("Commun_JeuOuvert"));
            }
        }
        private async void BtnNouveau_Click(object sender, RoutedEventArgs e)
        {
            TxtNom.Text = "";
            TxtCreationInfo.Text = Loc.T("Home_ChargementVersions");
            BtnCreer.IsEnabled = false;
            PanneauCreation.Visibility = Visibility.Visible;
            TxtNom.Focus();

            try
            {
                var versions = await MinecraftService.VersionsAsync();
                CmbVersion.ItemsSource = versions;
                CmbVersion.SelectedIndex = versions.Count > 0 ? 0 : -1;
            }
            catch (Exception ex)
            {
                TxtCreationInfo.Text = Loc.T("Home_VersionsErreur", ex.Message);
            }
        }

        private async void CmbVersion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbVersion.SelectedItem is not string version) return;

            CmbLoader.IsEnabled = false;
            BtnCreer.IsEnabled = false;
            TxtCreationInfo.Text = Loc.T("Commun_RechercheLoaders", version);

            var modes = await MinecraftService.Loaders.ModesDisponiblesAsync(version);
            if (CmbVersion.SelectedItem as string != version) return; // version changée entre-temps

            CmbLoader.ItemsSource = modes;
            // Un profil sert souvent à installer des mods : on propose Fabric par défaut s'il existe
            CmbLoader.SelectedItem = modes.Contains(ModLoaderService.Fabric) ? ModLoaderService.Fabric : modes[0];
            CmbLoader.IsEnabled = true;
            BtnCreer.IsEnabled = true;
            TxtCreationInfo.Text = Loc.T("Profil_InfoLoader");
        }

        private void BtnCreer_Click(object sender, RoutedEventArgs e)
        {
            if (CmbVersion.SelectedItem is not string version || CmbLoader.SelectedItem is not string loader) return;

            string nom = TxtNom.Text.Trim();
            if (nom.Length == 0)
                nom = $"{version} {loader}";

            var instance = InstanceStore.Creer(nom, version, loader);
            PanneauCreation.Visibility = Visibility.Collapsed;
            OuvrirDemande?.Invoke(instance);
        }

        private void BtnAnnulerCreation_Click(object sender, RoutedEventArgs e) => PanneauCreation.Visibility = Visibility.Collapsed;

        private void FondCreation_Click(object sender, MouseButtonEventArgs e) => PanneauCreation.Visibility = Visibility.Collapsed;

        // ───────────────────────── Modpacks ─────────────────────────

        private void BtnModpacks_Click(object sender, RoutedEventArgs e)
        {
            PanneauListe.Visibility = Visibility.Collapsed;
            PanneauModpacks.Visibility = Visibility.Visible;

            if (ExplorateurModpacks.Tag == null)
            {
                ExplorateurModpacks.Tag = "configuré";
                ExplorateurModpacks.Configurer(null, () => new[] { (ContentService.Modpack, Loc.T("Type_Modpacks")) });
            }
            else
            {
                ExplorateurModpacks.RafraichirEtats();
            }
        }


        private void BtnRetourListe_Click(object sender, RoutedEventArgs e) => Rafraichir();

        private async Task<bool> InstallerModpackAsync(ProjetDistant projet, string type)
        {
            TxtAttenteTitre.Text = projet.Titre.ToUpperInvariant();
            TxtAttente.Text = Loc.T("Commun_Preparation");
            PanneauAttente.Visibility = Visibility.Visible;

            ModpackService.Resultat resultat;
            try
            {
                resultat = await ModpackService.InstallerAsync(projet, new Progress<string>(m => TxtAttente.Text = m));
            }
            catch (Exception ex)
            {
                PanneauAttente.Visibility = Visibility.Collapsed;
                await Dialogs.ErreurAsync(Loc.T("Profil_ModpackImpossible"), ex.Message);
                return false;
            }

            PanneauAttente.Visibility = Visibility.Collapsed;
            OuvrirDemande?.Invoke(resultat.Instance);
            if (resultat.Bloques.Count > 0)
                await ProposerTelechargementsManuelsAsync(resultat);
            return true;
        }

        /// <summary>
        /// Certains auteurs CurseForge interdisent le téléchargement par des applications tierces :
        /// on liste ces fichiers et on propose d'ouvrir leur page pour les télécharger à la main.
        /// </summary>
        private static async Task ProposerTelechargementsManuelsAsync(ModpackService.Resultat resultat)
        {
            var liste = string.Join("\n", resultat.Bloques.Take(12).Select(b => "• " + Loc.T("Manuel_Ligne", b.Nom, b.Dossier)));
            if (resultat.Bloques.Count > 12)
                liste += "\n" + Loc.T("Manuel_Autres", resultat.Bloques.Count - 12);

            bool ouvrir = await Dialogs.ConfirmerAsync(Loc.T("Manuel_Titre"),
                Loc.T("Manuel_Texte", resultat.Bloques.Count, liste),
                Loc.T("Manuel_Ouvrir"), Loc.T("Commun_PlusTard"));
            if (!ouvrir) return;

            foreach (var bloque in resultat.Bloques)
                Process.Start(new ProcessStartInfo(bloque.Page) { UseShellExecute = true });
            Process.Start("explorer.exe", resultat.Instance.Dossier);
        }
    }
}
