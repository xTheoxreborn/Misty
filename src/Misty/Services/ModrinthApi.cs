using Misty.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace Misty.Services
{
    /// <summary>
    /// Client minimal de l'API Modrinth v2 (https://docs.modrinth.com/api/).
    /// </summary>
    internal static class ModrinthApi
    {
        private const string Base = "https://api.modrinth.com/v2/";

        private static readonly HttpClient http = CreerClient();

        private static readonly JsonSerializerOptions json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
        };

        private static HttpClient CreerClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            // Modrinth demande un User-Agent qui identifie l'application
            // Format "auteur/projet/version (contact)" : trop libre pour le parseur strict de .NET, d'où TryAddWithoutValidation
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"{AppInfo.GitHubRepo}/{AppInfo.Version} (github.com/{AppInfo.GitHubRepo})");
            return client;
        }

        /// <summary>Tris proposés par la recherche Modrinth.</summary>
        public static readonly (string Cle, string Libelle)[] Tris =
        {
            ("relevance", "Pertinence"),
            ("downloads", "Téléchargements"),
            ("follows", "Popularité"),
            ("newest", "Plus récents"),
            ("updated", "Mis à jour"),
        };

        /// <param name="type">mod, resourcepack, datapack, shader, modpack</param>
        /// <param name="categories">Catégories acceptées (OU), ex. le loader : ["fabric"].</param>
        public static async Task<ResultatRecherche> RechercherAsync(string requete, string type, string? versionMc,
            IEnumerable<string>? categories, string tri, int offset, int limite = 20)
        {
            var facettes = new List<string[]> { new[] { $"project_type:{type}" } };
            if (!string.IsNullOrEmpty(versionMc))
                facettes.Add(new[] { $"versions:{versionMc}" });
            if (categories?.Any() == true)
                facettes.Add(categories.Select(c => $"categories:{c}").ToArray());

            string url = $"search?query={Uri.EscapeDataString(requete)}&index={tri}&offset={offset}&limit={limite}"
                       + $"&facets={Uri.EscapeDataString(JsonSerializer.Serialize(facettes))}";

            return await http.GetFromJsonAsync<ResultatRecherche>(Base + url, json) ?? new ResultatRecherche();
        }

        /// <summary>Versions d'un projet, les plus récentes d'abord.</summary>
        public static async Task<List<VersionModrinth>> VersionsAsync(string projet, IEnumerable<string>? loaders, string? versionMc)
        {
            var parametres = new List<string>();
            if (loaders?.Any() == true)
                parametres.Add("loaders=" + Uri.EscapeDataString(JsonSerializer.Serialize(loaders)));
            if (!string.IsNullOrEmpty(versionMc))
                parametres.Add("game_versions=" + Uri.EscapeDataString(JsonSerializer.Serialize(new[] { versionMc })));

            string url = $"project/{Uri.EscapeDataString(projet)}/version" + (parametres.Count > 0 ? "?" + string.Join("&", parametres) : "");
            return await http.GetFromJsonAsync<List<VersionModrinth>>(Base + url, json) ?? new();
        }

        public static async Task<VersionModrinth?> VersionAsync(string versionId) =>
            await http.GetFromJsonAsync<VersionModrinth>(Base + "version/" + Uri.EscapeDataString(versionId), json);

        public static async Task<List<ProjetDetail>> ProjetsAsync(IEnumerable<string> ids)
        {
            var liste = ids.Distinct().ToList();
            if (liste.Count == 0) return new();
            string url = "projects?ids=" + Uri.EscapeDataString(JsonSerializer.Serialize(liste));
            return await http.GetFromJsonAsync<List<ProjetDetail>>(Base + url, json) ?? new();
        }

        /// <summary>Identifie des fichiers par leur SHA-1. Renvoie hash → version.</summary>
        public static async Task<Dictionary<string, VersionModrinth>> VersionsParHashAsync(IEnumerable<string> sha1)
        {
            var corps = new { hashes = sha1.Distinct().ToList(), algorithm = "sha1" };
            if (corps.hashes.Count == 0) return new();

            using var reponse = await http.PostAsJsonAsync(Base + "version_files", corps);
            reponse.EnsureSuccessStatusCode();
            return await reponse.Content.ReadFromJsonAsync<Dictionary<string, VersionModrinth>>(json) ?? new();
        }

        /// <summary>Conversion vers le modèle commun de l'explorateur.</summary>
        public static ProjetDistant VersProjet(ProjetModrinth p) => new()
        {
            Source = Sources.Modrinth,
            Id = p.ProjectId,
            Titre = p.Title,
            Description = p.Description,
            Auteur = p.Author,
            Telechargements = p.Downloads,
            IconeUrl = p.IconUrl,
            Categories = p.Categories,
        };

        public static string FormaterNombre(long n) => n switch
        {
            >= 1_000_000 => $"{n / 1_000_000.0:0.#} M",
            >= 1_000 => $"{n / 1_000.0:0.#} k",
            _ => n.ToString(),
        };
    }
}
