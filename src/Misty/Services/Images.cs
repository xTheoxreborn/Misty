using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Misty.Services
{
    /// <summary>Chargement d'images (icônes de projets, de profils).</summary>
    internal static class Images
    {
        /// <summary>Image distante, téléchargée en arrière-plan par WPF. null si pas d'URL.</summary>
        public static ImageSource? DepuisUrl(string? url, int taille = 96)
        {
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return null;
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = uri;
                image.DecodePixelWidth = taille;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                return image;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Image locale chargée en mémoire (le fichier n'est pas verrouillé).</summary>
        public static ImageSource? DepuisFichier(string chemin, int taille = 128)
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(chemin);
                image.DecodePixelWidth = taille;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }
    }
}
