using Misty.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Misty.Views
{
    /// <summary>
    /// Fenêtre principale : cadre, navigation entre les pages, boîtes de dialogue, démarrage.
    /// </summary>
    public partial class MainWindow : Window, IDialogHost
    {
        private readonly DiscordPresenceService discord;
        private readonly HomeView accueil;
        private readonly SettingsView options;
        private readonly ChangelogView notes;

        private TaskCompletionSource<bool>? dialogEnCours;

        public MainWindow()
        {
            InitializeComponent();

            Dialogs.Host = this;
            TxtVersionApp.Text = "v" + AppInfo.Version;

            discord = new DiscordPresenceService();
            discord.DansLeLauncher();

            accueil = new HomeView(discord);
            options = new SettingsView();
            notes = new ChangelogView();
            PageHost.Content = accueil;

            // Coins arrondis : on découpe le contenu à la forme du cadre
            FrameContent.SizeChanged += (_, e) =>
                FrameContent.Clip = new RectangleGeometry(new Rect(e.NewSize), 24, 24);

            GenererParticules();
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            Loaded += async (_, _) => await DemarrerAsync();
        }

        protected override void OnClosed(EventArgs e)
        {
            discord.Dispose();
            base.OnClosed(e);
        }

        // ───────────────────────── Démarrage ─────────────────────────

        private async Task DemarrerAsync()
        {
            await MigrerAncienDossierAsync();

            SettingsStore.Initialiser();
            ProfileStore.Initialiser();

            await VerifierMiseAJourAsync();
            await FinaliserMiseAJourAsync();

            await accueil.InitialiserAsync();
        }

        private async Task MigrerAncienDossierAsync()
        {
            try
            {
                bool migre = await UpdateService.MigrerAncienDossierAsync(() => { });
                if (migre)
                    await Dialogs.InfoAsync("Dossier migré", "Tes données ont été déplacées vers le nouveau dossier d'installation.");
            }
            catch (Exception ex)
            {
                await Dialogs.ErreurAsync("Migration impossible", "Erreur lors de la migration de l'ancien dossier : " + ex.Message);
            }
        }

        private async Task VerifierMiseAJourAsync()
        {
            UpdateService.ReleaseInfo? release;
            try
            {
                release = await UpdateService.VerifierAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Erreur vérification MAJ : " + ex.Message);
                return;
            }

            if (release == null) return;

            bool installer = await Dialogs.ConfirmerAsync(
                "Mise à jour disponible",
                $"La version {release.Version} est disponible (actuelle : {AppInfo.Version}).\nLa télécharger maintenant ?",
                "Mettre à jour", "Plus tard");

            if (!installer) return;

            try
            {
                await UpdateService.InstallerAsync(release);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                await Dialogs.ErreurAsync("Mise à jour impossible", ex.Message);
            }
        }

        private async Task FinaliserMiseAJourAsync()
        {
            try
            {
                if (await UpdateService.FinaliserMiseAJourAsync())
                    await Dialogs.InfoAsync("Mise à jour terminée", $"Misty est maintenant en version {AppInfo.Version} !");
            }
            catch (Exception ex)
            {
                await Dialogs.ErreurAsync("Nettoyage impossible", "Erreur lors de la suppression de l'ancienne version : " + ex.Message);
            }
        }

        // ───────────────────────── Navigation ─────────────────────────

        private void Nav_Checked(object sender, RoutedEventArgs e)
        {
            if (PageHost == null) return; // pendant InitializeComponent

            UserControl page = sender == NavOptions ? options
                             : sender == NavNotes ? notes
                             : accueil;

            if (page == accueil)
                accueil.Rafraichir(options.ConsommerChangementTypesVersions());
            if (page == options)
                options.Rafraichir();

            Naviguer(page);
        }

        private void Naviguer(UserControl page)
        {
            if (PageHost.Content == page) return;

            PageHost.Content = page;

            var duree = TimeSpan.FromMilliseconds(320);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            PageHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duree) { EasingFunction = ease });
            PageHost.RenderTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(18, 0, duree) { EasingFunction = ease });
        }

        // ───────────────────────── Barre de titre / latérale ─────────────────────────

        private void BtnMinimiser_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void BtnFermer_Click(object sender, RoutedEventArgs e) => Close();

        private void BtnDossier_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            Process.Start("explorer.exe", AppPaths.DataDir);
        }

        // ───────────────────────── Dialogues ─────────────────────────

        public Task<bool> AfficherAsync(string titre, string message, string ok, string? annuler, bool danger)
        {
            dialogEnCours?.TrySetResult(false);
            dialogEnCours = new TaskCompletionSource<bool>();

            DialogTitre.Text = titre;
            DialogMessage.Text = message;
            DialogOk.Content = ok;
            DialogOk.Style = (Style)FindResource(danger && annuler != null ? "DangerButton" : "PrimaryButton");
            DialogAnnuler.Content = annuler;
            DialogAnnuler.Visibility = annuler == null ? Visibility.Collapsed : Visibility.Visible;
            DialogIconBg.Background = (Brush)FindResource(danger ? "DangerBrush" : "BlueBrush");
            DialogIcon.Text = danger ? "" : annuler != null ? "" : "";

            DialogLayer.Visibility = Visibility.Visible;
            var duree = TimeSpan.FromMilliseconds(220);
            var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 };
            DialogLayer.BeginAnimation(OpacityProperty, new DoubleAnimation(1, duree));
            DialogCard.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.92, 1, duree) { EasingFunction = ease });
            DialogCard.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.92, 1, duree) { EasingFunction = ease });

            DialogOk.Focus();
            return dialogEnCours.Task;
        }

        private void FermerDialog(bool resultat)
        {
            var anim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
            anim.Completed += (_, _) =>
            {
                if (DialogLayer.Opacity == 0)
                    DialogLayer.Visibility = Visibility.Collapsed;
            };
            DialogLayer.BeginAnimation(OpacityProperty, anim);

            var tcs = dialogEnCours;
            dialogEnCours = null;
            tcs?.TrySetResult(resultat);
        }

        private void DialogOk_Click(object sender, RoutedEventArgs e) => FermerDialog(true);

        private void DialogAnnuler_Click(object sender, RoutedEventArgs e) => FermerDialog(false);

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (dialogEnCours == null) return;

            if (e.Key == Key.Escape)
            {
                FermerDialog(false);
                e.Handled = true;
            }
        }

        // ───────────────────────── Décor ─────────────────────────

        /// <summary>Petits carrés blancs translucides qui flottent lentement en fond.</summary>
        private void GenererParticules()
        {
            var rnd = new Random(7);
            for (int i = 0; i < 14; i++)
            {
                double taille = rnd.Next(8, 30);
                var carre = new Rectangle
                {
                    Width = taille,
                    Height = taille,
                    RadiusX = taille / 5,
                    RadiusY = taille / 5,
                    Fill = Brushes.White,
                    Opacity = 0.06 + rnd.NextDouble() * 0.14,
                };
                Canvas.SetLeft(carre, 120 + rnd.NextDouble() * 1020);
                Canvas.SetTop(carre, 20 + rnd.NextDouble() * 660);

                var deplacement = new TranslateTransform();
                carre.RenderTransform = deplacement;
                Particles.Children.Add(carre);

                deplacement.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
                {
                    From = -10 - rnd.Next(10),
                    To = 10 + rnd.Next(10),
                    Duration = TimeSpan.FromSeconds(4 + rnd.NextDouble() * 4),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                });
            }
        }
    }
}
