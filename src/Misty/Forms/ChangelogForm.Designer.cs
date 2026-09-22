namespace Misty.Forms
{
    partial class ChangelogForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ChangelogForm));
            lblAnciennesVersions = new Label();
            lblNouveautes = new Label();
            SuspendLayout();
            //
            // lblAnciennesVersions
            //
            lblAnciennesVersions.AutoSize = true;
            lblAnciennesVersions.Location = new Point(7, 5);
            lblAnciennesVersions.Name = "lblAnciennesVersions";
            lblAnciennesVersions.Size = new Size(38, 15);
            lblAnciennesVersions.TabIndex = 2;
            //
            // lblNouveautes
            //
            lblNouveautes.AutoSize = true;
            lblNouveautes.Location = new Point(7, 5);
            lblNouveautes.Name = "lblNouveautes";
            lblNouveautes.Size = new Size(38, 15);
            lblNouveautes.TabIndex = 3;
            //
            // ChangelogForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(864, 831);
            Controls.Add(lblNouveautes);
            Controls.Add(lblAnciennesVersions);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "ChangelogForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Notes de versions";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label lblAnciennesVersions;
        private Label lblNouveautes;
    }
}
