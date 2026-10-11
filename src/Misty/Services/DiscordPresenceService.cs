using DiscordRPC;

namespace Misty.Services
{
    /// <summary>
    /// Statut Discord (Rich Presence).
    /// </summary>
    internal sealed class DiscordPresenceService : IDisposable
    {
        private readonly DiscordRpcClient client;

        public DiscordPresenceService()
        {
            client = new DiscordRpcClient(AppInfo.DiscordAppId);
            client.Initialize();
        }
        public void DansLeLauncher()
        {
            client.SetPresence(new RichPresence
            {
                //Details = "Édite un pack de ressources",
                State = "Dans le launcher",
                Timestamps = Timestamps.Now,
            });
        }
        public void FermerRichPresence()
        {
            client.ClearPresence();
            //client.Deinitialize();
        }
        public void EditRessourcePack()
        {
            client.SetPresence(new RichPresence
            {
                State = "Édite un pack de ressources",
                //State = $"{version} {mode}",
                Timestamps = Timestamps.Now,
            });
        }
        public void EnJeu(string version, string mode)
        {
            client.SetPresence(new RichPresence
            {
                Details = "Joue à Minecraft",
                State = $"{version} {mode}",
                Timestamps = Timestamps.Now,
            });
        }
        public void Dispose() => client.Dispose();
    }
}
