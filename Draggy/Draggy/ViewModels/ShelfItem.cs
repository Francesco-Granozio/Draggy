using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Draggy.ViewModels
{
    public class ShelfItem
    {
        public string FilePath { get; set; }
        public string FileName => Path.GetFileName(FilePath);
        public ImageSource Thumbnail { get; set; } // carica con helper
    }
}
