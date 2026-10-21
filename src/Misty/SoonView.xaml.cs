using Misty.Models;
using Misty.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
using Microsoft.Win32;

namespace Misty
{
    /// <summary>
    /// Logique d'interaction pour SoonView.xaml
    /// </summary>
    public partial class SoonView : UserControl
    {
        private readonly DiscordPresenceService discord;

        public SoonView()
        {
            InitializeComponent();
            Loaded += (_, _) => RafraichirPacks();

            discord = new DiscordPresenceService();

            //discord.EditRessourcePack();
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













        private PackService.PackInfo? packEnEdition;
        private string? dossierTemplate;
        private ICollectionView? vueTextures;

        internal event Action<PackService.PackInfo>? EditeurDemande;

        private async void BtnTextures_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not PackService.PackInfo pack) return;

            if (string.IsNullOrEmpty(pack.VersionMc))
            {
                await Dialogs.ErreurAsync("Version inconnue",
                    "Ce pack n'a pas de version enregistrée. Recrée-le ou ajoute un fichier misty.json.");
                return;
            }

            // (garde ici ton avertissement « expérimental » s'il est déjà en place)

            EditeurDemande?.Invoke(pack);
        }


        private string? ChoisirEditeur()
        {
            var dialogue = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choisis ton éditeur d'images (ex. paintdotnet.exe)",
                Filter = "Programme (*.exe)|*.exe",
            };
            if (dialogue.ShowDialog() != true) return null;

            PackService.Editeur = dialogue.FileName;
            return dialogue.FileName;
        }

        private void BtnEditeur_Click(object sender, RoutedEventArgs e) => ChoisirEditeur();
        private async void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not PackService.PackInfo pack) return;

            var dialogue = new SaveFileDialog
            {
                Title = "Exporter le pack de textures",
                Filter = "Pack de ressources (*.zip)|*.zip",
                FileName = pack.Nom + ".zip",
                DefaultExt = ".zip",
                AddExtension = true,
                OverwritePrompt = true,
            };
            if (dialogue.ShowDialog() != true) return;

            var bouton = (Button)sender;
            bouton.IsEnabled = false;
            try
            {
                await Task.Run(() => PackService.Exporter(pack, dialogue.FileName));

                bool ouvrir = await Dialogs.ConfirmerAsync("Pack exporté",
                    $"« {pack.Nom} » a été exporté :\n\n{dialogue.FileName}\n\n" +
                    "Pour l'utiliser, place le zip dans le dossier resourcepacks d'un profil.",
                    "Ouvrir le dossier", "Fermer");

                if (ouvrir)
                    Process.Start("explorer.exe", $"/select,\"{dialogue.FileName}\"");
            }
            catch (Exception ex)
            {
                await Dialogs.ErreurAsync("Export impossible", ex.Message);
            }
            finally
            {
                bouton.IsEnabled = true;
            }
        }
    }
}
