using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Misty.Controls
{
    /// <summary>
    /// Petits visuels pixel art générés en code (à afficher avec BitmapScalingMode=NearestNeighbor).
    /// </summary>
    public static class PixelArt
    {
        private static readonly Color[] Herbe = { C("#5DBB4A"), C("#4CA43B"), C("#6FCF5A"), C("#56B044") };
        private static readonly Color[] Terre = { C("#8B5A3C"), C("#7A4E33"), C("#9C6B47"), C("#6B4329") };

        private static BitmapSource? blocHerbe;

        /// <summary>Face avant d'un bloc d'herbe 16x16.</summary>
        public static BitmapSource BlocHerbe => blocHerbe ??= CreerBlocHerbe();

        private static BitmapSource CreerBlocHerbe()
        {
            const int taille = 16;
            var rnd = new Random(42); // motif toujours identique
            var pixels = new uint[taille * taille];

            for (int x = 0; x < taille; x++)
            {
                // L'herbe "coule" sur 3 à 6 pixels selon la colonne
                int hauteurHerbe = 3 + rnd.Next(4);
                for (int y = 0; y < taille; y++)
                {
                    Color c = y < hauteurHerbe
                        ? Herbe[rnd.Next(Herbe.Length)]
                        : Terre[rnd.Next(Terre.Length)];
                    pixels[y * taille + x] = (uint)(c.A << 24 | c.R << 16 | c.G << 8 | c.B);
                }
            }

            var bmp = BitmapSource.Create(taille, taille, 96, 96, PixelFormats.Bgra32, null, pixels, taille * 4);
            bmp.Freeze();
            return bmp;
        }

        private static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    }
}
