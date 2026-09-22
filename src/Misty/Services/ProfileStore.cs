namespace Misty.Services
{
    /// <summary>
    /// Liste des pseudos hors-ligne, stockée dans listpseudo.txt ("listpseudo=a,b,c").
    /// </summary>
    internal static class ProfileStore
    {
        private const string Cle = "listpseudo=";

        public static void Initialiser()
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            if (!File.Exists(AppPaths.ProfilesFile))
                File.WriteAllLines(AppPaths.ProfilesFile, new[] { Cle + "PseudoTest" });
        }

        public static List<string> Lire()
        {
            var resultat = new List<string>();
            if (!File.Exists(AppPaths.ProfilesFile)) return resultat;

            foreach (var ligne in File.ReadAllLines(AppPaths.ProfilesFile))
            {
                if (ligne.StartsWith(Cle))
                {
                    string valeur = ligne.Substring(Cle.Length);
                    if (!string.IsNullOrEmpty(valeur))
                        resultat.AddRange(valeur.Split(','));
                }
            }
            return resultat;
        }

        public static void Ajouter(string pseudo)
        {
            if (string.IsNullOrWhiteSpace(pseudo)) return;

            var pseudos = Lire();
            if (pseudos.Contains(pseudo)) return;

            pseudos.Add(pseudo);
            Ecrire(pseudos);
        }

        public static void Supprimer(string pseudo)
        {
            var pseudos = Lire();
            if (pseudos.Remove(pseudo))
                Ecrire(pseudos);
        }

        private static void Ecrire(List<string> pseudos)
        {
            if (!File.Exists(AppPaths.ProfilesFile)) return;

            string nouvelleValeur = Cle + string.Join(",", pseudos);
            var lignes = File.ReadAllLines(AppPaths.ProfilesFile).ToList();
            int index = lignes.FindIndex(l => l.StartsWith(Cle));

            if (index >= 0)
                lignes[index] = nouvelleValeur;
            else
                lignes.Add(nouvelleValeur);

            File.WriteAllLines(AppPaths.ProfilesFile, lignes);
        }
    }
}
