using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

namespace Misty.Services
{
    /// <summary>
    /// Mise à jour automatique via les releases GitHub.
    ///
    /// Fonctionnement : le launcher est installé dans un dossier "Misty-{version}". La mise à jour
    /// télécharge le zip de la dernière release à côté, l'extrait (=> "Misty-{nouvelle}"), note l'ancien
    /// dossier dans data.txt (clé Suppr) puis lance le nouvel exe. Au démarrage suivant, le nouvel exe
    /// supprime l'ancien dossier et met à jour le raccourci.
    /// </summary>
    internal static class UpdateService
    {
        public record ReleaseInfo(string Version, string UrlTelechargement);

        /// <summary>Renvoie la dernière release si elle est différente de la version actuelle, sinon null.</summary>
        public static async Task<ReleaseInfo?> VerifierAsync()
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Misty"); // obligatoire, GitHub refuse sans ça

            string json = await client.GetStringAsync($"https://api.github.com/repos/{AppInfo.GitHubRepo}/releases/latest");
            using var doc = JsonDocument.Parse(json);

            string version = (doc.RootElement.GetProperty("tag_name").GetString() ?? "").TrimStart('v');
            string? url = doc.RootElement.GetProperty("assets")[0].GetProperty("browser_download_url").GetString();

            if (string.IsNullOrEmpty(version) || string.IsNullOrEmpty(url) || !EstPlusRecente(version, AppInfo.Version))
                return null;

            return new ReleaseInfo(version, url);
        }

        /// <summary>
        /// Compare deux versions segment par segment ("0.2.3.9.5.1" &gt; "0.2.3.9.5").
        /// System.Version ne gère que 4 segments, d'où cette comparaison maison.
        /// </summary>
        public static bool EstPlusRecente(string distante, string locale)
        {
            int[] a = Segments(distante), b = Segments(locale);
            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                int x = i < a.Length ? a[i] : 0, y = i < b.Length ? b[i] : 0;
                if (x != y) return x > y;
            }
            return false;

            // "3v2" -> 3 : on ne garde que les chiffres de tête de chaque segment
            static int[] Segments(string v) => v.Split('.')
                .Select(s => int.TryParse(new string(s.TakeWhile(char.IsDigit).ToArray()), out int n) ? n : 0)
                .ToArray();
        }

        /// <summary>Télécharge, extrait et lance la nouvelle version. L'appelant doit ensuite quitter l'application.</summary>
        public static async Task InstallerAsync(ReleaseInfo release)
        {
            string dossierActuel = $"Misty-{AppInfo.Version}";
            string racine = AppPaths.AppDir.Split(dossierActuel)[0];
            string zip = Path.Combine(racine, $"Misty-{release.Version}.zip");
            string nouveauDossier = Path.Combine(racine, $"Misty-{release.Version}");

            using (var client = new HttpClient())
            using (var response = await client.GetAsync(release.UrlTelechargement))
            {
                response.EnsureSuccessStatusCode();
                using var fs = new FileStream(zip, FileMode.Create);
                await response.Content.CopyToAsync(fs);
            }

            await ZipFile.ExtractToDirectoryAsync(zip, racine);
            File.Delete(zip);

            SettingsStore.Set(SettingsStore.Suppr, Path.Combine(racine, dossierActuel));

            if (File.Exists(AppPaths.CustomIcon))
                File.Copy(AppPaths.CustomIcon, Path.Combine(nouveauDossier, "Icon.ico"), overwrite: true);

            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(nouveauDossier, "Misty.exe"),
                UseShellExecute = true
            });
        }

        /// <summary>
        /// Après une mise à jour : supprime l'ancien dossier et remet le raccourci sur le nouvel exe.
        /// Renvoie true si une mise à jour vient d'être finalisée.
        /// </summary>
        public static async Task<bool> FinaliserMiseAJourAsync()
        {
            string ancienDossier = SettingsStore.Get(SettingsStore.Suppr);
            if (string.IsNullOrEmpty(ancienDossier)) return false;

            await Task.Delay(1000); // laisse le temps à l'ancien exe de se fermer

            string raccourci = SettingsStore.Get(SettingsStore.Raccourci);
            if (!string.IsNullOrEmpty(raccourci))
                ShortcutService.Creer(raccourci);

            if (Directory.Exists(ancienDossier))
                Directory.Delete(ancienDossier, true);

            SettingsStore.Set(SettingsStore.Suppr, "");
            return true;
        }

        /// <summary>
        /// Migre l'ancien dossier de données C:\TEXT vers %LOCALAPPDATA%\Misty.
        /// Renvoie true si une migration a eu lieu.
        /// </summary>
        public static async Task<bool> MigrerAncienDossierAsync(Action avantCopie)
        {
            if (!Directory.Exists(AppPaths.LegacyDataDir)) return false;

            if (!Directory.Exists(AppPaths.DataDir))
            {
                avantCopie();
                await CopierDossierAsync(AppPaths.LegacyDataDir, AppPaths.DataDir);
            }

            Directory.Delete(AppPaths.LegacyDataDir, true);
            return true;
        }

        private static async Task CopierDossierAsync(string source, string destination)
        {
            Directory.CreateDirectory(destination);

            foreach (string fichier in Directory.GetFiles(source))
            {
                using var src = File.OpenRead(fichier);
                using var dst = File.Create(Path.Combine(destination, Path.GetFileName(fichier)));
                await src.CopyToAsync(dst);
            }

            foreach (string sousDossier in Directory.GetDirectories(source))
                await CopierDossierAsync(sousDossier, Path.Combine(destination, Path.GetFileName(sousDossier)));
        }
    }
}
