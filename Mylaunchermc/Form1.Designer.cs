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
            pictureBox1 = new PictureBox();
            flowLayoutPanel1 = new FlowLayoutPanel();
            button7 = new Button();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            SuspendLayout();
            // 
            // button1
            // 
            button1.Location = new Point(188, 52);
            button1.Name = "button1";
            button1.Size = new Size(235, 23);
            button1.TabIndex = 0;
            button1.Text = "Télécharger";
            button1.UseVisualStyleBackColor = true;
            button1.Visible = false;
            button1.Click += button1_Click;
            // 
            // textBox1
            // 
            textBox1.Location = new Point(142, 91);
            textBox1.Multiline = true;
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(323, 40);
            textBox1.TabIndex = 1;
            textBox1.Visible = false;
            textBox1.TextChanged += textBox1_TextChanged;
            // 
            // textBox2
            // 
            textBox2.Location = new Point(142, 137);
            textBox2.Name = "textBox2";
            textBox2.Size = new Size(323, 23);
            textBox2.TabIndex = 2;
            textBox2.Visible = false;
            // 
            // button2
            // 
            button2.Enabled = false;
            button2.Location = new Point(506, 571);
            button2.Name = "button2";
            button2.Size = new Size(323, 23);
            button2.TabIndex = 3;
            button2.Text = "Lancer";
            button2.UseVisualStyleBackColor = true;
            button2.Click += button2_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(15, 516);
            label1.Name = "label1";
            label1.Size = new Size(52, 15);
            label1.TabIndex = 5;
            label1.Text = "Pseudo :";
            label1.Click += label1_Click;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(1319, 591);
            label2.Name = "label2";
            label2.Size = new Size(38, 15);
            label2.TabIndex = 6;
            label2.Text = "label2";
            label2.Click += label2_Click;
            // 
            // comboBox1
            // 
            comboBox1.FormattingEnabled = true;
            comboBox1.Location = new Point(506, 542);
            comboBox1.Name = "comboBox1";
            comboBox1.Size = new Size(166, 23);
            comboBox1.TabIndex = 7;
            comboBox1.SelectedIndexChanged += comboBox1_SelectedIndexChanged;
            // 
            // button3
            // 
            button3.Location = new Point(12, 543);
            button3.Name = "button3";
            button3.Size = new Size(121, 23);
            button3.TabIndex = 9;
            button3.Text = "Se connecter";
            button3.UseVisualStyleBackColor = true;
            button3.Click += button3_Click_1;
            // 
            // button4
            // 
            button4.Enabled = false;
            button4.Location = new Point(12, 571);
            button4.Name = "button4";
            button4.Size = new Size(121, 23);
            button4.TabIndex = 10;
            button4.Text = "Se déconnecter";
            button4.UseVisualStyleBackColor = true;
            button4.Click += button4_Click_1;
            // 
            // button5
            // 
            button5.Enabled = false;
            button5.Location = new Point(130, 571);
            button5.Name = "button5";
            button5.Size = new Size(130, 23);
            button5.TabIndex = 11;
            button5.Text = "Déconnexion totale";
            button5.UseVisualStyleBackColor = true;
            button5.Click += button5_Click_1;
            // 
            // comboBoxMode
            // 
            comboBoxMode.Enabled = false;
            comboBoxMode.FormattingEnabled = true;
            comboBoxMode.Location = new Point(678, 543);
            comboBoxMode.Name = "comboBoxMode";
            comboBoxMode.Size = new Size(151, 23);
            comboBoxMode.TabIndex = 13;
            comboBoxMode.SelectedIndexChanged += comboBoxMode_SelectedIndexChanged;
            // 
            // progressBar1
            // 
            progressBar1.Location = new Point(506, 305);
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new Size(323, 23);
            progressBar1.TabIndex = 16;
            progressBar1.Click += progressBar1_Click;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(658, 275);
            label3.Name = "label3";
            label3.Size = new Size(23, 15);
            label3.TabIndex = 17;
            label3.Text = "0%";
            // 
            // button6
            // 
            button6.Location = new Point(130, 543);
            button6.Name = "button6";
            button6.Size = new Size(130, 23);
            button6.TabIndex = 18;
            button6.Text = "Options";
            button6.UseVisualStyleBackColor = true;
            button6.Click += button6_Click;
            // 
            // comboBox_compte
            // 
            comboBox_compte.FormattingEnabled = true;
            comboBox_compte.Location = new Point(76, 514);
            comboBox_compte.Margin = new Padding(3, 2, 3, 2);
            comboBox_compte.Name = "comboBox_compte";
            comboBox_compte.Size = new Size(184, 23);
            comboBox_compte.TabIndex = 19;
            comboBox_compte.SelectedIndexChanged += comboBox_compte_SelectedIndexChanged;
            // 
            // pictureBox1
            // 
            pictureBox1.Image = (Image)resources.GetObject("pictureBox1.Image");
            pictureBox1.InitialImage = (Image)resources.GetObject("pictureBox1.InitialImage");
            pictureBox1.Location = new Point(835, 571);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(33, 23);
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.TabIndex = 20;
            pictureBox1.TabStop = false;
            pictureBox1.Click += pictureBox1_Click;
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.AutoScroll = true;
            flowLayoutPanel1.Location = new Point(985, 210);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Size = new Size(313, 210);
            flowLayoutPanel1.TabIndex = 23;
            // 
            // button7
            // 
            button7.Location = new Point(924, 108);
            button7.Name = "button7";
            button7.Size = new Size(75, 23);
            button7.TabIndex = 24;
            button7.Text = "button7";
            button7.UseVisualStyleBackColor = true;
            button7.Visible = false;
            button7.Click += button7_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.Control;
            ClientSize = new Size(1366, 613);
            Controls.Add(button7);
            Controls.Add(flowLayoutPanel1);
            Controls.Add(pictureBox1);
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
            Controls.Add(button2);
            Controls.Add(textBox2);
            Controls.Add(textBox1);
            Controls.Add(button1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "Form1";
            Text = "Misty";
            Load += Form1_Load;
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button button1;
        private TextBox textBox1;
        private TextBox textBox2;
        private Button button2;
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
        private PictureBox pictureBox1;
        private FlowLayoutPanel flowLayoutPanel1;
        private Button button7;
    }
}
