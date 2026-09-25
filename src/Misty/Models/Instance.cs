using Misty.Services;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace Misty.Models
{
    /// <summary>
    /// Un profil de jeu : une version + un loader + son propre dossier (mods, packs, mondes, options…).
    /// Enregistré dans %LOCALAPPDATA%\Misty\instances\{Id}\instance.json.
    /// </summary>
    public class Instance
    {
        public string Id { get; set; } = "";
        public string Nom { get; set; } = "";
        public string VersionMc { get; set; } = "";

        /// <summary>Nom du mode (voir les constantes de <see cref="ModLoaderService"/>).</summary>
        public string Loader { get; set; } = ModLoaderService.Vanilla;

        /// <summary>Version précise du loader (modpacks). null = la plus récente.</summary>
        public string? LoaderVersion { get; set; }

        /// <summary>Icône distante (modpack), utilisée si aucune icon.png locale.</summary>
        public string? IconeUrl { get; set; }

        /// <summary>Plateforme du modpack d'origine (modrinth / curseforge).</summary>
        public string? ModpackSource { get; set; }
        public string? ModpackProjectId { get; set; }
        public string? ModpackVersion { get; set; }

        public DateTime CreeLe { get; set; } = DateTime.Now;
        public DateTime? DernierLancement { get; set; }

        [JsonIgnore] public string Dossier => InstanceStore.Dossier(Id);
        [JsonIgnore] public string CheminIcone => Path.Combine(Dossier, "icon.png");
        [JsonIgnore] public string Resume => $"{VersionMc} · {Loader}";
        [JsonIgnore] public ImageSource? Icone => File.Exists(CheminIcone) ? Images.DepuisFichier(CheminIcone) : Images.DepuisUrl(IconeUrl);
        [JsonIgnore]
        public string DernierLancementTexte => DernierLancement is not DateTime d ? "Jamais lancé"
            : d.Date == DateTime.Today ? "Joué aujourd'hui"
            : d.Date == DateTime.Today.AddDays(-1) ? "Joué hier"
            : (DateTime.Today - d.Date).TotalDays < 30 ? $"Joué il y a {(int)(DateTime.Today - d.Date).TotalDays} j"
            : $"Joué le {d:dd/MM/yyyy}";

        [JsonIgnore] public bool EstModpack => ModpackProjectId != null;
        [JsonIgnore] public bool SupporteMods => ContentService.SupporteMods(Loader);

        public override string ToString() => Nom;
    }
}
