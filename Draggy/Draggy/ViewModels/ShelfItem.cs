using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Draggy.ViewModels
{
    public class ShelfItem : IDisposable
    {
        private string _filePath = string.Empty;
        private ImageSource? _thumbnail;
        private bool _disposed = false;

        public string FilePath 
        { 
            get => _filePath;
            set => _filePath = value ?? string.Empty;
        }
        
        public string FileName => Path.GetFileName(_filePath);
        
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
    }
}
