using Misty.Models;
using Misty.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Misty.Views
{
    /// <summary>
    /// Explorateur de contenu Modrinth / CurseForge : recherche, tri, défilement infini, bouton d'installation.
    /// L'installation elle-même est déléguée au parent via <see cref="Installer"/>.
    /// </summary>
    public partial class ContentBrowser : UserControl
    {
        private const int TaillePage = 20;

        /// <summary>Dernière plateforme choisie, retenue d'un explorateur à l'autre.</summary>
        private static string sourceRetenue = Sources.Modrinth;

        private readonly ObservableCollection<ResultatVM> resultats = new();
        private readonly DispatcherTimer antiRebond;

        private string source = sourceRetenue;
        private string type = ContentService.Mod;
        private Instance? instance;
        private int total;
        private int generation; // ignore les réponses de recherches périmées
        private bool chargement;
        private bool configuration; // vrai pendant Configurer : les événements ne relancent pas de recherche

        /// <summary>Installe le projet (type de contenu en 2e paramètre). Renvoie true si l'installation a réussi.</summary>
        public Func<ProjetDistant, string, Task<bool>>? Installer { get; set; }

        /// <summary>Indique si un projet est déjà installé.</summary>
        public Func<ProjetDistant, bool>? EstInstalle { get; set; }

        /// <summary>Clé de traduction du bouton quand le projet n'est pas installé.</summary>
        public string LibelleInstaller { get; set; } = "Explorateur_Installer";

        private IReadOnlyList<(string Type, string Libelle)> types = Array.Empty<(string, string)>();
        private Func<IReadOnlyList<(string Type, string Libelle)>>? sourceTypes;

        public ContentBrowser()
        {
            InitializeComponent();
            ListeResultats.ItemsSource = resultats;

            antiRebond = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            antiRebond.Tick += async (_, _) =>
            {
                antiRebond.Stop();
                await RechercherAsync(nouvelle: true);
            };

            Loc.Changee += () =>
            {
                if (sourceTypes == null) return;
                configuration = true;
                types = sourceTypes();
                ConstruireTypes();
                int tri = CmbTri.SelectedIndex;
                RemplirTris();
                CmbTri.SelectedIndex = Math.Max(0, tri);
                configuration = false;
                foreach (var r in resultats) r.Rafraichir();
                MajInfo();
                if (!chargement && total > 0) TxtBas.Text = resultats.Count >= total ? Loc.T("Explorateur_Fin") : "";
            };
        }

        /// <summary>
        /// Prépare l'explorateur. instance = null pour les modpacks (pas de filtre de version/loader).
        /// </summary>
        /// <param name="types">Fonction qui renvoie les types proposés (rappelée au changement de langue).</param>
        public async void Configurer(Instance? instance, Func<IReadOnlyList<(string Type, string Libelle)>> types, string? typeInitial = null)
        {
            configuration = true;
            this.instance = instance;
            sourceTypes = types;
            this.types = types();
            type = typeInitial ?? this.types[0].Type;
            if (type == ContentService.Mod && instance?.SupporteMods == false)
                type = ContentService.PackRessources;

            ConstruireTypes();

            source = sourceRetenue;
            (source == Sources.CurseForge ? SourceCurseForge : SourceModrinth).IsChecked = true;
            RemplirTris();

            TxtRecherche.Text = "";
            antiRebond.Stop(); // le TextChanged ci-dessus ne doit pas relancer une 2e recherche
            configuration = false;
            await RechercherAsync(nouvelle: true);
        }

        private void ConstruireTypes()
        {
            PanneauTypes.Children.Clear();
            PanneauTypes.Visibility = types.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            foreach (var (t, libelle) in types)
            {
                var onglet = new RadioButton
                {
                    Style = (Style)FindResource("TabPill"),
                    Content = libelle,
                    IsChecked = t == type,
                    Tag = t,
                };
                // Pas de mods possibles sur un profil Vanilla / OptiFine
                if (t == ContentService.Mod && instance != null && !instance.SupporteMods)
                {
                    onglet.IsEnabled = false;
                    onglet.ToolTip = Loc.T("Explorateur_ModsImpossibles");
                }
                onglet.Checked += async (s, _) =>
                {
                    if (configuration) return;
                    type = (string)((RadioButton)s!).Tag;
                    await RechercherAsync(nouvelle: true);
                };
                PanneauTypes.Children.Add(onglet);
            }
        }

        /// <summary>Met à jour l'état "Installé" des résultats affichés.</summary>
        public void RafraichirEtats()
        {
            foreach (var r in resultats)
                r.EstInstalle = EstInstalle?.Invoke(r.Projet) == true;
        }

        // ───────────────────────── Source ─────────────────────────

        private async void Source_Checked(object sender, RoutedEventArgs e)
        {
            source = sender == SourceCurseForge ? Sources.CurseForge : Sources.Modrinth;
            sourceRetenue = source;
            if (configuration) return;

            configuration = true;
            RemplirTris();
            configuration = false;
            await RechercherAsync(nouvelle: true);
        }

        private void RemplirTris()
        {
            CmbTri.Items.Clear();
            foreach (var (_, libelle) in Catalogue.Tris(source))
                CmbTri.Items.Add(libelle);
            CmbTri.SelectedIndex = 0;
        }

        // ───────────────────────── Recherche ─────────────────────────

        private void TxtRecherche_TextChanged(object sender, TextChangedEventArgs e)
        {
            antiRebond.Stop();
            antiRebond.Start();
        }

        private async void CmbTri_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!configuration && CmbTri.SelectedIndex >= 0)
                await RechercherAsync(nouvelle: true);
        }

        private async void Defilement_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Défilement infini : on charge la suite quand on approche du bas
            if (!chargement && resultats.Count < total
                && Defilement.VerticalOffset + Defilement.ViewportHeight >= Defilement.ExtentHeight - 300)
                await RechercherAsync(nouvelle: false);
        }

        private async Task RechercherAsync(bool nouvelle)
        {
            int maGeneration = nouvelle ? ++generation : generation;
            if (nouvelle)
            {
                resultats.Clear();
                total = 0;
                Defilement.ScrollToTop();
            }

            // CurseForge sans clé : on explique comment la configurer au lieu de chercher
            bool sansCle = source == Sources.CurseForge && !CurseForgeApi.Disponible;
            PanneauSansCle.Visibility = sansCle ? Visibility.Visible : Visibility.Collapsed;
            Defilement.Visibility = sansCle ? Visibility.Collapsed : Visibility.Visible;
            if (sansCle) return;

            chargement = true;
            TxtBas.Text = Loc.T("Commun_ChargementMaj");
            MajInfo();

            try
            {
                string tri = Catalogue.Tris(source)[Math.Max(0, CmbTri.SelectedIndex)].Cle;
                var (projets, nbTotal) = await Catalogue.RechercherAsync(source, TxtRecherche.Text.Trim(), type, instance,
                    tri, resultats.Count, TaillePage);

                if (maGeneration != generation) return; // une autre recherche a été lancée entre-temps

                total = nbTotal;
                foreach (var projet in projets)
                    resultats.Add(new ResultatVM(projet, LibelleInstaller) { EstInstalle = EstInstalle?.Invoke(projet) == true });

                TxtBas.Text = resultats.Count == 0 ? Loc.T("Explorateur_Aucun") : resultats.Count >= total ? Loc.T("Explorateur_Fin") : "";
            }
            catch (Exception ex)
            {
                if (maGeneration == generation)
                    TxtBas.Text = Loc.T("Explorateur_Injoignable", Sources.Libelle(source)).ToUpperInvariant() + " : " + ex.Message.ToUpperInvariant();
            }
            finally
            {
                if (maGeneration == generation)
                {
                    chargement = false;
                    MajInfo();
                }
            }
        }

        private void MajInfo()
        {
            string filtre = instance == null ? "" : " · " + Loc.T("Explorateur_Compatibles", instance.VersionMc) +
                (type == ContentService.Mod || type == ContentService.Shader ? $" ({instance.Loader})" : "");
            TxtInfo.Text = (total > 0 ? Loc.T("Explorateur_Resultats", total.ToString("N0"), Sources.Libelle(source)) : Sources.Libelle(source)) + filtre;
        }

        // ───────────────────────── Installation ─────────────────────────

        private async void BtnInstaller_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not ResultatVM resultat || Installer == null || resultat.EstInstalle) return;

            resultat.EnCours = true;
            try
            {
                if (await Installer(resultat.Projet, type))
                    resultat.EstInstalle = true;
            }
            finally
            {
                resultat.EnCours = false;
                RafraichirEtats(); // les dépendances installées changent aussi d'état
            }
        }

        /// <summary>Un résultat de recherche affiché.</summary>
        public class ResultatVM : INotifyPropertyChanged
        {
            private readonly string libelleInstaller;
            private bool estInstalle, enCours;

            public ResultatVM(ProjetDistant projet, string libelleInstaller)
            {
                Projet = projet;
                this.libelleInstaller = libelleInstaller;
                Icone = Images.DepuisUrl(projet.IconeUrl);
            }

            public ProjetDistant Projet { get; }
            public ImageSource? Icone { get; }
            public string Auteur => string.IsNullOrEmpty(Projet.Auteur) ? "" : "  " + Loc.T("Explorateur_Par", Projet.Auteur);
            public string Telechargements => ModrinthApi.FormaterNombre(Projet.Telechargements);
            public string Categories => string.Join(" · ", Projet.Categories
                .Where(c => c is not ("fabric" or "forge" or "neoforge" or "quilt" or "minecraft" or "datapack" or "iris" or "optifine"))
                .Take(4));

            public bool EstInstalle { get => estInstalle; set { estInstalle = value; Notifier(); } }
            public bool EnCours { get => enCours; set { enCours = value; Notifier(); } }

            public bool BoutonActif => !EnCours;
            public string LibelleBouton => Loc.T(EnCours ? "Explorateur_EnCours" : EstInstalle ? "Explorateur_Installe" : libelleInstaller);

            /// <summary>Après un changement de langue.</summary>
            public void Rafraichir() => Notifier(nameof(Auteur));
            public string IconeBouton => EnCours ? "" : EstInstalle ? "" : "";

            public event PropertyChangedEventHandler? PropertyChanged;

            private void Notifier([CallerMemberName] string? nom = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nom));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BoutonActif)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LibelleBouton)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IconeBouton)));
            }
        }
    }
}
