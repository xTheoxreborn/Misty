namespace Misty.Forms
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            btnLancer = new Button();
            lblPseudo = new Label();
            lblVersionApp = new Label();
            cmbVersion = new ComboBox();
            btnConnexion = new Button();
            btnDeconnexion = new Button();
            btnDeconnexionTotale = new Button();
            cmbMode = new ComboBox();
            progressBar = new ProgressBar();
            lblPourcentage = new Label();
            btnOptions = new Button();
            cmbCompte = new ComboBox();
            picDossier = new PictureBox();
            panelMods = new FlowLayoutPanel();
            picRecharger = new PictureBox();
            ((System.ComponentModel.ISupportInitialize)picDossier).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picRecharger).BeginInit();
            SuspendLayout();
            //
            // btnLancer
            //
            btnLancer.Enabled = false;
            btnLancer.Location = new Point(506, 571);
            btnLancer.Name = "btnLancer";
            btnLancer.Size = new Size(323, 23);
            btnLancer.TabIndex = 3;
            btnLancer.Text = "Lancer";
            btnLancer.UseVisualStyleBackColor = true;
            btnLancer.Click += btnLancer_Click;
            //
            // lblPseudo
            //
            lblPseudo.AutoSize = true;
            lblPseudo.Location = new Point(15, 516);
            lblPseudo.Name = "lblPseudo";
            lblPseudo.Size = new Size(52, 15);
            lblPseudo.TabIndex = 5;
            lblPseudo.Text = "Pseudo :";
            //
            // lblVersionApp
            //
            lblVersionApp.AutoSize = true;
            lblVersionApp.Cursor = Cursors.Hand;
            lblVersionApp.Location = new Point(1319, 591);
            lblVersionApp.Name = "lblVersionApp";
            lblVersionApp.Size = new Size(38, 15);
            lblVersionApp.TabIndex = 6;
            lblVersionApp.Text = "version";
            lblVersionApp.Click += lblVersionApp_Click;
            //
            // cmbVersion
            //
            cmbVersion.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbVersion.FormattingEnabled = true;
            cmbVersion.Location = new Point(506, 542);
            cmbVersion.Name = "cmbVersion";
            cmbVersion.Size = new Size(166, 23);
            cmbVersion.TabIndex = 7;
            cmbVersion.SelectedIndexChanged += cmbVersion_SelectedIndexChanged;
            //
            // btnConnexion
            //
            btnConnexion.Location = new Point(12, 543);
            btnConnexion.Name = "btnConnexion";
            btnConnexion.Size = new Size(121, 23);
            btnConnexion.TabIndex = 9;
            btnConnexion.Text = "Se connecter";
            btnConnexion.UseVisualStyleBackColor = true;
            btnConnexion.Click += btnConnexion_Click;
            //
            // btnDeconnexion
            //
            btnDeconnexion.Enabled = false;
            btnDeconnexion.Location = new Point(12, 571);
            btnDeconnexion.Name = "btnDeconnexion";
            btnDeconnexion.Size = new Size(121, 23);
            btnDeconnexion.TabIndex = 10;
            btnDeconnexion.Text = "Se déconnecter";
            btnDeconnexion.UseVisualStyleBackColor = true;
            btnDeconnexion.Click += btnDeconnexion_Click;
            //
            // btnDeconnexionTotale
            //
            btnDeconnexionTotale.Location = new Point(130, 571);
            btnDeconnexionTotale.Name = "btnDeconnexionTotale";
            btnDeconnexionTotale.Size = new Size(130, 23);
            btnDeconnexionTotale.TabIndex = 11;
            btnDeconnexionTotale.Text = "Déconnexion totale";
            btnDeconnexionTotale.UseVisualStyleBackColor = true;
            btnDeconnexionTotale.Click += btnDeconnexionTotale_Click;
            //
            // cmbMode
            //
            cmbMode.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbMode.Enabled = false;
            cmbMode.FormattingEnabled = true;
            cmbMode.Location = new Point(678, 543);
            cmbMode.Name = "cmbMode";
            cmbMode.Size = new Size(151, 23);
            cmbMode.TabIndex = 13;
            //
            // progressBar
            //
            progressBar.Location = new Point(506, 305);
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(323, 23);
            progressBar.TabIndex = 16;
            //
            // lblPourcentage
            //
            lblPourcentage.AutoSize = true;
            lblPourcentage.Location = new Point(658, 275);
            lblPourcentage.Name = "lblPourcentage";
            lblPourcentage.Size = new Size(23, 15);
            lblPourcentage.TabIndex = 17;
            lblPourcentage.Text = "0%";
            //
            // btnOptions
            //
            btnOptions.Location = new Point(130, 543);
            btnOptions.Name = "btnOptions";
            btnOptions.Size = new Size(130, 23);
            btnOptions.TabIndex = 18;
            btnOptions.Text = "Options";
            btnOptions.UseVisualStyleBackColor = true;
            btnOptions.Click += btnOptions_Click;
            //
            // cmbCompte
            //
            cmbCompte.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbCompte.FormattingEnabled = true;
            cmbCompte.Location = new Point(76, 514);
            cmbCompte.Margin = new Padding(3, 2, 3, 2);
            cmbCompte.Name = "cmbCompte";
            cmbCompte.Size = new Size(184, 23);
            cmbCompte.TabIndex = 19;
            cmbCompte.SelectedIndexChanged += cmbCompte_SelectedIndexChanged;
            //
            // picDossier
            //
            picDossier.Cursor = Cursors.Hand;
            picDossier.Image = (Image)resources.GetObject("picDossier.Image");
            picDossier.InitialImage = (Image)resources.GetObject("picDossier.InitialImage");
            picDossier.Location = new Point(835, 571);
            picDossier.Name = "picDossier";
            picDossier.Size = new Size(33, 23);
            picDossier.SizeMode = PictureBoxSizeMode.Zoom;
            picDossier.TabIndex = 20;
            picDossier.TabStop = false;
            picDossier.Click += picDossier_Click;
            //
            // panelMods
            //
            panelMods.AutoScroll = true;
            panelMods.Location = new Point(985, 210);
            panelMods.Name = "panelMods";
            panelMods.Size = new Size(313, 210);
            panelMods.TabIndex = 23;
            //
            // picRecharger
            //
            picRecharger.Cursor = Cursors.Hand;
            picRecharger.Image = (Image)resources.GetObject("picRecharger.Image");
            picRecharger.InitialImage = (Image)resources.GetObject("picRecharger.InitialImage");
            picRecharger.Location = new Point(835, 543);
            picRecharger.Name = "picRecharger";
            picRecharger.Size = new Size(33, 22);
            picRecharger.SizeMode = PictureBoxSizeMode.Zoom;
            picRecharger.TabIndex = 25;
            picRecharger.TabStop = false;
            picRecharger.Click += picRecharger_Click;
            //
            // MainForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.Control;
            ClientSize = new Size(1366, 613);
            Controls.Add(picRecharger);
            Controls.Add(panelMods);
            Controls.Add(picDossier);
            Controls.Add(cmbCompte);
            Controls.Add(btnOptions);
            Controls.Add(lblPourcentage);
            Controls.Add(progressBar);
            Controls.Add(cmbMode);
            Controls.Add(btnDeconnexionTotale);
            Controls.Add(btnDeconnexion);
            Controls.Add(btnConnexion);
            Controls.Add(cmbVersion);
            Controls.Add(lblVersionApp);
            Controls.Add(lblPseudo);
            Controls.Add(btnLancer);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "MainForm";
            Text = "Misty";
            ((System.ComponentModel.ISupportInitialize)picDossier).EndInit();
            ((System.ComponentModel.ISupportInitialize)picRecharger).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnLancer;
        private Label lblPseudo;
        private Label lblVersionApp;
        private ComboBox cmbVersion;
        private Button btnConnexion;
        private Button btnDeconnexion;
        private Button btnDeconnexionTotale;
        private ComboBox cmbMode;
        private ProgressBar progressBar;
        private Label lblPourcentage;
        private Button btnOptions;
        private ComboBox cmbCompte;
        private PictureBox picDossier;
        private FlowLayoutPanel panelMods;
        private PictureBox picRecharger;
    }
}
