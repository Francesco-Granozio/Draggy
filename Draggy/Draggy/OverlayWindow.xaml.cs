using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Draggy
{
    /// <summary>
    /// Logica di interazione per OverlayWindow.xaml
    /// </summary>
    public partial class OverlayWindow : Window
    {
        public OverlayWindow()
        {
            InitializeComponent();
        }

        private void Grid_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                System.Diagnostics.Debug.WriteLine("DragEnter: File rilevato");
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void Grid_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                System.Diagnostics.Debug.WriteLine("DragOver: File sopra l'overlay");
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Drop event triggered!");
            
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                System.Diagnostics.Debug.WriteLine($"Files dropped: {files.Length}");
                
                foreach (var file in files)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"Processing file: {file}");
                        
                        // Copia il file nella cache usando il CacheService
                        var cachedFilePath = Services.CacheService.CopyToCache(file);
                        System.Diagnostics.Debug.WriteLine($"File copied to cache: {cachedFilePath}");
                        
                        // Aggiungi l'item alla shelf
                        ViewModels.ShelfViewModel.Instance.AddItem(cachedFilePath);
                        System.Diagnostics.Debug.WriteLine($"File added to shelf");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Errore nell'aggiungere file {file}: {ex.Message}");
                        MessageBox.Show($"Errore nell'aggiungere file {System.IO.Path.GetFileName(file)}: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                
                // Mostra un feedback positivo
                MessageBox.Show($"Aggiunt{(files.Length == 1 ? "o" : "i")} {files.Length} file alla shelf!", "Successo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // caso di dati "virtuali" o altro - più complesso
                System.Diagnostics.Debug.WriteLine("Dati non-file trascinati - funzionalità non ancora implementata");
            }

            // Non nascondere l'overlay - resta sempre visibile per testing
            // this.Hide();
        }
    }

}
