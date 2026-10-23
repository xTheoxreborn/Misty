using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Misty.Services
{
    public record LangueInfo(string Code, string Nom, string Pays)
    {
        public override string ToString() => Nom;

        /// <summary>Drapeau embarqué (Assets/Flags/xx.png), ou null s'il n'existe pas.</summary>
        public ImageSource? Drapeau
        {
            get
            {
                try
                {
                    var img = new BitmapImage(new Uri($"pack://application:,,,/Misty;component/Assets/Flags/{Pays}.png"));
                    img.Freeze();
                    return img;
                }
                catch { return null; }
            }
        }
    }

    /// <summary>Gestion des traductions. Indexeur lié en XAML pour un changement de langue en direct.</summary>
    public sealed class Loc : INotifyPropertyChanged
    {
        private const string Defaut = "fr";
        private const string Prefixe = "Langues.";

        public static Loc Instance { get; } = new();

        private Dictionary<string, string> textes = new();
        private Dictionary<string, string> secours = new();

        public string Code { get; private set; } = Defaut;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Déclenché après un changement de langue : les textes posés en code doivent être recalculés.</summary>
        public static event Action? Changee;

        public string this[string cle] => Obtenir(cle);

        public static string T(string cle) => Instance.Obtenir(cle);
        public static string T(string cle, params object[] args) => string.Format(Instance.Obtenir(cle), args);

        private string Obtenir(string cle) =>
            textes.TryGetValue(cle, out var t) ? t
            : secours.TryGetValue(cle, out t) ? t
            : cle;

        /// <summary>À appeler une fois au démarrage : langue enregistrée, sinon celle de Windows.</summary>
        public static void Initialiser()
        {
            string code = SettingsStore.Get(SettingsStore.Langue);
            if (string.IsNullOrWhiteSpace(code))
                code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            Instance.Charger(code);
        }

        public void Charger(string code)
        {
            // Langue non traduite : anglais (plus universel), sinon la langue par défaut
            if (!Existe(code)) code = Existe("en") ? "en" : Defaut;

            secours = Lire(Defaut);
            textes = Lire(code);
            Code = code;

            // "Item[]" : tous les {loc:Tr ...} du XAML se rafraîchissent
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            Changee?.Invoke();
        }

        /// <summary>Lie une propriété à une traduction (équivalent code de {loc:Tr Cle}).</summary>
        public static void Lier(System.Windows.DependencyObject cible, System.Windows.DependencyProperty propriete, string cle) =>
            System.Windows.Data.BindingOperations.SetBinding(cible, propriete,
                new System.Windows.Data.Binding($"[{cle}]") { Source = Instance, Mode = System.Windows.Data.BindingMode.OneWay });

        public static List<LangueInfo> Disponibles() =>
            Assembly.GetExecutingAssembly().GetManifestResourceNames()
                .Where(n => n.StartsWith(Prefixe) && n.EndsWith(".json"))
                .Select(n => n[Prefixe.Length..^".json".Length])
                .Select(code => 
                {
                    var d = Lire(code);
                    return new LangueInfo(code, d.GetValueOrDefault("_nom", code), d.GetValueOrDefault("_drapeau", code));
                })
                .OrderBy(l => l.Nom)
                .ToList();

        private static bool Existe(string code) =>
            Assembly.GetExecutingAssembly().GetManifestResourceNames().Contains($"{Prefixe}{code}.json");

        private static Dictionary<string, string> Lire(string code)
        {
            using var flux = Assembly.GetExecutingAssembly().GetManifestResourceStream($"{Prefixe}{code}.json");
            return flux == null
                ? new()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(flux) ?? new();
        }
    }
}