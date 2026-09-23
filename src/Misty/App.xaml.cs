using Misty.Services;
using System.Windows;
using System.Windows.Threading;

namespace Misty
{
    public partial class App : Application
    {
        /// <summary>Filet de sécurité : une erreur non gérée affiche un message au lieu de fermer le launcher.</summary>
        private async void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            await Dialogs.ErreurAsync("Erreur inattendue", e.Exception.Message);
        }
    }
}
