using System.Security.Cryptography;

namespace Misty.Services
{
    /// <summary>Téléchargement de fichiers (Modrinth, CurseForge) avec vérification d'intégrité.</summary>
    internal static class Telechargement
    {
        private static readonly HttpClient http = CreerClient();

        private static HttpClient CreerClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"{AppInfo.GitHubRepo}/{AppInfo.Version} (github.com/{AppInfo.GitHubRepo})");
            return client;
        }

        /// <summary>Télécharge un fichier (via un .part) et vérifie son SHA-1 s'il est connu.</summary>
        public static async Task FichierAsync(string url, string destination, string? sha1 = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            string temporaire = destination + ".part";

            using (var reponse = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                reponse.EnsureSuccessStatusCode();
                using var flux = await reponse.Content.ReadAsStreamAsync();
                using var fichier = File.Create(temporaire);
                await flux.CopyToAsync(fichier);
            }

            if (!string.IsNullOrEmpty(sha1) && !string.Equals(await Sha1Async(temporaire), sha1, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(temporaire);
                throw new InvalidDataException($"Fichier corrompu : {Path.GetFileName(destination)}");
            }

            File.Move(temporaire, destination, overwrite: true);
        }

        public static async Task<string> Sha1Async(string chemin)
        {
            using var flux = File.OpenRead(chemin);
            return Convert.ToHexStringLower(await SHA1.HashDataAsync(flux));
        }
    }
}
