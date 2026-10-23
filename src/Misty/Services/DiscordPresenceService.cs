using DiscordRPC;

namespace Misty.Services
{
    /// <summary>
    /// Statut Discord (Rich Presence). Une seule instance pour toute l'application (<see cref="Instance"/>) :
    /// plusieurs clients avec le même ID se volent la connexion.
    /// </summary>
    internal sealed class DiscordPresenceService : IDisposable
    {
        public static DiscordPresenceService Instance { get; } = new();

        private readonly DiscordRpcClient client;
        private readonly Timestamps debut = Timestamps.Now;

        /// <summary>Dernier statut demandé, réappliqué quand on réactive l'option ou change de langue.</summary>
        private Func<RichPresence>? actuel;

        private DiscordPresenceService()
        {
            client = new DiscordRpcClient(AppInfo.DiscordAppId);
            client.Initialize();
            Loc.Changee += Reappliquer;
        }

        /// <summary>Option "Rich Presence Discord" (activée par défaut).</summary>
        public static bool Active => SettingsStore.GetBool(SettingsStore.RichPresence, true);

        public void DansLeLauncher() => Definir(() => new RichPresence
        {
            State = Loc.T("Discord_Launcher"),
            Timestamps = debut,
        });

        public void EditRessourcePack() => Definir(() => new RichPresence
        {
            State = Loc.T("Discord_Pack"),
            Timestamps = debut,
        });

        public void EnJeu(string version, string mode) => Definir(() => new RichPresence
        {
            Details = Loc.T("Discord_Jeu"),
            State = $"{version} {mode}",
            Timestamps = debut,
        });

        /// <summary>À appeler quand l'option change.</summary>
        public void Reappliquer()
        {
            if (Active && actuel != null) client.SetPresence(actuel());
            else client.ClearPresence();
        }

        private void Definir(Func<RichPresence> statut)
        {
            actuel = statut;
            Reappliquer();
        }

        public void Dispose() => client.Dispose();
    }
}
