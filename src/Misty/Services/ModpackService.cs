using Misty.Models;
using System.IO.Compression;
using System.Text.Json;

namespace Misty.Services
{
    /// <summary>
    /// Installation d'un modpack dans un nouveau profil :
    /// - Modrinth (.mrpack) : https://support.modrinth.com/en/articles/8802351-modrinth-modpack-format-mrpack
    /// - CurseForge (zip + manifest.json)
    /// </summary>
    internal static class ModpackService
    {
        /// <summary>Fichier qu'on n'a pas le droit de télécharger automatiquement (choix de l'auteur sur CurseForge).</summary>
        public record FichierBloque(string Nom, string Page, string Dossier);

        public record Resultat(Instance Instance, List<FichierBloque> Bloques);

        private static readonly JsonSerializerOptions json = new() { PropertyNameCaseInsensitive = true };

        public static Task<Resultat> InstallerAsync(ProjetDistant projet, IProgress<string> progression) =>
            projet.Source == Sources.CurseForge
                ? InstallerCurseForgeAsync(projet, progression)
                : InstallerModrinthAsync(projet, progression);

        // ───────────────────────── Modrinth ─────────────────────────

        private static async Task<Resultat> InstallerModrinthAsync(ProjetDistant projet, IProgress<string> progression)
        {
            progression.Report("Recherche de la dernière version…");
            var versions = await ModrinthApi.VersionsAsync(projet.Id, null, null);
            var version = versions.FirstOrDefault(v => v.VersionType == "release") ?? versions.FirstOrDefault()
                ?? throw new InvalidOperationException("Ce modpack n'a aucune version publiée.");
            var fichier = version.Files.FirstOrDefault(f => f.Filename.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("Cette version ne contient pas de fichier .mrpack.");

            return await AvecArchiveAsync(projet, fichier.Url, fichier.Hashes.GetValueOrDefault("sha1"), progression, async (zip, creer) =>
            {
                var index = await LireJsonAsync<IndexModpack>(zip, "modrinth.index.json");
                string versionMc = index.Dependencies.GetValueOrDefault("minecraft")
                    ?? throw new InvalidDataException("Le modpack n'indique pas sa version de Minecraft.");
                var (loader, versionLoader) = LoaderModrinth(index.Dependencies);

                var instance = creer(versionMc, loader, versionLoader, version.VersionNumber);

                // Fichiers à télécharger (sauf ceux réservés au serveur)
                var fichiers = index.Files
                    .Where(f => f.Env == null || f.Env.GetValueOrDefault("client") != "unsupported")
                    .ToList();
                await EnParalleleAsync(fichiers, progression,
                    f => TelechargerAvecSecoursAsync(f, CheminSur(instance.Dossier, f.Path)));

                progression.Report("Copie des fichiers de configuration…");
                Extraire(zip, "overrides/", instance.Dossier);
                Extraire(zip, "client-overrides/", instance.Dossier);

                return new Resultat(instance, new());
            });
        }

        private static (string Loader, string? Version) LoaderModrinth(Dictionary<string, string> dependances)
        {
            if (dependances.TryGetValue("fabric-loader", out var v)) return (ModLoaderService.Fabric, v);
            if (dependances.TryGetValue("quilt-loader", out v)) return (ModLoaderService.Quilt, v);
            if (dependances.TryGetValue("neoforge", out v)) return (ModLoaderService.NeoForge, v);
            if (dependances.TryGetValue("forge", out v)) return (ModLoaderService.Forge, v);
            return (ModLoaderService.Vanilla, null);
        }

        private static async Task TelechargerAvecSecoursAsync(FichierModpack fichier, string destination)
        {
            Exception? derniere = null;
            foreach (var url in fichier.Downloads)
            {
                try
                {
                    await Telechargement.FichierAsync(url, destination, fichier.Hashes.GetValueOrDefault("sha1"));
                    return;
                }
                catch (Exception ex)
                {
                    derniere = ex;
                }
            }
            throw new IOException($"Impossible de télécharger {fichier.Path}", derniere);
        }

        // ───────────────────────── CurseForge ─────────────────────────

        private static async Task<Resultat> InstallerCurseForgeAsync(ProjetDistant projet, IProgress<string> progression)
        {
            progression.Report("Recherche de la dernière version…");
            var fichiersPack = await CurseForgeApi.FichiersAsync(int.Parse(projet.Id), null);
            var fichierPack = fichiersPack.FirstOrDefault(f => f.ReleaseType == 1 && f.IsAvailable) ?? fichiersPack.FirstOrDefault()
                ?? throw new InvalidOperationException("Ce modpack n'a aucune version publiée.");
            if (string.IsNullOrEmpty(fichierPack.DownloadUrl))
                throw new InvalidOperationException("L'auteur de ce modpack n'autorise pas son téléchargement en dehors de CurseForge.");

            return await AvecArchiveAsync(projet, fichierPack.DownloadUrl, fichierPack.Sha1, progression, async (zip, creer) =>
            {
                var manifeste = await LireJsonAsync<ManifestCF>(zip, "manifest.json");
                var (loader, versionLoader) = LoaderCurseForge(manifeste.Minecraft.ModLoaders);

                var instance = creer(manifeste.Minecraft.Version, loader, versionLoader,
                    string.IsNullOrEmpty(manifeste.Version) ? fichierPack.DisplayName : manifeste.Version);

                // Infos des fichiers (URL, nom) et des projets (type, nom, icône)
                progression.Report("Lecture de la liste des mods…");
                var requis = manifeste.Files.Where(f => f.Required).ToList();
                var fichiers = (await CurseForgeApi.FichiersParIdsAsync(requis.Select(f => f.FileID))).ToDictionary(f => f.Id);
                var mods = (await CurseForgeApi.ModsAsync(requis.Select(f => f.ProjectID))).ToDictionary(m => m.Id);

                var bloques = new List<FichierBloque>();
                var metas = new List<ContenuMeta>();
                var aTelecharger = new List<(FichierCF Fichier, string Destination)>();

                foreach (var entree in requis)
                {
                    if (!fichiers.TryGetValue(entree.FileID, out var fichier)) continue;
                    mods.TryGetValue(entree.ProjectID, out var mod);

                    string type = CurseForgeApi.TypeDepuisClasse(mod?.ClassId ?? CurseForgeApi.ClasseMods);
                    string dossier = ContentService.Dossier(instance, type);
                    string destination = CheminSur(instance.Dossier, Path.Combine(Path.GetRelativePath(instance.Dossier, dossier), Path.GetFileName(fichier.FileName)));

                    if (string.IsNullOrEmpty(fichier.DownloadUrl))
                    {
                        bloques.Add(new FichierBloque(mod?.Name ?? fichier.DisplayName,
                            CurseForgeApi.PageFichier(mod, entree.ProjectID, entree.FileID), Path.GetFileName(dossier)));
                        continue;
                    }

                    aTelecharger.Add((fichier, destination));
                    metas.Add(new ContenuMeta
                    {
                        Chemin = Path.GetRelativePath(instance.Dossier, destination).Replace('\\', '/'),
                        Source = Sources.CurseForge,
                        ProjectId = entree.ProjectID.ToString(),
                        VersionId = entree.FileID.ToString(),
                        Titre = mod?.Name ?? fichier.DisplayName,
                        VersionNom = fichier.DisplayName,
                        IconeUrl = mod?.Logo?.ThumbnailUrl,
                        Type = type,
                    });
                }

                await EnParalleleAsync(aTelecharger, progression,
                    t => Telechargement.FichierAsync(t.Fichier.DownloadUrl!, t.Destination, t.Fichier.Sha1));
                ContentService.EnregistrerMetas(instance, metas);

                progression.Report("Copie des fichiers de configuration…");
                Extraire(zip, (string.IsNullOrEmpty(manifeste.Overrides) ? "overrides" : manifeste.Overrides.Trim('/')) + "/", instance.Dossier);

                return new Resultat(instance, bloques);
            });
        }

        /// <summary>"forge-47.2.0" → (Forge, "47.2.0"). Le loader principal est privilégié.</summary>
        private static (string Loader, string? Version) LoaderCurseForge(List<LoaderManifestCF> loaders)
        {
            var principal = loaders.FirstOrDefault(l => l.Primary) ?? loaders.FirstOrDefault();
            if (principal == null) return (ModLoaderService.Vanilla, null);

            int tiret = principal.Id.IndexOf('-');
            string nom = tiret < 0 ? principal.Id : principal.Id[..tiret];
            string? version = tiret < 0 ? null : principal.Id[(tiret + 1)..];

            return nom.ToLowerInvariant() switch
            {
                "forge" => (ModLoaderService.Forge, version),
                "neoforge" => (ModLoaderService.NeoForge, version),
                "fabric" => (ModLoaderService.Fabric, version),
                "quilt" => (ModLoaderService.Quilt, version),
                _ => throw new InvalidDataException($"Mod loader non pris en charge : {principal.Id}"),
            };
        }

        // ───────────────────────── Commun ─────────────────────────

        /// <summary>
        /// Télécharge l'archive du modpack, puis appelle <paramref name="installer"/> avec une fonction qui crée le profil.
        /// Si quelque chose échoue, le profil à moitié créé est supprimé.
        /// </summary>
        private static async Task<Resultat> AvecArchiveAsync(ProjetDistant projet, string url, string? sha1, IProgress<string> progression,
            Func<ZipArchive, Func<string, string, string?, string, Instance>, Task<Resultat>> installer)
        {
            string temporaire = Path.Combine(Path.GetTempPath(), $"misty-{Guid.NewGuid():N}.zip");
            Instance? instance = null;
            try
            {
                progression.Report("Téléchargement du modpack…");
                await Telechargement.FichierAsync(url, temporaire, sha1);

                using var zip = ZipFile.OpenRead(temporaire);
                return await installer(zip, (versionMc, loader, versionLoader, versionPack) =>
                {
                    instance = InstanceStore.Creer(projet.Titre, versionMc, loader, versionLoader);
                    instance.IconeUrl = projet.IconeUrl;
                    instance.ModpackSource = projet.Source;
                    instance.ModpackProjectId = projet.Id;
                    instance.ModpackVersion = versionPack;
                    InstanceStore.Sauver(instance);
                    return instance;
                });
            }
            catch
            {
                if (instance != null)
                {
                    try { InstanceStore.Supprimer(instance); } catch { }
                }
                throw;
            }
            finally
            {
                try { File.Delete(temporaire); } catch { }
            }
        }

        private static async Task<T> LireJsonAsync<T>(ZipArchive zip, string nom)
        {
            var entree = zip.GetEntry(nom) ?? throw new InvalidDataException($"{nom} manquant dans le modpack.");
            using var flux = entree.Open();
            return await JsonSerializer.DeserializeAsync<T>(flux, json) ?? throw new InvalidDataException($"{nom} illisible.");
        }

        /// <summary>Exécute les téléchargements 6 par 6 en affichant l'avancement.</summary>
        private static async Task EnParalleleAsync<T>(IReadOnlyList<T> elements, IProgress<string> progression, Func<T, Task> action)
        {
            int faits = 0;
            using var limite = new SemaphoreSlim(6);
            await Task.WhenAll(elements.Select(async e =>
            {
                await limite.WaitAsync();
                try
                {
                    await action(e);
                    progression.Report($"Téléchargement des fichiers… {Interlocked.Increment(ref faits)}/{elements.Count}");
                }
                finally
                {
                    limite.Release();
                }
            }));
        }

        private static void Extraire(ZipArchive zip, string prefixe, string racine)
        {
            foreach (var entree in zip.Entries)
            {
                if (!entree.FullName.StartsWith(prefixe, StringComparison.OrdinalIgnoreCase) || entree.Name.Length == 0)
                    continue; // hors du préfixe, ou dossier

                string destination = CheminSur(racine, entree.FullName[prefixe.Length..]);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entree.ExtractToFile(destination, overwrite: true);
            }
        }

        /// <summary>Refuse les chemins qui sortiraient du dossier du profil ("../", chemins absolus…).</summary>
        private static string CheminSur(string racine, string relatif)
        {
            string racineComplete = Path.GetFullPath(racine) + Path.DirectorySeparatorChar;
            string complet = Path.GetFullPath(Path.Combine(racine, relatif));
            if (!complet.StartsWith(racineComplete, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Chemin invalide dans le modpack : " + relatif);
            return complet;
        }
    }
}
