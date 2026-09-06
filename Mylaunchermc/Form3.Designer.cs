namespace Misty
{
    partial class Form3
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form3));
            button1 = new Button();
            comboBox_ram = new ComboBox();
            label2 = new Label();
            comboBox_compte = new ComboBox();
            textBox_compte = new TextBox();
            button_supprcompte = new Button();
            label1 = new Label();
            label3 = new Label();
            button_scan_ram = new Button();
            label4 = new Label();
            label7 = new Label();
            button_find_raccourci = new Button();
            checkBox1 = new CheckBox();
            button2 = new Button();
            SuspendLayout();
            // 
            // button1
            // 
            button1.Location = new Point(18, 31);
            button1.Margin = new Padding(3, 2, 3, 2);
            button1.Name = "button1";
            button1.Size = new Size(89, 22);
            button1.TabIndex = 0;
            button1.Text = "Créer";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // comboBox_ram
            // 
            comboBox_ram.Enabled = false;
            comboBox_ram.FormattingEnabled = true;
            comboBox_ram.Location = new Point(78, 95);
            comboBox_ram.Margin = new Padding(3, 2, 3, 2);
            comboBox_ram.Name = "comboBox_ram";
            comboBox_ram.Size = new Size(133, 23);
            comboBox_ram.TabIndex = 1;
            comboBox_ram.SelectedIndexChanged += comboBox1_SelectedIndexChanged;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(16, 97);
            label2.Name = "label2";
            label2.Size = new Size(37, 15);
            label2.TabIndex = 3;
            label2.Text = "Ram :";
            // 
            // comboBox_compte
            // 
            comboBox_compte.FormattingEnabled = true;
            comboBox_compte.Location = new Point(222, 32);
            comboBox_compte.Margin = new Padding(3, 2, 3, 2);
            comboBox_compte.Name = "comboBox_compte";
            comboBox_compte.Size = new Size(182, 23);
            comboBox_compte.TabIndex = 4;
            comboBox_compte.Visible = false;
            // 
            // textBox_compte
            // 
            textBox_compte.Location = new Point(222, 32);
            textBox_compte.Margin = new Padding(3, 2, 3, 2);
            textBox_compte.Name = "textBox_compte";
            textBox_compte.Size = new Size(182, 23);
            textBox_compte.TabIndex = 5;
            textBox_compte.Visible = false;
            // 
            // button_supprcompte
            // 
            button_supprcompte.Location = new Point(113, 31);
            button_supprcompte.Margin = new Padding(3, 2, 3, 2);
            button_supprcompte.Name = "button_supprcompte";
            button_supprcompte.Size = new Size(89, 22);
            button_supprcompte.TabIndex = 6;
            button_supprcompte.Text = "Supprimer";
            button_supprcompte.UseVisualStyleBackColor = true;
            button_supprcompte.Click += button2_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(18, 14);
            label1.Name = "label1";
            label1.Size = new Size(56, 15);
            label1.TabIndex = 7;
            label1.Text = "Compte :";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(12, 63);
            label3.Name = "label3";
            label3.Size = new Size(382, 15);
            label3.TabIndex = 8;
            label3.Text = "___________________________________________________________________________";
            // 
            // button_scan_ram
            // 
            button_scan_ram.Location = new Point(231, 95);
            button_scan_ram.Name = "button_scan_ram";
            button_scan_ram.Size = new Size(75, 23);
            button_scan_ram.TabIndex = 9;
            button_scan_ram.Text = "Scan";
            button_scan_ram.UseVisualStyleBackColor = true;
            button_scan_ram.Click += button_scan_ram_Click;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(12, 130);
            label4.Name = "label4";
            label4.Size = new Size(382, 15);
            label4.TabIndex = 10;
            label4.Text = "___________________________________________________________________________";
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(118, 163);
            label7.Name = "label7";
            label7.Size = new Size(0, 15);
            label7.TabIndex = 13;
            label7.Visible = false;
            // 
            // button_find_raccourci
            // 
            button_find_raccourci.Location = new Point(12, 187);
            button_find_raccourci.Name = "button_find_raccourci";
            button_find_raccourci.Size = new Size(75, 23);
            button_find_raccourci.TabIndex = 14;
            button_find_raccourci.Text = "Chercher";
            button_find_raccourci.UseVisualStyleBackColor = true;
            button_find_raccourci.Visible = false;
            button_find_raccourci.Click += button_find_raccourci_Click;
            // 
            // checkBox1
            // 
            checkBox1.AutoSize = true;
            checkBox1.Location = new Point(16, 162);
            checkBox1.Name = "checkBox1";
            checkBox1.Size = new Size(84, 19);
            checkBox1.TabIndex = 15;
            checkBox1.Text = "Raccourci :";
            checkBox1.UseVisualStyleBackColor = true;
            checkBox1.CheckedChanged += checkBox1_CheckedChanged;
            // 
            // button2
            // 
            button2.Location = new Point(93, 187);
            button2.Name = "button2";
            button2.Size = new Size(75, 23);
            button2.TabIndex = 16;
            button2.Text = "Recharger";
            button2.UseVisualStyleBackColor = true;
            button2.Visible = false;
            button2.Click += button2_Click_1;
            // 
            // Form3
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(424, 220);
            Controls.Add(button2);
            Controls.Add(checkBox1);
            Controls.Add(button_find_raccourci);
            Controls.Add(label7);
            Controls.Add(label4);
            Controls.Add(button_scan_ram);
            Controls.Add(label3);
            Controls.Add(label1);
            Controls.Add(button_supprcompte);
            Controls.Add(textBox_compte);
            Controls.Add(comboBox_compte);
            Controls.Add(label2);
            Controls.Add(comboBox_ram);
            Controls.Add(button1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "Form3";
            Text = "Options";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button button1;
        private ComboBox comboBox_ram;
        private Label label2;
        private ComboBox comboBox_compte;
        private TextBox textBox_compte;
        private Button button_supprcompte;
        private Label label1;
        private Label label3;
        private Button button_scan_ram;
        private Label label4;
        private Label label7;
        private Button button_find_raccourci;
        private CheckBox checkBox1;
        private Button button2;
    }
}