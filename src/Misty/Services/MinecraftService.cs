using CmlLib.Core;

namespace Misty.Services
{
    /// <summary>
    /// Launcher Minecraft partagé : versions, bibliothèques et assets dans %LOCALAPPDATA%\Misty.
    /// </summary>
    internal static class MinecraftService
    {
        public static MinecraftPath Chemin { get; } = new(AppPaths.DataDir);
        public static MinecraftLauncher Launcher { get; } = new(Chemin);
        public static ModLoaderService Loaders { get; } = new(Launcher, Chemin);

        /// <summary>Versions à proposer : releases + snapshots/bêtas/alphas selon les options.</summary>
        public static async Task<List<string>> VersionsAsync()
        {
            bool snapshot = SettingsStore.GetBool(SettingsStore.Snapshot);
            bool beta = SettingsStore.GetBool(SettingsStore.Beta);
            bool alpha = SettingsStore.GetBool(SettingsStore.Alpha);

            var resultat = new List<string>();
            foreach (var v in await Launcher.GetAllVersionsAsync())
            {
                string type = v.Type?.ToString() ?? "";

                if (type.Equals("release", StringComparison.OrdinalIgnoreCase)
                    || (snapshot && type.Equals("snapshot", StringComparison.OrdinalIgnoreCase))
                    || (beta && type.Equals("old_beta", StringComparison.OrdinalIgnoreCase))
                    || (alpha && type.Equals("old_alpha", StringComparison.OrdinalIgnoreCase)))
                {
                    resultat.Add(v.Name);
                }
            }
            return resultat;
        }
    }
}
