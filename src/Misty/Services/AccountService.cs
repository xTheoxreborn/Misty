using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;

namespace Misty.Services
{
    /// <summary>
    /// Session Microsoft (premium) partagée entre les vues.
    /// </summary>
    internal static class AccountService
    {
        private static JELoginHandler? loginHandler;

        public static MSession? Session { get; private set; }

        /// <summary>Déclenché à chaque connexion / déconnexion.</summary>
        public static event Action? SessionChanged;

        public static async Task ConnecterAsync()
        {
            loginHandler ??= JELoginHandlerBuilder.BuildDefault();
            Session = await loginHandler.Authenticate();
            SettingsStore.Set(SettingsStore.Premium, "True");
            SessionChanged?.Invoke();
        }

        /// <summary>Déconnexion rapide : le compte reste en cache pour la prochaine connexion.</summary>
        public static void Deconnecter()
        {
            Session = null;
            SettingsStore.Set(SettingsStore.Premium, "False");
            SessionChanged?.Invoke();
        }

        /// <summary>Déconnexion totale : supprime aussi le compte mis en cache.</summary>
        public static async Task DeconnexionTotaleAsync()
        {
            loginHandler ??= JELoginHandlerBuilder.BuildDefault();
            await loginHandler.Signout();
            Deconnecter();
        }
    }
}
