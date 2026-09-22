using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.ProcessBuilder;
using Misty.Services;
using System.Diagnostics;

namespace Misty.Forms
{
    /// <summary>
    /// Fenêtre principale : choix du compte, de la version, du mod loader et lancement du jeu.
    /// </summary>
    public partial class MainForm : Form
    {
        private readonly MinecraftPath mcPath;
        private readonly MinecraftLauncher launcher;
        private readonly ModLoaderService modLoaders;
        private readonly DiscordPresenceService discord;

        private JELoginHandler? loginHandler;
        private MSession? session;
        private string selectedVersion = "";

        public MainForm()
        {
            InitializeComponent();

            discord = new DiscordPresenceService();
            discord.DansLeLauncher();

            lblVersionApp.Text = AppInfo.Version;
            lblVersionApp.Location = new Point(
                ClientSize.Width - lblVersionApp.Width - 4,
                ClientSize.Height - lblVersionApp.Height - 4);

            MaximumSize = Size;
            MinimumSize = Size;

            if (File.Exists(AppPaths.CustomIcon))
                Icon = new Icon(AppPaths.CustomIcon);

            mcPath = new MinecraftPath(AppPaths.DataDir);
            launcher = new MinecraftLauncher(mcPath);
            modLoaders = new ModLoaderService(launcher, mcPath);

            launcher.FileProgressChanged += (sender, args) =>
            {
                if (args.TotalTasks <= 0) return;
                progressBar.Maximum = args.TotalTasks;
                progressBar.Value = Math.Min(args.ProgressedTasks, args.TotalTasks);
                lblPourcentage.Text = $"{args.ProgressedTasks * 100 / args.TotalTasks}%";
            };

            // Empêche la molette de changer la sélection des listes par accident
            cmbVersion.MouseWheel += BloquerMolette;
            cmbMode.MouseWheel += BloquerMolette;
            cmbCompte.MouseWheel += BloquerMolette;

            Load += async (_, _) => await DemarrerAsync();
        }

        private static void BloquerMolette(object? sender, MouseEventArgs e) => ((HandledMouseEventArgs)e).Handled = true;

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            discord.Dispose();
            base.OnFormClosing(e);
        }

        // ───────────────────────── Démarrage ─────────────────────────

        private async Task DemarrerAsync()
        {
            await MigrerAncienDossierAsync();

            SettingsStore.Initialiser();
            ProfileStore.Initialiser();

            await VerifierMiseAJourAsync();
            await FinaliserMiseAJourAsync();

            ChargerPseudos();
            _ = ConnexionPremiumAutoAsync(); // en parallèle du chargement des versions
            await ChargerVersionsAsync();
            ChargerDerniereVersion();
        }

        private async Task MigrerAncienDossierAsync()
        {
            try
            {
                bool migre = await UpdateService.MigrerAncienDossierAsync(() =>
                    MessageBox.Show("Misty est en cours de mise à jour vers le nouveau dossier d'installation. Veuillez ne pas fermer l'application et ne pas toucher l'application."));

                if (migre)
                    MessageBox.Show("Misty a été mis à jour vers le nouveau dossier d'installation avec succès !");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur lors de la migration de l'ancien dossier : " + ex.Message);
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

            var reponse = MessageBox.Show(
                $"Une nouvelle version est disponible : {release.Version} (actuelle : {AppInfo.Version}).\nTélécharger maintenant ?",
                "Mise à jour disponible",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (reponse != DialogResult.Yes) return;

            try
            {
                await UpdateService.InstallerAsync(release);
                Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur lors de la mise à jour : " + ex.Message);
            }
        }

        private async Task FinaliserMiseAJourAsync()
        {
            try
            {
                if (await UpdateService.FinaliserMiseAJourAsync())
                    MessageBox.Show("Mise à jour réussie avec succès !", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur lors de la suppression de l'ancienne version : " + ex.Message);
            }
        }

        // ───────────────────────── Comptes ─────────────────────────

        private void ChargerPseudos()
        {
            cmbCompte.Items.Clear();
            foreach (var pseudo in ProfileStore.Lire())
                cmbCompte.Items.Add(pseudo);

            cmbCompte.Text = SettingsStore.Get(SettingsStore.LastPseudo);
        }

        private void cmbCompte_SelectedIndexChanged(object sender, EventArgs e)
        {
            SettingsStore.Set(SettingsStore.LastPseudo, cmbCompte.Text);
        }

        private async Task ConnexionPremiumAutoAsync()
        {
            if (SettingsStore.GetBool(SettingsStore.Premium) && session == null)
                await SeConnecterAsync();
        }

        private async void btnConnexion_Click(object sender, EventArgs e)
        {
            if (await SeConnecterAsync())
                SettingsStore.Set(SettingsStore.Premium, "True");
        }

        private async Task<bool> SeConnecterAsync()
        {
            try
            {
                btnConnexion.Enabled = false;
                btnConnexion.Text = "Connexion en cours...";

                loginHandler = JELoginHandlerBuilder.BuildDefault();
                session = await loginHandler.Authenticate();

                btnConnexion.Text = session.Username;
                cmbCompte.Enabled = false;
                btnDeconnexion.Enabled = true;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur de connexion : " + ex.Message);
                btnConnexion.Text = "Se connecter";
                btnConnexion.Enabled = true;
                return false;
            }
        }

        private void btnDeconnexion_Click(object sender, EventArgs e)
        {
            Deconnecter();
        }

        /// <summary>Déconnexion + suppression du compte Microsoft mis en cache.</summary>
        private async void btnDeconnexionTotale_Click(object sender, EventArgs e)
        {
            try
            {
                if (loginHandler != null)
                    await loginHandler.Signout();

                Deconnecter();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur de déconnexion : " + ex.Message);
            }
        }

        private void Deconnecter()
        {
            session = null;
            btnConnexion.Enabled = true;
            btnConnexion.Text = "Se connecter";
            cmbCompte.Enabled = true;
            btnDeconnexion.Enabled = false;

            SettingsStore.Set(SettingsStore.Premium, "False");
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

                cmbVersion.Items.Clear();
                foreach (var v in versions)
                {
                    string type = v.Type?.ToString() ?? "";

                    if (type.Equals("release", StringComparison.OrdinalIgnoreCase)
                        || (snapshot && type.Equals("snapshot", StringComparison.OrdinalIgnoreCase))
                        || (beta && type.Equals("old_beta", StringComparison.OrdinalIgnoreCase))
                        || (alpha && type.Equals("old_alpha", StringComparison.OrdinalIgnoreCase)))
                    {
                        cmbVersion.Items.Add(v.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur au chargement des versions : " + ex.Message);
            }
        }

        private void ChargerDerniereVersion()
        {
            cmbVersion.Text = SettingsStore.Get(SettingsStore.LastVersion);
        }

        private async void cmbVersion_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnLancer.Enabled = false;
            cmbMode.Enabled = false;
            cmbMode.Items.Clear();

            string version = cmbVersion.Text;
            selectedVersion = version;
            SettingsStore.Set(SettingsStore.LastVersion, version);

            var modes = await modLoaders.ModesDisponiblesAsync(version);

            // L'utilisateur a changé de version pendant le chargement : on laisse la nouvelle sélection gérer
            if (version != selectedVersion) return;

            cmbMode.Items.AddRange(modes.ToArray());
            cmbMode.SelectedIndex = 0;
            cmbMode.Enabled = true;
            btnLancer.Enabled = true;
        }

        private async void picRecharger_Click(object sender, EventArgs e)
        {
            string versionActuelle = cmbVersion.Text;
            cmbMode.Enabled = false;
            cmbMode.Items.Clear();

            await ChargerVersionsAsync();
            cmbVersion.SelectedItem = versionActuelle;
        }

        // ───────────────────────── Lancement ─────────────────────────

        private async void btnLancer_Click(object sender, EventArgs e)
        {
            string version = selectedVersion;
            string mode = cmbMode.Text;

            btnLancer.Enabled = false;
            try
            {
                string nomVersion;
                try
                {
                    nomVersion = await modLoaders.InstallerAsync(version, mode);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Erreur pendant le téléchargement : " + ex.Message);
                    return;
                }

                await LancerJeuAsync(version, mode, nomVersion);
            }
            finally
            {
                btnLancer.Enabled = true;
            }
        }

        private async Task LancerJeuAsync(string version, string mode, string nomVersion)
        {
            string pseudo = string.IsNullOrWhiteSpace(cmbCompte.Text) ? "Misty" : cmbCompte.Text;

            if (!int.TryParse(SettingsStore.Get(SettingsStore.Ram), out int ramMo))
                ramMo = AppInfo.RamParDefautMo;

            try
            {
                var option = new MLaunchOption
                {
                    Session = session ?? MSession.CreateOfflineSession(pseudo),
                    MaximumRamMb = ramMo
                };

                Process process = mode == ModLoaderService.OptiFine
                    ? await launcher.InstallAndBuildProcessAsync(nomVersion, option)
                    : await launcher.BuildProcessAsync(nomVersion, option);

                discord.EnJeu(version, mode);

                process.EnableRaisingEvents = true;
                process.Exited += (s, args) => Invoke(discord.DansLeLauncher);

                process.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Impossible de lancer le jeu : " + ex.Message);
            }
        }

        // ───────────────────────── Divers ─────────────────────────

        private void lblVersionApp_Click(object sender, EventArgs e)
        {
            using var form = new ChangelogForm();
            form.ShowDialog();
        }

        private void btnOptions_Click(object sender, EventArgs e)
        {
            using (var form = new SettingsForm())
                form.ShowDialog();

            // Les options ont pu modifier les profils ou le fichier de config
            ChargerPseudos();
            ChargerDerniereVersion();
        }

        private void picDossier_Click(object sender, EventArgs e)
        {
            Process.Start("explorer.exe", AppPaths.DataDir);
        }
    }
}
