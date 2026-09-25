using Misty.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace Misty.Models
{
    /// <summary>
    /// Métadonnées d'un fichier installé depuis Modrinth / CurseForge (ou identifié par son hash).
    /// Stockées dans instances\{Id}\misty-contenu.json.
    /// </summary>
    public class ContenuMeta
    {
        /// <summary>Chemin relatif au dossier du profil, sans le suffixe ".disabled".</summary>
        public string Chemin { get; set; } = "";
        /// <summary>modrinth ou curseforge (null = modrinth, fichiers créés avant CurseForge).</summary>
        public string? Source { get; set; }
        public string? ProjectId { get; set; }
        public string? VersionId { get; set; }
        public string Titre { get; set; } = "";
        public string? VersionNom { get; set; }
        public string? IconeUrl { get; set; }
        public string Type { get; set; } = "";

        public bool Correspond(string source, string id) => (Source ?? Sources.Modrinth) == source && ProjectId == id;
    }

    /// <summary>Un fichier présent dans le profil (mod, pack de ressources, datapack, shader).</summary>
    public class ContenuInstalle : INotifyPropertyChanged
    {
        public required string CheminFichier { get; set; }
        public required string Type { get; init; }
        public string? Monde { get; init; }
        public ContenuMeta? Meta { get; set; }

        public string NomFichier => Path.GetFileName(CheminFichier);
        public string Titre => Meta?.Titre is { Length: > 0 } t ? t : ContentService.NomSansSuffixe(NomFichier);
        public ImageSource? Icone => Images.DepuisUrl(Meta?.IconeUrl);

        public string Details
        {
            get
            {
                var morceaux = new List<string> { ContentService.LibelleType(Type) };
                if (Meta?.VersionNom is { Length: > 0 } v) morceaux.Add(v);
                if (Meta?.Source == Sources.CurseForge) morceaux.Add("CurseForge");
                if (Monde != null) morceaux.Add("monde « " + Monde + " »");
                if (Meta?.ProjectId == null) morceaux.Add(NomFichier);
                return string.Join(" · ", morceaux);
            }
        }

        public bool Actif
        {
            get => !CheminFichier.EndsWith(ContentService.SuffixeDesactive, StringComparison.OrdinalIgnoreCase);
        }

        public void Rafraichir()
        {
            OnPropertyChanged(nameof(Titre));
            OnPropertyChanged(nameof(Icone));
            OnPropertyChanged(nameof(Details));
            OnPropertyChanged(nameof(Actif));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? nom = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nom));
    }
}
