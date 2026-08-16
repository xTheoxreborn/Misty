namespace Misty
{
    partial class Form1
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            button1 = new Button();
            textBox1 = new TextBox();
            textBox2 = new TextBox();
            button2 = new Button();
            textBox3 = new TextBox();
            label1 = new Label();
            label2 = new Label();
            comboBox1 = new ComboBox();
            button3 = new Button();
            button4 = new Button();
            button5 = new Button();
            comboBoxMode = new ComboBox();
            progressBar1 = new ProgressBar();
            label3 = new Label();
            button6 = new Button();
            comboBox_compte = new ComboBox();
            SuspendLayout();
            // 
            // button1
            // 
            button1.Location = new Point(633, 173);
            button1.Margin = new Padding(3, 4, 3, 4);
            button1.Name = "button1";
            button1.Size = new Size(269, 31);
            button1.TabIndex = 0;
            button1.Text = "Télécharger";
            button1.UseVisualStyleBackColor = true;
            button1.Visible = false;
            button1.Click += button1_Click;
            // 
            // textBox1
            // 
            textBox1.Location = new Point(578, 237);
            textBox1.Margin = new Padding(3, 4, 3, 4);
            textBox1.Multiline = true;
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(369, 52);
            textBox1.TabIndex = 1;
            textBox1.Visible = false;
            textBox1.TextChanged += textBox1_TextChanged;
            // 
            // textBox2
            // 
            textBox2.Location = new Point(578, 299);
            textBox2.Margin = new Padding(3, 4, 3, 4);
            textBox2.Name = "textBox2";
            textBox2.Size = new Size(369, 27);
            textBox2.TabIndex = 2;
            textBox2.Visible = false;
            // 
            // button2
            // 
            button2.Enabled = false;
            button2.Location = new Point(578, 761);
            button2.Margin = new Padding(3, 4, 3, 4);
            button2.Name = "button2";
            button2.Size = new Size(369, 31);
            button2.TabIndex = 3;
            button2.Text = "Lancer";
            button2.UseVisualStyleBackColor = true;
            button2.Click += button2_Click;
            // 
            // textBox3
            // 
            textBox3.Location = new Point(85, 649);
            textBox3.Margin = new Padding(3, 4, 3, 4);
            textBox3.Name = "textBox3";
            textBox3.Size = new Size(198, 27);
            textBox3.TabIndex = 4;
            textBox3.Visible = false;
            textBox3.TextChanged += textBox3_TextChanged;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(17, 688);
            label1.Name = "label1";
            label1.Size = new Size(64, 20);
            label1.TabIndex = 5;
            label1.Text = "Pseudo :";
            label1.Click += label1_Click;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(1505, 785);
            label2.Name = "label2";
            label2.Size = new Size(50, 20);
            label2.TabIndex = 6;
            label2.Text = "label2";
            label2.Click += label2_Click;
            // 
            // comboBox1
            // 
            comboBox1.FormattingEnabled = true;
            comboBox1.Location = new Point(578, 723);
            comboBox1.Margin = new Padding(3, 4, 3, 4);
            comboBox1.Name = "comboBox1";
            comboBox1.Size = new Size(189, 28);
            comboBox1.TabIndex = 7;
            comboBox1.SelectedIndexChanged += comboBox1_SelectedIndexChanged;
            // 
            // button3
            // 
            button3.Location = new Point(14, 724);
            button3.Margin = new Padding(3, 4, 3, 4);
            button3.Name = "button3";
            button3.Size = new Size(138, 31);
            button3.TabIndex = 9;
            button3.Text = "Se connecter";
            button3.UseVisualStyleBackColor = true;
            button3.Click += button3_Click_1;
            // 
            // button4
            // 
            button4.Enabled = false;
            button4.Location = new Point(14, 761);
            button4.Margin = new Padding(3, 4, 3, 4);
            button4.Name = "button4";
            button4.Size = new Size(138, 31);
            button4.TabIndex = 10;
            button4.Text = "Se déconnecter";
            button4.UseVisualStyleBackColor = true;
            button4.Click += button4_Click_1;
            // 
            // button5
            // 
            button5.Enabled = false;
            button5.Location = new Point(149, 761);
            button5.Margin = new Padding(3, 4, 3, 4);
            button5.Name = "button5";
            button5.Size = new Size(148, 31);
            button5.TabIndex = 11;
            button5.Text = "Déconnexion totale";
            button5.UseVisualStyleBackColor = true;
            button5.Click += button5_Click_1;
            // 
            // comboBoxMode
            // 
            comboBoxMode.Enabled = false;
            comboBoxMode.FormattingEnabled = true;
            comboBoxMode.Location = new Point(775, 724);
            comboBoxMode.Margin = new Padding(3, 4, 3, 4);
            comboBoxMode.Name = "comboBoxMode";
            comboBoxMode.Size = new Size(172, 28);
            comboBoxMode.TabIndex = 13;
            comboBoxMode.SelectedIndexChanged += comboBoxMode_SelectedIndexChanged;
            // 
            // progressBar1
            // 
            progressBar1.Location = new Point(578, 407);
            progressBar1.Margin = new Padding(3, 4, 3, 4);
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new Size(369, 31);
            progressBar1.TabIndex = 16;
            progressBar1.Click += progressBar1_Click;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(752, 367);
            label3.Name = "label3";
            label3.Size = new Size(29, 20);
            label3.TabIndex = 17;
            label3.Text = "0%";
            // 
            // button6
            // 
            button6.Location = new Point(149, 724);
            button6.Margin = new Padding(3, 4, 3, 4);
            button6.Name = "button6";
            button6.Size = new Size(148, 31);
            button6.TabIndex = 18;
            button6.Text = "Options";
            button6.UseVisualStyleBackColor = true;
            button6.Click += button6_Click;
            // 
            // comboBox_compte
            // 
            comboBox_compte.FormattingEnabled = true;
            comboBox_compte.Location = new Point(87, 685);
            comboBox_compte.Name = "comboBox_compte";
            comboBox_compte.Size = new Size(210, 28);
            comboBox_compte.TabIndex = 19;
            comboBox_compte.SelectedIndexChanged += comboBox_compte_SelectedIndexChanged;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.Control;
            ClientSize = new Size(1561, 817);
            Controls.Add(comboBox_compte);
            Controls.Add(button6);
            Controls.Add(label3);
            Controls.Add(progressBar1);
            Controls.Add(comboBoxMode);
            Controls.Add(button5);
            Controls.Add(button4);
            Controls.Add(button3);
            Controls.Add(comboBox1);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(textBox3);
            Controls.Add(button2);
            Controls.Add(textBox2);
            Controls.Add(textBox1);
            Controls.Add(button1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(3, 4, 3, 4);
            Name = "Form1";
            Text = "Misty";
            Load += Form1_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button button1;
        private TextBox textBox1;
        private TextBox textBox2;
        private Button button2;
        private TextBox textBox3;
        private Label label1;
        private Label label2;
        private ComboBox comboBox1;
        private Button button3;
        private Button button4;
        private Button button5;
        private ComboBox comboBoxMode;
        private ProgressBar progressBar1;
        private Label label3;
        private Button button6;
        private ComboBox comboBox_compte;
    }
}
