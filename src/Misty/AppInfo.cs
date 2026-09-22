namespace Misty
{
    /// <summary>
    /// Constantes globales de l'application.
    /// </summary>
    internal static class AppInfo
    {
        /// <summary>Version du launcher. Doit correspondre au tag de la release GitHub (sans le "v")
        /// et au nom du dossier d'installation "Misty-{Version}".</summary>
        public const string Version = "0.2.3.9.5.1";

        /// <summary>Version du format de data.txt. À incrémenter quand on ajoute/retire une clé.</summary>
        public const string DataVersion = "1.2.2";

        public const string GitHubRepo = "xTheoxreborn/Misty";
        public const string DiscordAppId = "1536551827640680528";
        public const int RamParDefautMo = 4096;
    }
}
