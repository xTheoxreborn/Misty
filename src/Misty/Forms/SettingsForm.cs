using Misty.Services;

namespace Misty.Forms
{
    /// <summary>
    /// Fenêtre d'options : profils hors-ligne, RAM, raccourci, types de versions, dossier de données.
    /// </summary>
    public partial class SettingsForm : Form
    {
        private readonly string ramEnregistree;

        public SettingsForm()
        {
            InitializeComponent();

            MaximumSize = Size;
            MinimumSize = Size;

            cmbRam.MouseWheel += (s, e) => ((HandledMouseEventArgs)e).Handled = true;

            ChargerPseudos();
            AfficherTailleDossier();

            ramEnregistree = SettingsStore.Get(SettingsStore.Ram);
            if (!string.IsNullOrEmpty(ramEnregistree))
            {
                cmbRam.Items.Add(ramEnregistree);
                cmbRam.SelectedItem = ramEnregistree;
            }

            string raccourci = SettingsStore.Get(SettingsStore.Raccourci);
            if (!string.IsNullOrEmpty(raccourci))
            {
                lblCheminRaccourci.Text = raccourci;
                chkRaccourci.Checked = true;
            }

            chkSnapshot.Checked = SettingsStore.GetBool(SettingsStore.Snapshot);
            chkBeta.Checked = SettingsStore.GetBool(SettingsStore.Beta);
            chkAlpha.Checked = SettingsStore.GetBool(SettingsStore.Alpha);
        }

        // ───────────────────────── Comptes hors-ligne ─────────────────────────

        private void ChargerPseudos()
        {
            cmbCompte.Items.Clear();
            foreach (var pseudo in ProfileStore.Lire())
                cmbCompte.Items.Add(pseudo);
        }

        /// <summary>1er clic : affiche le champ de saisie. 2e clic : crée le profil.</summary>
        private void btnCreerCompte_Click(object sender, EventArgs e)
        {
            if (btnSupprimerCompte.Text == "Confirmer ?")
            {
                btnSupprimerCompte.Text = "Supprimer";
                cmbCompte.Visible = false;
            }

            if (btnCreerCompte.Text == "Créer")
            {
                btnCreerCompte.Text = "Confirmer ?";
                txtCompte.Visible = true;
            }
            else
            {
                ProfileStore.Ajouter(txtCompte.Text);
                ChargerPseudos();

                btnCreerCompte.Text = "Créer";
                txtCompte.Visible = false;
                txtCompte.Text = "";
            }
        }

        /// <summary>1er clic : affiche la liste des profils. 2e clic : supprime le profil choisi.</summary>
        private void btnSupprimerCompte_Click(object sender, EventArgs e)
        {
            if (btnCreerCompte.Text == "Confirmer ?")
            {
                btnCreerCompte.Text = "Créer";
                txtCompte.Visible = false;
            }

            if (btnSupprimerCompte.Text == "Supprimer")
            {
                btnSupprimerCompte.Text = "Confirmer ?";
                cmbCompte.Visible = true;
            }
            else
            {
                ProfileStore.Supprimer(cmbCompte.Text);
                ChargerPseudos();

                btnSupprimerCompte.Text = "Supprimer";
                cmbCompte.Visible = false;
            }
        }

        // ───────────────────────── RAM ─────────────────────────

        private void btnScanRam_Click(object sender, EventArgs e)
        {
            cmbRam.Items.Clear();
            foreach (int ram in SystemInfo.OptionsRamMo())
                cmbRam.Items.Add(ram);

            cmbRam.Enabled = true;
            cmbRam.Text = ramEnregistree;
        }

        private void cmbRam_SelectedIndexChanged(object sender, EventArgs e)
        {
            SettingsStore.Set(SettingsStore.Ram, cmbRam.Text);
        }

        // ───────────────────────── Raccourci ─────────────────────────

        private void chkRaccourci_CheckedChanged(object sender, EventArgs e)
        {
            bool actif = chkRaccourci.Checked;
            btnChercherRaccourci.Visible = actif;
            btnRechargerRaccourci.Visible = actif;
            lblCheminRaccourci.Visible = actif;

            if (!actif)
            {
                ShortcutService.Supprimer(lblCheminRaccourci.Text);
                SettingsStore.Set(SettingsStore.Raccourci, "");
            }
        }

        private void btnChercherRaccourci_Click(object sender, EventArgs e)
        {
            using var dossier = new FolderBrowserDialog();
            if (dossier.ShowDialog() != DialogResult.OK) return;

            ShortcutService.Supprimer(lblCheminRaccourci.Text); // ancien emplacement

            lblCheminRaccourci.Text = dossier.SelectedPath;
            SettingsStore.Set(SettingsStore.Raccourci, dossier.SelectedPath);
            CreerRaccourci(dossier.SelectedPath);
        }

        private void btnRechargerRaccourci_Click(object sender, EventArgs e)
        {
            CreerRaccourci(lblCheminRaccourci.Text);
        }

        private static void CreerRaccourci(string dossier)
        {
            try
            {
                ShortcutService.Creer(dossier);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur lors de la création du raccourci : " + ex.Message);
            }
        }

        // ───────────────────────── Versions ─────────────────────────

        private void chkSnapshot_CheckedChanged(object sender, EventArgs e) =>
            SettingsStore.SetBool(SettingsStore.Snapshot, chkSnapshot.Checked);

        private void chkBeta_CheckedChanged(object sender, EventArgs e) =>
            SettingsStore.SetBool(SettingsStore.Beta, chkBeta.Checked);

        private void chkAlpha_CheckedChanged(object sender, EventArgs e) =>
            SettingsStore.SetBool(SettingsStore.Alpha, chkAlpha.Checked);

        // ───────────────────────── Dossier de données ─────────────────────────

        private void AfficherTailleDossier()
        {
            double tailleMo = SystemInfo.TailleDossier(AppPaths.DataDir) / (1024.0 * 1024.0);
            lblTailleDossier.Text = $"Taille : {tailleMo:F2} Mo / {tailleMo / 1024.0:F2} Go";
        }

        private void btnToutSupprimer_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Êtes-vous sûr de vouloir supprimer le profil Misty et toutes ses données ?", "Confirmation",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try
            {
                if (Directory.Exists(AppPaths.DataDir))
                    Directory.Delete(AppPaths.DataDir, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur lors de la suppression : " + ex.Message);
            }
            AfficherTailleDossier();

            if (MessageBox.Show("Voulez-vous fermer le launcher afin de ne pas recréer de données ?", "Confirmation",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                Application.Exit();
        }
    }
}
