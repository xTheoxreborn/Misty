using Misty.Models;

namespace Misty.Services
{
    /// <summary>
    /// Recherche de contenu, quelle que soit la plateforme (Modrinth ou CurseForge).
    /// </summary>
    internal static class Catalogue
    {
        public static (string Cle, string Libelle)[] Tris(string source) =>
            source == Sources.CurseForge ? CurseForgeApi.Tris : ModrinthApi.Tris;

        /// <param name="instance">Profil cible (filtre version + loader), ou null pour les modpacks.</param>
        public static async Task<(List<ProjetDistant> Projets, int Total)> RechercherAsync(string source, string requete,
            string type, Instance? instance, string tri, int offset, int limite)
        {
            if (source == Sources.CurseForge)
            {
                // Sur CurseForge, les mods Quilt sont presque tous publiés comme mods Fabric
                int? loader = instance == null || type != ContentService.Mod ? null
                    : CurseForgeApi.TypeLoader(instance.Loader == ModLoaderService.Quilt ? ModLoaderService.Fabric : instance.Loader);

                var (mods, total) = await CurseForgeApi.RechercherAsync(requete, CurseForgeApi.Classe(type),
                    instance?.VersionMc, loader, tri, offset, limite);
                return (mods.Select(CurseForgeApi.VersProjet).ToList(), total);
            }

            var reponse = await ModrinthApi.RechercherAsync(requete, type, instance?.VersionMc,
                instance != null ? ContentService.CategoriesRecherche(instance, type) : null, tri, offset, limite);
            return (reponse.Hits.Select(ModrinthApi.VersProjet).ToList(), reponse.TotalHits);
        }
    }
}
