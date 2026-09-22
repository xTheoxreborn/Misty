using Microsoft.VisualBasic.Devices;

namespace Misty.Services
{
    internal static class SystemInfo
    {
        /// <summary>Quantités de RAM proposées (en Mo), par pas de 1 Go, strictement sous la RAM totale.</summary>
        public static List<int> OptionsRamMo()
        {
            double ramGo = new ComputerInfo().TotalPhysicalMemory / 1024.0 / 1024.0 / 1024.0;
            int totalGo = (int)Math.Ceiling(ramGo);

            var options = new List<int>();
            for (int i = 1; i < totalGo; i++)
                options.Add(i * 1024);
            return options;
        }

        /// <summary>Taille totale d'un dossier en octets (0 s'il n'existe pas).</summary>
        public static long TailleDossier(string dossier)
        {
            if (!Directory.Exists(dossier)) return 0;

            return Directory.EnumerateFiles(dossier, "*", SearchOption.AllDirectories)
                            .Sum(f => new FileInfo(f).Length);
        }
    }
}
