using CmlLib.Core.ProcessBuilder;
using Microsoft.VisualBasic.Devices;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices.Marshalling;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static System.Windows.Forms.LinkLabel;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Misty
{
    public partial class Form3 : Form
    {
        //public static string Ram_choisi = "";
        float RamTotale = 0;
        int TRamTotale = 0;
        string ram_in_data = "";
        string raccourci = "";
        readonly string arguments = "(Get-ComputerInfo).OsTotalVisibleMemorySize";
        public Form3()
        {
            InitializeComponent();
            ChargerPseudos(); // on charge la liste dès l'ouverture du Form3

            //comboBox_compte.MouseWheel += comboBox_compte_MouseWheel;

            comboBox_compte.DropDownStyle = ComboBoxStyle.DropDownList;

            MaximumSize = Size;
            MinimumSize = Size;


            if (File.Exists(Form1.data))
            {
                var lignes = File.ReadAllLines(Form1.data);

                foreach (var ligne in lignes)
                {
                    if (ligne.StartsWith("ram="))
                    {
                        //comboBox_ram.Text = ligne.Split('=')[1];
                        ram_in_data = ligne.Split('=')[1];
                        comboBox_ram.Items.Add(ram_in_data);
                        comboBox_ram.SelectedItem = ram_in_data;
                    }
                }
            }
            else
            {
                comboBox_ram.Enabled = false;
                comboBox_ram.SelectedItem = "4096";
            }

            if (File.Exists(Form1.data))
            {
                var lignes = File.ReadAllLines(Form1.data);

                foreach (var ligne in lignes)
                {
                    if (ligne.StartsWith("raccourci="))
                    {
                        if (ligne.Split('=')[1] != "")
                        {
                            raccourci = ligne.Split('=')[1];
                            label7.Text = raccourci;
                            checkBox1.Checked = true;
                        }
                            
                    }
                }
            }

            comboBox_ram.MouseWheel += comboBox2_MouseWheel;
            comboBox_ram.DropDownStyle = ComboBoxStyle.DropDownList;
        }


        private void comboBox2_MouseWheel(object sender, MouseEventArgs e) { ((HandledMouseEventArgs)e).Handled = true; }

        // ===== Ajoute un nouveau pseudo à la liste existante =====
        private void AjouterPseudo(string nouveauPseudo)
        {
            if (string.IsNullOrWhiteSpace(nouveauPseudo)) return;

            List<string> pseudos = LireListePseudos();

            // Évite les doublons
            if (!pseudos.Contains(nouveauPseudo))
            {
                pseudos.Add(nouveauPseudo);
                EcrireListePseudos(pseudos);
            }

            ChargerPseudos(); // rafraîchit la comboBox avec la liste à jour
        }
        private void SupprimerPseudo(string pseudoASupprimer)
        {
            List<string> pseudos = LireListePseudos();
            if (pseudos.Contains(pseudoASupprimer))
            {
                pseudos.Remove(pseudoASupprimer);
                EcrireListePseudos(pseudos);
            }
            ChargerPseudos(); // rafraîchit la comboBox avec la liste à jour
        }

        // ===== Lit la liste actuelle depuis le fichier, renvoie une List<string> =====
        private List<string> LireListePseudos()
        {
            var resultat = new List<string>();

            if (!File.Exists(Form1.listpseudo)) return resultat;

            var lignes = File.ReadAllLines(Form1.listpseudo);
            foreach (var ligne in lignes)
            {
                if (ligne.StartsWith("listpseudo="))
                {
                    string valeur = ligne.Substring("listpseudo=".Length);
                    if (!string.IsNullOrEmpty(valeur))
                    {
                        resultat.AddRange(valeur.Split(','));
                    }
                }
            }
            return resultat;
        }

        // ===== Réécrit la liste complète dans le fichier =====
        private void EcrireListePseudos(List<string> pseudos)
        {
            if (!File.Exists(Form1.listpseudo)) return;

            string nouvelleValeur = "listpseudo=" + string.Join(",", pseudos);

            var lignes = File.ReadAllLines(Form1.listpseudo);
            bool ligneExiste = false;

            for (int i = 0; i < lignes.Length; i++)
            {
                if (lignes[i].StartsWith("listpseudo="))
                {
                    lignes[i] = nouvelleValeur;
                    ligneExiste = true;
                }
            }

            if (ligneExiste)
            {
                File.WriteAllLines(Form1.listpseudo, lignes);
            }
            else
            {
                File.AppendAllLines(Form1.listpseudo, new[] { nouvelleValeur });
            }
        }

        // ===== Remplit la comboBox avec tous les pseudos connus =====
        private void ChargerPseudos()
        {
            List<string> pseudos = LireListePseudos();

            comboBox_compte.Items.Clear();
            foreach (var pseudo in pseudos)
            {
                comboBox_compte.Items.Add(pseudo);
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!File.Exists(Form1.data)) return;

            var lignes = File.ReadAllLines(Form1.data);
            for (int i = 0; i < lignes.Length; i++)
            {
                if (lignes[i].StartsWith("ram="))
                    lignes[i] = "ram=" + comboBox_ram.Text;
            }
            File.WriteAllLines(Form1.data, lignes);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (button_supprcompte.Text == "Confirmer ?")
            {
                button_supprcompte.Text = "Supprimer";
                comboBox_compte.Visible = false;
            }
            if (button1.Text == "Créer")
            {
                button1.Text = "Confirmer ?";
                textBox_compte.Visible = true;
            }
            else
            {
                AjouterPseudo(textBox_compte.Text); // <- appelée ici maintenant

                button1.Text = "Créer";
                textBox_compte.Visible = false;
                textBox_compte.Text = ""; // on vide le champ pour la prochaine saisie
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (button1.Text == "Confirmer ?")
            {
                button1.Text = "Créer";
                textBox_compte.Visible = false;
            }
            if (button_supprcompte.Text == "Supprimer")
            {
                button_supprcompte.Text = "Confirmer ?";
                comboBox_compte.Visible = true;
            }
            else
            {
                SupprimerPseudo(comboBox_compte.Text); // <- appelée ici maintenant

                button_supprcompte.Text = "Supprimer";
                comboBox_compte.Visible = false;
                comboBox_compte.Text = ""; // on vide le champ pour la prochaine saisie
            }
        }

        private void button_scan_ram_Click(object sender, EventArgs e)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = $"/C {arguments}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using Process process = Process.Start(startInfo);

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            string result = output;

            RamTotale = Convert.ToSingle(output);
            RamTotale = RamTotale / 1024 / 1024;

            TRamTotale = (int)Math.Ceiling(RamTotale);

            comboBox_ram.Items.Clear();

            for (int i = 1; i < TRamTotale; i++)
            {
                int ram_possible = i * 1024;
                comboBox_ram.Items.Add(ram_possible);
            }

            comboBox_ram.Enabled = true;
            //Thread.Sleep(500);
            comboBox_ram.Text = ram_in_data;

            //MessageBox.Show(RamTotale.ToString());
        }

        private void button_find_raccourci_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dossier = new FolderBrowserDialog();

            if (dossier.ShowDialog() == DialogResult.OK)
            {
                label7.Text = dossier.SelectedPath;

                if (!File.Exists(Form1.data)) return;

                var lignes = File.ReadAllLines(Form1.data);
                for (int i = 0; i < lignes.Length; i++)
                {
                    if (lignes[i].StartsWith("raccourci="))
                        lignes[i] = "raccourci=" + dossier.SelectedPath;
                }
                File.WriteAllLines(Form1.data, lignes);
            }
            string psScript = @$"
$wshshell = New-Object -ComObject WScript.Shell;
$lnk = $wshshell.CreateShortcut('{dossier.SelectedPath}\Misty.lnk');
$lnk.TargetPath = '{AppContext.BaseDirectory}\Misty.exe';
$lnk.Save();
";
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"{psScript}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (Process process = Process.Start(startInfo))
            {
                process.WaitForExit(); // Attend que PowerShell ait fini de créer le raccourci
            }
        }
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                button_find_raccourci.Visible = true;
                label7.Visible = true;
            }
            else
            {
                button_find_raccourci.Visible = false;
                label7.Visible = false;
                File.Delete(label7.Text + @"\Misty.lnk");

                if (!File.Exists(Form1.data)) return;

                var lignes = File.ReadAllLines(Form1.data);
                for (int i = 0; i < lignes.Length; i++)
                {
                    if (lignes[i].StartsWith("raccourci="))
                        lignes[i] = "raccourci=" + "";
                }
                File.WriteAllLines(Form1.data, lignes);
            }
        }
                
    }
}
