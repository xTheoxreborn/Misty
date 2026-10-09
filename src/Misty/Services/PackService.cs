using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

namespace Misty.Services
{
    internal static class PackService
    {
        private static readonly HttpClient http = new();

        public static string DossierTemplates => Path.Combine(AppPaths.DataDir, "templates");
        public static string DossierPacks => Path.Combine(AppPaths.DataDir, "packs");

        /// <summary>Télécharge (une seule fois) le template vanilla d'une version. Renvoie (dossier, pack_format).</summary>
        public static async Task<(string Dossier, int Format)> TemplateAsync(string versionMc, IProgress<string> progression)
        {
            string dossier = Path.Combine(DossierTemplates, versionMc);
            string marqueur = Path.Combine(dossier, ".ok");
            if (File.Exists(marqueur))
                return (dossier, int.Parse(File.ReadAllText(Path.Combine(dossier, ".format"))));

            progression.Report("Recherche de la version…");
            using var manifeste = JsonDocument.Parse(await http.GetStringAsync(
                "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json"));
            string urlVersion = manifeste.RootElement.GetProperty("versions").EnumerateArray()
                .First(v => v.GetProperty("id").GetString() == versionMc).GetProperty("url").GetString()!;

            using var version = JsonDocument.Parse(await http.GetStringAsync(urlVersion));
            var client = version.RootElement.GetProperty("downloads").GetProperty("client");

            progression.Report("Téléchargement du template…");
            string jar = Path.Combine(Path.GetTempPath(), $"misty-{Guid.NewGuid():N}.jar");
            try
            {
                await Telechargement.FichierAsync(client.GetProperty("url").GetString()!, jar, client.GetProperty("sha1").GetString());

                progression.Report("Extraction des textures…");
                Directory.CreateDirectory(dossier);
                using var zip = ZipFile.OpenRead(jar);
                foreach (var entree in zip.Entries)
                {
                    bool utile = entree.FullName.StartsWith("assets/minecraft/textures/") || entree.FullName == "pack.png";
                    if (!utile || entree.Name.Length == 0) continue;

                    string destination = Path.Combine(dossier, entree.FullName.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    entree.ExtractToFile(destination, overwrite: true);
                }

                int format = LireFormat(zip);
                File.WriteAllText(Path.Combine(dossier, ".format"), format.ToString());
                File.WriteAllText(marqueur, "");
                return (dossier, format);
            }
            finally
            {
                try { File.Delete(jar); } catch { }
            }
        }

        /// <summary>Le jar contient version.json avec le pack_format des packs de ressources.</summary>
        private static int LireFormat(ZipArchive jar)
        {
            var entree = jar.GetEntry("version.json");
            if (entree == null) return 1;
            using var doc = JsonDocument.Parse(entree.Open());
            if (!doc.RootElement.TryGetProperty("pack_version", out var pv)) return 1;

            if (pv.ValueKind == JsonValueKind.Number) return pv.GetInt32();          // anciennes versions
            if (pv.TryGetProperty("resource", out var r)) return r.GetInt32();       // 1.20.2+
            if (pv.TryGetProperty("resource_major", out var m)) return m.GetInt32(); // versions très récentes
            return 1;
        }

        /// <summary>Crée le dossier du pack avec son pack.mcmeta.</summary>
        public static string CreerPack(string nom, string description, int format, string versionMc)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) nom = nom.Replace(c, '_');
            string dossier = Path.Combine(DossierPacks, nom);
            if (Directory.Exists(dossier))
                throw new IOException("Un pack portant ce nom existe déjà.");

            Directory.CreateDirectory(dossier);
            var mcmeta = new { pack = new { pack_format = format, description } };
            File.WriteAllText(Path.Combine(dossier, "pack.mcmeta"),
                JsonSerializer.Serialize(mcmeta, new JsonSerializerOptions { WriteIndented = true }));

            // Infos propres à Misty (ignorées par Minecraft)
            File.WriteAllText(Path.Combine(dossier, "misty.json"),
                JsonSerializer.Serialize(new { versionMc }));
            return dossier;
        }

        /// <summary>
        /// Copie une texture du template vers le pack (si pas déjà fait) et l'ouvre dans l'éditeur.
        /// cheminRelatif ex. : "assets/minecraft/textures/block/stone.png"
        /// </summary>
        public static void ModifierTexture(string dossierPack, string dossierTemplate, string cheminRelatif, string? editeur)
        {
            string source = Path.Combine(dossierTemplate, cheminRelatif);
            string copie = Path.Combine(dossierPack, cheminRelatif);

            if (!File.Exists(copie))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(copie)!);
                File.Copy(source, copie);
            }

            var info = string.IsNullOrWhiteSpace(editeur)
                ? new ProcessStartInfo(copie) { UseShellExecute = true }                       // appli par défaut
                : new ProcessStartInfo(editeur, $"\"{copie}\"") { UseShellExecute = true };    // éditeur choisi
            Process.Start(info);
        }

        /// <summary>Remet une texture à l'original : on supprime simplement la copie du pack.</summary>
        public static void Restaurer(string dossierPack, string cheminRelatif)
        {
            string copie = Path.Combine(dossierPack, cheminRelatif);
            if (File.Exists(copie)) File.Delete(copie);
        }
        public record PackInfo(string Nom, string Description, string Dossier, int NbTextures, string VersionMc);

        public static List<PackInfo> Lister()
        {
            var liste = new List<PackInfo>();
            if (!Directory.Exists(DossierPacks)) return liste;

            foreach (var dossier in Directory.GetDirectories(DossierPacks))
            {
                string description = "";
                try
                {
                    string mcmeta = Path.Combine(dossier, "pack.mcmeta");
                    if (File.Exists(mcmeta))
                    {
                        using var doc = JsonDocument.Parse(File.ReadAllText(mcmeta));
                        if (doc.RootElement.GetProperty("pack").TryGetProperty("description", out var d)
                            && d.ValueKind == JsonValueKind.String)
                            description = d.GetString() ?? "";
                    }
                }
                catch { /* mcmeta illisible : on affiche quand même le pack */ }

                // Version de Minecraft enregistrée à la création du pack
                string versionMc = "";
                try
                {
                    string infos = Path.Combine(dossier, "misty.json");
                    if (File.Exists(infos))
                    {
                        using var doc = JsonDocument.Parse(File.ReadAllText(infos));
                        if (doc.RootElement.TryGetProperty("versionMc", out var v))
                            versionMc = v.GetString() ?? "";
                    }
                }
                catch { /* misty.json illisible : on affiche quand même le pack */ }

                int nb = Directory.Exists(Path.Combine(dossier, "assets"))
                    ? Directory.GetFiles(Path.Combine(dossier, "assets"), "*.png", SearchOption.AllDirectories).Length
                    : 0;

                liste.Add(new PackInfo(Path.GetFileName(dossier), description, dossier, nb, versionMc));
            }

            return liste.OrderBy(p => p.Nom).ToList();
        }

    }
}