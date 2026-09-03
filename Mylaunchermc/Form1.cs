using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.Installer.NeoForge;
using CmlLib.Core.Installer.NeoForge.Installers;
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ModLoaders.LiteLoader;
using CmlLib.Core.ModLoaders.QuiltMC;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.VersionMetadata;
using DiscordRPC;
using Microsoft.VisualBasic;
using Misty.Service;
using Optifine.Installer;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;

namespace Misty
{

    public partial class Form1 : Form
    {
        string chemin = @"C:\TEXT\";
        public static string data = @"C:\TEXT\data.txt";
        public static string listpseudo = @"C:\TEXT\listpseudo.txt";
        string version_data;
        string selectedVersion = "";
        string Nomversion;
        string installedVersionName;
        string Ram_choisie;
        string app_version;
        string cheminDossier = AppContext.BaseDirectory;
        string Path_APP;
        int first, second;
        int nbre_chaine;
        string new_appversion;
        string Suppr;

        MinecraftLauncher launcher;
        JELoginHandler loginHandler;
        MSession? session;
        ForgeInstaller forgee;
        NeoForgeInstaller neoForgee;
        MinecraftPath mcPath;
        OptifineInstaller optifineInstaller;

        public Form1()
        {
            InitializeComponent();

            InitialiserDiscordPresence();

            app_version = "0.2.3.9";

            version_data = "1.2";

            first = 1319;
            second = 591;

            Position_label_version();

            label2.Text = app_version;
            label2.Location = new Point(first, second);

            MaximumSize = Size;
            MinimumSize = Size;

            if (File.Exists(cheminDossier + "icon.ico"))
                this.Icon = new Icon(cheminDossier + "icon.ico");

            mcPath = new MinecraftPath(chemin);
            launcher = new MinecraftLauncher(mcPath);
            forgee = new ForgeInstaller(launcher);
            neoForgee = new NeoForgeInstaller(launcher);
            optifineInstaller = new OptifineInstaller(new HttpClient());

            system_register();
            //verif_datafile();

            // Progression du téléchargement (remplace tes anciens compteurs "tache")
            launcher.FileProgressChanged += (sender, args) =>
            {
                //textBox2.Text = $"{args.Name} ({args.EventType})";
                //textBox1.Text = $"{args.ProgressedTasks}/{args.TotalTasks}";

                int maxbar = Convert.ToInt32(args.TotalTasks);
                progressBar1.Maximum = maxbar;
                progressBar1.Value = Convert.ToInt32(args.ProgressedTasks);

                int pourcentage = (int)((double)args.ProgressedTasks / args.TotalTasks * 100);
                label3.Text = $"{pourcentage}%";
            };
            launcher.ByteProgressChanged += (sender, args) =>
            {
                textBox1.Text = $"{args.ProgressedBytes}/{args.TotalBytes} octets";
            };

            _ = ChargerVersionsAsync(); // async fire-and-forget dans le constructeur


            comboBox1.MouseWheel += comboBox1_MouseWheel;
            comboBoxMode.MouseWheel += comboBox1_MouseWheel;
            comboBox_compte.MouseWheel += comboBox1_MouseWheel;   // <- réutilise la même méthode

            comboBox1.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxMode.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox_compte.DropDownStyle = ComboBoxStyle.DropDownList;

            //Fairetest();            
        }


        private void comboBox1_MouseWheel(object sender, MouseEventArgs e) { ((HandledMouseEventArgs)e).Handled = true; }
        private async Task Initiale_Path(string urlTelechargement)
        {
            Path_APP = $"Misty-{app_version}";
            cheminDossier = cheminDossier.Split(Path_APP)[0];

            await download_newversion_app(urlTelechargement);
        }
        private async Task download_newversion_app(string url)
        {
            string cheminDossierZip = cheminDossier + "Misty-" + new_appversion + ".zip";

            using (HttpClient client = new HttpClient())
            using (HttpResponseMessage response = await client.GetAsync(url))
            {
                response.EnsureSuccessStatusCode();

                using (FileStream fs = new FileStream(cheminDossierZip, FileMode.Create))
                {
                    await response.Content.CopyToAsync(fs);
                }
            }

            await ZipFile.ExtractToDirectoryAsync(cheminDossierZip, cheminDossier);
            File.Delete(cheminDossierZip);


            if (!File.Exists(data)) return;

            var lignes = File.ReadAllLines(data);
            for (int i = 0; i < lignes.Length; i++)
            {
                if (lignes[i].StartsWith("Suppr="))
                    lignes[i] = "Suppr=" + cheminDossier + Path_APP;
            }
            File.WriteAllLines(data, lignes);

            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(cheminDossier, $"Misty-{new_appversion}", "Misty.exe"),
                UseShellExecute = true
            });

            System.Windows.Forms.Application.Exit();
        }
        // ── Chargement des versions disponibles (remplace List_release) ──
        private async Task ChargerVersionsAsync()
        {
            try
            {
                var versions = await launcher.GetAllVersionsAsync();

                comboBox1.Items.Clear();
                foreach (var v in versions)
                {
                    if (v.Type != null && v.Type.ToString().Equals("Release", StringComparison.OrdinalIgnoreCase))
                    {
                        comboBox1.Items.Add(v.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur au chargement des versions : " + ex.Message);
            }
        }
        private async Task download_icone(string url, string nomFichier)
        {
            string cheminDossiercache = cheminDossier + "\\cache\\" + nomFichier;

            using (HttpClient client = new HttpClient())
            using (HttpResponseMessage response = await client.GetAsync(url))
            {
                response.EnsureSuccessStatusCode();

                using (FileStream fs = new FileStream(cheminDossiercache, FileMode.Create))
                {
                    await response.Content.CopyToAsync(fs);
                }
            }
            // no return value; file is saved to cache
        }
        private void Position_label_version()
        {
            nbre_chaine = app_version.Length;
            if (nbre_chaine < 8)
            {
                nbre_chaine = 8 - nbre_chaine;
                first += nbre_chaine * 5;
            }
            else
            {
                nbre_chaine -= 8;
                first -= nbre_chaine * 6;
            }
        }
        private async Task ChargerPseudoEtDerniereVersion()
        {
            string premium = string.Empty;

            if (File.Exists(data))
            {
                var lignes = File.ReadAllLines(data);

                foreach (var ligne in lignes)
                {
                    if (ligne.StartsWith("lastpseudo="))
                        comboBox_compte.Text = ligne.Split('=')[1];

                    if (ligne.StartsWith("lastversion="))
                        comboBox1.Text = ligne.Split('=')[1];

                    if (ligne.StartsWith("premium="))
                    {
                        premium = ligne.Split('=')[1];

                        if (premium == "True")
                        {
                            try
                            {
                                button3.Enabled = false;
                                button3.Text = "Connexion en cours...";

                                loginHandler = JELoginHandlerBuilder.BuildDefault();
                                session = await loginHandler.Authenticate();

                                button3.Text = session.Username;
                                comboBox_compte.Enabled = false;

                                button4.Enabled = true;
                                button5.Enabled = true;
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("Erreur de connexion : " + ex.Message);
                                button3.Text = "Se connecter";
                                button3.Enabled = true;
                            }
                        }
                    }

                    if (ligne.StartsWith("Suppr="))
                    {
                        Suppr = ligne.Split('=')[1];

                        if (Suppr.StartsWith("C:"))
                        {
                            Thread.Sleep(1000);

                            do
                            {
                                Directory.Delete(Suppr);

                                if (Directory.Exists(Suppr))
                                {
                                    MessageBox.Show("Impossible de supprimer l'ancien dossier. Veuillez fermer votre explorateur de fichiers.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                            }
                            while (Directory.Exists(Suppr));

                            MessageBox.Show("Mise à jour réussie avec succès !", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information);

                            for (int i = 0; i < lignes.Length; i++)
                            {
                                if (lignes[i].StartsWith("Suppr="))
                                    lignes[i] = "Suppr=";
                            }
                            File.WriteAllLines(data, lignes);
                        }
                    }
                }
            }
            else
                comboBox_compte.Text = "PseudoTest";
        }
        /*
        private async Task Fairetest()
        {
            string dossierMods = Path.Combine(chemin, "mods");

            flowLayoutPanel1.Controls.Clear();

            if (Directory.Exists(dossierMods))
            {
                string[] mods = Directory.GetFiles(dossierMods, "*.jar");

                foreach (string mod in mods)
                {

                    ModrinthService modrinth = new ModrinthService();

                    var resultat = await modrinth.RechercherMod("Jade");
                    if (resultat != null)
                    {
                        await download_icone(resultat.IconUrl ?? "", "jade.png");
                        MessageBox.Show($"Nom : {resultat.Title}\nDescription : {resultat.Description}\nTéléchargements : {resultat.Downloads}\nImage : {resultat.IconUrl}");
                    }

                    Thread.Sleep(1000);
                    Panel panel = new Panel();

                    panel.Width = 400;
                    panel.Height = 70;
                    panel.Margin = new Padding(5);

                    PictureBox picture = new PictureBox();

                    picture.Image = System.Drawing.Image.FromFile($"{chemin}\\cache\\{(resultat?.Title ?? "jade")}.png");
                    picture.Width = 50;
                    picture.Height = 50;
                    picture.Left = 5;
                    picture.Top = 5;

                    picture.SizeMode = PictureBoxSizeMode.Zoom;

                    Label label = new Label();

                    label.Text = Path.GetFileName(mod);
                    label.AutoSize = true;
                    label.Left = 65;
                    label.Top = 20;

                    panel.Controls.Add(picture);
                    panel.Controls.Add(label);
                    //label.Controls.Add(label);

                    flowLayoutPanel1.Controls.Add(label);
                }
            }
        }*/
        private void affect_ram()
        {
            if (File.Exists(Form1.data))
            {
                var lignes = File.ReadAllLines(Form1.data);

                foreach (var ligne in lignes)
                {
                    if (ligne.StartsWith("ram="))
                    {
                        Ram_choisie = ligne.Split('=')[1];

                        if (string.IsNullOrEmpty(Ram_choisie))
                            Ram_choisie = "4096";
                    }
                }
            }
            else
                Ram_choisie = "4096";

        }
        private void Form1_Load(object sender, EventArgs e) { }

        // ── Téléchargement / installation de la version choisie ──
        private async void button1_Click(object sender, EventArgs e)
        {
            await redownloadMC();
        }


        // ── Lancement du jeu ──
        private async void button2_Click(object sender, EventArgs e)
        {
            affect_ram();
            await downloadMC();
            Programlaunch();
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            discordClient?.Dispose();
            base.OnFormClosing(e);
        }
        private List<string> LireListePseudos()
        {
            var resultat = new List<string>();

            if (!File.Exists(listpseudo)) return resultat;

            var lignes = File.ReadAllLines(listpseudo);
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
        private void ChargerPseudos()
        {
            List<string> pseudos = LireListePseudos();

            comboBox_compte.Items.Clear(); // adapte le nom si ta comboBox s'appelle autrement
            foreach (var pseudo in pseudos)
            {
                comboBox_compte.Items.Add(pseudo);
            }
        }
        private void textBox1_TextChanged(object sender, EventArgs e) { }
        private void label1_Click(object sender, EventArgs e) { }


        private void label2_Click(object sender, EventArgs e)
        {
            Form2 form = new Form2();
            form.ShowDialog();
        }
        private async Task system_register()
        {
            await verif_datafile();

            if (!Directory.Exists(chemin))
                Directory.CreateDirectory(chemin);

            if (!File.Exists(data))
            {
                using (StreamWriter sw = File.CreateText(data))
                {
                    sw.WriteLine($"versiondatatxt={version_data}");
                    sw.WriteLine("lastpseudo=PseudoTest");
                    sw.WriteLine("lastversion=");
                    sw.WriteLine("premium=");
                    sw.WriteLine("ram=");
                    sw.WriteLine("Suppr=");
                }
            }
            if (!File.Exists(listpseudo))
            {
                using (StreamWriter sw = File.CreateText(listpseudo))
                {
                    sw.WriteLine("listpseudo=PseudoTest");
                }
            }

            await VerifierMiseAJour();
            ChargerPseudos();
            ChargerPseudoEtDerniereVersion();
        }
        private async Task verif_datafile()
        {
            string version_data2 = string.Empty;
            string lastPseudo = string.Empty;
            string lastVersion = string.Empty;
            string dossierasuppr = string.Empty;
            string ramassocie = string.Empty;
            string premium = string.Empty;

            if (File.Exists(data))
            {
                var lignes = File.ReadAllLines(data);

                foreach (var ligne in lignes)
                {
                    if (ligne.StartsWith("versiondatatxt="))
                        version_data2 = ligne.Split('=')[1];

                    if (ligne.StartsWith("lastpseudo="))
                        lastPseudo = ligne.Split('=')[1];

                    if (ligne.StartsWith("lastversion="))
                        lastVersion = ligne.Split('=')[1];

                    if (ligne.StartsWith("Suppr="))
                        dossierasuppr = ligne.Split('=')[1];

                    if (ligne.StartsWith("ram="))
                        ramassocie = ligne.Split('=')[1];

                    if (ligne.StartsWith("premium="))
                        premium = ligne.Split('=')[1];
                }
                if (version_data != version_data2)
                {
                    File.Move(data, chemin + "data_old.txt");

                    using (StreamWriter sw = File.CreateText(data))
                    {
                        sw.WriteLine($"versiondatatxt={version_data}");
                        sw.WriteLine($"lastpseudo={lastPseudo}");
                        sw.WriteLine($"lastversion={lastVersion}");
                        sw.WriteLine($"ram={ramassocie}");
                        sw.WriteLine($"premium={premium}");
                        //sw.WriteLine($"raccour);
                        sw.WriteLine($"Suppr={dossierasuppr}");
                    }

                    File.Delete(chemin + "data_old.txt");
                }
            }
        }
        private async Task VerifierMiseAJour()
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Misty"); // obligatoire, GitHub refuse sans ça

                string json = await client.GetStringAsync("https://api.github.com/repos/xTheoxreborn/Misty/releases/latest");

                using var doc = JsonDocument.Parse(json);
                string versionDistante = doc.RootElement.GetProperty("tag_name").GetString(); // ex: "v0.1.3"
                string urlTelechargement = doc.RootElement
                    .GetProperty("assets")[0]
                    .GetProperty("browser_download_url")
                    .GetString();

                string versionDistanteNettoyee = versionDistante.TrimStart('v'); // enlève le "v" devant si présent
                new_appversion = versionDistanteNettoyee;

                if (versionDistanteNettoyee != app_version)
                {

                    var resultat = MessageBox.Show(
                        $"Une nouvelle version est disponible : {versionDistanteNettoyee} (actuelle : {app_version}).\nTélécharger maintenant ?",
                        "Mise à jour disponible",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (resultat == DialogResult.Yes)
                    {
                        //Process.Start(new ProcessStartInfo(urlTelechargement) { UseShellExecute = true });
                        Initiale_Path(urlTelechargement);
                    }
                    /*
                    else if (resultat == MessageBoxResult.No)
                    {
                        label_maj.Visibility = Visibility.Visible;
                        //button_maj.Visibility = Visibility.Visible;
                    }*/
                }
                /*
                else
                {
                    label_maj.Visibility = Visibility.Hidden;
                }*/
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur vérification MAJ : " + ex.Message);
            }
        }
        private async void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            button2.Enabled = false;

            selectedVersion = comboBox1.Text;

            if (!File.Exists(data)) return;

            var lignes = File.ReadAllLines(data);
            for (int i = 0; i < lignes.Length; i++)
            {
                if (lignes[i].StartsWith("lastversion="))
                    lignes[i] = "lastversion=" + comboBox1.Text;
            }
            File.WriteAllLines(data, lignes);

            comboBoxMode.Enabled = false;
            comboBoxMode.Items.Clear();

            await ChargerModesDisponiblesAsync(selectedVersion);
        }

        private async Task ChargerModesDisponiblesAsync(string versionMinecraft)
        {
            comboBoxMode.Items.Add("Vanilla");

            if (VersionPeutAvoirForge(versionMinecraft))
            {
                try
                {
                    var forgeInstaller = new ForgeInstaller(launcher);
                    var versionsForge = await forgeInstaller.GetForgeVersions(versionMinecraft);
                    if (versionsForge.Any())
                        comboBoxMode.Items.Add("Forge");
                }
                catch { }
            }
            if (VersionPeutAvoirOptifine(versionMinecraft))
            {
                try
                {
                    var versionsOptifine = await optifineInstaller.GetOptifineVersionsAsync();
                    if (versionsOptifine.Any(v => v.MinecraftVersion == versionMinecraft))
                    {
                        comboBoxMode.Items.Add("OptiFine");
                    }
                }
                catch { }
            }

            if (VersionPeutAvoirNeoForge(versionMinecraft))
            {
                try
                {
                    var neoforgeInstaller = new NeoForgeInstaller(launcher);
                    var versionsNeoForge = await neoforgeInstaller.GetForgeVersions(versionMinecraft);
                    if (versionsNeoForge.Any())
                        comboBoxMode.Items.Add("NeoForge");
                }
                catch { }
            }

            if (VersionPeutAvoirFabric(versionMinecraft))
            {
                try
                {
                    var fabricInstaller = new FabricInstaller(new HttpClient());
                    var versionsFabric = await fabricInstaller.GetLoaders(versionMinecraft);
                    if (versionsFabric.Any())
                        comboBoxMode.Items.Add("Fabric");
                }
                catch { }
            }
            /*
            if (VersionPeutAvoirQuilt(versionMinecraft))
            {
                try
                {
                    var quiltInstaller = new QuiltInstaller(new HttpClient());
                    var versionsQuilt = await quiltInstaller.GetLoaders(versionMinecraft);
                    if (versionsQuilt.Any())
                        comboBoxMode.Items.Add("Quilt");
                }
                catch { }
            }*/

            if (VersionPeutAvoirLiteLoader(versionMinecraft))
            {
                try
                {
                    var liteLoaderInstaller = new LiteLoaderInstaller(new HttpClient());
                    var loaders = await liteLoaderInstaller.GetAllLiteLoaders();
                    if (loaders.Any(l => l.BaseVersion == versionMinecraft))
                        comboBoxMode.Items.Add("LiteLoader");
                }
                catch { }
            }

            comboBoxMode.SelectedIndex = 0;
            comboBoxMode.Enabled = true;

            button2.Enabled = true; // Réactive le bouton de lancement après avoir chargé les modes
        }
        private bool VersionPeutAvoirForge(string version)
        {
            if (!System.Version.TryParse(NettoyerVersion(version), out var v))
                return false;

            var min = new Version(1, 7, 10);
            var max = new Version(26, 3);

            return v >= min && v <= max;
        }
        private bool VersionPeutAvoirOptifine(string version)
        {
            if (!System.Version.TryParse(NettoyerVersion(version), out var v))
                return false;

            var min = new Version(1, 7, 2);
            var max = new Version(26, 3);

            return v >= min && v <= max;
        }
        private bool VersionPeutAvoirFabric(string version)
        {
            if (!System.Version.TryParse(NettoyerVersion(version), out var v))
                return false;

            var min = new Version(1, 14, 0); // Fabric a été introduit vers cette période
            var max = new Version(26, 3);

            return v >= min && v <= max;
        }

        private bool VersionPeutAvoirQuilt(string version)
        {
            if (!System.Version.TryParse(NettoyerVersion(version), out var v))
                return false;

            var min = new Version(1, 14, 0); // Quilt est un fork de Fabric, plage similaire
            var max = new Version(26, 3);

            return v >= min && v <= max;
        }

        private bool VersionPeutAvoirLiteLoader(string version)
        {
            if (!System.Version.TryParse(NettoyerVersion(version), out var v))
                return false;

            var min = new Version(1, 0, 0);
            var max = new Version(1, 12, 2); // LiteLoader n'a jamais suivi les versions récentes

            return v >= min && v <= max;
        }
        private bool VersionPeutAvoirNeoForge(string version)
        {
            if (!System.Version.TryParse(NettoyerVersion(version), out var v))
                return false;

            var min = new Version(1, 20, 1);
            var max = new Version(26, 3);

            return v >= min && v <= max;
        }
        private string NettoyerVersion(string version)
        {
            var parts = version.Split('.');
            return parts.Length == 2 ? version + ".0" : version;
        }

        // ── Connexion / déconnexion Microsoft (inchangé) ──

        private async void button3_Click(object sender, EventArgs e) { }
        private void button4_Click(object sender, EventArgs e) { }
        private async void button5_Click(object sender, EventArgs e) { }

        private async void button3_Click_1(object sender, EventArgs e)
        {
            try
            {
                button3.Enabled = false;
                button3.Text = "Connexion en cours...";

                loginHandler = JELoginHandlerBuilder.BuildDefault();
                session = await loginHandler.Authenticate();

                button3.Text = session.Username;
                comboBox_compte.Enabled = false;

                button4.Enabled = true;
                button5.Enabled = true;

                if (!File.Exists(data)) return;

                var lignes = File.ReadAllLines(data);
                for (int i = 0; i < lignes.Length; i++)
                {
                    if (lignes[i].StartsWith("premium="))
                        lignes[i] = "premium=" + "True";
                }
                File.WriteAllLines(data, lignes);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur de connexion : " + ex.Message);
                button3.Text = "Se connecter";
                button3.Enabled = true;
            }
        }

        private void button4_Click_1(object sender, EventArgs e)
        {
            button3.Enabled = true;
            comboBox_compte.Enabled = true;
            button3.Text = "Se connecter";
            session = null;

            button4.Enabled = false;
            button5.Enabled = false;

            if (!File.Exists(data)) return;

            var lignes = File.ReadAllLines(data);
            for (int i = 0; i < lignes.Length; i++)
            {
                if (lignes[i].StartsWith("premium="))
                    lignes[i] = "premium=" + "False";
            }
            File.WriteAllLines(data, lignes);
        }

        private async void button5_Click_1(object sender, EventArgs e)
        {
            try
            {
                if (loginHandler != null)
                    await loginHandler.Signout();

                session = null;
                button3.Enabled = true;
                button3.Text = "Se connecter";
                comboBox_compte.Enabled = true;

                button4.Enabled = false;
                button5.Enabled = false;

                if (!File.Exists(data)) return;

                var lignes = File.ReadAllLines(data);
                for (int i = 0; i < lignes.Length; i++)
                {
                    if (lignes[i].StartsWith("premium="))
                        lignes[i] = "premium=" + "False";
                }
                File.WriteAllLines(data, lignes);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur de déconnexion : " + ex.Message);
            }
        }

        private void label3_Click(object sender, EventArgs e)
        {

        }
        private async Task downloadMC()
        {
            button1.Enabled = false;

            try
            {
                switch (comboBoxMode.Text)
                {
                    case "Vanilla":
                        await launcher.InstallAsync(selectedVersion);
                        break;

                    case "OptiFine":
                        await launcher.InstallAsync(selectedVersion);

                        var versionsOptifine = await optifineInstaller.GetOptifineVersionsAsync();
                        var optifineChoisi = versionsOptifine.FirstOrDefault(v => v.MinecraftVersion == selectedVersion);

                        try
                        {
                            installedVersionName = await optifineInstaller.InstallOptifineAsync(mcPath.BasePath, optifineChoisi);
                            await launcher.InstallAsync(installedVersionName);
                        }
                        catch (Exception ex)
                        {
                            string Message = ex.ToString();

                            int debut = Message.IndexOf("versions\\");

                            if (debut != -1)
                            {
                                string chemin = Message.Substring(debut);

                                installedVersionName = chemin.Split('\\')[1];
                            }
                        }

                        Nomversion = installedVersionName;
                        break;

                    case "Forge":
                        installedVersionName = await forgee.Install(selectedVersion, new ForgeInstallOptions());
                        await launcher.InstallAsync(installedVersionName);
                        Nomversion = installedVersionName;
                        break;

                    case "NeoForge":
                        installedVersionName = await neoForgee.Install(selectedVersion, new NeoForgeInstallOptions());
                        await launcher.InstallAsync(installedVersionName);
                        Nomversion = installedVersionName;
                        break;

                    case "Fabric":
                        var fabricInstaller = new FabricInstaller(new HttpClient());
                        installedVersionName = await fabricInstaller.Install(selectedVersion, mcPath);
                        await launcher.InstallAsync(installedVersionName);
                        Nomversion = installedVersionName;
                        break;
                    /*
                case "Quilt":
                    var quiltInstaller = new QuiltInstaller(new HttpClient());
                    installedVersionName = await quiltInstaller.Install(selectedVersion, mcPath);
                    await launcher.InstallAsync(installedVersionName);
                    Nomversion = installedVersionName;
                    break;
                    */
                    case "LiteLoader":
                        var liteLoaderInstaller = new LiteLoaderInstaller(new HttpClient());
                        var loaders = await liteLoaderInstaller.GetAllLiteLoaders();
                        var loaderChoisi = loaders.First(l => l.BaseVersion == selectedVersion);

                        installedVersionName = await liteLoaderInstaller.Install(
                            loaderChoisi,
                            await launcher.GetVersionAsync(selectedVersion),
                            mcPath);

                        await launcher.InstallAsync(installedVersionName);
                        Nomversion = installedVersionName;
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur pendant le téléchargement : " + ex.Message);
            }
            finally
            {
                button1.Enabled = true;
            }
        }
        private async Task redownloadMC()
        {
            if (string.IsNullOrEmpty(selectedVersion))
            {
                MessageBox.Show("Sélectionne une version d'abord.");
                return;
            }

            button1.Enabled = false;

            try
            {
                if (comboBoxMode.Text == "Vanilla")
                {
                    await launcher.InstallAsync(selectedVersion);
                    textBox1.Text = "Installation terminée.";
                }
                else // Forge
                {
                    var installedVersionName = await forgee.Install(selectedVersion, new ForgeInstallOptions());
                    await launcher.InstallAsync(installedVersionName);
                    textBox1.Text = "Installation Forge terminée.";
                    Nomversion = installedVersionName; // Stocke le nom de la version Forge installée
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur pendant le téléchargement : " + ex.Message);
            }
            finally
            {
                button1.Enabled = true;
            }
        }
        private async Task Programlaunch()
        {
            selectedVersion = comboBox1.Text;


            string pseudo = string.IsNullOrWhiteSpace(comboBox_compte.Text) ? "Misty" : comboBox_compte.Text;

            try
            {
                MSession sessionAUtiliser = session ?? MSession.CreateOfflineSession(pseudo);
                System.Diagnostics.Process process;


                if (comboBoxMode.Text == "Vanilla")
                {
                    var launchOption = new MLaunchOption
                    {
                        Session = sessionAUtiliser,
                        MaximumRamMb = Convert.ToInt32(Ram_choisie)
                    };
                    process = await launcher.BuildProcessAsync(selectedVersion, launchOption);
                }
                else if (comboBoxMode.Text == "OptiFine")
                {
                    process = await launcher.InstallAndBuildProcessAsync(Nomversion, new MLaunchOption
                    {
                        Session = sessionAUtiliser,
                        MaximumRamMb = Convert.ToInt32(Ram_choisie)
                    });
                }
                else
                {
                    process = await launcher.BuildProcessAsync(Nomversion, new MLaunchOption
                    {
                        Session = sessionAUtiliser,
                        MaximumRamMb = Convert.ToInt32(Ram_choisie),
                    });
                }

                // ===== Statut Discord "en train de jouer" =====
                discordClient.SetPresence(new RichPresence()
                {
                    Details = "Joue à Minecraft",
                    State = selectedVersion + " " + comboBoxMode.Text,
                    Timestamps = Timestamps.Now,
                    /*
                    Buttons = new DiscordRPC.Button[]
                    {
                        new DiscordRPC.Button()
                        {
                            Label = "Télécharger Misty",
                            Url = "https://github.com/xTheoxreborn/Misty/releases/latest"
                        }
                    }*/
                });

                process.EnableRaisingEvents = true;
                process.Exited += (s, args) =>
                {
                    this.Invoke(() =>
                    {
                        discordClient.SetPresence(new RichPresence()
                        {
                            Details = "Dans le launcher",
                            Timestamps = Timestamps.Now,

                            /*
                            Buttons = new DiscordRPC.Button[]
                            {
                                new DiscordRPC.Button()
                                {
                                    Label = "Télécharger Misty",
                                    Url = "https://github.com/xTheoxreborn/Misty/releases/latest"
                                }
                            }*/
                        });
                    });
                };

                process.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Impossible de lancer le jeu : " + ex.Message);
            }
        }

        private void comboBox_ram_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void progressBar1_Click(object sender, EventArgs e)
        {

        }


        DiscordRpcClient discordClient;

        private void InitialiserDiscordPresence()
        {
            discordClient = new DiscordRpcClient("1536551827640680528");

            discordClient.Initialize();

            discordClient.SetPresence(new RichPresence()
            {
                //Details = "Prépare son lancement",
                State = "Dans le launcher",
                Timestamps = Timestamps.Now,
                /*
                Buttons = new DiscordRPC.Button[]
                {
                    new DiscordRPC.Button()
                    {
                        Label = "Télécharger Misty",
                        Url = "https://github.com/xTheoxreborn/Misty/releases/latest"
                    }
                }
                
                Assets = new Assets()
                {
                    LargeImageKey = "logo",  // nom d'une image que tu upload plus tard dans le portail Discord
                    LargeImageText = "Misty"
                }*/

            });
        }

        private void button6_Click(object sender, EventArgs e)
        {
            Form3 form = new Form3();
            form.FormClosed += Form3_FormClosed;
            form.ShowDialog();
        }
        private void Form3_FormClosed(object sender, FormClosedEventArgs e)
        {
            ChargerPseudos();
            ChargerPseudoEtDerniereVersion();
        }

        private void comboBox_compte_SelectedIndexChanged(object sender, EventArgs e)
        {
            var lignes = File.ReadAllLines(data);
            for (int i = 0; i < lignes.Length; i++)
            {
                if (lignes[i].StartsWith("lastpseudo="))
                    lignes[i] = "lastpseudo=" + comboBox_compte.Text;
            }
            File.WriteAllLines(data, lignes);
        }

        private void comboBoxMode_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            Process.Start("explorer.exe", chemin);
        }

        private async void button7_Click(object sender, EventArgs e)
        {
            /*
            ModrinthService modrinth = new ModrinthService();

            var resultat = await modrinth.RechercherMod("Jade");
            await download_icone(resultat.IconUrl, "jade");
            MessageBox.Show($"Nom du mod : {resultat.Title}\nDescription : {resultat.Description}\nVersion : {resultat.LatestVersion}\nok :{resultat.LatestVersion}");
            */
            //Fairetest();
        }
    }
}