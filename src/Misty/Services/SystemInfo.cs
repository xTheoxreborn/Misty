using System.Runtime.InteropServices;

namespace Misty.Services
{
    internal static class SystemInfo
    {
        /// <summary>RAM physique totale, en Mo.</summary>
        public static int RamTotaleMo()
        {
            var status = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            if (!GlobalMemoryStatusEx(ref status))
                return 8192;
            return (int)(status.ullTotalPhys / 1024 / 1024);
        }

        /// <summary>RAM maximale proposée : la RAM totale arrondie au Go supérieur, moins 1 Go pour le système.</summary>
        public static int RamMaxProposeeMo()
        {
            int totalGo = (int)Math.Ceiling(RamTotaleMo() / 1024.0);
            return Math.Max(1, totalGo - 1) * 1024;
        }

        /// <summary>Taille totale d'un dossier en octets (0 s'il n'existe pas).</summary>
        public static long TailleDossier(string dossier)
        {
            if (!Directory.Exists(dossier)) return 0;

            return Directory.EnumerateFiles(dossier, "*", SearchOption.AllDirectories)
                            .Sum(f => new FileInfo(f).Length);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatusEx
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);
    }
}
