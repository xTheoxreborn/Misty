using Misty.Models;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;

namespace Misty.Services
{
    /// <summary>
    /// Client minimal de l'API CurseForge v1 (https://docs.curseforge.com/rest-api/).
    ///
    /// L'API exige une clé personnelle (console.curseforge.com), qui doit rester privée :
    /// elle est lue depuis src/Misty/curseforge.key (ignoré par git, intégré à l'exe au build)
    /// ou depuis la variable d'environnement MISTY_CURSEFORGE_KEY.
    /// </summary>
    internal static class CurseForgeApi
    {
        private const string Base = "https://api.curseforge.com/v1/";
        private const int IdMinecraft = 432;

        // Classes CurseForge (types de projets) pour Minecraft
        public const int ClasseMods = 6;
        public const int ClassePacksRessources = 12;
        public const int ClasseShaders = 6552;
        public const int ClasseDatapacks = 6945;
        public const int ClasseModpacks = 4471;

        /// <summary>Clé API, ou null si elle n'est pas configurée.</summary>
        public static string? Cle { get; } = LireCle();

        public static bool Disponible => Cle != null;

        private static readonly HttpClient http = CreerClient();

        private static readonly JsonSerializerOptions json = new() { PropertyNameCaseInsensitive = true };

        public static readonly (string Cle, string Libelle)[] Tris =
        {
            ("2", "Popularité"),
            ("6", "Téléchargements"),
            ("3", "Mis à jour"),
            ("1", "En vedette"),
            ("4", "Nom"),
        };

        private static string? LireCle()
        {
            string? cle = Environment.GetEnvironmentVariable("MISTY_CURSEFORGE_KEY");
            if (string.IsNullOrWhiteSpace(cle))
            {
                using var flux = Assembly.GetExecutingAssembly().GetManifestResourceStream("Misty.curseforge.key");
                if (flux != null)
                    cle = new StreamReader(flux).ReadToEnd();
            }
            cle = cle?.Trim();
            return string.IsNullOrEmpty(cle) ? null : cle;
        }

        private static HttpClient CreerClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"{AppInfo.GitHubRepo}/{AppInfo.Version} (github.com/{AppInfo.GitHubRepo})");
            if (Cle != null)
                client.DefaultRequestHeaders.Add("x-api-key", Cle);
            return client;
        }

        private static void VerifierCle()
        {
            if (Cle == null)
                throw new InvalidOperationException("Clé API CurseForge manquante : colle-la dans src/Misty/curseforge.key puis recompile.");
        }

        public static int Classe(string type) => type switch
        {
            ContentService.Mod => ClasseMods,
            ContentService.PackRessources => ClassePacksRessources,
            ContentService.Shader => ClasseShaders,
            ContentService.Datapack => ClasseDatapacks,
            ContentService.Modpack => ClasseModpacks,
            _ => throw new ArgumentException("Type inconnu : " + type),
        };

        /// <summary>Type de contenu Misty correspondant à une classe CurseForge (pour ranger les fichiers d'un modpack).</summary>
        public static string TypeDepuisClasse(int classe) => classe switch
        {
            ClassePacksRessources => ContentService.PackRessources,
            ClasseShaders => ContentService.Shader,
            _ => ContentService.Mod,
        };

        /// <summary>Identifiant CurseForge du loader (ModLoaderType), ou null.</summary>
        public static int? TypeLoader(string loader) => loader switch
        {
            ModLoaderService.Forge => 1,
            ModLoaderService.LiteLoader => 3,
            ModLoaderService.Fabric => 4,
            ModLoaderService.Quilt => 5,
            ModLoaderService.NeoForge => 6,
            _ => null,
        };

        /// <summary>Noms de loaders acceptés dans GameVersions d'un fichier de mod.</summary>
        public static string[] NomsLoader(string loader) => loader switch
        {
            ModLoaderService.Quilt => new[] { "Quilt", "Fabric" }, // Quilt charge aussi les mods Fabric
            _ => new[] { loader },
        };

        // ───────────────────────── Requêtes ─────────────────────────

        public static async Task<(List<ModCF> Mods, int Total)> RechercherAsync(string requete, int classe, string? versionMc,
            int? typeLoader, string tri, int index, int taille)
        {
            VerifierCle();
            // CurseForge refuse index + pageSize > 10 000
            taille = Math.Min(taille, 10_000 - index);
            if (taille <= 0) return (new(), index);

            string url = $"mods/search?gameId={IdMinecraft}&classId={classe}&searchFilter={Uri.EscapeDataString(requete)}"
                       + $"&sortField={tri}&sortOrder=desc&index={index}&pageSize={taille}";
            if (!string.IsNullOrEmpty(versionMc)) url += "&gameVersion=" + Uri.EscapeDataString(versionMc);
            if (typeLoader != null) url += "&modLoaderType=" + typeLoader;

            var reponse = await http.GetFromJsonAsync<ReponseCF<List<ModCF>>>(Base + url, json);
            return (reponse?.Data ?? new(), Math.Min(reponse?.Pagination?.TotalCount ?? 0, 10_000));
        }

        /// <summary>Fichiers d'un projet (les plus récents d'abord), filtrés sur une version de Minecraft.</summary>
        public static async Task<List<FichierCF>> FichiersAsync(int modId, string? versionMc)
        {
            VerifierCle();
            var fichiers = new List<FichierCF>();
            for (int index = 0; index < 500; index += 50) // on se limite aux 500 fichiers les plus récents
            {
                string url = $"mods/{modId}/files?index={index}&pageSize=50";
                if (!string.IsNullOrEmpty(versionMc)) url += "&gameVersion=" + Uri.EscapeDataString(versionMc);

                var reponse = await http.GetFromJsonAsync<ReponseCF<List<FichierCF>>>(Base + url, json);
                var page = reponse?.Data ?? new();
                fichiers.AddRange(page);
                if (page.Count < 50 || fichiers.Count >= (reponse?.Pagination?.TotalCount ?? 0)) break;
            }
            return fichiers.OrderByDescending(f => f.FileDate).ToList();
        }

        public static async Task<List<FichierCF>> FichiersParIdsAsync(IEnumerable<int> ids)
        {
            VerifierCle();
            var resultat = new List<FichierCF>();
            foreach (var paquet in ids.Distinct().Chunk(500))
            {
                using var reponse = await http.PostAsJsonAsync(Base + "mods/files", new { fileIds = paquet });
                reponse.EnsureSuccessStatusCode();
                resultat.AddRange((await reponse.Content.ReadFromJsonAsync<ReponseCF<List<FichierCF>>>(json))?.Data ?? new());
            }
            return resultat;
        }

        public static async Task<List<ModCF>> ModsAsync(IEnumerable<int> ids)
        {
            VerifierCle();
            var resultat = new List<ModCF>();
            foreach (var paquet in ids.Distinct().Chunk(500))
            {
                using var reponse = await http.PostAsJsonAsync(Base + "mods", new { modIds = paquet });
                reponse.EnsureSuccessStatusCode();
                resultat.AddRange((await reponse.Content.ReadFromJsonAsync<ReponseCF<List<ModCF>>>(json))?.Data ?? new());
            }
            return resultat;
        }

        /// <summary>Identifie des fichiers par leur empreinte CurseForge (voir <see cref="Empreinte"/>).</summary>
        public static async Task<List<CorrespondanceCF>> EmpreintesAsync(IEnumerable<uint> empreintes)
        {
            VerifierCle();
            var liste = empreintes.Distinct().ToList();
            if (liste.Count == 0) return new();

            using var reponse = await http.PostAsJsonAsync(Base + $"fingerprints/{IdMinecraft}", new { fingerprints = liste });
            reponse.EnsureSuccessStatusCode();
            return (await reponse.Content.ReadFromJsonAsync<ReponseCF<EmpreintesCF>>(json))?.Data?.ExactMatches ?? new();
        }

        /// <summary>Page du fichier sur le site CurseForge (pour un téléchargement manuel).</summary>
        public static string PageFichier(ModCF? mod, int modId, int fileId) =>
            mod?.Links?.WebsiteUrl is { Length: > 0 } site
                ? $"{site.TrimEnd('/')}/files/{fileId}"
                : $"https://www.curseforge.com/projects/{modId}";

        /// <summary>
        /// Empreinte CurseForge d'un fichier : MurmurHash2 (graine 1) calculé en ignorant
        /// les octets d'espacement (tabulation, retour chariot, saut de ligne, espace).
        /// </summary>
        public static uint Empreinte(string chemin)
        {
            var octets = File.ReadAllBytes(chemin).Where(b => b is not (9 or 10 or 13 or 32)).ToArray();

            const uint m = 0x5bd1e995;
            const int r = 24;
            uint longueur = (uint)octets.Length;
            uint h = 1 ^ longueur;
            int i = 0;

            while (longueur >= 4)
            {
                uint k = (uint)(octets[i] | octets[i + 1] << 8 | octets[i + 2] << 16 | octets[i + 3] << 24);
                k *= m;
                k ^= k >> r;
                k *= m;
                h *= m;
                h ^= k;
                i += 4;
                longueur -= 4;
            }

            switch (longueur)
            {
                case 3:
                    h ^= (uint)octets[i + 2] << 16;
                    goto case 2;
                case 2:
                    h ^= (uint)octets[i + 1] << 8;
                    goto case 1;
                case 1:
                    h ^= octets[i];
                    h *= m;
                    break;
            }

            h ^= h >> 13;
            h *= m;
            h ^= h >> 15;
            return h;
        }

        /// <summary>Conversion vers le modèle commun de l'explorateur.</summary>
        public static ProjetDistant VersProjet(ModCF mod) => new()
        {
            Source = Sources.CurseForge,
            Id = mod.Id.ToString(),
            Titre = mod.Name,
            Description = mod.Summary,
            Auteur = mod.Authors.FirstOrDefault()?.Name ?? "",
            Telechargements = mod.DownloadCount,
            IconeUrl = mod.Logo?.ThumbnailUrl ?? mod.Logo?.Url,
            Categories = mod.Categories.Select(c => c.Name).ToList(),
        };
    }
}
