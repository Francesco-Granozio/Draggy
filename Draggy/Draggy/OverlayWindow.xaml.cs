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
            DataContext = ViewModels.ShelfViewModel.Instance;
            
            // Debug: verifica che il DataContext sia impostato correttamente
            System.Diagnostics.Debug.WriteLine($"DataContext impostato: {DataContext != null}");
            System.Diagnostics.Debug.WriteLine($"ItemsControl DataContext: {ItemsControlFiles.DataContext != null}");
        }

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Permette di trascinare la finestra cliccando sull'header
            this.DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            // Minimizza la finestra
            this.WindowState = WindowState.Minimized;
        }

        private bool _isResizing = false;
        private Point _resizeStartPoint;
        private Size _resizeStartSize;

        private void ResizeBorder_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                _isResizing = true;
                _resizeStartPoint = e.GetPosition(this);
                _resizeStartSize = new Size(this.Width, this.Height);
                this.CaptureMouse();
                e.Handled = true;
            }
        }

        private void ResizeBorder_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isResizing)
            {
                Point currentPoint = e.GetPosition(this);
                double deltaX = currentPoint.X - _resizeStartPoint.X;
                double deltaY = currentPoint.Y - _resizeStartPoint.Y;

                double newWidth = Math.Max(200, _resizeStartSize.Width + deltaX);
                double newHeight = Math.Max(150, _resizeStartSize.Height + deltaY);

                this.Width = newWidth;
                this.Height = newHeight;
            }
        }

        protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_isResizing)
            {
                _isResizing = false;
                this.ReleaseMouseCapture();
            }
            base.OnMouseLeftButtonUp(e);
        }

        private void Grid_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                System.Diagnostics.Debug.WriteLine("DragEnter: File rilevato");
                
                // Aumenta la trasparenza quando inizia il drag - stile Apple
                MainBorder.Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255));
                MainBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(180, 0, 122, 255)); // Apple Blue
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

        private void Grid_DragLeave(object sender, DragEventArgs e)
        {
            // Ripristina la trasparenza normale quando il drag esce dalla finestra
            MainBorder.Background = new SolidColorBrush(Color.FromArgb(21, 255, 255, 255)); // #15FFFFFF
            MainBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(48, 204, 204, 204)); // #30CCCCCC
        }

        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Drop event triggered!");
            e.Handled = true;
            
            // Ripristina la trasparenza normale dopo il drop
            MainBorder.Background = new SolidColorBrush(Color.FromArgb(21, 255, 255, 255)); // #15FFFFFF
            MainBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(48, 204, 204, 204)); // #30CCCCCC
            
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
                
                // Mostra un feedback positivo ma più discreto
                System.Diagnostics.Debug.WriteLine($"Aggiunt{(files.Length == 1 ? "o" : "i")} {files.Length} file alla shelf!");
                
                // Forza l'aggiornamento dell'UI
                ItemsControlFiles.Items.Refresh();
            }
            else
            {
                // caso di dati "virtuali" o altro - più complesso
                System.Diagnostics.Debug.WriteLine("Dati non-file trascinati - funzionalità non ancora implementata");
            }
        }
    }

}
