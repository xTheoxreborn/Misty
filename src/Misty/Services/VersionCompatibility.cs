namespace Misty.Services
{
    /// <summary>
    /// Plages de versions Minecraft supportées par chaque mod loader
    /// (filtre rapide avant d'interroger les API des loaders).
    /// </summary>
    internal static class VersionCompatibility
    {
        private static readonly Version MaxConnue = new(26, 4);

        public static bool Forge(string version) => Entre(version, new(1, 7, 10), MaxConnue);
        public static bool OptiFine(string version) => Entre(version, new(1, 7, 2), MaxConnue);
        public static bool NeoForge(string version) => Entre(version, new(1, 20, 1), MaxConnue);
        public static bool Fabric(string version) => Entre(version, new(1, 14, 0), MaxConnue);
        public static bool Quilt(string version) => Entre(version, new(1, 14, 0), MaxConnue); // fork de Fabric
        public static bool LiteLoader(string version) => Entre(version, new(1, 5, 2), new(1, 12, 2)); // abandonné après 1.12.2

        private static bool Entre(string version, Version min, Version max)
        {
            if (!System.Version.TryParse(Normaliser(version), out var v))
                return false;
            return v >= min && v <= max;
        }

        // "1.20" -> "1.20.0" pour que la comparaison avec "1.20.1" fonctionne
        private static string Normaliser(string version) =>
            version.Split('.').Length == 2 ? version + ".0" : version;
    }
}
