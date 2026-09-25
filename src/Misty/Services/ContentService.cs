using Misty.Models;
using System.Text.Json;

namespace Misty.Services
{
    /// <summary>
    /// Contenu d'un profil : mods, packs de ressources, datapacks, shaders.
    /// Installation depuis Modrinth ou CurseForge (avec dépendances), liste, activation/désactivation, suppression.
    /// </summary>
    internal static class ContentService
    {
        public const string Mod = "mod";
        public const string PackRessources = "resourcepack";
        public const string Datapack = "datapack";
        public const string Shader = "shader";
        public const string Modpack = "modpack";

        /// <summary>Un fichier désactivé est renommé "xxx.jar.disabled" (même convention que Modrinth App / Prism).</summary>
        public const string SuffixeDesactive = ".disabled";

        private const string FichierMeta = "misty-contenu.json";

        // Projets Modrinth utiles pour les shaders
        public const string IdIris = "YL57xq9U";
        public const string IdOculus = "GchcoXML";

        public static readonly (string Type, string Libelle)[] Types =
        {
            (Mod, "Mods"),
            (PackRessources, "Packs de ressources"),
            (Datapack, "Datapacks"),
            (Shader, "Shaders"),
        };

        private static readonly JsonSerializerOptions json = new() { WriteIndented = true };

        public static string LibelleType(string type) => type switch
        {
            Mod => "Mod",
            PackRessources => "Pack de ressources",
            Datapack => "Datapack",
            Shader => "Shader",
            Modpack => "Modpack",
            _ => type,
        };

        public static bool SupporteMods(string loader) => loader is ModLoaderService.Fabric or ModLoaderService.Quilt
            or ModLoaderService.Forge or ModLoaderService.NeoForge or ModLoaderService.LiteLoader;

        /// <summary>Loaders Modrinth compatibles avec le profil pour ce type de contenu (null = pas de filtre).</summary>
        public static string[]? LoadersModrinth(Instance instance, string type) => type switch
        {
            Mod => instance.Loader switch
            {
                ModLoaderService.Fabric => new[] { "fabric" },
                ModLoaderService.Quilt => new[] { "quilt", "fabric" }, // Quilt charge aussi les mods Fabric
                ModLoaderService.Forge => new[] { "forge" },
                ModLoaderService.NeoForge => new[] { "neoforge" },
                ModLoaderService.LiteLoader => new[] { "liteloader" },
                _ => new[] { "__aucun__" },
            },
            PackRessources => new[] { "minecraft" },
            Datapack => new[] { "datapack" },
            Shader => instance.Loader == ModLoaderService.OptiFine ? new[] { "optifine" } : new[] { "iris", "optifine" },
            _ => null,
        };

        /// <summary>Catégories à passer à la recherche (le loader pour les mods et shaders).</summary>
        public static string[]? CategoriesRecherche(Instance instance, string type) =>
            type is Mod or Shader ? LoadersModrinth(instance, type) : null;

        public static string Dossier(Instance instance, string type, string? monde = null) => type switch
        {
            Mod => Path.Combine(instance.Dossier, "mods"),
            PackRessources => Path.Combine(instance.Dossier, "resourcepacks"),
            Shader => Path.Combine(instance.Dossier, "shaderpacks"),
            Datapack => Path.Combine(instance.Dossier, "saves", monde ?? throw new ArgumentException("Monde requis pour un datapack"), "datapacks"),
            _ => throw new ArgumentException("Type inconnu : " + type),
        };

        /// <summary>Mondes du profil, du plus récent au plus ancien.</summary>
        public static List<string> Mondes(Instance instance)
        {
            string saves = Path.Combine(instance.Dossier, "saves");
            if (!Directory.Exists(saves)) return new();
            return new DirectoryInfo(saves).GetDirectories()
                .OrderByDescending(d => d.LastWriteTime)
                .Select(d => d.Name)
                .ToList();
        }

        public static string NomSansSuffixe(string nomFichier)
        {
            if (nomFichier.EndsWith(SuffixeDesactive, StringComparison.OrdinalIgnoreCase))
                nomFichier = nomFichier[..^SuffixeDesactive.Length];
            return Path.GetFileNameWithoutExtension(nomFichier);
        }

        // ───────────────────────── Liste ─────────────────────────

        public static List<ContenuInstalle> Lister(Instance instance)
        {
            var metas = LireMeta(instance).GroupBy(m => m.Chemin, StringComparer.OrdinalIgnoreCase)
                                          .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);
            var resultat = new List<ContenuInstalle>();

            void Scanner(string dossier, string type, string[] extensions, string? monde = null)
            {
                if (!Directory.Exists(dossier)) return;
                foreach (var fichier in Directory.GetFiles(dossier))
                {
                    string nom = fichier.EndsWith(SuffixeDesactive, StringComparison.OrdinalIgnoreCase) ? fichier[..^SuffixeDesactive.Length] : fichier;
                    if (!extensions.Any(e => nom.EndsWith(e, StringComparison.OrdinalIgnoreCase))) continue;

                    metas.TryGetValue(Relatif(instance, fichier), out var meta);
                    resultat.Add(new ContenuInstalle { CheminFichier = fichier, Type = type, Monde = monde, Meta = meta });
                }
            }

            Scanner(Path.Combine(instance.Dossier, "mods"), Mod, new[] { ".jar", ".litemod" });
            Scanner(Path.Combine(instance.Dossier, "resourcepacks"), PackRessources, new[] { ".zip" });
            Scanner(Path.Combine(instance.Dossier, "shaderpacks"), Shader, new[] { ".zip" });
            foreach (var monde in Mondes(instance))
                Scanner(Dossier(instance, Datapack, monde), Datapack, new[] { ".zip" }, monde);

            return resultat.OrderBy(c => c.Type).ThenBy(c => c.Titre, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        /// <summary>Indique si un projet (d'une source donnée) est présent dans la liste.</summary>
        public static bool EstInstalle(IEnumerable<ContenuInstalle> contenus, string source, string id) =>
            contenus.Any(c => c.Meta?.Correspond(source, id) == true);

        /// <summary>
        /// Identifie les fichiers ajoutés à la main, pour afficher leur nom et leur icône :
        /// d'abord via Modrinth (SHA-1), puis via CurseForge (empreinte) pour ceux qui restent inconnus.
        /// Les fichiers introuvables sont mémorisés pour ne pas être re-hachés à chaque fois.
        /// </summary>
        public static async Task IdentifierAsync(Instance instance, IReadOnlyList<ContenuInstalle> contenus)
        {
            var inconnus = contenus.Where(c => c.Meta == null).ToList();
            if (inconnus.Count == 0) return;

            // Chaque fichier inconnu reçoit une métadonnée (vide si on ne le trouve nulle part)
            foreach (var contenu in inconnus)
                contenu.Meta = new ContenuMeta { Chemin = Relatif(instance, contenu.CheminFichier), Type = contenu.Type };

            // 1. Modrinth
            var parHash = new Dictionary<string, ContenuInstalle>();
            foreach (var contenu in inconnus)
            {
                try { parHash[await Task.Run(() => Telechargement.Sha1Async(contenu.CheminFichier))] = contenu; }
                catch { }
            }
            var versions = await ModrinthApi.VersionsParHashAsync(parHash.Keys);
            var projets = (await ModrinthApi.ProjetsAsync(versions.Values.Select(v => v.ProjectId))).ToDictionary(p => p.Id);

            foreach (var (hash, contenu) in parHash)
            {
                if (!versions.TryGetValue(hash, out var version) || !projets.TryGetValue(version.ProjectId, out var projet)) continue;
                var meta = contenu.Meta!;
                meta.Source = Sources.Modrinth;
                meta.ProjectId = projet.Id;
                meta.VersionId = version.Id;
                meta.Titre = projet.Title;
                meta.VersionNom = version.VersionNumber;
                meta.IconeUrl = projet.IconUrl;
            }

            // 2. CurseForge, pour ce qui n'est pas sur Modrinth
            var restants = inconnus.Where(c => c.Meta!.ProjectId == null).ToList();
            if (restants.Count > 0 && CurseForgeApi.Disponible)
            {
                try
                {
                    var parEmpreinte = new Dictionary<uint, ContenuInstalle>();
                    foreach (var contenu in restants)
                    {
                        try { parEmpreinte[await Task.Run(() => CurseForgeApi.Empreinte(contenu.CheminFichier))] = contenu; }
                        catch { }
                    }
                    var correspondances = await CurseForgeApi.EmpreintesAsync(parEmpreinte.Keys);
                    var mods = (await CurseForgeApi.ModsAsync(correspondances.Select(c => c.Id))).ToDictionary(m => m.Id);

                    foreach (var correspondance in correspondances)
                    {
                        if (!parEmpreinte.TryGetValue((uint)correspondance.File.FileFingerprint, out var contenu)
                            || !mods.TryGetValue(correspondance.Id, out var mod)) continue;

                        var meta = contenu.Meta!;
                        meta.Source = Sources.CurseForge;
                        meta.ProjectId = mod.Id.ToString();
                        meta.VersionId = correspondance.File.Id.ToString();
                        meta.Titre = mod.Name;
                        meta.VersionNom = correspondance.File.DisplayName;
                        meta.IconeUrl = mod.Logo?.ThumbnailUrl;
                    }
                }
                catch { } // CurseForge injoignable : on garde les noms de fichiers
            }

            EnregistrerMetas(instance, inconnus.Select(c => c.Meta!));
            foreach (var contenu in inconnus)
                contenu.Rafraichir();
        }

        /// <summary>Ajoute ou remplace des métadonnées (même chemin) dans misty-contenu.json.</summary>
        public static void EnregistrerMetas(Instance instance, IEnumerable<ContenuMeta> nouvelles)
        {
            var metas = LireMeta(instance);
            foreach (var meta in nouvelles)
            {
                metas.RemoveAll(m => m.Chemin.Equals(meta.Chemin, StringComparison.OrdinalIgnoreCase));
                metas.Add(meta);
            }
            EcrireMeta(instance, metas);
        }

        public static void Basculer(ContenuInstalle contenu)
        {
            string nouveau = contenu.Actif
                ? contenu.CheminFichier + SuffixeDesactive
                : contenu.CheminFichier[..^SuffixeDesactive.Length];
            File.Move(contenu.CheminFichier, nouveau);
            contenu.CheminFichier = nouveau;
            contenu.Rafraichir();
        }

        public static void Supprimer(Instance instance, ContenuInstalle contenu)
        {
            if (File.Exists(contenu.CheminFichier))
                File.Delete(contenu.CheminFichier);

            var metas = LireMeta(instance);
            metas.RemoveAll(m => m.Chemin.Equals(Relatif(instance, contenu.CheminFichier), StringComparison.OrdinalIgnoreCase));
            EcrireMeta(instance, metas);
        }

        // ───────────────────────── Installation ─────────────────────────

        /// <summary>
        /// Installe la meilleure version compatible d'un projet (et ses dépendances requises pour les mods).
        /// Remplace la version déjà installée du même projet. Renvoie les noms des projets installés.
        /// </summary>
        public static async Task<List<string>> InstallerAsync(Instance instance, string source, string projectId, string type,
            string? monde = null, IProgress<string>? progression = null)
        {
            var installes = new List<string>();
            var metas = LireMeta(instance);
            try
            {
                if (source == Sources.CurseForge)
                    await InstallerProjetCurseForgeAsync(instance, int.Parse(projectId), type, monde, metas, installes,
                        new HashSet<int>(), progression);
                else
                    await InstallerProjetAsync(instance, projectId, type, monde, null, metas, installes,
                        new HashSet<string>(), progression);
            }
            finally
            {
                EcrireMeta(instance, metas); // on garde ce qui a déjà été installé même en cas d'erreur
            }
            return installes;
        }

        private static async Task InstallerProjetAsync(Instance instance, string projectId, string type, string? monde,
            VersionModrinth? versionImposee, List<ContenuMeta> metas, List<string> installes, HashSet<string> visites,
            IProgress<string>? progression)
        {
            if (!visites.Add(projectId)) return;

            var version = versionImposee ?? MeilleureVersion(
                await ModrinthApi.VersionsAsync(projectId, LoadersModrinth(instance, type), instance.VersionMc));
            if (version == null)
            {
                string titre = (await ModrinthApi.ProjetsAsync(new[] { projectId })).FirstOrDefault()?.Title ?? projectId;
                throw new InvalidOperationException($"{titre} n'a pas de version compatible avec {instance.VersionMc} ({instance.Loader}).");
            }

            var fichier = version.FichierPrincipal ?? throw new InvalidOperationException("Cette version ne contient aucun fichier.");
            var projet = (await ModrinthApi.ProjetsAsync(new[] { projectId })).FirstOrDefault();
            projectId = projet?.Id ?? projectId; // on garde l'ID réel, même si on a reçu un slug
            visites.Add(projectId);
            string titreProjet = projet?.Title ?? fichier.Filename;
            string dossier = Dossier(instance, type, monde);

            RetirerAncienneVersion(instance, metas, Sources.Modrinth, projectId, type, dossier);

            progression?.Report($"Téléchargement de {titreProjet}…");
            string destination = Path.Combine(dossier, NomFichierSur(fichier.Filename));
            await Telechargement.FichierAsync(fichier.Url, destination, fichier.Hashes.GetValueOrDefault("sha1"));

            metas.RemoveAll(m => m.Chemin.Equals(Relatif(instance, destination), StringComparison.OrdinalIgnoreCase));
            metas.Add(new ContenuMeta
            {
                Chemin = Relatif(instance, destination),
                Source = Sources.Modrinth,
                ProjectId = projectId,
                VersionId = version.Id,
                Titre = titreProjet,
                VersionNom = version.VersionNumber,
                IconeUrl = projet?.IconUrl,
                Type = type,
            });
            installes.Add(titreProjet);

            if (type != Mod) return;

            // Dépendances requises
            foreach (var dependance in version.Dependencies.Where(d => d.DependencyType == "required"))
            {
                VersionModrinth? versionDep = dependance.VersionId != null ? await ModrinthApi.VersionAsync(dependance.VersionId) : null;
                string? idDep = dependance.ProjectId ?? versionDep?.ProjectId;
                if (idDep == null || metas.Any(m => m.Correspond(Sources.Modrinth, idDep))) continue; // inconnue ou déjà là

                await InstallerProjetAsync(instance, idDep, Mod, null, versionDep, metas, installes, visites, progression);
            }
        }

        /// <summary>Privilégie la dernière version stable, sinon la plus récente.</summary>
        private static VersionModrinth? MeilleureVersion(List<VersionModrinth> versions) =>
            versions.FirstOrDefault(v => v.VersionType == "release") ?? versions.FirstOrDefault();

        private static async Task InstallerProjetCurseForgeAsync(Instance instance, int modId, string type, string? monde,
            List<ContenuMeta> metas, List<string> installes, HashSet<int> visites, IProgress<string>? progression)
        {
            if (!visites.Add(modId)) return;

            var mod = (await CurseForgeApi.ModsAsync(new[] { modId })).FirstOrDefault()
                ?? throw new InvalidOperationException($"Projet CurseForge {modId} introuvable.");

            // Fichiers compatibles : bonne version de Minecraft (+ bon loader pour les mods)
            var fichiers = (await CurseForgeApi.FichiersAsync(modId, instance.VersionMc))
                .Where(f => f.IsAvailable && f.GameVersions.Contains(instance.VersionMc))
                .ToList();
            if (type == Mod)
            {
                var loaders = CurseForgeApi.NomsLoader(instance.Loader);
                fichiers = fichiers.Where(f => f.GameVersions.Any(v => loaders.Contains(v, StringComparer.OrdinalIgnoreCase))).ToList();
            }

            var fichier = fichiers.FirstOrDefault(f => f.ReleaseType == 1) ?? fichiers.FirstOrDefault()
                ?? throw new InvalidOperationException($"{mod.Name} n'a pas de version compatible avec {instance.VersionMc} ({instance.Loader}) sur CurseForge.");

            if (string.IsNullOrEmpty(fichier.DownloadUrl))
                throw new InvalidOperationException(
                    $"L'auteur de {mod.Name} n'autorise pas le téléchargement depuis d'autres applications que CurseForge.\n\n" +
                    $"Télécharge-le sur {CurseForgeApi.PageFichier(mod, modId, fichier.Id)} puis place-le dans le dossier du profil.");

            string dossier = Dossier(instance, type, monde);
            RetirerAncienneVersion(instance, metas, Sources.CurseForge, modId.ToString(), type, dossier);

            progression?.Report($"Téléchargement de {mod.Name}…");
            string destination = Path.Combine(dossier, NomFichierSur(fichier.FileName));
            await Telechargement.FichierAsync(fichier.DownloadUrl, destination, fichier.Sha1);

            metas.RemoveAll(m => m.Chemin.Equals(Relatif(instance, destination), StringComparison.OrdinalIgnoreCase));
            metas.Add(new ContenuMeta
            {
                Chemin = Relatif(instance, destination),
                Source = Sources.CurseForge,
                ProjectId = modId.ToString(),
                VersionId = fichier.Id.ToString(),
                Titre = mod.Name,
                VersionNom = fichier.DisplayName,
                IconeUrl = mod.Logo?.ThumbnailUrl,
                Type = type,
            });
            installes.Add(mod.Name);

            if (type != Mod) return;

            // Dépendances requises (relationType 3)
            foreach (var dependance in fichier.Dependencies.Where(d => d.RelationType == 3))
            {
                if (metas.Any(m => m.Correspond(Sources.CurseForge, dependance.ModId.ToString()))) continue;
                await InstallerProjetCurseForgeAsync(instance, dependance.ModId, Mod, null, metas, installes, visites, progression);
            }
        }

        /// <summary>Mise à jour : retire l'ancienne version du même projet (au même endroit).</summary>
        private static void RetirerAncienneVersion(Instance instance, List<ContenuMeta> metas, string source, string projectId,
            string type, string dossier)
        {
            foreach (var ancien in metas.Where(m => m.Correspond(source, projectId) && m.Type == type).ToList())
            {
                string chemin = Path.Combine(instance.Dossier, ancien.Chemin);
                if (!string.Equals(Path.GetDirectoryName(chemin), dossier, StringComparison.OrdinalIgnoreCase)) continue;
                SupprimerFichier(chemin);
                SupprimerFichier(chemin + SuffixeDesactive);
                metas.Remove(ancien);
            }
        }

        /// <summary>
        /// Prérequis manquant pour les shaders : renvoie l'ID Modrinth du mod à installer (Iris / Oculus),
        /// "" si les shaders sont impossibles avec ce loader, null si tout est prêt.
        /// Iris / Oculus installés depuis CurseForge ou à la main sont reconnus à leur nom.
        /// </summary>
        public static string? PrerequisShaders(Instance instance, IReadOnlyList<ContenuInstalle> contenus)
        {
            bool Present(string idModrinth, string nom) => contenus.Any(c => c.Type == Mod
                && (c.Meta?.Correspond(Sources.Modrinth, idModrinth) == true || c.Titre.StartsWith(nom, StringComparison.OrdinalIgnoreCase)));

            return instance.Loader switch
            {
                ModLoaderService.OptiFine => null,
                ModLoaderService.Fabric or ModLoaderService.Quilt or ModLoaderService.NeoForge =>
                    Present(IdIris, "Iris") ? null : IdIris,
                ModLoaderService.Forge => Present(IdOculus, "Oculus") ? null : IdOculus,
                _ => "",
            };
        }

        // ───────────────────────── Utilitaires ─────────────────────────

        private static string Relatif(Instance instance, string chemin)
        {
            if (chemin.EndsWith(SuffixeDesactive, StringComparison.OrdinalIgnoreCase))
                chemin = chemin[..^SuffixeDesactive.Length];
            return Path.GetRelativePath(instance.Dossier, chemin).Replace('\\', '/');
        }

        private static string NomFichierSur(string nom)
        {
            nom = Path.GetFileName(nom);
            foreach (char c in Path.GetInvalidFileNameChars())
                nom = nom.Replace(c, '_');
            return nom;
        }

        private static void SupprimerFichier(string chemin)
        {
            if (File.Exists(chemin)) File.Delete(chemin);
        }

        private static List<ContenuMeta> LireMeta(Instance instance)
        {
            string chemin = Path.Combine(instance.Dossier, FichierMeta);
            try
            {
                return File.Exists(chemin)
                    ? JsonSerializer.Deserialize<List<ContenuMeta>>(File.ReadAllText(chemin)) ?? new()
                    : new();
            }
            catch
            {
                return new();
            }
        }

        private static void EcrireMeta(Instance instance, List<ContenuMeta> metas)
        {
            Directory.CreateDirectory(instance.Dossier);
            File.WriteAllText(Path.Combine(instance.Dossier, FichierMeta), JsonSerializer.Serialize(metas, json));
        }
    }
}
