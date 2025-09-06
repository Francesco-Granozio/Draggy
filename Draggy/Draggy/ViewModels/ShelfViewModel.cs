using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using Draggy.Services;
using Draggy.Commands;

namespace Draggy.ViewModels
{
    public class ShelfViewModel : INotifyPropertyChanged
    {
        private static ShelfViewModel? _instance;
        public static ShelfViewModel Instance => _instance ??= new ShelfViewModel();

        public ObservableCollection<ShelfItem> Items { get; } = new();

        public event Action<bool>? ItemsChanged;

        public ICommand StartDragCommand { get; }
        public ICommand RemoveItemCommand { get; }
        public ICommand ClearAllCommand { get; }

        private ShelfViewModel()
        {
            StartDragCommand = new RelayCommand<ShelfItem>(StartDrag);
            RemoveItemCommand = new RelayCommand<ShelfItem>(RemoveItem);
            ClearAllCommand = new RelayCommand(ClearAll);
            
            // Sottoscrivi ai cambiamenti della collezione
            Items.CollectionChanged += (s, e) => 
            {
                ItemsChanged?.Invoke(Items.Count > 0);
            };
        }

        public void AddItem(string filePath)
        {
            try
            {
                // Ottimizzazione: usa FileInfo una sola volta
                var fileInfo = new FileInfo(filePath);
                var fileName = fileInfo.Name;
                
                // Controlla se esiste già un file con lo stesso nome e dimensione
                var existingItem = Items.FirstOrDefault(x => 
                {
                    try
                    {
                        var existingFileInfo = new FileInfo(x.FilePath);
                        return existingFileInfo.Name == fileName && existingFileInfo.Length == fileInfo.Length;
                    }
                    catch
                    {
                        return false; // Se non riesce a leggere il file, considera diverso
                    }
                });
                
                if (existingItem == null)
                {
                    var item = new ShelfItem
                    {
                        FilePath = filePath,
                        // Usa il nuovo sistema di caching per i thumbnail
                        Thumbnail = ThumbnailCache.GetThumbnail(filePath, 48)
                    };
                    
                    Items.Add(item);
                    System.Diagnostics.Debug.WriteLine($"Aggiunto nuovo item: {fileName}");
                    
                    // Log delle statistiche della cache
                    LogCacheStatistics();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Item duplicato ignorato: {fileName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore nell'aggiungere item: {ex.Message}");
            }
        }

        private void StartDrag(ShelfItem? item)
        {
            if (item?.FilePath != null && File.Exists(item.FilePath))
            {
                var data = new System.Windows.DataObject(System.Windows.DataFormats.FileDrop, new string[] { item.FilePath });
                System.Windows.DragDrop.DoDragDrop(App.Current.MainWindow, data, System.Windows.DragDropEffects.Copy | System.Windows.DragDropEffects.Move);
            }
        }

        private void RemoveItem(ShelfItem? item)
        {
            if (item != null)
            {
                // Rimuovi il file dalla cache fisica
                CacheService.DeleteFromCache(item.FilePath);
                
                // Rimuovi il thumbnail dal cache
                ThumbnailCache.RemoveFromCache(item.FilePath);
                
                Items.Remove(item);
                
                System.Diagnostics.Debug.WriteLine($"Item rimosso: {Path.GetFileName(item.FilePath)}");
                LogCacheStatistics();
            }
        }

        private void ClearAll()
        {
            // Pulisci tutti i file dalla cache fisica e thumbnail
            foreach (var item in Items)
            {
                CacheService.DeleteFromCache(item.FilePath);
                ThumbnailCache.RemoveFromCache(item.FilePath);
            }

            Items.Clear();

            // Rimuovi anche eventuali file residui rimasti nella cache
            CacheService.ClearCache();
            ThumbnailCache.ClearCache();

            System.Diagnostics.Debug.WriteLine("Tutti gli items rimossi e cache completamente pulita");
            LogCacheStatistics();
        }

        private void LogCacheStatistics()
        {
            var cacheSize = CacheService.GetCacheSize();
            var cacheFiles = CacheService.GetCacheFileCount();
            var thumbnailCacheSize = ThumbnailCache.GetCacheSize();
            var thumbnailMemoryUsage = ThumbnailCache.GetEstimatedMemoryUsage();
            
            System.Diagnostics.Debug.WriteLine($"Statistiche cache - File: {cacheFiles} ({cacheSize / (1024 * 1024)}MB), Thumbnail: {thumbnailCacheSize} ({thumbnailMemoryUsage / 1024}KB)");
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
