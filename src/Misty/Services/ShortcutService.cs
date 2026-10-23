using System.Runtime.InteropServices;

namespace Misty.Services
{
    /// <summary>
    /// Création / suppression du raccourci "Misty.lnk" vers l'exe courant.
    /// </summary>
    internal static class ShortcutService
    {
        private const string NomRaccourci = "Misty.lnk";

        public static void Creer(string dossier)
        {
            if (string.IsNullOrWhiteSpace(dossier)) return;

            Supprimer(dossier);

            Type shellType = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new InvalidOperationException("WScript.Shell indisponible.");
            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                dynamic lnk = shell.CreateShortcut(Path.Combine(dossier, NomRaccourci));
                lnk.TargetPath = AppPaths.AppExe;
                lnk.WorkingDirectory = AppPaths.AppDir;
                if (File.Exists(AppPaths.CustomIcon))
                    lnk.IconLocation = AppPaths.CustomIcon;
                lnk.Save();
                Marshal.FinalReleaseComObject(lnk);
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }

        /// <summary>
        /// Raccourcis créés par l'installateur (menu Démarrer, bureau) : s'ils visent encore l'ancien dossier
        /// après une mise à jour, on les fait pointer vers l'exe actuel.
        /// </summary>
        public static void Rediriger(string ancienDossier)
        {
            string[] candidats =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Misty.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Misty.lnk"),
            };

            Type shellType = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new InvalidOperationException("WScript.Shell indisponible.");
            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                foreach (string chemin in candidats.Where(File.Exists))
                {
                    dynamic lnk = shell.CreateShortcut(chemin);
                    string cible = lnk.TargetPath ?? "";
                    if (cible.StartsWith(ancienDossier, StringComparison.OrdinalIgnoreCase))
                    {
                        lnk.TargetPath = AppPaths.AppExe;
                        lnk.WorkingDirectory = AppPaths.AppDir;
                        lnk.IconLocation = File.Exists(AppPaths.CustomIcon) ? AppPaths.CustomIcon : AppPaths.AppExe;
                        lnk.Save();
                    }
                    Marshal.FinalReleaseComObject(lnk);
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }

        public static void Supprimer(string dossier)
        {
            if (string.IsNullOrWhiteSpace(dossier)) return;

            string chemin = Path.Combine(dossier, NomRaccourci);
            if (File.Exists(chemin))
                File.Delete(chemin);
        }
    }
}
