namespace Misty.Forms
{
    partial class SettingsForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SettingsForm));
            btnCreerCompte = new Button();
            cmbRam = new ComboBox();
            lblRam = new Label();
            cmbCompte = new ComboBox();
            txtCompte = new TextBox();
            btnSupprimerCompte = new Button();
            lblCompte = new Label();
            separateur1 = new Label();
            btnScanRam = new Button();
            separateur2 = new Label();
            lblCheminRaccourci = new Label();
            btnChercherRaccourci = new Button();
            chkRaccourci = new CheckBox();
            btnRechargerRaccourci = new Button();
            separateur3 = new Label();
            lblGestionVersions = new Label();
            chkSnapshot = new CheckBox();
            chkBeta = new CheckBox();
            chkAlpha = new CheckBox();
            separateur4 = new Label();
            btnToutSupprimer = new Button();
            lblDossierApp = new Label();
            lblTailleDossier = new Label();
            SuspendLayout();
            //
            // btnCreerCompte
            //
            btnCreerCompte.Location = new Point(18, 31);
            btnCreerCompte.Margin = new Padding(3, 2, 3, 2);
            btnCreerCompte.Name = "btnCreerCompte";
            btnCreerCompte.Size = new Size(89, 22);
            btnCreerCompte.TabIndex = 0;
            btnCreerCompte.Text = "Créer";
            btnCreerCompte.UseVisualStyleBackColor = true;
            btnCreerCompte.Click += btnCreerCompte_Click;
            //
            // cmbRam
            //
            cmbRam.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbRam.Enabled = false;
            cmbRam.FormattingEnabled = true;
            cmbRam.Location = new Point(78, 95);
            cmbRam.Margin = new Padding(3, 2, 3, 2);
            cmbRam.Name = "cmbRam";
            cmbRam.Size = new Size(133, 23);
            cmbRam.TabIndex = 1;
            cmbRam.SelectedIndexChanged += cmbRam_SelectedIndexChanged;
            //
            // lblRam
            //
            lblRam.AutoSize = true;
            lblRam.Location = new Point(16, 97);
            lblRam.Name = "lblRam";
            lblRam.Size = new Size(37, 15);
            lblRam.TabIndex = 3;
            lblRam.Text = "Ram :";
            //
            // cmbCompte
            //
            cmbCompte.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbCompte.FormattingEnabled = true;
            cmbCompte.Location = new Point(222, 32);
            cmbCompte.Margin = new Padding(3, 2, 3, 2);
            cmbCompte.Name = "cmbCompte";
            cmbCompte.Size = new Size(182, 23);
            cmbCompte.TabIndex = 4;
            cmbCompte.Visible = false;
            //
            // txtCompte
            //
            txtCompte.Location = new Point(222, 32);
            txtCompte.Margin = new Padding(3, 2, 3, 2);
            txtCompte.Name = "txtCompte";
            txtCompte.Size = new Size(182, 23);
            txtCompte.TabIndex = 5;
            txtCompte.Visible = false;
            //
            // btnSupprimerCompte
            //
            btnSupprimerCompte.Location = new Point(113, 31);
            btnSupprimerCompte.Margin = new Padding(3, 2, 3, 2);
            btnSupprimerCompte.Name = "btnSupprimerCompte";
            btnSupprimerCompte.Size = new Size(89, 22);
            btnSupprimerCompte.TabIndex = 6;
            btnSupprimerCompte.Text = "Supprimer";
            btnSupprimerCompte.UseVisualStyleBackColor = true;
            btnSupprimerCompte.Click += btnSupprimerCompte_Click;
            //
            // lblCompte
            //
            lblCompte.AutoSize = true;
            lblCompte.Location = new Point(18, 14);
            lblCompte.Name = "lblCompte";
            lblCompte.Size = new Size(56, 15);
            lblCompte.TabIndex = 7;
            lblCompte.Text = "Compte :";
            //
            // separateur1
            //
            separateur1.AutoSize = true;
            separateur1.Location = new Point(12, 63);
            separateur1.Name = "separateur1";
            separateur1.Size = new Size(382, 15);
            separateur1.TabIndex = 8;
            separateur1.Text = "___________________________________________________________________________";
            //
            // btnScanRam
            //
            btnScanRam.Location = new Point(231, 95);
            btnScanRam.Name = "btnScanRam";
            btnScanRam.Size = new Size(75, 23);
            btnScanRam.TabIndex = 9;
            btnScanRam.Text = "Scan";
            btnScanRam.UseVisualStyleBackColor = true;
            btnScanRam.Click += btnScanRam_Click;
            //
            // separateur2
            //
            separateur2.AutoSize = true;
            separateur2.Location = new Point(12, 130);
            separateur2.Name = "separateur2";
            separateur2.Size = new Size(382, 15);
            separateur2.TabIndex = 10;
            separateur2.Text = "___________________________________________________________________________";
            //
            // lblCheminRaccourci
            //
            lblCheminRaccourci.AutoSize = true;
            lblCheminRaccourci.Location = new Point(118, 163);
            lblCheminRaccourci.Name = "lblCheminRaccourci";
            lblCheminRaccourci.Size = new Size(0, 15);
            lblCheminRaccourci.TabIndex = 13;
            lblCheminRaccourci.Visible = false;
            //
            // btnChercherRaccourci
            //
            btnChercherRaccourci.Location = new Point(12, 187);
            btnChercherRaccourci.Name = "btnChercherRaccourci";
            btnChercherRaccourci.Size = new Size(75, 23);
            btnChercherRaccourci.TabIndex = 14;
            btnChercherRaccourci.Text = "Chercher";
            btnChercherRaccourci.UseVisualStyleBackColor = true;
            btnChercherRaccourci.Visible = false;
            btnChercherRaccourci.Click += btnChercherRaccourci_Click;
            //
            // chkRaccourci
            //
            chkRaccourci.AutoSize = true;
            chkRaccourci.Location = new Point(16, 162);
            chkRaccourci.Name = "chkRaccourci";
            chkRaccourci.Size = new Size(84, 19);
            chkRaccourci.TabIndex = 15;
            chkRaccourci.Text = "Raccourci :";
            chkRaccourci.UseVisualStyleBackColor = true;
            chkRaccourci.CheckedChanged += chkRaccourci_CheckedChanged;
            //
            // btnRechargerRaccourci
            //
            btnRechargerRaccourci.Location = new Point(93, 187);
            btnRechargerRaccourci.Name = "btnRechargerRaccourci";
            btnRechargerRaccourci.Size = new Size(75, 23);
            btnRechargerRaccourci.TabIndex = 16;
            btnRechargerRaccourci.Text = "Recharger";
            btnRechargerRaccourci.UseVisualStyleBackColor = true;
            btnRechargerRaccourci.Visible = false;
            btnRechargerRaccourci.Click += btnRechargerRaccourci_Click;
            //
            // separateur3
            //
            separateur3.AutoSize = true;
            separateur3.Location = new Point(12, 213);
            separateur3.Name = "separateur3";
            separateur3.Size = new Size(382, 15);
            separateur3.TabIndex = 17;
            separateur3.Text = "___________________________________________________________________________";
            //
            // lblGestionVersions
            //
            lblGestionVersions.AutoSize = true;
            lblGestionVersions.Location = new Point(12, 238);
            lblGestionVersions.Name = "lblGestionVersions";
            lblGestionVersions.Size = new Size(120, 15);
            lblGestionVersions.TabIndex = 18;
            lblGestionVersions.Text = "Gestion des versions :";
            //
            // chkSnapshot
            //
            chkSnapshot.AutoSize = true;
            chkSnapshot.Location = new Point(17, 258);
            chkSnapshot.Name = "chkSnapshot";
            chkSnapshot.Size = new Size(75, 19);
            chkSnapshot.TabIndex = 19;
            chkSnapshot.Text = "Snapshot";
            chkSnapshot.UseVisualStyleBackColor = true;
            chkSnapshot.CheckedChanged += chkSnapshot_CheckedChanged;
            //
            // chkBeta
            //
            chkBeta.AutoSize = true;
            chkBeta.Location = new Point(118, 258);
            chkBeta.Name = "chkBeta";
            chkBeta.Size = new Size(49, 19);
            chkBeta.TabIndex = 20;
            chkBeta.Text = "Beta";
            chkBeta.UseVisualStyleBackColor = true;
            chkBeta.CheckedChanged += chkBeta_CheckedChanged;
            //
            // chkAlpha
            //
            chkAlpha.AutoSize = true;
            chkAlpha.Location = new Point(189, 258);
            chkAlpha.Name = "chkAlpha";
            chkAlpha.Size = new Size(98, 19);
            chkAlpha.TabIndex = 21;
            chkAlpha.Text = "alpha + Indev";
            chkAlpha.UseVisualStyleBackColor = true;
            chkAlpha.CheckedChanged += chkAlpha_CheckedChanged;
            //
            // separateur4
            //
            separateur4.AutoSize = true;
            separateur4.Location = new Point(12, 280);
            separateur4.Name = "separateur4";
            separateur4.Size = new Size(382, 15);
            separateur4.TabIndex = 22;
            separateur4.Text = "___________________________________________________________________________";
            //
            // btnToutSupprimer
            //
            btnToutSupprimer.Location = new Point(153, 311);
            btnToutSupprimer.Name = "btnToutSupprimer";
            btnToutSupprimer.Size = new Size(96, 23);
            btnToutSupprimer.TabIndex = 23;
            btnToutSupprimer.Text = "Tout supprimer";
            btnToutSupprimer.UseVisualStyleBackColor = true;
            btnToutSupprimer.Click += btnToutSupprimer_Click;
            //
            // lblDossierApp
            //
            lblDossierApp.AutoSize = true;
            lblDossierApp.Location = new Point(12, 315);
            lblDossierApp.Name = "lblDossierApp";
            lblDossierApp.Size = new Size(135, 15);
            lblDossierApp.TabIndex = 24;
            lblDossierApp.Text = "Dossier de l'application :";
            //
            // lblTailleDossier
            //
            lblTailleDossier.AutoSize = true;
            lblTailleDossier.Location = new Point(12, 341);
            lblTailleDossier.Name = "lblTailleDossier";
            lblTailleDossier.Size = new Size(38, 15);
            lblTailleDossier.TabIndex = 25;
            lblTailleDossier.Text = "Taille :";
            //
            // SettingsForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(424, 384);
            Controls.Add(lblTailleDossier);
            Controls.Add(lblDossierApp);
            Controls.Add(btnToutSupprimer);
            Controls.Add(separateur4);
            Controls.Add(chkAlpha);
            Controls.Add(chkBeta);
            Controls.Add(chkSnapshot);
            Controls.Add(lblGestionVersions);
            Controls.Add(separateur3);
            Controls.Add(btnRechargerRaccourci);
            Controls.Add(chkRaccourci);
            Controls.Add(btnChercherRaccourci);
            Controls.Add(lblCheminRaccourci);
            Controls.Add(separateur2);
            Controls.Add(btnScanRam);
            Controls.Add(separateur1);
            Controls.Add(lblCompte);
            Controls.Add(btnSupprimerCompte);
            Controls.Add(txtCompte);
            Controls.Add(cmbCompte);
            Controls.Add(lblRam);
            Controls.Add(cmbRam);
            Controls.Add(btnCreerCompte);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "SettingsForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Options";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnCreerCompte;
        private ComboBox cmbRam;
        private Label lblRam;
        private ComboBox cmbCompte;
        private TextBox txtCompte;
        private Button btnSupprimerCompte;
        private Label lblCompte;
        private Label separateur1;
        private Button btnScanRam;
        private Label separateur2;
        private Label lblCheminRaccourci;
        private Button btnChercherRaccourci;
        private CheckBox chkRaccourci;
        private Button btnRechargerRaccourci;
        private Label separateur3;
        private Label lblGestionVersions;
        private CheckBox chkSnapshot;
        private CheckBox chkBeta;
        private CheckBox chkAlpha;
        private Label separateur4;
        private Button btnToutSupprimer;
        private Label lblDossierApp;
        private Label lblTailleDossier;
    }
}
