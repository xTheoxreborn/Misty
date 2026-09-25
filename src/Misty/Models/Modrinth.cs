namespace Misty.Models
{
    /// <summary>Sources de contenu prises en charge.</summary>
    public static class Sources
    {
        public const string Modrinth = "modrinth";
        public const string CurseForge = "curseforge";

        public static string Libelle(string? source) => source == CurseForge ? "CurseForge" : "Modrinth";
    }

    /// <summary>Un projet (mod, pack, modpack…) trouvé sur Modrinth ou CurseForge, tel qu'affiché dans l'explorateur.</summary>
    public class ProjetDistant
    {
        public required string Source { get; init; }
        /// <summary>ID du projet sur sa plateforme (numérique pour CurseForge).</summary>
        public required string Id { get; init; }
        public string Titre { get; init; } = "";
        public string Description { get; init; } = "";
        public string Auteur { get; init; } = "";
        public long Telechargements { get; init; }
        public string? IconeUrl { get; init; }
        public List<string> Categories { get; init; } = new();
    }

    // DTO de l'API Modrinth v2 (https://docs.modrinth.com/api/), en snake_case côté JSON.

    public class ProjetModrinth
    {
        public string ProjectId { get; set; } = "";
        public string Slug { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Author { get; set; } = "";
        public long Downloads { get; set; }
        public string? IconUrl { get; set; }
        public string ProjectType { get; set; } = "";
        public List<string> Categories { get; set; } = new();
    }

    public class ResultatRecherche
    {
        public List<ProjetModrinth> Hits { get; set; } = new();
        public int TotalHits { get; set; }
    }

    /// <summary>Projet renvoyé par /project/{id} ou /projects (champs utiles seulement).</summary>
    public class ProjetDetail
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string? IconUrl { get; set; }
        public string ProjectType { get; set; } = "";
    }

    public class VersionModrinth
    {
        public string Id { get; set; } = "";
        public string ProjectId { get; set; } = "";
        public string Name { get; set; } = "";
        public string VersionNumber { get; set; } = "";
        public string VersionType { get; set; } = "";
        public List<string> GameVersions { get; set; } = new();
        public List<string> Loaders { get; set; } = new();
        public List<FichierModrinth> Files { get; set; } = new();
        public List<DependanceModrinth> Dependencies { get; set; } = new();

        public FichierModrinth? FichierPrincipal => Files.FirstOrDefault(f => f.Primary) ?? Files.FirstOrDefault();
    }

    public class FichierModrinth
    {
        public string Url { get; set; } = "";
        public string Filename { get; set; } = "";
        public bool Primary { get; set; }
        public long Size { get; set; }
        public Dictionary<string, string> Hashes { get; set; } = new();
    }

    public class DependanceModrinth
    {
        public string? VersionId { get; set; }
        public string? ProjectId { get; set; }
        public string? FileName { get; set; }
        public string DependencyType { get; set; } = "";
    }

    // ── Format .mrpack (modrinth.index.json) ──

    public class IndexModpack
    {
        public string Name { get; set; } = "";
        public string VersionId { get; set; } = "";
        public List<FichierModpack> Files { get; set; } = new();
        public Dictionary<string, string> Dependencies { get; set; } = new();
    }

    public class FichierModpack
    {
        public string Path { get; set; } = "";
        public Dictionary<string, string> Hashes { get; set; } = new();
        public Dictionary<string, string>? Env { get; set; }
        public List<string> Downloads { get; set; } = new();
        public long FileSize { get; set; }
    }
}
