using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using Misty.Controls;
using Misty.Models;
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
    /// Page d'accueil : choix du compte, du profil (ou jeu rapide : version + mod loader) et lancement du jeu.
    /// </summary>
    public partial class HomeView : UserControl
    {
        /// <summary>Élément de la liste des profils ; Instance = null pour le jeu rapide.</summary>
        private sealed record ChoixProfil(string Nom, Instance? Instance)
        {
            public override string ToString() => Nom;
        }

        private readonly DiscordPresenceService discord;

        private string selectedVersion = "";
        private bool modesCharges;
        private bool installationEnCours;
        private bool jeuEnCours;
        private Instance? profil;

        /// <summary>L'utilisateur veut gérer le contenu du profil sélectionné.</summary>
        public event Action<Instance>? GererProfilDemande;

        internal HomeView(DiscordPresenceService discord)
        {
            InitializeComponent();

            this.discord = discord;
            MinecraftService.Launcher.FileProgressChanged += ProgressionFichiers;

            AccountService.SessionChanged += () => Dispatcher.Invoke(MajCompte);
            InstanceStore.Modifie += () => Dispatcher.Invoke(ChargerProfils);

            AnimerBloc();
        }

        // ───────────────────────── Initialisation ─────────────────────────

        /// <summary>Appelé une fois au démarrage, après la préparation des fichiers de config.</summary>
        public async Task InitialiserAsync()
        {
            ChargerPseudos();
            MajCompte();
            ChargerProfils();

            _ = ConnexionPremiumAutoAsync(); // en parallèle du chargement des versions

            Statut("Chargement des versions…");
            await ChargerVersionsAsync();
            ChargerDerniereVersion();
            if (profil == null && CmbVersion.SelectedItem == null)
                Statut("Choisis une version pour commencer");
        }

        /// <summary>Appelé quand on revient sur la page (les options ont pu changer).</summary>
        public async void Rafraichir(bool rechargerVersions)
        {
            ChargerPseudos();
            ChargerProfils();
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

        // ───────────────────────── Profils ─────────────────────────

        private void ChargerProfils()
        {
            string? idVoulu = profil?.Id ?? SettingsStore.Get(SettingsStore.DernierProfil);

            var choix = new List<ChoixProfil> { new("Jeu rapide", null) };
            choix.AddRange(InstanceStore.Lister().Select(i => new ChoixProfil(i.Nom, i)));

            CmbProfil.SelectionChanged -= CmbProfil_SelectionChanged;
            CmbProfil.ItemsSource = choix;
            CmbProfil.SelectedItem = choix.FirstOrDefault(c => c.Instance?.Id == idVoulu) ?? choix[0];
            CmbProfil.SelectionChanged += CmbProfil_SelectionChanged;

            profil = (CmbProfil.SelectedItem as ChoixProfil)?.Instance;
            MajModeProfil();
        }

        private void CmbProfil_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            profil = (CmbProfil.SelectedItem as ChoixProfil)?.Instance;
            SettingsStore.Set(SettingsStore.DernierProfil, profil?.Id ?? "");
            MajModeProfil();
        }

        /// <summary>Bascule l'affichage entre le jeu rapide et le profil sélectionné.</summary>
        private void MajModeProfil()
        {
            bool avecProfil = profil != null;
            PanneauRapide.Visibility = avecProfil ? Visibility.Collapsed : Visibility.Visible;
            PanneauProfil.Visibility = avecProfil ? Visibility.Visible : Visibility.Collapsed;

            if (profil != null)
            {
                CarteLibelle.Text = "PROFIL · " + profil.Nom.ToUpperInvariant();
                CarteVersion.Text = profil.VersionMc;
                CarteLoader.Text = profil.Loader.ToUpperInvariant();
                TxtResumeProfil.Text = ResumeContenu(profil);

                // L'icône du profil remplace le bloc d'herbe (en lissé : ce n'est pas du pixel art)
                var icone = profil.Icone;
                Bloc.Source = icone ?? PixelArt.BlocHerbe;
                RenderOptions.SetBitmapScalingMode(Bloc, icone != null ? BitmapScalingMode.HighQuality : BitmapScalingMode.NearestNeighbor);
            }
            else
            {
                CarteLibelle.Text = "VERSION SÉLECTIONNÉE";
                CarteVersion.Text = string.IsNullOrEmpty(selectedVersion) ? "—" : selectedVersion;
                CarteLoader.Text = (CmbMode.SelectedItem as string ?? ModLoaderService.Vanilla).ToUpperInvariant();
                Bloc.Source = PixelArt.BlocHerbe;
                RenderOptions.SetBitmapScalingMode(Bloc, BitmapScalingMode.NearestNeighbor);
            }

            if (!installationEnCours)
            {
                BtnJouer.IsEnabled = avecProfil || modesCharges;
                if (avecProfil || modesCharges)
                    Statut(jeuEnCours ? "Minecraft est en cours d'exécution" : "Prêt à jouer");
            }
        }

        /// <summary>"12 mods · 2 shaders"</summary>
        private static string ResumeContenu(Instance instance)
        {
            var morceaux = new List<string>();
            try
            {
                foreach (var groupe in ContentService.Lister(instance).Where(c => c.Actif).GroupBy(c => c.Type))
                {
                    int n = groupe.Count();
                    string libelle = groupe.Key switch
                    {
                        ContentService.Mod => n > 1 ? "mods" : "mod",
                        ContentService.PackRessources => n > 1 ? "packs de ressources" : "pack de ressources",
                        ContentService.Datapack => n > 1 ? "datapacks" : "datapack",
                        ContentService.Shader => n > 1 ? "shaders" : "shader",
                        _ => groupe.Key,
                    };
                    morceaux.Add($"{n} {libelle}");
                }
            }
            catch { }
            return morceaux.Count > 0 ? string.Join(" · ", morceaux) : "Aucun contenu : clique sur le crayon pour en ajouter";
        }

        private void BtnGererProfil_Click(object sender, RoutedEventArgs e)
        {
            if (profil != null)
                GererProfilDemande?.Invoke(profil);
        }

        /// <summary>Sélectionne un profil et lance la partie (depuis la page Profils).</summary>
        public void SelectionnerEtJouer(Instance instance)
        {
            profil = instance;
            SettingsStore.Set(SettingsStore.DernierProfil, instance.Id);
            ChargerProfils();
            if (!installationEnCours)
                BtnJouer_Click(this, new RoutedEventArgs());
        }

        // ───────────────────────── Versions (jeu rapide) ─────────────────────────

        private async Task ChargerVersionsAsync()
        {
            try
            {
                var versions = await MinecraftService.VersionsAsync();
                CmbVersion.Items.Clear();
                foreach (var v in versions)
                    CmbVersion.Items.Add(v);
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
            if (profil == null) Statut("Chargement des versions…");

            await ChargerVersionsAsync();

            if (versionActuelle != null && CmbVersion.Items.Contains(versionActuelle))
                CmbVersion.SelectedItem = versionActuelle;
            else if (profil == null)
                Statut("Choisis une version pour commencer");

            BtnRecharger.IsEnabled = true;
        }

        private async void BtnRecharger_Click(object sender, RoutedEventArgs e) => await RechargerVersionsAsync();

        private async void CmbVersion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbVersion.SelectedItem is not string version) return;

            modesCharges = false;
            CmbMode.IsEnabled = false;
            CmbMode.Items.Clear();

            selectedVersion = version;
            SettingsStore.Set(SettingsStore.LastVersion, version);
            if (profil == null)
            {
                BtnJouer.IsEnabled = false;
                CarteVersion.Text = version;
                Statut($"Recherche des mod loaders pour {version}…");
            }

            var modes = await MinecraftService.Loaders.ModesDisponiblesAsync(version);

            // L'utilisateur a changé de version pendant le chargement : on laisse la nouvelle sélection gérer
            if (version != selectedVersion) return;

            foreach (var mode in modes)
                CmbMode.Items.Add(mode);
            CmbMode.SelectedIndex = 0;
            CmbMode.IsEnabled = !installationEnCours;
            modesCharges = true;

            if (profil == null)
                MajModeProfil();
        }

        private void CmbMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (profil == null)
                CarteLoader.Text = (CmbMode.SelectedItem as string ?? ModLoaderService.Vanilla).ToUpperInvariant();
        }

        // ───────────────────────── Lancement ─────────────────────────

        private async void BtnJouer_Click(object sender, RoutedEventArgs e)
        {
            var instance = profil;
            string version = instance?.VersionMc ?? selectedVersion;
            string mode = instance?.Loader ?? CmbMode.SelectedItem as string ?? ModLoaderService.Vanilla;
            if (string.IsNullOrEmpty(version)) return;

            installationEnCours = true;
            BtnJouer.IsEnabled = false;
            CmbVersion.IsEnabled = false;
            CmbMode.IsEnabled = false;
            CmbProfil.IsEnabled = false;
            AfficherProgression(0, $"Préparation de {instance?.Nom ?? $"{version} {mode}"}…");

            try
            {
                string nomVersion;
                try
                {
                    nomVersion = await MinecraftService.Loaders.InstallerAsync(version, mode, instance?.LoaderVersion);
                }
                catch (Exception ex)
                {
                    Statut("Échec du téléchargement");
                    await Dialogs.ErreurAsync("Téléchargement impossible", ex.Message);
                    return;
                }

                AfficherProgression(100, "Lancement de Minecraft…");
                await LancerJeuAsync(version, mode, nomVersion, instance);
            }
            finally
            {
                installationEnCours = false;
                BtnJouer.Content = "JOUER";
                BtnJouer.IsEnabled = true;
                CmbVersion.IsEnabled = true;
                CmbMode.IsEnabled = modesCharges;
                CmbProfil.IsEnabled = true;
                TxtPourcentage.Text = "";
            }
        }

        private async Task LancerJeuAsync(string version, string mode, string nomVersion, Instance? instance)
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

                // Un profil a son propre dossier de jeu (mods, options, mondes) ; le jeu rapide utilise le dossier principal
                var launcher = MinecraftService.Launcher;
                if (instance != null)
                {
                    launcher = new MinecraftLauncher(InstanceStore.CheminMinecraft(instance));
                    launcher.FileProgressChanged += ProgressionFichiers;
                }

                Process process = mode == ModLoaderService.OptiFine
                    ? await launcher.InstallAndBuildProcessAsync(nomVersion, option)
                    : await launcher.BuildProcessAsync(nomVersion, option);

                discord.EnJeu(version, instance != null ? $"{mode} · {instance.Nom}" : mode);

                process.EnableRaisingEvents = true;
                process.Exited += (s, args) => Dispatcher.Invoke(() =>
                {
                    jeuEnCours = false;
                    discord.DansLeLauncher();
                    Statut("Prêt à jouer");
                    if (profil != null) TxtResumeProfil.Text = ResumeContenu(profil);
                });

                process.Start();
                jeuEnCours = true;
                Statut($"{instance?.Nom ?? "Minecraft " + version} est lancé, bon jeu !");

                if (instance != null)
                {
                    instance.DernierLancement = DateTime.Now;
                    InstanceStore.Sauver(instance);
                }
            }
            catch (Exception ex)
            {
                Statut("Échec du lancement");
                await Dialogs.ErreurAsync("Impossible de lancer le jeu", ex.Message);
            }
        }

        // ───────────────────────── Affichage ─────────────────────────

        private void ProgressionFichiers(object? sender, CmlLib.Core.Installers.InstallerProgressChangedEventArgs args) => Dispatcher.Invoke(() =>
        {
            if (args.TotalTasks <= 0) return;
            double pourcentage = args.ProgressedTasks * 100.0 / args.TotalTasks;
            AfficherProgression(pourcentage, $"Téléchargement… {args.Name}");
        });

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
