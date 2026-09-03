using System;
using System.Threading.Tasks;
using Modrinth;
using Modrinth.Models;

namespace Misty.Service
{
    public class ModrinthService
    {
        public async Task<dynamic?> RechercherMod(string nom)
        {
            var client = new ModrinthClient(userAgent: "Misty/1.0");

            var search = await client.Project.SearchAsync(nom);

            var project = search.Hits.FirstOrDefault();

            return project;
        }


        /*
        public async Task<SearchResult?> RechercherModAsync(string nom)
        {
            var client = new ModrinthClient(userAgent: "Misty/1.0");

            var search = await client.Project.SearchAsync(nom);

            if (search?.Hits == null || search.Hits.Length == 0)
                return null;

            return search.Hits[0];
        }

        public async Task<SearchResult?> RechercherModAsync(string nom)
        {
            var result = await client.Project.SearchAsync(nom);

            if (result.Hits == null || result.Hits.Length == 0)
                return null;

            return result.Hits[0];
        }
        ModrinthService modrinth = new ModrinthService();

        string[] fichiers = Directory.GetFiles(
            dossierMods,
            "*.jar"
        );

        private async void RechercherMods()
        {
            foreach (string fichier in fichiers)
            {
                string nomFichier = Path.GetFileNameWithoutExtension(fichier);

                var mod = await modrinth.RechercherModAsync(nomFichier);

                if (mod != null)
                {
                    MessageBox.Show(
                        $"Nom : {mod.Title}\n" +
                        $"Description : {mod.Description}\n" +
                        $"Téléchargements : {mod.Downloads}\n" +
                        $"Image : {mod.IconUrl}"
                    );
                }
            }
        }
        private string NettoyerNomMod(string nom)
        {
            nom = nom.ToLower();

            string[] motsASupprimer =
            {
                "-fabric",
                "-forge",
                "-neoforge",
                "-quilt"
            };

            foreach (string mot in motsASupprimer)
            {
                int index = nom.IndexOf(mot);

                if (index != -1)
                    nom = nom.Substring(0, index);
            }

            return nom;
        }
        private async Task<Image?> TéléchargerImageAsync(string url)
        {
            using HttpClient http = new HttpClient();

            byte[] data = await http.GetByteArrayAsync(url);

            using MemoryStream stream = new MemoryStream(data);

            return Image.FromStream(stream);
    
            Image? image = await TéléchargerImageAsync(mod.IconUrl);

            if (image != null)
            {
                pictureBox1.Image = image;
            }
        }



        private async Task AjouterMod(string fichier)
        {
            string nomFichier = Path.GetFileNameWithoutExtension(fichier);

            ModrinthService modrinth = new ModrinthService();

            var mod = await modrinth.RechercherModAsync(nomFichier);

            if (mod == null)
            {
                // Mod introuvable
                return;
            }

            Panel panel = new Panel();

            panel.Width = flowMods.ClientSize.Width - 25;
            panel.Height = 90;

            PictureBox image = new PictureBox();

            image.Width = 70;
            image.Height = 70;
            image.Left = 10;
            image.Top = 10;
            image.SizeMode = PictureBoxSizeMode.Zoom;

            if (!string.IsNullOrEmpty(mod.IconUrl))
            {
                image.Image = await TéléchargerImageAsync(mod.IconUrl);
            }

            Label nom = new Label();

            nom.Text = mod.Title;
            nom.Left = 90;
            nom.Top = 10;
            nom.AutoSize = true;

            Label description = new Label();

            description.Text = mod.Description;
            description.Left = 90;
            description.Top = 35;
            description.Width = 300;
            description.Height = 40;

            panel.Controls.Add(image);
            panel.Controls.Add(nom);
            panel.Controls.Add(description);

            flowMods.Controls.Add(panel);

            string[] mods = Directory.GetFiles(dossierMods,"*.jar");

            foreach (string mod in mods)
            {
                await AjouterMod(mod);
            }
        }*/
    }
}