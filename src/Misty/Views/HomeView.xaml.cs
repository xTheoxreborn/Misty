using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using Misty.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Misty.Views
{
    /// <summary>
    /// Page d'accueil : choix du compte, de la version, du mod loader et lancement du jeu.
    /// </summary>
    public partial class HomeView : UserControl
    {
        private readonly MinecraftPath mcPath;
        private readonly MinecraftLauncher launcher;
        private readonly ModLoaderService modLoaders;
        private readonly DiscordPresenceService discord;

        private string selectedVersion = "";
        private bool installationEnCours;
        private bool jeuEnCours;

        internal HomeView(DiscordPresenceService discord)
        {
            InitializeComponent();

            this.discord = discord;
            mcPath = new MinecraftPath(AppPaths.DataDir);
            launcher = new MinecraftLauncher(mcPath);
            modLoaders = new ModLoaderService(launcher, mcPath);

            launcher.FileProgressChanged += (sender, args) => Dispatcher.Invoke(() =>
            {
                if (args.TotalTasks <= 0) return;
                double pourcentage = args.ProgressedTasks * 100.0 / args.TotalTasks;
                AfficherProgression(pourcentage, $"Téléchargement… {args.Name}");
            });

            AccountService.SessionChanged += () => Dispatcher.Invoke(MajCompte);

            AnimerBloc();
        }

        // ───────────────────────── Initialisation ─────────────────────────

        /// <summary>Appelé une fois au démarrage, après la préparation des fichiers de config.</summary>
        public async Task InitialiserAsync()
        {
            ChargerPseudos();
            MajCompte();

            _ = ConnexionPremiumAutoAsync(); // en parallèle du chargement des versions

            Statut("Chargement des versions…");
            await ChargerVersionsAsync();
            ChargerDerniereVersion();
            if (CmbVersion.SelectedItem == null)
                Statut("Choisis une version pour commencer");
        }

        /// <summary>Appelé quand on revient sur la page (les options ont pu changer).</summary>
        public async void Rafraichir(bool rechargerVersions)
        {
            ChargerPseudos();
            if (rechargerVersions)
                await RechargerVersionsAsync();
        }

        // ───────────────────────── Comptes ─────────────────────────

        private void ChargerPseudos()
        {
            CmbCompte.SelectionChanged -= CmbCompte_SelectionChanged;
            CmbCompte.Items.Clear();
            foreach (var pseudo in ProfileStore.Lire())
                CmbCompte.Items.Add(pseudo);

            string dernier = SettingsStore.Get(SettingsStore.LastPseudo);
            CmbCompte.SelectedItem = CmbCompte.Items.Contains(dernier) ? dernier : CmbCompte.Items.Cast<object>().FirstOrDefault();
            CmbCompte.SelectionChanged += CmbCompte_SelectionChanged;

            MajAvatar();
        }

        private void CmbCompte_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbCompte.SelectedItem is string pseudo)
                SettingsStore.Set(SettingsStore.LastPseudo, pseudo);
            MajAvatar();
        }

        private async Task ConnexionPremiumAutoAsync()
        {
            if (SettingsStore.GetBool(SettingsStore.Premium) && AccountService.Session == null)
                await SeConnecterAsync();
        }

        private async void BtnMicrosoft_Click(object sender, RoutedEventArgs e)
        {
            if (AccountService.Session == null)
                await SeConnecterAsync();
            else
                AccountService.Deconnecter();
        }

        private async Task SeConnecterAsync()
        {
            BtnMicrosoft.IsEnabled = false;
            string ancienStatut = TxtStatut.Text;
            Statut("Connexion au compte Microsoft…");
            try
            {
                await AccountService.ConnecterAsync();
            }
            catch (Exception ex)
            {
                await Dialogs.ErreurAsync("Connexion impossible", ex.Message);
            }
            finally
            {
                BtnMicrosoft.IsEnabled = true;
                Statut(ancienStatut);
            }
        }

        private void MajCompte()
        {
            MSession? session = AccountService.Session;
            bool premium = session != null;

            CmbCompte.Visibility = premium ? Visibility.Collapsed : Visibility.Visible;
            PanneauPremium.Visibility = premium ? Visibility.Visible : Visibility.Collapsed;
            TxtPseudoPremium.Text = session?.Username ?? "";

            BtnMicrosoft.Content = premium ? "" : "";
            BtnMicrosoft.ToolTip = premium ? "Se déconnecter" : "Se connecter avec un compte Microsoft";

            PastilleCompte.ToolTip = premium ? $"Compte Microsoft : {session!.Username}" : "Hors-ligne";
            PastilleCompteIcone.Text = premium ? "" : "";
            ((Border)PastilleCompte.Child).Background = (Brush)FindResource(premium ? "GreenBrush" : "BlueBrush");

            MajAvatar();
        }

        private string PseudoActuel =>
            AccountService.Session?.Username
            ?? (CmbCompte.SelectedItem as string is { Length: > 0 } p ? p : "Misty");

        /// <summary>Tête du skin (mc-heads.net). Si le téléchargement échoue, le bloc d'herbe reste visible dessous.</summary>
        private void MajAvatar()
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri($"https://mc-heads.net/avatar/{Uri.EscapeDataString(PseudoActuel)}/64");
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.DownloadFailed += (_, _) => Avatar.Source = null;
                Avatar.Source = image;
            }
            catch
            {
                Avatar.Source = null;
            }
        }

        // ───────────────────────── Versions ─────────────────────────

        private async Task ChargerVersionsAsync()
        {
            bool snapshot = SettingsStore.GetBool(SettingsStore.Snapshot);
            bool beta = SettingsStore.GetBool(SettingsStore.Beta);
            bool alpha = SettingsStore.GetBool(SettingsStore.Alpha);

            try
            {
                var versions = await launcher.GetAllVersionsAsync();

                CmbVersion.Items.Clear();
                foreach (var v in versions)
                {
                    string type = v.Type?.ToString() ?? "";

                    if (type.Equals("release", StringComparison.OrdinalIgnoreCase)
                        || (snapshot && type.Equals("snapshot", StringComparison.OrdinalIgnoreCase))
                        || (beta && type.Equals("old_beta", StringComparison.OrdinalIgnoreCase))
                        || (alpha && type.Equals("old_alpha", StringComparison.OrdinalIgnoreCase)))
                    {
                        CmbVersion.Items.Add(v.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                Statut("Impossible de charger les versions");
                await Dialogs.ErreurAsync("Versions indisponibles", "Erreur au chargement des versions : " + ex.Message);
            }
        }

        private void ChargerDerniereVersion()
        {
            string derniere = SettingsStore.Get(SettingsStore.LastVersion);
            if (CmbVersion.Items.Contains(derniere))
                CmbVersion.SelectedItem = derniere;
            else if (CmbVersion.Items.Count > 0)
                CmbVersion.SelectedIndex = 0; // la plus récente
        }

        private async Task RechargerVersionsAsync()
        {
            object? versionActuelle = CmbVersion.SelectedItem;
            BtnRecharger.IsEnabled = false;
            Statut("Chargement des versions…");

            await ChargerVersionsAsync();

            if (versionActuelle != null && CmbVersion.Items.Contains(versionActuelle))
                CmbVersion.SelectedItem = versionActuelle;
            else
                Statut("Choisis une version pour commencer");

            BtnRecharger.IsEnabled = true;
        }

        private async void BtnRecharger_Click(object sender, RoutedEventArgs e) => await RechargerVersionsAsync();

        private async void CmbVersion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbVersion.SelectedItem is not string version) return;

            BtnJouer.IsEnabled = false;
            CmbMode.IsEnabled = false;
            CmbMode.Items.Clear();

            selectedVersion = version;
            CarteVersion.Text = version;
            SettingsStore.Set(SettingsStore.LastVersion, version);
            Statut($"Recherche des mod loaders pour {version}…");

            var modes = await modLoaders.ModesDisponiblesAsync(version);

            // L'utilisateur a changé de version pendant le chargement : on laisse la nouvelle sélection gérer
            if (version != selectedVersion) return;

            foreach (var mode in modes)
                CmbMode.Items.Add(mode);
            CmbMode.SelectedIndex = 0;
            CmbMode.IsEnabled = true;

            if (!installationEnCours)
            {
                BtnJouer.IsEnabled = true;
                Statut(jeuEnCours ? "Minecraft est en cours d'exécution" : "Prêt à jouer");
            }
        }

        private void CmbMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CarteLoader.Text = (CmbMode.SelectedItem as string ?? ModLoaderService.Vanilla).ToUpperInvariant();
        }

        // ───────────────────────── Lancement ─────────────────────────

        private async void BtnJouer_Click(object sender, RoutedEventArgs e)
        {
            string version = selectedVersion;
            string mode = CmbMode.SelectedItem as string ?? ModLoaderService.Vanilla;

            installationEnCours = true;
            BtnJouer.IsEnabled = false;
            CmbVersion.IsEnabled = false;
            CmbMode.IsEnabled = false;
            AfficherProgression(0, $"Préparation de {version} {mode}…");

            try
            {
                string nomVersion;
                try
                {
                    nomVersion = await modLoaders.InstallerAsync(version, mode);
                }
                catch (Exception ex)
                {
                    Statut("Échec du téléchargement");
                    await Dialogs.ErreurAsync("Téléchargement impossible", ex.Message);
                    return;
                }

                AfficherProgression(100, "Lancement de Minecraft…");
                await LancerJeuAsync(version, mode, nomVersion);
            }
            finally
            {
                installationEnCours = false;
                BtnJouer.Content = "JOUER";
                BtnJouer.IsEnabled = true;
                CmbVersion.IsEnabled = true;
                CmbMode.IsEnabled = true;
                TxtPourcentage.Text = "";
            }
        }

        private async Task LancerJeuAsync(string version, string mode, string nomVersion)
        {
            if (!int.TryParse(SettingsStore.Get(SettingsStore.Ram), out int ramMo))
                ramMo = AppInfo.RamParDefautMo;

            try
            {
                var option = new MLaunchOption
                {
                    Session = AccountService.Session ?? MSession.CreateOfflineSession(PseudoActuel),
                    MaximumRamMb = ramMo
                };

                Process process = mode == ModLoaderService.OptiFine
                    ? await launcher.InstallAndBuildProcessAsync(nomVersion, option)
                    : await launcher.BuildProcessAsync(nomVersion, option);

                discord.EnJeu(version, mode);

                process.EnableRaisingEvents = true;
                process.Exited += (s, args) => Dispatcher.Invoke(() =>
                {
                    jeuEnCours = false;
                    discord.DansLeLauncher();
                    Statut("Prêt à jouer");
                });

                process.Start();
                jeuEnCours = true;
                Statut($"Minecraft {version} est lancé, bon jeu !");
            }
            catch (Exception ex)
            {
                Statut("Échec du lancement");
                await Dialogs.ErreurAsync("Impossible de lancer le jeu", ex.Message);
            }
        }

        // ───────────────────────── Affichage ─────────────────────────

        private void Statut(string texte) => TxtStatut.Text = texte;

        private void AfficherProgression(double pourcentage, string statut)
        {
            Statut(statut);
            TxtPourcentage.Text = $"{pourcentage:0}%";
            if (installationEnCours)
                BtnJouer.Content = $"{pourcentage:0}%";

            // Animation douce plutôt qu'un saut brutal
            Progression.BeginAnimation(ProgressBar.ValueProperty,
                new DoubleAnimation(pourcentage, TimeSpan.FromMilliseconds(250)) { EasingFunction = new QuadraticEase() });
        }

        /// <summary>Le bloc de la carte flotte doucement, son ombre suit.</summary>
        private void AnimerBloc()
        {
            var duree = TimeSpan.FromSeconds(2.6);
            var ease = new SineEase { EasingMode = EasingMode.EaseInOut };

            Bloc.RenderTransform.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(-6, 8, duree) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease });

            var ombre = (ScaleTransform)OmbreBloc.RenderTransform;
            var anim = new DoubleAnimation(0.8, 1.1, duree) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease };
            ombre.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            ombre.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        }
    }
}
