using CmlLib.Core;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.Installer.NeoForge;
using CmlLib.Core.Installer.NeoForge.Installers;
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ModLoaders.LiteLoader;
using Optifine.Installer;

namespace Misty.Services
{
    /// <summary>
    /// Détection et installation des mod loaders (Forge, NeoForge, Fabric, OptiFine, LiteLoader).
    /// </summary>
    internal sealed class ModLoaderService
    {
        public const string Vanilla = "Vanilla";
        public const string Forge = "Forge";
        public const string NeoForge = "NeoForge";
        public const string Fabric = "Fabric";
        public const string OptiFine = "OptiFine";
        public const string LiteLoader = "LiteLoader";

        private static readonly HttpClient http = new();

        private readonly MinecraftLauncher launcher;
        private readonly MinecraftPath mcPath;
        private readonly ForgeInstaller forgeInstaller;
        private readonly NeoForgeInstaller neoForgeInstaller;
        private readonly OptifineInstaller optifineInstaller;

        public ModLoaderService(MinecraftLauncher launcher, MinecraftPath mcPath)
        {
            this.launcher = launcher;
            this.mcPath = mcPath;
            forgeInstaller = new ForgeInstaller(launcher);
            neoForgeInstaller = new NeoForgeInstaller(launcher);
            optifineInstaller = new OptifineInstaller(http);
        }

        /// <summary>Liste des modes disponibles pour une version de Minecraft (Vanilla toujours en premier).</summary>
        public async Task<List<string>> ModesDisponiblesAsync(string version)
        {
            var modes = new List<string> { Vanilla };

            if (VersionCompatibility.Forge(version))
                await Essayer(async () => (await forgeInstaller.GetForgeVersions(version)).Any(), Forge);

            if (VersionCompatibility.OptiFine(version))
                await Essayer(async () => (await optifineInstaller.GetOptifineVersionsAsync()).Any(v => v.MinecraftVersion == version), OptiFine);

            if (VersionCompatibility.NeoForge(version))
                await Essayer(async () => (await neoForgeInstaller.GetForgeVersions(version)).Any(), NeoForge);

            if (VersionCompatibility.Fabric(version))
                await Essayer(async () => (await new FabricInstaller(http).GetLoaders(version)).Any(), Fabric);

            if (VersionCompatibility.LiteLoader(version))
                await Essayer(async () => (await new LiteLoaderInstaller(http).GetAllLiteLoaders()).Any(l => l.BaseVersion == version), LiteLoader);

            return modes;

            async Task Essayer(Func<Task<bool>> disponible, string mode)
            {
                try
                {
                    if (await disponible())
                        modes.Add(mode);
                }
                catch { } // loader indisponible pour cette version : on ne le propose pas
            }
        }

        /// <summary>Installe la version + le loader choisi. Renvoie le nom de version à lancer.</summary>
        public async Task<string> InstallerAsync(string version, string mode)
        {
            string nomVersion;

            switch (mode)
            {
                case OptiFine:
                    await launcher.InstallAsync(version);
                    var optifine = (await optifineInstaller.GetOptifineVersionsAsync())
                        .First(v => v.MinecraftVersion == version);
                    try
                    {
                        nomVersion = await optifineInstaller.InstallOptifineAsync(mcPath.BasePath, optifine);
                    }
                    catch (Exception ex) when (ExtraireNomVersion(ex.ToString()) is string nom)
                    {
                        // L'installeur OptiFine peut lever une exception alors que la version est bien créée :
                        // on récupère son nom depuis le chemin "versions\<nom>" contenu dans le message.
                        nomVersion = nom;
                    }
                    break;

                case Forge:
                    nomVersion = await forgeInstaller.Install(version, new ForgeInstallOptions());
                    break;

                case NeoForge:
                    nomVersion = await neoForgeInstaller.Install(version, new NeoForgeInstallOptions());
                    break;

                case Fabric:
                    nomVersion = await new FabricInstaller(http).Install(version, mcPath);
                    break;

                case LiteLoader:
                    var loader = (await new LiteLoaderInstaller(http).GetAllLiteLoaders())
                        .First(l => l.BaseVersion == version);
                    nomVersion = await new LiteLoaderInstaller(http).Install(loader, await launcher.GetVersionAsync(version), mcPath);
                    break;

                default:
                    nomVersion = version;
                    break;
            }

            await launcher.InstallAsync(nomVersion);
            return nomVersion;
        }

        private static string? ExtraireNomVersion(string message)
        {
            int debut = message.IndexOf("versions\\");
            return debut == -1 ? null : message.Substring(debut).Split('\\')[1];
        }
    }
}
