using Microsoft.Win32;
using Misty.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Misty.Views
{
    /// <summary>
    /// Options : profils, compte Microsoft, RAM, raccourci, types de versions, stockage, langue.
    /// </summary>
    public partial class SettingsView : UserControl
    {
        // true pendant qu'on remplit les contrôles : les événements ne doivent rien enregistrer
        private bool chargement = true;
        private bool typesVersionsModifies;
        private bool chargementLangue;

        public SettingsView()
        {
            InitializeComponent();

            TxtDossier.Text = AppPaths.DataDir;

            AccountService.SessionChanged += () => Dispatcher.Invoke(MajMicrosoft);
            Loc.Changee += MajTextes;
            chargement = false;
        }

        /// <summary>Textes calculés en code (rappelé au changement de langue).</summary>
        private void MajTextes()
        {
            TxtAPropos.Text = Loc.T("Options_APropos_Texte", AppInfo.Version);
            TxtRamTotale.Text = Loc.T("Options_Ram_Texte", (SystemInfo.RamTotaleMo() / 1024.0).ToString("0.#"));
            string raccourci = SettingsStore.Get(SettingsStore.Raccourci);
            TxtCheminRaccourci.Text = string.IsNullOrEmpty(raccourci) ? Loc.T("Options_AucunDossier") : raccourci;
            MajMicrosoft();
            AfficherRam();
            _ = CalculerTailleAsync();
        }

        private void InitLangues()
        {
            chargementLangue = true;
            CmbLangue.ItemsSource = Loc.Disponibles();
            CmbLangue.SelectedValue = Loc.Instance.Code;
            chargementLangue = false;
        }

        private void CmbLangue_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (chargementLangue || CmbLangue.SelectedValue is not string code || code == Loc.Instance.Code) return;

            SettingsStore.Set(SettingsStore.Langue, code);
            Loc.Instance.Charger(code);
        }

        /// <summary>Recharge l'affichage depuis les fichiers de config (appelé à chaque ouverture de la page).</summary>
        public void Rafraichir()
        {
            InitLangues();

            chargement = true;
            try
            {
                ChargerProfils();

                int ramMax = SystemInfo.RamMaxProposeeMo();
                SliderRam.Maximum = ramMax;
                if (!int.TryParse(SettingsStore.Get(SettingsStore.Ram), out int ram))
                    ram = AppInfo.RamParDefautMo;
                SliderRam.Value = Math.Clamp(ram, 1024, ramMax);

                ChkSnapshot.IsChecked = SettingsStore.GetBool(SettingsStore.Snapshot);
                ChkBeta.IsChecked = SettingsStore.GetBool(SettingsStore.Beta);
                ChkAlpha.IsChecked = SettingsStore.GetBool(SettingsStore.Alpha);

                ChkRaccourci.IsChecked = !string.IsNullOrEmpty(SettingsStore.Get(SettingsStore.Raccourci));
                PanneauRaccourci.Visibility = ChkRaccourci.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                Discordrichpresence1.IsChecked = DiscordPresenceService.Active;
            }
            finally
            {
                chargement = false;
            }

            MajTextes();
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

            if (await Dialogs.ConfirmerAsync(Loc.T("Options_SupprimerCompte"), Loc.T("Options_SupprimerCompte_Texte", pseudo),
                    Loc.T("Commun_Supprimer"), danger: true))
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
                ? Loc.T("Compte_Connecte", session.Username ?? "")
                : Loc.T("Compte_NonConnecte");

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
                await Dialogs.ErreurAsync(Loc.T("Compte_ConnexionImpossible"), ex.Message);
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
                await Dialogs.InfoAsync(Loc.T("Compte_Oublie"), Loc.T("Compte_Oublie_Texte"));
            }
            catch (Exception ex)
            {
                await Dialogs.ErreurAsync(Loc.T("Compte_ErreurDeconnexion"), ex.Message);
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

        private void AfficherRam() => TxtRam.Text = $"{SliderRam.Value / 1024:0} {Loc.T("Unite_Go")}";

        // ───────────────────────── Versions ─────────────────────────

        private void ChkTypes_Click(object sender, RoutedEventArgs e)
        {
            SettingsStore.SetBool(SettingsStore.Snapshot, ChkSnapshot.IsChecked == true);
            SettingsStore.SetBool(SettingsStore.Beta, ChkBeta.IsChecked == true);
            SettingsStore.SetBool(SettingsStore.Alpha, ChkAlpha.IsChecked == true);
            typesVersionsModifies = true;
        }

        // ───────────────────────── Rich Presence Discord ─────────────────────────

        private void Chkrich(object sender, RoutedEventArgs e)
        {
            if (chargement) return;
            SettingsStore.SetBool(SettingsStore.RichPresence, Discordrichpresence1.IsChecked == true);
            DiscordPresenceService.Instance.Reappliquer();
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
                TxtCheminRaccourci.Text = Loc.T("Options_AucunDossier");
            }
        }

        private async void BtnChoisirRaccourci_Click(object sender, RoutedEventArgs e) => await ChoisirDossierRaccourciAsync();

        private async Task ChoisirDossierRaccourciAsync()
        {
            var dialogue = new OpenFolderDialog { Title = Loc.T("Options_DossierRaccourci") };
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
                await Dialogs.ErreurAsync(Loc.T("Options_Raccourci"), Loc.T("Options_RaccourciErreur", ex.Message));
            }
        }

        // ───────────────────────── Stockage ─────────────────────────

        private async Task CalculerTailleAsync()
        {
            TxtTaille.Text = Loc.T("Options_Calcul");
            long octets = await Task.Run(() =>
            {
                try { return SystemInfo.TailleDossier(AppPaths.DataDir); }
                catch { return 0L; }
            });
            double mo = octets / (1024.0 * 1024.0);
            TxtTaille.Text = mo >= 1024 ? $"{mo / 1024:0.00} {Loc.T("Unite_Go")}" : $"{mo:0} {Loc.T("Unite_Mo")}";
        }

        private void BtnOuvrirDossier_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            Process.Start("explorer.exe", AppPaths.DataDir);
        }

        private async void BtnToutSupprimer_Click(object sender, RoutedEventArgs e)
        {
            if (!await Dialogs.ConfirmerAsync(Loc.T("Options_ToutSupprimer"), Loc.T("Options_ToutSupprimer_Texte"),
                    Loc.T("Options_ToutSupprimer"), danger: true))
                return;

            try
            {
                if (Directory.Exists(AppPaths.DataDir))
                    Directory.Delete(AppPaths.DataDir, true);
            }
            catch (Exception ex)
            {
                await Dialogs.ErreurAsync(Loc.T("Options_SuppressionIncomplete"), ex.Message);
            }
            await CalculerTailleAsync();

            if (await Dialogs.ConfirmerAsync(Loc.T("Options_FermerMisty"), Loc.T("Options_FermerMisty_Texte"),
                    Loc.T("Commun_Fermer"), Loc.T("Options_Rester")))
                Application.Current.Shutdown();
        }

        private void BtnGitHub_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo($"https://github.com/{AppInfo.GitHubRepo}") { UseShellExecute = true });
        }
    }
}
