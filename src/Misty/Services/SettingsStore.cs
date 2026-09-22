namespace Misty.Services
{
    /// <summary>
    /// Lecture / écriture de data.txt (format "cle=valeur", une clé par ligne).
    /// </summary>
    internal static class SettingsStore
    {
        public const string VersionData = "versiondatatxt";
        public const string LastPseudo = "lastpseudo";
        public const string LastVersion = "lastversion";
        public const string Ram = "ram";
        public const string Premium = "premium";
        public const string Raccourci = "raccourci";
        public const string Snapshot = "snapshot";
        public const string Beta = "beta";
        public const string Alpha = "alpha";
        public const string Suppr = "Suppr";

        // Ordre des clés dans le fichier
        private static readonly string[] Cles =
            { VersionData, LastPseudo, LastVersion, Ram, Premium, Raccourci, Snapshot, Beta, Alpha, Suppr };

        public static string Get(string cle)
        {
            if (!File.Exists(AppPaths.DataFile)) return string.Empty;

            string valeur = string.Empty;
            foreach (var ligne in File.ReadAllLines(AppPaths.DataFile))
            {
                if (ligne.StartsWith(cle + "="))
                    valeur = ligne.Substring(cle.Length + 1);
            }
            return valeur;
        }

        public static bool GetBool(string cle) => Get(cle).Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>Modifie une clé. Ne fait rien si data.txt n'existe pas.</summary>
        public static void Set(string cle, string valeur)
        {
            if (!File.Exists(AppPaths.DataFile)) return;

            var lignes = File.ReadAllLines(AppPaths.DataFile).ToList();
            bool trouvee = false;
            for (int i = 0; i < lignes.Count; i++)
            {
                if (lignes[i].StartsWith(cle + "="))
                {
                    lignes[i] = cle + "=" + valeur;
                    trouvee = true;
                }
            }
            if (!trouvee)
                lignes.Add(cle + "=" + valeur);

            File.WriteAllLines(AppPaths.DataFile, lignes);
        }

        public static void SetBool(string cle, bool valeur) => Set(cle, valeur ? "true" : "false");

        /// <summary>
        /// Crée data.txt s'il n'existe pas, ou le réécrit au format actuel (en gardant les valeurs)
        /// si sa version est différente de <see cref="AppInfo.DataVersion"/>.
        /// </summary>
        public static void Initialiser()
        {
            Directory.CreateDirectory(AppPaths.DataDir);

            if (!File.Exists(AppPaths.DataFile))
            {
                Ecrire(new Dictionary<string, string> { [LastPseudo] = "PseudoTest" });
                return;
            }

            if (Get(VersionData) != AppInfo.DataVersion)
                Ecrire(Cles.ToDictionary(c => c, Get));
        }

        private static void Ecrire(Dictionary<string, string> valeurs)
        {
            valeurs[VersionData] = AppInfo.DataVersion;
            File.WriteAllLines(AppPaths.DataFile,
                Cles.Select(c => c + "=" + valeurs.GetValueOrDefault(c, string.Empty)));
        }
    }
}
