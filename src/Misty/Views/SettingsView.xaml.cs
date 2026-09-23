using Microsoft.Win32;
using Misty.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Misty.Views
{
    /// <summary>
    /// Options : profils, compte Microsoft, RAM, raccourci, types de versions, stockage.
    /// </summary>
    public partial class SettingsView : UserControl
    {
        // true pendant qu'on remplit les contrôles : les événements ne doivent rien enregistrer
        private bool chargement = true;
        private bool typesVersionsModifies;

        public SettingsView()
        {
            InitializeComponent();

            TxtDossier.Text = AppPaths.DataDir;
            TxtAPropos.Text = $"Misty {AppInfo.Version} · licence MIT";

            AccountService.SessionChanged += () => Dispatcher.Invoke(MajMicrosoft);
            chargement = false;
        }

        /// <summary>Recharge l'affichage depuis les fichiers de config (appelé à chaque ouverture de la page).</summary>
        public void Rafraichir()
        {
            chargement = true;
            try
            {
                ChargerProfils();
                MajMicrosoft();

                int ramMax = SystemInfo.RamMaxProposeeMo();
                SliderRam.Maximum = ramMax;
                TxtRamTotale.Text = $"RAM allouée à Minecraft (ton PC a {SystemInfo.RamTotaleMo() / 1024.0:0.#} Go).";
                if (!int.TryParse(SettingsStore.Get(SettingsStore.Ram), out int ram))
                    ram = AppInfo.RamParDefautMo;
                SliderRam.Value = Math.Clamp(ram, 1024, ramMax);
                AfficherRam();

                ChkSnapshot.IsChecked = SettingsStore.GetBool(SettingsStore.Snapshot);
                ChkBeta.IsChecked = SettingsStore.GetBool(SettingsStore.Beta);
                ChkAlpha.IsChecked = SettingsStore.GetBool(SettingsStore.Alpha);

                string raccourci = SettingsStore.Get(SettingsStore.Raccourci);
                TxtCheminRaccourci.Text = string.IsNullOrEmpty(raccourci) ? "Aucun dossier choisi" : raccourci;
                ChkRaccourci.IsChecked = !string.IsNullOrEmpty(raccourci);
                PanneauRaccourci.Visibility = ChkRaccourci.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            }
            finally
            {
                chargement = false;
            }

            _ = CalculerTailleAsync();
        }

        /// <summary>Indique (une seule fois) si les types de versions ont changé depuis le dernier appel.</summary>
        public bool ConsommerChangementTypesVersions()
        {
            bool modifie = typesVersionsModifies;
            typesVersionsModifies = false;
            return modifie;
        }

        // ───────────────────────── Profils ─────────────────────────

        private void ChargerProfils() => ListeProfils.ItemsSource = ProfileStore.Lire();

        private void BtnAjouterProfil_Click(object sender, RoutedEventArgs e) => AjouterProfil();

        private void TxtNouveauProfil_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                AjouterProfil();
        }

        private void AjouterProfil()
        {
            string pseudo = TxtNouveauProfil.Text.Trim().Replace(",", "");
            if (pseudo.Length == 0) return;

            ProfileStore.Ajouter(pseudo);
            TxtNouveauProfil.Text = "";
            ChargerProfils();
        }

        private async void BtnSupprimerProfil_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not string pseudo) return;

            if (await Dialogs.ConfirmerAsync("Supprimer le profil", $"Supprimer le profil « {pseudo} » ?", "Supprimer", danger: true))
            {
                ProfileStore.Supprimer(pseudo);
                ChargerProfils();
            }
        }

        // ───────────────────────── Microsoft ─────────────────────────

        private void MajMicrosoft()
        {
            var session = AccountService.Session;
            TxtEtatMicrosoft.Text = session != null
                ? $"Connecté en tant que {session.Username}."
                : "Non connecté : les parties se lancent avec un profil hors-ligne.";

            BtnConnexion.Visibility = session == null ? Visibility.Visible : Visibility.Collapsed;
            BtnDeconnexion.Visibility = session != null ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void BtnConnexion_Click(object sender, RoutedEventArgs e)
        {
            BtnConnexion.IsEnabled = false;
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
                BtnConnexion.IsEnabled = true;
            }
        }

        private void BtnDeconnexion_Click(object sender, RoutedEventArgs e) => AccountService.Deconnecter();

        private async void BtnDeconnexionTotale_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await AccountService.DeconnexionTotaleAsync();
                await Dialogs.InfoAsync("Compte oublié", "Le compte Microsoft a été retiré de ce PC.");
            }
            catch (Exception ex)
            {
                await Dialogs.ErreurAsync("Erreur de déconnexion", ex.Message);
            }
        }

        // ───────────────────────── RAM ─────────────────────────

        private void SliderRam_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtRam == null) return; // pendant InitializeComponent
            AfficherRam();
            if (!chargement)
                SettingsStore.Set(SettingsStore.Ram, ((int)SliderRam.Value).ToString());
        }

        private void AfficherRam() => TxtRam.Text = $"{SliderRam.Value / 1024:0} GO";

        // ───────────────────────── Versions ─────────────────────────

        private void ChkTypes_Click(object sender, RoutedEventArgs e)
        {
            SettingsStore.SetBool(SettingsStore.Snapshot, ChkSnapshot.IsChecked == true);
            SettingsStore.SetBool(SettingsStore.Beta, ChkBeta.IsChecked == true);
            SettingsStore.SetBool(SettingsStore.Alpha, ChkAlpha.IsChecked == true);
            typesVersionsModifies = true;
        }

        // ───────────────────────── Raccourci ─────────────────────────

        private async void ChkRaccourci_Changed(object sender, RoutedEventArgs e)
        {
            if (PanneauRaccourci == null || chargement) return;

            bool actif = ChkRaccourci.IsChecked == true;
            PanneauRaccourci.Visibility = actif ? Visibility.Visible : Visibility.Collapsed;

            if (actif)
            {
                // Pas encore de dossier : on le demande directement
                if (string.IsNullOrEmpty(SettingsStore.Get(SettingsStore.Raccourci)))
                    await ChoisirDossierRaccourciAsync();

                // Choix annulé : on remet l'interrupteur sur off
                if (string.IsNullOrEmpty(SettingsStore.Get(SettingsStore.Raccourci)))
                {
                    chargement = true;
                    ChkRaccourci.IsChecked = false;
                    chargement = false;
                    PanneauRaccourci.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                ShortcutService.Supprimer(SettingsStore.Get(SettingsStore.Raccourci));
                SettingsStore.Set(SettingsStore.Raccourci, "");
                TxtCheminRaccourci.Text = "Aucun dossier choisi";
            }
        }

        private async void BtnChoisirRaccourci_Click(object sender, RoutedEventArgs e) => await ChoisirDossierRaccourciAsync();

        private async Task ChoisirDossierRaccourciAsync()
        {
            var dialogue = new OpenFolderDialog { Title = "Dossier du raccourci Misty" };
            if (dialogue.ShowDialog() != true) return;

            ShortcutService.Supprimer(SettingsStore.Get(SettingsStore.Raccourci)); // ancien emplacement

            SettingsStore.Set(SettingsStore.Raccourci, dialogue.FolderName);
            TxtCheminRaccourci.Text = dialogue.FolderName;
            await CreerRaccourciAsync(dialogue.FolderName);
        }

        private async void BtnRecreerRaccourci_Click(object sender, RoutedEventArgs e)
        {
            string dossier = SettingsStore.Get(SettingsStore.Raccourci);
            if (string.IsNullOrEmpty(dossier))
                await ChoisirDossierRaccourciAsync();
            else
                await CreerRaccourciAsync(dossier);
        }

        private static async Task CreerRaccourciAsync(string dossier)
        {
            try
            {
                ShortcutService.Creer(dossier);
            }
            catch (Exception ex)
            {
                await Dialogs.ErreurAsync("Raccourci", "Erreur lors de la création du raccourci : " + ex.Message);
            }
        }

        // ───────────────────────── Stockage ─────────────────────────

        private async Task CalculerTailleAsync()
        {
            TxtTaille.Text = "CALCUL…";
            long octets = await Task.Run(() =>
            {
                try { return SystemInfo.TailleDossier(AppPaths.DataDir); }
                catch { return 0L; }
            });
            double mo = octets / (1024.0 * 1024.0);
            TxtTaille.Text = mo >= 1024 ? $"{mo / 1024:0.00} GO" : $"{mo:0} MO";
        }

        private void BtnOuvrirDossier_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            Process.Start("explorer.exe", AppPaths.DataDir);
        }

        private async void BtnToutSupprimer_Click(object sender, RoutedEventArgs e)
        {
            if (!await Dialogs.ConfirmerAsync("Tout supprimer",
                    "Supprimer le dossier Misty et toutes ses données (versions, mods, mondes, profils) ?",
                    "Tout supprimer", danger: true))
                return;

            try
            {
                if (Directory.Exists(AppPaths.DataDir))
                    Directory.Delete(AppPaths.DataDir, true);
            }
            catch (Exception ex)
            {
                await Dialogs.ErreurAsync("Suppression incomplète", ex.Message);
            }
            await CalculerTailleAsync();

            if (await Dialogs.ConfirmerAsync("Fermer Misty ?",
                    "Fermer le launcher maintenant pour ne pas recréer de données ?", "Fermer", "Rester"))
                Application.Current.Shutdown();
        }

        private void BtnGitHub_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo($"https://github.com/{AppInfo.GitHubRepo}") { UseShellExecute = true });
        }
    }
}
