using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Misty.Services
{
    internal static class PackService
    {
        private static readonly HttpClient http = new();

        public static string DossierTemplates => Path.Combine(AppPaths.DataDir, "templates");
        public static string DossierPacks => Path.Combine(AppPaths.DataDir, "packs");

        public const string RacineTextures = "assets/minecraft/textures/";

        public record PackInfo(string Nom, string Description, string Dossier, int NbTextures, string VersionMc)
        {
            /// <summary>pack.png du pack, sinon celle de Minecraft (template), sinon null.</summary>
            public ImageSource? Icone => ChargerIcone(Path.Combine(Dossier, "pack.png"))
                ?? ChargerIcone(Path.Combine(DossierTemplates, VersionMc, "pack.png"));

            public bool APackPng => File.Exists(Path.Combine(Dossier, "pack.png"));
        }

        /// <summary>Télécharge (une seule fois) le template vanilla d'une version. Renvoie (dossier, pack_format).</summary>
        public static async Task<(string Dossier, int Format)> TemplateAsync(string versionMc, IProgress<string> progression)
        {
            string dossier = Path.Combine(DossierTemplates, versionMc);
            string marqueur = Path.Combine(dossier, ".ok");
            if (File.Exists(marqueur) && int.TryParse(File.ReadAllText(Path.Combine(dossier, ".format")), out int dejaLa))
                return (dossier, dejaLa);

            progression.Report(Loc.T("Pack_RechercheVersion"));
            using var manifeste = JsonDocument.Parse(await http.GetStringAsync(
                "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json"));
            string urlVersion = manifeste.RootElement.GetProperty("versions").EnumerateArray()
                .Where(v => v.GetProperty("id").GetString() == versionMc)
                .Select(v => v.GetProperty("url").GetString())
                .FirstOrDefault()
                ?? throw new InvalidOperationException(Loc.T("Pack_VersionInconnue", versionMc));

            using var version = JsonDocument.Parse(await http.GetStringAsync(urlVersion));
            var client = version.RootElement.GetProperty("downloads").GetProperty("client");

            progression.Report(Loc.T("Pack_TelechargementTemplate"));
            string jar = Path.Combine(Path.GetTempPath(), $"misty-{Guid.NewGuid():N}.jar");
            try
            {
                await Telechargement.FichierAsync(client.GetProperty("url").GetString()!, jar, client.GetProperty("sha1").GetString());

                progression.Report(Loc.T("Pack_Extraction"));
                if (Directory.Exists(dossier)) Directory.Delete(dossier, true); // extraction précédente interrompue
                Directory.CreateDirectory(dossier);
                using var zip = ZipFile.OpenRead(jar);
                foreach (var entree in zip.Entries)
                {
                    bool utile = entree.FullName.StartsWith(RacineTextures) || entree.FullName == "pack.png";
                    if (!utile || entree.Name.Length == 0) continue;

                    string destination = Path.Combine(dossier, entree.FullName.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    entree.ExtractToFile(destination, overwrite: true);
                }

                if (ListerTextures(dossier).Count == 0)
                    throw new InvalidOperationException(Loc.T("Pack_VersionTropAncienne", versionMc));

                int format = LireFormat(zip) ?? FormatDepuisNom(versionMc);
                File.WriteAllText(Path.Combine(dossier, ".format"), format.ToString());
                File.WriteAllText(marqueur, "");
                return (dossier, format);
            }
            finally
            {
                try { File.Delete(jar); } catch { }
            }
        }

        /// <summary>Le jar contient version.json (1.14+) avec le pack_format des packs de ressources.</summary>
        private static int? LireFormat(ZipArchive jar)
        {
            var entree = jar.GetEntry("version.json");
            if (entree == null) return null;
            using var doc = JsonDocument.Parse(entree.Open());
            if (!doc.RootElement.TryGetProperty("pack_version", out var pv)) return null;

            if (pv.ValueKind == JsonValueKind.Number) return pv.GetInt32();          // anciennes versions
            if (pv.TryGetProperty("resource", out var r)) return r.GetInt32();       // 1.20.2+
            if (pv.TryGetProperty("resource_major", out var m)) return m.GetInt32(); // 1.21.9+
            return null;
        }

        /// <summary>Avant la 1.14, le jar n'indique pas le format : on le déduit du numéro de version.</summary>
        private static int FormatDepuisNom(string versionMc)
        {
            var morceaux = versionMc.Split('.', '-', ' ');
            if (morceaux.Length < 2 || morceaux[0] != "1" || !int.TryParse(morceaux[1], out int mineure)) return 1;
            return mineure switch
            {
                <= 8 => 1,
                <= 10 => 2,
                <= 12 => 3,
                <= 14 => 4,
                _ => 4,
            };
        }

        /// <summary>Crée le dossier du pack avec son pack.mcmeta.</summary>
        public static string CreerPack(string nom, string description, int format, string versionMc)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) nom = nom.Replace(c, '_');
            nom = nom.Trim().TrimEnd('.');
            if (nom.Length == 0) throw new IOException(Loc.T("Pack_NomInvalide"));

            string dossier = Path.Combine(DossierPacks, nom);
            if (Directory.Exists(dossier))
                throw new IOException(Loc.T("Pack_ExisteDeja"));

            Directory.CreateDirectory(dossier);
            EcrireMcmeta(dossier, format, description);

            // Infos propres à Misty (ignorées par Minecraft)
            File.WriteAllText(Path.Combine(dossier, "misty.json"),
                JsonSerializer.Serialize(new { versionMc }));
            return dossier;
        }

        private static void EcrireMcmeta(string dossier, int format, string description)
        {
            var pack = new JsonObject { ["pack_format"] = format, ["description"] = description };
            // Depuis le format 65 (1.21.9), Minecraft exige min_format / max_format
            if (format >= 65)
            {
                pack["min_format"] = format;
                pack["max_format"] = format;
            }
            File.WriteAllText(Path.Combine(dossier, "pack.mcmeta"),
                new JsonObject { ["pack"] = pack }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        /// <summary>Remplace l'icône du pack (pack.png), redimensionnée en 128 px.</summary>
        public static void DefinirIcone(PackInfo pack, string image)
        {
            var source = new BitmapImage();
            source.BeginInit();
            source.UriSource = new Uri(image);
            source.DecodePixelWidth = 128;
            source.CacheOption = BitmapCacheOption.OnLoad;
            source.EndInit();

            var encodeur = new PngBitmapEncoder();
            encodeur.Frames.Add(BitmapFrame.Create(source));
            using var fichier = File.Create(Path.Combine(pack.Dossier, "pack.png"));
            encodeur.Save(fichier);
        }

        /// <summary>
        /// Copie une texture du template vers le pack (si pas déjà fait) et l'ouvre dans l'éditeur externe.
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

        /// <summary>Remet une texture à l'original : on supprime la copie du pack (et les dossiers devenus vides).</summary>
        public static void Restaurer(string dossierPack, string cheminRelatif)
        {
            string copie = Path.Combine(dossierPack, cheminRelatif);
            if (File.Exists(copie)) File.Delete(copie);

            var dossier = Path.GetDirectoryName(copie);
            while (dossier != null && dossier.Length > dossierPack.Length
                   && Directory.Exists(dossier) && !Directory.EnumerateFileSystemEntries(dossier).Any())
            {
                Directory.Delete(dossier);
                dossier = Path.GetDirectoryName(dossier);
            }
        }

        /// <summary>Zippe le pack (pack.mcmeta à la racine du zip, comme Minecraft l'attend). misty.json est exclu.</summary>
        public static void Exporter(PackInfo pack, string destination)
        {
            if (File.Exists(destination)) File.Delete(destination); // l'utilisateur a déjà confirmé l'écrasement

            using var zip = ZipFile.Open(destination, ZipArchiveMode.Create);

            foreach (var fichier in Directory.EnumerateFiles(pack.Dossier, "*", SearchOption.AllDirectories))
            {
                string relatif = Path.GetRelativePath(pack.Dossier, fichier).Replace('\\', '/');
                if (relatif.Equals("misty.json", StringComparison.OrdinalIgnoreCase)) continue;

                zip.CreateEntryFromFile(fichier, relatif, CompressionLevel.Optimal);
            }

            // Icône du pack : celle de Minecraft, si le pack n'en a pas
            if (!pack.APackPng)
            {
                string icone = Path.Combine(DossierTemplates, pack.VersionMc, "pack.png");
                if (File.Exists(icone))
                    zip.CreateEntryFromFile(icone, "pack.png", CompressionLevel.Optimal);
            }
        }

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

                string textures = Path.Combine(dossier, "assets", "minecraft", "textures");
                int nb = Directory.Exists(textures)
                    ? Directory.GetFiles(textures, "*.png", SearchOption.AllDirectories).Length
                    : 0;

                liste.Add(new PackInfo(Path.GetFileName(dossier), description, dossier, nb, versionMc));
            }

            return liste.OrderBy(p => p.Nom, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        private static string FichierEditeur => Path.Combine(AppPaths.DataDir, "editeur.txt");

        /// <summary>Chemin de l'éditeur d'images choisi (ex. paintdotnet.exe), ou null.</summary>
        public static string? Editeur
        {
            get => File.Exists(FichierEditeur) ? File.ReadAllText(FichierEditeur).Trim() : null;
            set => File.WriteAllText(FichierEditeur, value ?? "");
        }

        /// <summary>Toutes les textures PNG du template, ex. "block/stone.png".</summary>
        public static List<string> ListerTextures(string dossierTemplate)
        {
            string racine = Path.Combine(dossierTemplate, "assets", "minecraft", "textures");
            if (!Directory.Exists(racine)) return new();

            return Directory.GetFiles(racine, "*.png", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(racine, f).Replace('\\', '/'))
                .OrderBy(f => f)
                .ToList();
        }

        private static ImageSource? ChargerIcone(string chemin)
        {
            if (!File.Exists(chemin)) return null;
            try
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.CreateOptions = BitmapCreateOptions.IgnoreImageCache; // l'icône peut avoir changé
                img.UriSource = new Uri(chemin);
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch { return null; }
        }
    }
}
