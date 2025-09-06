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
using System.Windows.Interop;

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

            // Salva automaticamente le dimensioni quando cambiano
            this.SizeChanged += OverlayWindow_SizeChanged;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var source = (HwndSource)PresentationSource.FromVisual(this);
            source.AddHook(WndProc);
        }

        private void OverlayWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Persisti dimensioni e posizione correnti
            if (App.Current is App app)
            {
                app.SaveWindowBounds();
            }
        }

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Permette di trascinare la finestra cliccando sull'header
            if (App.Current is App app)
            {
                try
                {
                    app.StartWindowMove();
                    // Disabilita il drop durante lo spostamento per evitare trigger di drag
                    var previousAllowDrop = this.AllowDrop;
                    this.AllowDrop = false;
                    this.DragMove();
                    this.AllowDrop = previousAllowDrop;
                }
                finally
                {
                    // Aggiorna le posizioni salvate dopo il movimento
                    app.UpdateSavedPosition(this.Left, this.Top);
                    app.EndWindowMove();
                    System.Diagnostics.Debug.WriteLine($"Posizione salvata: ({this.Left}, {this.Top})");
                }
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            // Minimizza la finestra
            this.WindowState = WindowState.Minimized;
        }

        private void ClearCacheButton_Click(object sender, RoutedEventArgs e)
        {
            // Pulisci la cache fisica
            Services.CacheService.ClearCache();
            
            // Pulisci la cache dei thumbnail
            Services.ThumbnailCache.ClearCache();
            
            // Forza la garbage collection per liberare memoria
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            System.Diagnostics.Debug.WriteLine("Cache pulita manualmente e memoria liberata");
            
            // Mostra un messaggio di conferma
            MessageBox.Show("Cache pulita con successo!", "Cache", MessageBoxButton.OK, MessageBoxImage.Information);
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
                if (App.Current is App app)
                {
                    app.StartWindowResize();
                    this.AllowDrop = false;
                }
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
                // Salva le dimensioni aggiornate al termine del resize
                if (App.Current is App app)
                {
                    app.SaveWindowBounds();
                    app.EndWindowResize();
                    this.AllowDrop = true;
                }
            }
            base.OnMouseLeftButtonUp(e);
        }

        private const int WM_NCHITTEST = 0x0084;
        private const int WM_ENTERSIZEMOVE = 0x0231;
        private const int WM_EXITSIZEMOVE = 0x0232;
        private const int HTNOWHERE = 0;
        private const int HTCLIENT = 1;
        private const int HTCAPTION = 2;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_NCHITTEST)
            {
                // Estrae coordinate schermo
                int x = (short)((long)lParam & 0xFFFF);
                int y = (short)(((long)lParam >> 16) & 0xFFFF);
                var screenPoint = new System.Windows.Point(x, y);
                var windowPoint = this.PointFromScreen(screenPoint);

                double grip = 8.0;
                bool left = windowPoint.X >= 0 && windowPoint.X < grip;
                bool right = windowPoint.X <= this.ActualWidth && windowPoint.X > this.ActualWidth - grip;
                bool top = windowPoint.Y >= 0 && windowPoint.Y < grip;
                bool bottom = windowPoint.Y <= this.ActualHeight && windowPoint.Y > this.ActualHeight - grip;

                if (left && top)
                {
                    handled = true;
                    return new IntPtr(HTTOPLEFT);
                }
                if (right && top)
                {
                    handled = true;
                    return new IntPtr(HTTOPRIGHT);
                }
                if (left && bottom)
                {
                    handled = true;
                    return new IntPtr(HTBOTTOMLEFT);
                }
                if (right && bottom)
                {
                    handled = true;
                    return new IntPtr(HTBOTTOMRIGHT);
                }
                if (left)
                {
                    handled = true;
                    return new IntPtr(HTLEFT);
                }
                if (right)
                {
                    handled = true;
                    return new IntPtr(HTRIGHT);
                }
                if (top)
                {
                    handled = true;
                    return new IntPtr(HTTOP);
                }
                if (bottom)
                {
                    handled = true;
                    return new IntPtr(HTBOTTOM);
                }

                // Lascia alla gestione normale
                return IntPtr.Zero;
            }

            if (msg == WM_ENTERSIZEMOVE)
            {
                if (App.Current is App app)
                {
                    app.StartWindowResize();
                    this.AllowDrop = false;
                }
                return IntPtr.Zero;
            }

            if (msg == WM_EXITSIZEMOVE)
            {
                if (App.Current is App app)
                {
                    app.EndWindowResize();
                    this.AllowDrop = true;
                    app.SaveWindowBounds();
                }
                return IntPtr.Zero;
            }

            return IntPtr.Zero;
        }

        private void Grid_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) || Services.DragVirtualFileHelper.ContainsVirtualFile(e.Data) || Services.DragVirtualFileHelper.ContainsDownloadableUrl(e.Data))
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
            if (e.Data.GetDataPresent(DataFormats.FileDrop) || Services.DragVirtualFileHelper.ContainsVirtualFile(e.Data) || Services.DragVirtualFileHelper.ContainsDownloadableUrl(e.Data))
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
            
            // Reset del flag di drag dopo un breve delay
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                if (App.Current is App app)
                {
                    app.ResetDragFlag();
                    System.Diagnostics.Debug.WriteLine("Drag leave - reset flag");
                }
            };
            timer.Start();
        }

        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Drop event triggered!");
            e.Handled = true;
            
            // Reset del flag di drag dopo il drop
            if (App.Current is App app)
            {
                app.ResetDragFlag();
                System.Diagnostics.Debug.WriteLine("Drop - reset flag");
            }
            
            // Ripristina la trasparenza normale dopo il drop
            MainBorder.Background = new SolidColorBrush(Color.FromArgb(21, 255, 255, 255)); // #15FFFFFF
            MainBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(48, 204, 204, 204)); // #30CCCCCC
            
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[]? files = null;
                try
                {
                    files = (string[])e.Data.GetData(DataFormats.FileDrop);
                }
                catch (System.Runtime.InteropServices.COMException comEx)
                {
                    System.Diagnostics.Debug.WriteLine($"FileDrop retrieval failed, trying virtual files. {comEx.Message}");
                }

                if (files != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Files dropped: {files.Length}");
                    
                    foreach (var file in files)
                    {
                        try
                        {
                            System.Diagnostics.Debug.WriteLine($"Processing file: {file}");
                            
                            // Copia il file nella cache usando il CacheService
                            var cachedFilePath = Services.CacheService.CopyToCache(file);
                            System.Diagnostics.Debug.WriteLine($"File copied to cache: {cachedFilePath}");
                            
                            // Aggiungi l'item alla shelf con percorso originale
                            ViewModels.ShelfViewModel.Instance.AddItem(cachedFilePath, file);
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
                    return;
                }
            }
            
            if (Services.DragVirtualFileHelper.ContainsVirtualFile(e.Data))
            {
                var saved = Services.DragVirtualFileHelper.TrySaveVirtualFilesToCache(e.Data);
                if (saved.Count > 0)
                {
                    foreach (var path in saved)
                    {
                        try
                        {
                            // Per i virtual files, non sempre conosciamo l'originale; usa solo il path in cache
                            ViewModels.ShelfViewModel.Instance.AddItem(path, null);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Errore nell'aggiungere file virtuale {System.IO.Path.GetFileName(path)}: {ex.Message}");
                        }
                    }
                    ItemsControlFiles.Items.Refresh();
                    return;
                }
            }

            // Optional: some providers offer only a URL to download the content
            if (Services.DragVirtualFileHelper.ContainsDownloadableUrl(e.Data))
            {
                var saved = await Services.DragVirtualFileHelper.TrySaveFromUrlsToCacheAsync(e.Data);
                if (saved.Count > 0)
                {
                    foreach (var path in saved)
                    {
                        try
                        {
                            ViewModels.ShelfViewModel.Instance.AddItem(path, null);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Errore nell'aggiungere file da URL {System.IO.Path.GetFileName(path)}: {ex.Message}");
                        }
                    }
                    ItemsControlFiles.Items.Refresh();
                    return;
                }
            }

            // caso di dati non supportati
            System.Diagnostics.Debug.WriteLine("Dati non-file trascinati - formati non supportati");
        }
    }

}
