using CmlLib.Core;
using Misty.Models;
using System.Text;
using System.Text.Json;

namespace Misty.Services
{
    /// <summary>
    /// Profils de jeu : un dossier par profil dans %LOCALAPPDATA%\Misty\instances\.
    /// Les versions, bibliothèques et assets restent partagés dans %LOCALAPPDATA%\Misty.
    /// </summary>
    internal static class InstanceStore
    {
        public static readonly string Racine = Path.Combine(AppPaths.DataDir, "instances");
        private const string Fichier = "instance.json";

        private static readonly JsonSerializerOptions json = new() { WriteIndented = true };

        /// <summary>Déclenché quand un profil est créé, modifié ou supprimé.</summary>
        public static event Action? Modifie;

        public static string Dossier(string id) => Path.Combine(Racine, id);

        public static List<Instance> Lister()
        {
            var instances = new List<Instance>();
            if (!Directory.Exists(Racine)) return instances;

            foreach (var dossier in Directory.GetDirectories(Racine))
            {
                try
                {
                    string chemin = Path.Combine(dossier, Fichier);
                    if (!File.Exists(chemin)) continue;
                    var instance = JsonSerializer.Deserialize<Instance>(File.ReadAllText(chemin));
                    if (instance == null) continue;
                    instance.Id = Path.GetFileName(dossier); // le dossier fait foi
                    instances.Add(instance);
                }
                catch { } // instance.json illisible : on l'ignore
            }

            return instances
                .OrderByDescending(i => i.DernierLancement ?? i.CreeLe)
                .ToList();
        }

        public static Instance? Trouver(string? id) =>
            string.IsNullOrEmpty(id) ? null : Lister().FirstOrDefault(i => i.Id == id);

        public static Instance Creer(string nom, string versionMc, string loader, string? loaderVersion = null)
        {
            var instance = new Instance
            {
                Id = NouvelId(nom),
                Nom = nom.Trim(),
                VersionMc = versionMc,
                Loader = loader,
                LoaderVersion = loaderVersion,
            };

            foreach (var sousDossier in new[] { "mods", "resourcepacks", "shaderpacks", "saves" })
                Directory.CreateDirectory(Path.Combine(instance.Dossier, sousDossier));

            Sauver(instance);
            return instance;
        }

        public static void Sauver(Instance instance)
        {
            Directory.CreateDirectory(instance.Dossier);
            File.WriteAllText(Path.Combine(instance.Dossier, Fichier), JsonSerializer.Serialize(instance, json));
            Modifie?.Invoke();
        }

        public static void Supprimer(Instance instance)
        {
            if (Directory.Exists(instance.Dossier))
                Directory.Delete(instance.Dossier, true);
            Modifie?.Invoke();
        }

        /// <summary>Chemins Minecraft du profil : dossier de jeu propre, le reste partagé.</summary>
        public static MinecraftPath CheminMinecraft(Instance instance) =>
            new MinecraftPath(AppPaths.DataDir) { BasePath = instance.Dossier };

        /// <summary>"Mon Profil !" -> "mon-profil-3fa2" (unique).</summary>
        private static string NouvelId(string nom)
        {
            var slug = new StringBuilder();
            foreach (char c in nom.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
            {
                if (char.IsAsciiLetterOrDigit(c)) slug.Append(c);
                else if ((c == ' ' || c == '-' || c == '_') && slug.Length > 0 && slug[^1] != '-') slug.Append('-');
                if (slug.Length >= 24) break;
            }
            string base_ = slug.ToString().Trim('-');
            if (base_.Length == 0) base_ = "profil";

            string id;
            do id = $"{base_}-{Random.Shared.Next(0x10000):x4}";
            while (Directory.Exists(Dossier(id)));
            return id;
        }
    }
}
