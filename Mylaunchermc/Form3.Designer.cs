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
            SuspendLayout();
            // 
            // button1
            // 
            button1.Location = new Point(21, 41);
            button1.Name = "button1";
            button1.Size = new Size(102, 29);
            button1.TabIndex = 0;
            button1.Text = "Créer";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // comboBox_ram
            // 
            comboBox_ram.Enabled = false;
            comboBox_ram.FormattingEnabled = true;
            comboBox_ram.Items.AddRange(new object[] { "5000" });
            comboBox_ram.Location = new Point(91, 149);
            comboBox_ram.Name = "comboBox_ram";
            comboBox_ram.Size = new Size(151, 28);
            comboBox_ram.TabIndex = 1;
            comboBox_ram.SelectedIndexChanged += comboBox1_SelectedIndexChanged;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(21, 152);
            label2.Name = "label2";
            label2.Size = new Size(46, 20);
            label2.TabIndex = 3;
            label2.Text = "Ram :";
            // 
            // comboBox_compte
            // 
            comboBox_compte.FormattingEnabled = true;
            comboBox_compte.Location = new Point(254, 42);
            comboBox_compte.Name = "comboBox_compte";
            comboBox_compte.Size = new Size(207, 28);
            comboBox_compte.TabIndex = 4;
            comboBox_compte.Visible = false;
            // 
            // textBox_compte
            // 
            textBox_compte.Location = new Point(254, 43);
            textBox_compte.Name = "textBox_compte";
            textBox_compte.Size = new Size(207, 27);
            textBox_compte.TabIndex = 5;
            textBox_compte.Visible = false;
            // 
            // button_supprcompte
            // 
            button_supprcompte.Location = new Point(129, 41);
            button_supprcompte.Name = "button_supprcompte";
            button_supprcompte.Size = new Size(102, 29);
            button_supprcompte.TabIndex = 6;
            button_supprcompte.Text = "Supprimer";
            button_supprcompte.UseVisualStyleBackColor = true;
            button_supprcompte.Click += button2_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(21, 18);
            label1.Name = "label1";
            label1.Size = new Size(69, 20);
            label1.TabIndex = 7;
            label1.Text = "Compte :";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(12, 95);
            label3.Name = "label3";
            label3.Size = new Size(459, 20);
            label3.TabIndex = 8;
            label3.Text = "___________________________________________________________________________";
            // 
            // Form3
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(485, 215);
            Controls.Add(label3);
            Controls.Add(label1);
            Controls.Add(button_supprcompte);
            Controls.Add(textBox_compte);
            Controls.Add(comboBox_compte);
            Controls.Add(label2);
            Controls.Add(comboBox_ram);
            Controls.Add(button1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(3, 4, 3, 4);
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
    }
}