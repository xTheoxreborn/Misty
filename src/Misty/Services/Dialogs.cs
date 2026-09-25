namespace Misty.Services
{
    /// <summary>Surface capable d'afficher une boîte de dialogue (implémentée par MainWindow).</summary>
    internal interface IDialogHost
    {
        Task<bool> AfficherAsync(string titre, string message, string ok, string? annuler, bool danger);
        Task<string?> ChoisirAsync(string titre, string message, IReadOnlyList<string> options);
    }

    /// <summary>
    /// Boîtes de dialogue intégrées à la fenêtre (remplacent les MessageBox Windows).
    /// </summary>
    internal static class Dialogs
    {
        public static IDialogHost? Host { get; set; }

        public static Task InfoAsync(string titre, string message) =>
            Afficher(titre, message, "OK", null, false);

        public static Task ErreurAsync(string titre, string message) =>
            Afficher(titre, message, "OK", null, true);

        public static Task<bool> ConfirmerAsync(string titre, string message, string oui = "Oui", string non = "Annuler", bool danger = false) =>
            Afficher(titre, message, oui, non, danger);

        /// <summary>Choix dans une liste. Renvoie l'option choisie, ou null si annulé.</summary>
        public static Task<string?> ChoisirAsync(string titre, string message, IReadOnlyList<string> options) =>
            Host?.ChoisirAsync(titre, message, options) ?? Task.FromResult(options.FirstOrDefault());

        private static Task<bool> Afficher(string titre, string message, string ok, string? annuler, bool danger)
        {
            if (Host != null)
                return Host.AfficherAsync(titre, message, ok, annuler, danger);

            // Fenêtre principale pas encore prête : repli sur la MessageBox système
            var boutons = annuler == null ? System.Windows.MessageBoxButton.OK : System.Windows.MessageBoxButton.YesNo;
            var r = System.Windows.MessageBox.Show(message, titre, boutons);
            return Task.FromResult(r is System.Windows.MessageBoxResult.OK or System.Windows.MessageBoxResult.Yes);
        }
    }
}
