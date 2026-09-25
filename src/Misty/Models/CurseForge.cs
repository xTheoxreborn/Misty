namespace Misty.Models
{
    // DTO de l'API CurseForge v1 (https://docs.curseforge.com/rest-api/), en camelCase côté JSON.

    public class ReponseCF<T>
    {
        public T Data { get; set; } = default!;
        public PaginationCF? Pagination { get; set; }
    }

    public class PaginationCF
    {
        public int Index { get; set; }
        public int PageSize { get; set; }
        public int ResultCount { get; set; }
        public int TotalCount { get; set; }
    }

    public class ModCF
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Slug { get; set; } = "";
        public string Summary { get; set; } = "";
        public long DownloadCount { get; set; }
        public int ClassId { get; set; }
        public LogoCF? Logo { get; set; }
        public List<AuteurCF> Authors { get; set; } = new();
        public List<CategorieCF> Categories { get; set; } = new();
        public LiensCF? Links { get; set; }
    }

    public class LogoCF
    {
        public string? ThumbnailUrl { get; set; }
        public string? Url { get; set; }
    }

    public class AuteurCF
    {
        public string Name { get; set; } = "";
    }

    public class CategorieCF
    {
        public string Name { get; set; } = "";
    }

    public class LiensCF
    {
        public string? WebsiteUrl { get; set; }
    }

    public class FichierCF
    {
        public int Id { get; set; }
        public int ModId { get; set; }
        public string DisplayName { get; set; } = "";
        public string FileName { get; set; } = "";
        /// <summary>1 = release, 2 = bêta, 3 = alpha.</summary>
        public int ReleaseType { get; set; }
        /// <summary>null si l'auteur interdit le téléchargement par des applications tierces.</summary>
        public string? DownloadUrl { get; set; }
        public DateTime FileDate { get; set; }
        public bool IsAvailable { get; set; } = true;
        /// <summary>Empreinte MurmurHash2 du fichier (voir CurseForgeApi.Empreinte).</summary>
        public long FileFingerprint { get; set; }
        public List<HashCF> Hashes { get; set; } = new();
        public List<DependanceCF> Dependencies { get; set; } = new();
        /// <summary>Versions de Minecraft ET noms de loaders ("1.21.1", "Fabric"…).</summary>
        public List<string> GameVersions { get; set; } = new();

        public string? Sha1 => Hashes.FirstOrDefault(h => h.Algo == 1)?.Value;
    }

    public class HashCF
    {
        public string Value { get; set; } = "";
        /// <summary>1 = SHA-1, 2 = MD5.</summary>
        public int Algo { get; set; }
    }

    public class DependanceCF
    {
        public int ModId { get; set; }
        /// <summary>3 = dépendance requise.</summary>
        public int RelationType { get; set; }
    }

    public class EmpreintesCF
    {
        public List<CorrespondanceCF> ExactMatches { get; set; } = new();
    }

    public class CorrespondanceCF
    {
        public int Id { get; set; }
        public FichierCF File { get; set; } = new();
    }

    // ── Format des modpacks CurseForge (manifest.json) ──

    public class ManifestCF
    {
        public MinecraftCF Minecraft { get; set; } = new();
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public List<FichierManifestCF> Files { get; set; } = new();
        public string? Overrides { get; set; }
    }

    public class MinecraftCF
    {
        public string Version { get; set; } = "";
        public List<LoaderManifestCF> ModLoaders { get; set; } = new();
    }

    public class LoaderManifestCF
    {
        /// <summary>"forge-47.2.0", "fabric-0.15.7", "neoforge-21.1.77"…</summary>
        public string Id { get; set; } = "";
        public bool Primary { get; set; }
    }

    public class FichierManifestCF
    {
        public int ProjectID { get; set; }
        public int FileID { get; set; }
        public bool Required { get; set; } = true;
    }
}
