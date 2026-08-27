using CmlLib.Core.ProcessBuilder;
using Microsoft.VisualBasic.Devices;
using System.Diagnostics;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Misty
{
    public partial class Form3 : Form
    {
        //public static string Ram_choisi = "";
        float RamTotale = 0;
        int TRamTotale = 0;
        string arguments = "(Get-ComputerInfo).OsTotalVisibleMemorySize";
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
                        comboBox_ram.Text = ligne.Split('=')[1];
                }
            }
            else
                comboBox_ram.Text = "4096";
        }



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

            comboBox_compte.Items.Clear(); // adapte le nom si ta comboBox s'appelle autrement
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

            //Ram_choisi = comboBox_ram.Text;
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

            for (int i = 1; i < TRamTotale; i++)
            {
                int ram_possible = i * 1024;
                comboBox_ram.Items.Add(ram_possible);
            }

            //MessageBox.Show(RamTotale.ToString());
        }
    }
}
