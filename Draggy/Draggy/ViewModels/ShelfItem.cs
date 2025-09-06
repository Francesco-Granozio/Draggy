using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Draggy.Services;
using System.Windows.Input;
using Draggy.Commands;

namespace Draggy.ViewModels
{
    public class ShelfItem : IDisposable
    {
        private string _filePath = string.Empty; // cached file path
        private string _originalPath = string.Empty; // original source path (if known)
        private ImageSource? _thumbnail;
        private bool _disposed = false;

        public ICommand OpenFileCommand { get; }
        public ICommand CopyPathCommand { get; }
        public ICommand OpenFolderCommand { get; }

        public ShelfItem()
        {
            OpenFileCommand = new RelayCommand(ExecuteOpenFile);
            CopyPathCommand = new RelayCommand(ExecuteCopyPath);
            OpenFolderCommand = new RelayCommand(ExecuteOpenFolder);
        }

        public string FilePath 
        { 
            get => _filePath;
            set => _filePath = value ?? string.Empty;
        }
        
        public string OriginalPath
        {
            get => _originalPath;
            set => _originalPath = value ?? string.Empty;
        }

        public string FileName
        {
            get
            {
                var source = string.IsNullOrEmpty(_originalPath) ? _filePath : _originalPath;
                return Path.GetFileName(source);
            }
        }
        
        public ImageSource? Thumbnail 
        { 
            get => _thumbnail;
            set
            {
                // Libera il thumbnail precedente se presente
                if (_thumbnail != null && _thumbnail != value)
                {
                    // Il thumbnail viene gestito dal ThumbnailCache, non serve liberarlo qui
                }
                _thumbnail = value;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                // Libera le risorse gestite
                _thumbnail = null;
                _disposed = true;
            }
        }

        ~ShelfItem()
        {
            Dispose(false);
        }

        private string GetBestPath()
        {
            if (!string.IsNullOrWhiteSpace(_originalPath) && File.Exists(_originalPath))
                return _originalPath;
            return _filePath;
        }

        private void ExecuteOpenFile()
        {
            try
            {
                var path = GetBestPath();
                if (File.Exists(path))
                {
                    var psi = new System.Diagnostics.ProcessStartInfo(path)
                    {
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore apertura file: {ex.Message}");
            }
        }

        private void ExecuteCopyPath()
        {
            try
            {
                var path = GetBestPath();
                System.Windows.Clipboard.SetText(path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore copia percorso: {ex.Message}");
            }
        }

        private void ExecuteOpenFolder()
        {
            try
            {
                var path = GetBestPath();
                if (File.Exists(path))
                {
                    var argument = $"/select,\"{path}\"";
                    System.Diagnostics.Process.Start("explorer.exe", argument);
                }
                else
                {
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = dir,
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore apertura cartella: {ex.Message}");
            }
        }

        public ImageSource? FileIcon16
        {
            get
            {
                try
                {
                    var path = GetBestPath();
                    if (File.Exists(path))
                    {
                        // Preferisci l'icona del sistema operativo
                        var icon = ShellThumbnail.GetFileIcon(path, 16);
                        if (icon != null)
                            return icon;
                        // Fallback: miniatura
                        return ThumbnailCache.GetThumbnail(path, 16);
                    }
                }
                catch { }
                return null;
            }
        }
    }
}
