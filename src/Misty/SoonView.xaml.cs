using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Misty.Models;
using Misty.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Misty.Services;

namespace Misty
{
    /// <summary>
    /// Logique d'interaction pour SoonView.xaml
    /// </summary>
    public partial class SoonView : UserControl
    {
        public SoonView()
        {
            InitializeComponent();
            Loaded += (_, _) => RafraichirPacks();
        }

        private void BtnConnexion_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private async void BtnCreerPack_Click(object sender, RoutedEventArgs e)
        {
            PanneauPack.Visibility = Visibility.Visible;
            TxtNomPack.Focus();

            if (CmbVersion.Items.Count == 0)
            {
                try
                {
                    var versions = await MinecraftService.VersionsAsync();
                    CmbVersion.ItemsSource = versions;
                    CmbVersion.SelectedIndex = versions.Count > 0 ? 0 : -1;
                }
                catch (Exception ex)
                {
                    await Dialogs.ErreurAsync("Versions introuvables", ex.Message);
                }
            }
        }

        private async void BtnValiderPack_Click(object sender, RoutedEventArgs e)
        {
            string nom = TxtNomPack.Text.Trim();

            if (nom.Length == 0 || CmbVersion.SelectedItem is not string version)
            {
                await Dialogs.ErreurAsync("Informations manquantes", "Donne un nom au pack et choisis une version.");
                return;
            }

            BtnValiderPack.IsEnabled = false;
            try
            {
                var progression = new Progress<string>(m => BtnValiderPack.Content = m);
                var (_, format) = await PackService.TemplateAsync(version, progression);
                string dossier = PackService.CreerPack(nom, TxtDescriptionPack.Text.Trim(), format, version);

                RafraichirPacks();   // ← à ajouter

                PanneauPack.Visibility = Visibility.Collapsed;
                await Dialogs.InfoAsync("Pack créé", $"« {nom} » a été créé.\n\n{dossier}");
            }
            catch (Exception ex)
            {
                await Dialogs.ErreurAsync("Création impossible", ex.Message);
            }
            finally
            {
                BtnValiderPack.Content = "Créer le pack";
                BtnValiderPack.IsEnabled = true;
            }

        }
        private async void CmbVersion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbVersion.SelectedItem is not string version) return;

            //CmbLoader.IsEnabled = false;
            //BtnCreer.IsEnabled = false;
            //TxtCreationInfo.Text = $"Recherche des mod loaders pour {version}…";

            var modes = await MinecraftService.Loaders.ModesDisponiblesAsync(version);
            if (CmbVersion.SelectedItem as string != version) return; // version changée entre-temps

            //CmbLoader.ItemsSource = modes;
            //// Un profil sert souvent à installer des mods : on propose Fabric par défaut s'il existe
            //CmbLoader.SelectedItem = modes.Contains(ModLoaderService.Fabric) ? ModLoaderService.Fabric : modes[0];
            //CmbLoader.IsEnabled = true;
            //BtnCreer.IsEnabled = true;
            //TxtCreationInfo.Text = "Les mods ne sont disponibles qu'avec un mod loader (Fabric, Forge, NeoForge, Quilt…).";
        }
        //private void BtnCreerPack_Click(object sender, RoutedEventArgs e) =>
        //    PanneauPack.Visibility = Visibility.Visible;

        private void BtnAnnulerPack_Click(object sender, RoutedEventArgs e) =>
            PanneauPack.Visibility = Visibility.Collapsed;

        private void FondPack_Click(object sender, MouseButtonEventArgs e) =>
            PanneauPack.Visibility = Visibility.Collapsed;

        
        private void RafraichirPacks()
        {
            var packs = PackService.Lister();
            ListePacks.ItemsSource = packs;
            TxtAucunPack.Visibility = packs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnDossierPack_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is PackService.PackInfo pack)
                Process.Start("explorer.exe", pack.Dossier);
        }

        private async void BtnSupprimerPack_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not PackService.PackInfo pack) return;

            if (!await Dialogs.ConfirmerAsync("Supprimer le pack",
                    $"Supprimer « {pack.Nom} » ? Toutes ses textures seront effacées définitivement.",
                    "Supprimer", danger: true))
                return;

            try
            {
                Directory.Delete(pack.Dossier, recursive: true);
                RafraichirPacks();
            }
            catch (Exception ex)
            {
                await Dialogs.ErreurAsync("Suppression impossible", ex.Message);
                RafraichirPacks();
            }
        }

    }
}
