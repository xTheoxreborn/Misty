namespace Misty.Services
{
    /// <summary>
    /// Tous les chemins utilisés par le launcher.
    /// </summary>
    internal static class AppPaths
    {
        /// <summary>Dossier de données : %LOCALAPPDATA%\Misty (contient aussi le .minecraft).</summary>
        public static readonly string DataDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Misty");

        public static readonly string DataFile = Path.Combine(DataDir, "data.txt");
        public static readonly string ProfilesFile = Path.Combine(DataDir, "listpseudo.txt");

        /// <summary>Ancien dossier de données (avant 0.2.3.9.5), migré au démarrage.</summary>
        public const string LegacyDataDir = @"C:\TEXT\";

        /// <summary>Dossier de l'exécutable.</summary>
        public static readonly string AppDir = AppContext.BaseDirectory;

        public static readonly string AppExe = Path.Combine(AppDir, "Misty.exe");

        /// <summary>Icône personnalisée optionnelle posée à côté de l'exe.</summary>
        public static readonly string CustomIcon = Path.Combine(AppDir, "Icon.ico");
    }
}
