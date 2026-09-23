using System.Reflection;
using System.Windows.Controls;

namespace Misty.Views
{
    /// <summary>
    /// Notes de version, lues depuis CHANGELOG.md (intégré à l'exe).
    /// </summary>
    public partial class ChangelogView : UserControl
    {
        public record Section(string Titre, List<string> Lignes, bool EstRecente);

        public ChangelogView()
        {
            InitializeComponent();
            ListeVersions.ItemsSource = Lire();
        }

        /// <summary>Découpe le markdown : "## titre" = une section, "- texte" = une ligne.</summary>
        private static List<Section> Lire()
        {
            var sections = new List<Section>();

            using var flux = Assembly.GetExecutingAssembly().GetManifestResourceStream("Misty.CHANGELOG.md");
            if (flux == null) return sections;

            using var lecteur = new StreamReader(flux);
            Section? courante = null;
            string? ligne;
            while ((ligne = lecteur.ReadLine()) != null)
            {
                if (ligne.StartsWith("## "))
                {
                    courante = new Section(ligne[3..].Trim().ToUpperInvariant(), new List<string>(), sections.Count == 0);
                    sections.Add(courante);
                }
                else if (ligne.StartsWith("- ") && courante != null)
                {
                    courante.Lignes.Add(ligne[2..].Replace("`", ""));
                }
            }
            return sections;
        }
    }
}
